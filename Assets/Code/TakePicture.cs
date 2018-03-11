//#define CREATE_NEW_DEFAULT //aktivera om en ny default-fil ska skapas. även i BitmapEncoding.cs
#define WRITE_IMAGES_TO_DISK //aktivera om innan- och efter-bilder ska skrivas till Application.PersistentDataPath-mappen?

/*
	TODO-list: + fixat, - kvar att fixa, = uppskjutet till obestämd framtid, ~ symptomen fixade men kommer nog behöva en bättre lösning
		+ Rensa bort bitmaps-knapp -> promptar användaren efter "reset defaults" i stället
		+ Felvarning
        + Kamerabild direkt i UI
        + Tillbakaknappen ska gå tillbaka / stänga av
        + Kamerabilden är 90 grader fel (kunna ändra i settings?) -> sköts automatiskt
        + Ta bort laddningsikonen i splash screenen, kolla build settings
        + ta bort onödiga permissions
        = mjuk bitifiering -> tog 500 ggr så lång tid och gav ingen märkbar skillnad i resultat, kan kanske vara aktuell om den optimeras
        + kunna rätta inläsningen
        + Bugg, antagligen vid rotationen, gör att pixlarna hamnar lite hipp som happ i en mycket streckad bild -> berodde på att height och width byttes vid varje taget kort i st f en gång
 */

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

public class TakePicture : MonoBehaviour
{
    //references to other gameobjects/components
    public GameObject DebugTextInstance, SudokuPanel, PicturePanel;
    public SettingExtender SettingManager;
    public Popup infoPopup;
    public QuestionPopup questionPopup;
    public DigitPrompt digitPrompt;
    public RawImage CameraUI, OverlayUI;

    public Color BorderColor;

    public static TakePicture Instance; //static reference to the (only) TakePicture instance
    public static Text debugText;
    WebCamTexture texture;
	int textureWidth, textureHeight;

	private static string oldStatus = "";
	public static string Status = "Starting up";
    public const string StatusDone = "Done", StatusError = "Error: ";

    [Range(0, 1)]
    public float lumaR;                             //Default 0.2126f   Luma of pure red
    [Range(0, 1)]
    public float lumaG;                             //Default 0.7152f   Luma of pure green
    [Range(0, 1)]
	public float lumaB;                             //Default 0.0722f   Luma of pure blue
    public float LumaExponent;                      //Default 1.2f      The exponent used after calculating the average luma to filter noise
	public int HorizontalChunks;                    //Default 10        The number of sections to split the image in horizontally
    public int VerticalChunks;                      //Default 10        The number of sections to split the image in vertically
    public int CornerQueueLength;                   //Default 20        The number of samples used when tracing the edges and finding the corners of the sudoku
    public float SearchRadiusFactor;                //Default 0.15f     How far away from the predicted spot to search for digits (mutliplied by the distance to the next point)
    [Range(0, 100)]
    public float InstantMatchPercent;               //Default 90        How well a scanned bitmap must match a stored bitmap to be considered identical
    public int MaxStoredBitmapsPerDigit;            //Default 25        The maxmimum number of bitmaps that should be stored to the disk, per digit
    public bool KnownDigitsBold = false;            //                  if set to true, the sudoku tiles with the Defined field set to true get boldfaced
    public string CameraName;
	
    //error messages
    public const string CannotSolveMessage = "Could not solve the sudoku, possibly due to an incorrect scan. If the sudoku was not properly read, please press <i>Incorrect Scan</i>.";
    public const string NoCamerasMessage = "The system does not have any attached cameras.";

    /// <summary>
    /// Initialization code for the major parts of the application.
    /// </summary>
    void Start()
	{
        Screen.fullScreen = false;

        //set up references
        debugText = DebugTextInstance.GetComponent<Text>();
		debugText.enabled = true;
        Instance = this;
        Popup.Instance = infoPopup;
        QuestionPopup.Instance = questionPopup;
        BitmapEncoding.PersistentPath = Application.persistentDataPath;

        foreach (var v in WebCamTexture.devices)
			print("\"" + v.name + "\"" + (v.isFrontFacing ? " (Front facing)" : ""));
		print("Persistent: " + BitmapEncoding.PersistentPath);        

        //load persistent data
		Status = "Loading Digit Bitmaps";
		storedBitmaps = BitmapEncoding.LoadBitmaps();
		Status = "Loading Settings";
		SettingManager.LoadSettings();

        //start camera
        Status = "Looking for cameras";
        try
        {
            texture = new WebCamTexture(CameraName);
            texture.Play();
            if (WebCamTexture.devices.Length == 0)
                throw new System.Exception(NoCamerasMessage);
        }
        catch (System.Exception e)
        {
            Popup.ActivatePopup("Could not start the camera:" + System.Environment.NewLine + e.Message);
            CameraUI.enabled = OverlayUI.enabled = false;
        }
		textureWidth = texture.width;
		textureHeight = texture.height;
		side = Mathf.Min(textureWidth, textureHeight) - 10;

        //create overlay, rotate and stretch images correctly
        Status = "Assigning images";
		Texture2D overlay = CreateOverlay(textureWidth, textureHeight, side);
        OverlayUI.texture = overlay;
        CameraUI.texture = texture;
        CameraUI.GetComponent<AspectRatioFitter>().aspectRatio = textureWidth / (float)textureHeight;
        if ((texture.videoRotationAngle + 360) % 180 == 90)
        {
            int i = textureWidth;
            textureWidth = textureHeight;
            textureHeight = i;
        }
        CameraUI.transform.parent.localEulerAngles = new Vector3(0, 0, -texture.videoRotationAngle);

		SudokuPanel.GetComponent<SudokuCreator>().Init();

		Status = "Ready to take picture";
		debugText.enabled = false;
	}
	
    //"global variables", used in multiple functions and/or files
    [HideInInspector()]
	public bool success = false,                        //was the sudoku successfully solved?
        allZero = false,                                //was the scanned sudoku interpreted as completely empty?
        taken = false;                                  //has a picture been taken (should the script not expect keyboard/button input)?
	bool TakePictureNow = false;                        //has a button been pressed to trigger a capture the next frame?
	public void Take() { TakePictureNow = true; }       //method to enable other scripts and Unity to trigger the capture

	Color[] original, debugPicture;                     //the one-dimensional arrays representing the taken photo and the processed debug picture
	int side;                                           //the side length of the square texture to be processed by the OCR
	[HideInInspector()]
    public bool[][][,] storedBitmaps;                   //the bitmaps stored on disk
    [HideInInspector()]
	public int[,] solvedSudoku, unsolvedSudoku;         //the scanned sudoku, with zeroes representing empty tiles, and the solved sudoku
    [HideInInspector()]
	public System.Threading.Thread workThread;          //the worker thread (multithreading to make sure Unity doesn't freeze while the algorithm is running)

    #if WRITE_IMAGES_TO_DISK
	public static Queue<KeyValuePair<int, Color>> colorq = new Queue<KeyValuePair<int, Color>>();  //pixels to be colored when rendering a debug picture. used in OCR.cs
    #endif

    #if CREATE_NEW_DEFAULT
    //the digits, starting from the lower left corner, counting right and then up, to override actual identification and create a new default bitmap file
	static Queue<int> orderedDigits = new Queue<int>(new int[] {7,9,1,3,4,2,7,1,4,6,7,8,3,7,6,5,3,7,2,1,9,1,4,2,8,9,5,1,5,2,3,9,1,8,7});
    #endif
    /// <summary>
    /// Main program loop.
    /// </summary>
    void Update()
	{
		if (!taken)
		{
            if ((TakePictureNow && texture != null && texture.isPlaying) || Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space))
			{
                //grab the image data from the WebCamTexture
				taken = true;
				original = texture.GetPixels();
				Color[] pixels = new Color[original.Length];
				System.Array.Copy(original, pixels, original.Length);
				texture.Stop();
				print(string.Format("Took {0}x{1} picture", texture.width, texture.height));

                //refresh the settings and rotate the texture if it is needed
				Status = "Loading Settings";
				SettingManager.LoadSettings();
                ArrayHandling.RotateTexture(ref pixels, texture.width, texture.height, texture.videoRotationAngle);

                //start a new worker thread
				debugText.enabled = true;
				if (workThread != null && workThread.IsAlive)
				{
					workThread.Abort();
					workThread.Join(200);
				}
				workThread = new System.Threading.Thread(() => Worker(pixels));
				workThread.Start();

				PicturePanel.SetActive(false);
                CameraUI.transform.parent.gameObject.SetActive(false);
			}
		}
        //check if the worker thread has completed its tasks
		else if (Status == StatusDone) //this works, but an enum would probably be better suited for this kind of check
		{
			Status = "";
			debugText.enabled = false;
			
			FillSudoku(solvedSudoku, unsolvedSudoku);
			print("Displayed the result");

            #if WRITE_IMAGES_TO_DISK
			Texture2D originalTexture = new Texture2D(texture.width, texture.height),
                processedTexture = new Texture2D(side, side);
            originalTexture.filterMode = FilterMode.Point;
            originalTexture.SetPixels(original);
			originalTexture.Apply();
            processedTexture.filterMode = FilterMode.Point;
            processedTexture.SetPixels(debugPicture);
			processedTexture.Apply();

            File.WriteAllBytes(Path.Combine(BitmapEncoding.PersistentPath, "Output.png"), processedTexture.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(BitmapEncoding.PersistentPath, "Input.png"), originalTexture.EncodeToPNG());
            print("Saved images to " + BitmapEncoding.PersistentPath);
            #endif
        }

        //update explanatory text if it is needed
		if (debugText.enabled && Status != oldStatus)
		{
			if (Status.Contains(StatusError))
				debugText.text = Status;
			else
				debugText.text = (Status.Length != 0 ? "Please Wait" + System.Environment.NewLine + Status : "");
			oldStatus = Status;
		}

        //trigger popup if needed
        if (Popup.queued != null)
        {
            Popup.ActivatePopup(Popup.queued.Value.Key, Popup.queued.Value.Value);
            Popup.queued = null;
        }

        //if the user presses escape (back button on Android), go back/abort calculation/exit the app
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            var eb = PicturePanel.GetComponentsInChildren<ExtraButtons>(true)[0];
            if (PicturePanel.activeInHierarchy || (debugText.enabled && workThread != null && workThread.IsAlive))
                Application.Quit();
            else if (infoPopup.gameObject.activeInHierarchy)
                infoPopup.Close();
            else if (QuestionPopup.Instance.gameObject.activeInHierarchy)
                QuestionPopup.Instance.Close(false);
            else if (digitPrompt.gameObject.activeInHierarchy)
                digitPrompt.Choose(-1);
            else if (SudokuPanel.activeInHierarchy)
                Reset(false);
            else if (SettingManager.AdvancedSettingsPanel != null && SettingManager.AdvancedSettingsPanel.activeInHierarchy)
                eb.DirectlyFromAdvanced();
            else if (eb.HelpPanel != null && eb.HelpPanel.activeInHierarchy)
                eb.ReturnFromHelp();
        }
	}

    /// <summary>
    /// Called when the application is closed. Stops any worker threads and closes the camera feed.
    /// </summary>
    void OnApplicationQuit()
    {
        if (workThread != null && workThread.IsAlive)
        {
            workThread.Abort();
            workThread.Join(200);
        }
        workThread = null;
        if (texture.isPlaying)
            texture.Stop();
    }

    /// <summary>
    /// Fills the sudoku with the computed values.
    /// </summary>
    /// <param name="solved">The calculated values.</param>
    /// <param name="unsolved">The scanned values.</param>
	public void FillSudoku(int[,] solved, int[,] unsolved)
	{
		SudokuPanel.transform.parent.gameObject.SetActive(true);
		SudokuTile[] sudokuTiles = SudokuPanel.GetComponentsInChildren<SudokuTile>();

		int x, y;
		for (int i = 0; i < 81; i++)
		{
            //converts the child index to a (x, y) sudoku coordinate
            //this is a mess due to the initialization in SudokuCreator.Init()
			x = (i / 3) % 3 + (i / 27) * 3;
			y = 8 - (i % 3 + ((i / 9) % 3) * 3);
			sudokuTiles[i].Value = solved[x, y];
            sudokuTiles[i].Bitmap = OCR.identifiedBitmaps[x + y * 9];
            
            if (unsolved[x, y] > 0)
            {
                sudokuTiles[i].Display();
                sudokuTiles[i].Defined = true;
                if (KnownDigitsBold)
                    sudokuTiles[i].GetComponent<Text>().fontStyle = FontStyle.Bold;
            }
            else
            {
                sudokuTiles[i].Hide();
                sudokuTiles[i].Defined = false;
            }
		}
	}

    /// <summary>
    /// Resets the global variables to their initial values and prepares another scan.
    /// </summary>
    /// <param name="saveBitmaps">Whether to save the scanned bitmaps or not.</param>
    public void Reset(bool saveBitmaps)
	{
        //saves the bitmaps if the user wants to and the scan was a nonempty, correct sudoku
        if (success && saveBitmaps && !allZero)
		{
			Status = "Saving Bitmaps";
            BitmapEncoding.SaveBitmaps(storedBitmaps, MaxStoredBitmapsPerDigit);
		}
		else
			storedBitmaps = BitmapEncoding.LoadBitmaps();
		texture.Play();
		Status = "Ready to take picture";
		taken = false;
		TakePictureNow = false;
		success = false;

		debugText.enabled = false;
		SudokuPanel.transform.parent.gameObject.SetActive(false);
		PicturePanel.SetActive(true);
        CameraUI.transform.parent.gameObject.SetActive(true);
	}

    /// <summary>
    /// The main working method. Handles the flow from creating a bitmap
    /// from the scanned image to analysing its digits and solving the sudoku.
    /// </summary>
    /// <param name="pixels">The scanned image as a one-dimensional array of Colors.</param>
	public void Worker(Color[] pixels)
	{
		try
		{
			var timer = System.Diagnostics.Stopwatch.StartNew();
            //cut out the sudoku part
            pixels = ArrayHandling.Cut(pixels, textureWidth, textureHeight,
			             (textureWidth - side) / 2, (textureHeight - side) / 2, side, side);
			
			Status = "Creating Bitmap"; //the current status is displayed while the background thread is active
			bool[] bits = Bitify(pixels, side, side, HorizontalChunks, VerticalChunks); //generate a "black-and-white" bitmap from the picture

            int[] coords;
			int searchRadius;
			Status = "Finding Corners";

            int[][] corners = OCR.FindCorners2(bits, side, side, CornerQueueLength); //find the corners of the sudoku
            if (corners == null)
            {
                Popup.queued = new KeyValuePair<string, System.Action>(OCR.NoSudokuMessage, null);
                corners = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 } };
            }
            
            #if WRITE_IMAGES_TO_DISK
			foreach (int[] corner in corners)
				colorq.Enqueue(new KeyValuePair<int, Color>(corner[0] + corner[1] * side, Color.yellow));
            #endif

            searchRadius = Mathf.RoundToInt(SearchRadiusFactor * Mathf.Sqrt(
				Mathf.Pow((corners[2][0] - corners[0][0]) / 9f, 2) +
				Mathf.Pow((corners[2][1] - corners[0][1]) / 9f, 2)
				));
				
			Status = "Finding Digits";
			coords = OCR.FindDigitCoordinates(corners, side); //calculate where the digits should be
			
			Status = "Identifying Digits";
			unsolvedSudoku = OCR.GetSudoku(bits, side, side, coords, searchRadius, storedBitmaps, InstantMatchPercent, MaxStoredBitmapsPerDigit); //identify all digits

            allZero = ArrayHandling.OnlyZeroes(unsolvedSudoku);

			var sb = new System.Text.StringBuilder();
			for (int y = 8; y >= 0; y--)
			{
				for (int x = 0; x < 9; x++)
					sb.Append(unsolvedSudoku[x, y]);
				sb.AppendLine();
			}
			print(sb);
			
			Status = "Solving Sudoku";
			solvedSudoku = ArrayHandling.Solve(unsolvedSudoku, ref success); //solve the sudoku

            if (!success)
                Popup.queued = new KeyValuePair<string, System.Action>(CannotSolveMessage, null);

			sb = new System.Text.StringBuilder();
			for (int y = 8; y >= 0; y--)
			{
				for (int x = 0; x < 9; x++)
					sb.Append(solvedSudoku[x, y]);
				sb.AppendLine();
			}
			print(sb);

            #if WRITE_IMAGES_TO_DISK
			Status = "Rendering Debug Picture";
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = bits[i] ? Color.white : Color.black;
			
			while (colorq.Count > 0)
			{
				var kvp = colorq.Dequeue();
				if (kvp.Key >= 0 && kvp.Key < pixels.Length)
					pixels[kvp.Key] = kvp.Value;
			}
            #endif
			debugPicture = pixels;
            

            timer.Stop();
			print("Done");
			print("Time: " + timer.ElapsedMilliseconds + " ms");
			Status = StatusDone; //signal to the main thread that the solved sudoku is availible
		}
		catch (System.Exception e)
		{
			print(e);
            Status = StatusError + e.Message;
		}
	}

    /// <summary>
    /// Create an overlay to indicate to the user where to point the camera.
    /// </summary>
    /// <param name="width">The total texture width.</param>
    /// <param name="height">The total texture height.</param>
    /// <param name="side">The side of the fully transparent square in the middle of the texture.</param>
    /// <returns>The generated width x height texture with a side x side transparent square in the middle.</returns>
	public static Texture2D CreateOverlay(int width, int height, int side)
	{
		Texture2D overlay = new Texture2D(width, height);
		Color[] overlayPixels = new Color[width * height];
		
		int ymin = (height - side) / 2,
		    ymax = (height + side) / 2,
		    xmin = (width - side) / 2,
		    xmax = (width + side) / 2;
		Color tinted = new Color(0, 0, 0, 0.7f);

        //draw tinted border and transparent middle
        for (int x = 0; x < overlay.width; x++)
            for (int y = 0; y < overlay.height; y++)
                if (x < xmin || x > xmax || y < ymin || y > ymax)
                    overlayPixels[x + y * overlay.width] = tinted;
                else
                    overlayPixels[x + y * overlay.width] = Color.clear;

        //draw horizontal lines
		for (int x = xmin; x <= xmax; x++)
		{
			overlayPixels[x + width * ymin] = Instance.BorderColor;
            overlayPixels[x + width * (ymin + 1)] = Instance.BorderColor;
			overlayPixels[x + width * (ymax - 1)] = Instance.BorderColor;
			overlayPixels[x + width * ymax] = Instance.BorderColor;
		}

        //draw vertical lines
		for (int y = ymin; y <= ymax; y++)
		{
			overlayPixels[xmin + width * y] = Instance.BorderColor;
			overlayPixels[(xmin + 1) + width * y] = Instance.BorderColor;
			overlayPixels[(xmax - 1) + width * y] = Instance.BorderColor;
			overlayPixels[xmax + width * y] = Instance.BorderColor;
		}

		overlay.SetPixels(overlayPixels);
		overlay.Apply();
		return overlay;
	}

    /// <summary>
    /// Creates a bitmap from the given texture,
    /// attempting to minimize image artifacts due to brightness gradients.
    /// </summary>
    /// <param name="pixels">The scanned image as a one-dimensional array of Colors.</param>
    /// <param name="width">The width of the scanned image.</param>
    /// <param name="height">The width of the scanned image.</param>
    /// <param name="chunksX">
    /// The number of sections to split the image in horizontally
    /// when computing the local average luma.
    /// </param>
    /// <param name="chunksY">
    /// The number of sections to split the image in vertically
    /// when computing the local average luma.
    /// </param>
    /// <returns>A width x height bitmap where dark pixels become false and light pixels become true.</returns>
	public bool[] Bitify(Color[] pixels, int width, int height, int chunksX, int chunksY)
	{
		int xsize = width / chunksX, ysize = height / chunksY;
		float[] luma = new float[pixels.Length];
		bool[] result = new bool[pixels.Length];
		Color c;
		int index;

		//Splits the image into chunks and computes their average luma,
		//then assigns true or false to each point depending on whether their luma is above or below average
		for (int chunkX = 0; chunkX < chunksX; chunkX++)
			for (int chunkY = 0; chunkY < chunksY; chunkY++)
			{
				float averageLuma = 0;
			    
				for (int x = chunkX * xsize; x < (chunkX + 1) * xsize && x < width; x++)
					for (int y = chunkY * ysize; y < (chunkY + 1) * ysize && y < height; y++)
					{
						index = x + y * width;
						c = pixels[index];
						luma[index] = c.r * lumaR + c.g * lumaG + c.b * lumaB;
						averageLuma += luma[index];
					}
				averageLuma /= (Mathf.Min((chunkX + 1) * xsize, width) - chunkX * xsize) * (Mathf.Min((chunkY + 1) * ysize, height) - chunkY * ysize);
				averageLuma = ModifyAverage(averageLuma);

				for (int x = chunkX * xsize; x < (chunkX + 1) * xsize && x < width; x++)
					for (int y = chunkY * ysize; y < (chunkY + 1) * ysize && y < height; y++)
						result[x + y * width] = luma[x + y * width] > averageLuma;
			}

		return result;
	}

    /// <summary>
    /// Adjusts the luma threshold to make sure inconsistencies are smoothed out.
    /// </summary>
    /// <param name="averageLuma">The true average luma value.</param>
    /// <returns>The luma value to use as a threshold.</returns>
    public float ModifyAverage(float averageLuma)
	{
		return Mathf.Clamp01(Mathf.Pow(averageLuma, LumaExponent));
	}

    /// <summary>
    /// Prints the value of bitmap to the Unity console in a coherent way.
    /// 8 represents false (dark pixel) and 7 represents true (light pixel)
    /// due to their respective visual appearance.
    /// </summary>
    /// <param name="bitmap">The bitmap to be printed.</param>
	public static void Visualize(bool[,] bitmap)
	{
		var sb = new System.Text.StringBuilder();
		for (int y = bitmap.GetLength(1) - 1; y >= 0; y--)
		{
			for (int x = 0; x < bitmap.GetLength(0); x++)
				sb.Append(bitmap[x, y] ? 7 : 8);
			sb.AppendLine();
		}
		sb.Append(bitmap.GetLength(0) + "x" + bitmap.GetLength(1));
		print(sb);
	}
}