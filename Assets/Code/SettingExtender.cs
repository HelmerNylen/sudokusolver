using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Provides setting loading/saving behaviour,
/// as well as syncing between sliders and input fields under the advanced settings screen.
/// </summary>
public class SettingExtender : MonoBehaviour
{
    //references to other gameobjects/components
    public GameObject AdvancedSettingsPanel;
    Slider slider;
    InputField inputField;

    public bool NothingToSync;                      //whether the component is used for loading/saving only (true) or should sync slider and text values (false)
	float Latest = float.MinValue;                  //latest value - used to check which value is the newest

	public const string FormatString = "0.##";      //the format of floats in the text field, to avoid 10+ decimal digits
    public const string FileName = "Settings.txt";  //the file in which settings are saved

    public enum Setting
    {
        //the order here is important - the values need to be as they appear in the advanced settings window
        lumaR,
		lumaG,
		lumaB,
		LumaExponent,
		HorizontalChunks,
		VerticalChunks,
		CornerQueueLength,
		SearchRadiusFactor,
		InstantMatchPercent,
		MaxStoredBitmapsPerDigit,
        CameraName
    }

    void Start()
	{
		slider = GetComponentInChildren<Slider>();
		inputField = GetComponentInChildren<InputField>();
		if (!NothingToSync)
			SyncValues();
	}

    /// <summary>
    /// Sync the values of the slider and the text field.
    /// </summary>
	public void SyncValues()
	{
		if (slider == null)
            return;

        //update the value of the text fields if the slider value is the new one
		if (slider.value != Latest)
			inputField.text = (Latest = slider.value).ToString(FormatString);
		else
		{
			float tmp;
			if (float.TryParse(inputField.text, out tmp))
			{
				tmp = Mathf.Clamp(tmp, slider.minValue, slider.maxValue);
                //update the slider value if the text field value is valid and in the correct range, and round the text field value
				if (tmp != Latest)
					inputField.text = (slider.value = Latest = tmp).ToString(FormatString);
			}
			else
				inputField.text = Latest.ToString(FormatString); //reset the text field value if the user set it to something invalid
		}
	}

    /// <summary>
    /// Loads a dictionary of settings and their values from the settings file.
    /// </summary>
    /// <returns>A dictionary with the setting names as keys and setting values as values.</returns>
	private static Dictionary<Setting, string> LoadDictionary()
	{
		string path = Path.Combine(BitmapEncoding.PersistentPath, FileName);
		Dictionary<Setting, string> dict = new Dictionary<Setting, string>();

        //save the default settings if there are none to begin with
		if (!File.Exists(path))
			SaveDefaultSettings();
        
		var read = new System.Text.StringBuilder(); //a mutable string with all the text read since the last semicolon
		string str;
		using (var stream = File.OpenText(path))
			while (stream.Peek() != -1)
			{
                //read chars until a semicolon is encountered
				if ((char)stream.Peek() != ';')
					read.Append((char)stream.Read());
				else
				{
					stream.Read(); //skip said semicolon
					str = read.ToString().Trim();

                    //interpret what is to left of the (first) '=' as the setting name (dict key)
                    //and what is to the right as the setting value (dict value)
					dict.Add((Setting)System.Enum.Parse(typeof(Setting), str.Remove(str.IndexOf('='))),
                        str.Substring(str.IndexOf('=') + 1));
					read = new System.Text.StringBuilder();
				}
			}

		return dict;
	}

    /// <summary>
    /// Write a setting dictionary to the (UTF-8) settings file,
    /// so that it can be read by LoadDictionary() later.
    /// </summary>
    /// <param name="settings">A dictionary with the setting names as keys and setting values as values.</param>
	private static void SaveDictionary(Dictionary<Setting, string> settings)
	{
		string path = Path.Combine(BitmapEncoding.PersistentPath, FileName);
		if (File.Exists(path))
			File.Delete(path);
		using (var stream = File.CreateText(path))
			foreach (var setting in settings)
				stream.WriteLine(setting.Key.ToString("F") + "=" + setting.Value + ";");
	}

    /// <summary>
    /// Save the current settings to the settings file.
    /// </summary>
	public void SaveSettings()
	{
		print("Saving settings");
		var sliders = AdvancedSettingsPanel.GetComponentsInChildren<Slider>(true);
		SaveDictionary(new Dictionary<Setting, string>()
    	{
            {Setting.lumaR,						    sliders[0].value.ToString()},
			{Setting.lumaG,						    sliders[1].value.ToString()},
			{Setting.lumaB,						    sliders[2].value.ToString()},
			{Setting.LumaExponent,				    sliders[3].value.ToString()},
			{Setting.HorizontalChunks,			    sliders[4].value.ToString()},
			{Setting.VerticalChunks,				sliders[5].value.ToString()},
			{Setting.CornerQueueLength,			    sliders[6].value.ToString()},
			{Setting.SearchRadiusFactor,			sliders[7].value.ToString()},
			{Setting.InstantMatchPercent,			sliders[8].value.ToString()},
			{Setting.MaxStoredBitmapsPerDigit,	    sliders[9].value.ToString()},
            {Setting.CameraName,                    TakePicture.Instance.CameraName}
        });
	}

    /// <summary>
    /// Save the default settings to the settings file, overriding any custom settings.
    /// </summary>
	public static void SaveDefaultSettings()
	{
		print("Saving default settings");
		SaveDictionary(new Dictionary<Setting, string> {
			//these are the actual defaults - if the defaults are to be changed, this is were it should happen
			{Setting.lumaR,						"0.2126"},
			{Setting.lumaG,						"0.7152"},
			{Setting.lumaB,						"0.0722"},
			{Setting.LumaExponent,				"1.2"},
			{Setting.HorizontalChunks,			"10"},
			{Setting.VerticalChunks,			"10"},
			{Setting.CornerQueueLength,			"20"},
			{Setting.SearchRadiusFactor,		"0.15"},
			{Setting.InstantMatchPercent,		"90"},
			{Setting.MaxStoredBitmapsPerDigit,	"25"},
            {Setting.CameraName,                GetDefaultCameraName()}
		});
	}

    /// <summary>
    /// Tries to find a backwards-facing camera and returns its name.
    /// If none are found, picks the first availible camera, if any.
    /// </summary>
    /// <returns>The camera name to be used by the WebCamTexture, or an empty string if no cameras are found.</returns>
    static string GetDefaultCameraName()
    {
        foreach (var device in WebCamTexture.devices)
            if (!device.isFrontFacing)
                return device.name;
        if (WebCamTexture.devices.Length == 0)
        {
            print("Could not find any cameras.");
            return "";
        }
        return WebCamTexture.devices[0].name;
    }

    /// <summary>
    /// Sets the slider values (which updates the text fields as well) under advanced settings,
    /// as well as the appropriate fields in TakePicture.
    /// </summary>
	public void LoadSettings()
	{
		print("Loading settings");
		var dict = LoadDictionary();
		Slider[] sliders = AdvancedSettingsPanel.GetComponentsInChildren<Slider>(true);

        sliders[(int)Setting.CornerQueueLength].value        = TakePicture.Instance.CornerQueueLength		 = int.Parse(dict[Setting.CornerQueueLength]);
		sliders[(int)Setting.HorizontalChunks].value         = TakePicture.Instance.HorizontalChunks		 = int.Parse(dict[Setting.HorizontalChunks]);
		sliders[(int)Setting.InstantMatchPercent].value      = TakePicture.Instance.InstantMatchPercent		 = float.Parse(dict[Setting.InstantMatchPercent]);
		sliders[(int)Setting.lumaB].value                    = TakePicture.Instance.lumaB					 = float.Parse(dict[Setting.lumaB]);
		sliders[(int)Setting.LumaExponent].value             = TakePicture.Instance.LumaExponent			 = float.Parse(dict[Setting.LumaExponent]);
		sliders[(int)Setting.lumaG].value                    = TakePicture.Instance.lumaG					 = float.Parse(dict[Setting.lumaG]);
		sliders[(int)Setting.lumaR].value                    = TakePicture.Instance.lumaR					 = float.Parse(dict[Setting.lumaR]);
		sliders[(int)Setting.MaxStoredBitmapsPerDigit].value = TakePicture.Instance.MaxStoredBitmapsPerDigit = int.Parse(dict[Setting.MaxStoredBitmapsPerDigit]);
		sliders[(int)Setting.SearchRadiusFactor].value       = TakePicture.Instance.SearchRadiusFactor		 = float.Parse(dict[Setting.SearchRadiusFactor]);
		sliders[(int)Setting.VerticalChunks].value           = TakePicture.Instance.VerticalChunks			 = int.Parse(dict[Setting.VerticalChunks]);
                                                               TakePicture.Instance.CameraName               = dict[Setting.CameraName];
    }
}
