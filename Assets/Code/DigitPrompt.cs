using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// The class handling the prompt when the user wants to correct a scan.
/// </summary>
public class DigitPrompt : MonoBehaviour
{
    //references to other gameobjects/components
    public GameObject Sudoku;
    
    public SudokuTile Coord = null;         //The clicked SudokuTile
    public int PreviousNumber = -1;         //the clicked SudokuTile's original value - there's no need to recalculate the entire sudoku if nothing changed
    public bool[,] Bitmap = null;           //the bitmap associated with the SudokuTile
    public Color standard, marked;          //colors of the digits in the prompt

    //info messages
    public const string SolvedMessage = "The sudoku was solved";

    /// <summary>
    /// Opens the prompt.
    /// </summary>
    /// <param name="coord">The calling SudokuTile</param>
    public void ActivatePrompt(SudokuTile coord)
    {
        //update fields
        Coord = coord;
        PreviousNumber = Coord.Value;
        Bitmap = Coord.Bitmap;
        if (Bitmap == null || Bitmap.Length == 1)
            Bitmap = new bool[,] { { true } };

        //render the bitmap as a texture
        Color[] colors = new Color[Bitmap.Length];
        for (int i = 0; i < Bitmap.GetLength(0); i++)
            for (int j = 0; j < Bitmap.GetLength(1); j++)
                colors[i + j * Bitmap.GetLength(0)] = Bitmap[i, j] ? Color.white : Color.black;

        var tex = new Texture2D(Bitmap.GetLength(0), Bitmap.GetLength(1));
        tex.filterMode = FilterMode.Point;
        tex.SetPixels(colors);
        tex.Apply();
        var ri = gameObject.GetComponentInChildren<RawImage>();
        ri.texture = tex;
        ri.GetComponent<AspectRatioFitter>().aspectRatio = Bitmap.GetLength(0) / (float)Bitmap.GetLength(1);

        //mark the original number if it was defined
        if (Coord.Defined)
            foreach (var t in GetComponentsInChildren<Text>())
                if (t.text == PreviousNumber.ToString())
                {
                    t.color = marked;
                    break;
                }
    }

    /// <summary>
    /// Called when the user presses a digit or exits the prompt.
    /// </summary>
    /// <param name="number">The number the user pressed. -1 for pressing the back button or equivalent.</param>
    public void Choose(int number)
    {
        if (number != -1 && number != PreviousNumber) //check if the sudoku really needs updating
        {
            Coord.Value = number;
            Coord.Defined = number > 0;
            if (Coord.Defined)
                Coord.Display();
            else
                Coord.Hide();

            //remove old identification from the array of saved bitmaps
            if (PreviousNumber != 0)
            {
                bool[][,] oldBitmapsRow = new bool[TakePicture.Instance.storedBitmaps[PreviousNumber].Length - 1][,];
                bool skip = true;
                for (int i = 0; i < TakePicture.Instance.storedBitmaps[PreviousNumber].Length; i++)
                {
                    if (skip && TakePicture.Instance.storedBitmaps[PreviousNumber][i] == Bitmap)
                        skip = false;
                    else if (skip && i == oldBitmapsRow.Length) //do not remove anything if there was no identical bitmap stored
                        break;
                    else
                        oldBitmapsRow[i - (skip ? 0 : 1)] = TakePicture.Instance.storedBitmaps[PreviousNumber][i];
                }

                if (!skip)
                    TakePicture.Instance.storedBitmaps[PreviousNumber] = oldBitmapsRow;
            }

            //append new bitmap
            if (number != 0)
            {
                bool[][,] newBitmapsRow = new bool[TakePicture.Instance.storedBitmaps[number].Length + 1][,];
                System.Array.Copy(TakePicture.Instance.storedBitmaps[number], newBitmapsRow, TakePicture.Instance.storedBitmaps[number].Length);
                newBitmapsRow[newBitmapsRow.Length - 1] = Bitmap;
                TakePicture.Instance.storedBitmaps[number] = newBitmapsRow;
            }
            
            RecalculateSudoku();
        }
        //reset fields and close the prompt
        Coord = null;
        PreviousNumber = -1;
        Bitmap = null;
        gameObject.GetComponentInChildren<RawImage>().texture = Texture2D.whiteTexture;
        foreach (var t in GetComponentsInChildren<Text>())
            t.color = standard;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Refreshes the sudoku view after a change is made.
    /// </summary>
    public void RecalculateSudoku()
    {
        print("Recalculating sudoku");
        TakePicture.debugText.enabled = true;
        TakePicture.Status = "Solving Sudoku";
        Sudoku.transform.parent.gameObject.SetActive(false);

        TakePicture.Instance.unsolvedSudoku = new int[9, 9];
        TakePicture.Instance.solvedSudoku = new int[9, 9];
        SudokuTile[] sudokuTiles = Sudoku.GetComponentsInChildren<SudokuTile>(true);

        //Reads the values from the sudoku tiles, see TakePicture.FillSudoku
        int x, y;
        for (int i = 0; i < 81; i++)
        {
            x = (i / 3) % 3 + (i / 27) * 3;
            y = 8 - (i % 3 + ((i / 9) % 3) * 3);
            if (sudokuTiles[i].Defined)
                TakePicture.Instance.unsolvedSudoku[x, y] = sudokuTiles[i].Value;
        }

        TakePicture.Instance.allZero = ArrayHandling.OnlyZeroes(TakePicture.Instance.unsolvedSudoku);

        //there should not be an active work thread at this point, but if there is, shut it down
        if (TakePicture.Instance.workThread != null && TakePicture.Instance.workThread.IsAlive)
        {
            TakePicture.Instance.workThread.Abort();
            TakePicture.Instance.workThread.Join(200);
        }
        //start reworker in a new thread
        TakePicture.Instance.workThread = new System.Threading.Thread(Reworker);
        TakePicture.Instance.workThread.Start();
    }

    /// <summary>
    /// Method for solving the sudoku in a separate thread.
    /// </summary>
    public void Reworker()
    {
        TakePicture.Instance.solvedSudoku = ArrayHandling.Solve(TakePicture.Instance.unsolvedSudoku, ref TakePicture.Instance.success);
        if (TakePicture.Instance.success)
           Popup.queued = new KeyValuePair<string, System.Action>(SolvedMessage, null);

        TakePicture.Status = TakePicture.StatusDone; //triggers a check in TakePicture.Update to refill the sudoku with the new solution
        print("Recalculation done");
    }
}