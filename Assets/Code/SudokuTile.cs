using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component for the GameObjects that represent a tile in the sudoku.
/// </summary>
public class SudokuTile : MonoBehaviour
{
    //references to other gameobjects/components
    public static GameObject DigitPrompt;

    public float LongPressTime;         //time required to trigger the digit selection prompt. set in prefab.

    public int Value = 0;               //the digit this tile contains
    public bool[,] Bitmap;              //the scanned bitmap that was analyzed to select the digit
	public bool Displaying = false;     //whether the tile is currently displaying its value
    public bool Defined = false;        //whether the digit in this tile was taken from the scan (true) or calculated afterwards (false)

    /// <summary>
    /// Display the value of the tile.
    /// </summary>
    public void Display()
	{
		Displaying = true;
		GetComponent<Text>().text = (Value == 0 ? "" : Value.ToString());
	}

    /// <summary>
    /// Hide the value of the tile.
    /// </summary>
	public void Hide()
	{
		Displaying = false;
		GetComponent<Text>().text = "";
	}


    public float LastPressed = -1;
    /// <summary>
    /// Called when the user starts pressing the tile.
    /// </summary>
    public void PressBegin()
    {
        LastPressed = Time.time;
    }

    /// <summary>
    /// Called when the user releases the tile. If enough time has passed,
    /// the digit prompt is opened.
    /// </summary>
    public void PressEnd()
    {
        if (LastPressed != -1 && Time.time - LastPressed >= LongPressTime)
        {
            LastPressed = -1;
            DigitPrompt.SetActive(true);
            DigitPrompt.GetComponent<DigitPrompt>().ActivatePrompt(this);
        }
    }
}
