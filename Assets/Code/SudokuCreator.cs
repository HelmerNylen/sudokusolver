using UnityEngine;

/// <summary>
/// Creates the sudoku structure.
/// </summary>
public class SudokuCreator : MonoBehaviour
{
    //references to other gameobjects/components
    public GameObject Tile, SubColumn, SubRow, Column;
    public GameObject DigitPrompt;

    /// <summary>
    /// Creates the sudoku structure. Only to be called once.
    /// </summary>
    //  I got tired of copy-pasting 81 GameObjects every time I wanted to change
    //  the font color slightly, so this creates the whole structure live from a single prefab
	public void Init()
	{
        //add three tiles to a column
		var subcol = Instantiate(SubColumn) as GameObject;
		for (int i = 0; i < 3; i++)
			(Instantiate(Tile) as GameObject).transform.SetParent(subcol.transform, false);

        //add three columns to a row, creating a 3x3 tile
		var subrow = Instantiate(SubRow) as GameObject;
		subcol.transform.SetParent(subrow.transform, false);
		for (int i = 0; i < 2; i++)
			(Instantiate(subcol) as GameObject).transform.SetParent(subrow.transform, false);

        //add three of these bigger tiles to a column
		var col = Instantiate(Column) as GameObject;
		subrow.transform.SetParent(col.transform, false);
		for (int i = 0; i < 2; i++)
			(Instantiate(subrow) as GameObject).transform.SetParent(col.transform, false);

        //and finally, add three columns to the sudoku panel
		col.transform.SetParent(this.transform, false);
		for (int i = 0; i < 2; i++)
			(Instantiate(col) as GameObject).transform.SetParent(this.transform, false);

        SudokuTile.DigitPrompt = DigitPrompt;
	}

    /// <summary>
    /// Displays all digits in the sudoku.
    /// </summary>
	public void Reveal()
	{
		foreach (SudokuTile tile in GetComponentsInChildren<SudokuTile>())
			tile.Display();
	}
}
