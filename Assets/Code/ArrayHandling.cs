using UnityEngine;
using System.Collections.Generic;

public static class ArrayHandling
{
    #region Linear-Rectangular Conversion
    /// <summary>
    /// Converts a rectangular array into a linear array of the
    /// same length, using the following pattern:
    /// Flatten(array)[x + array.GetLength(0) * y] == array[x, y]
    /// </summary>
    public static T[] Flatten<T>(T[,] array) //never used
    {
        T[] newarray = new T[array.Length];
        int width = array.GetLength(0), height = array.GetLength(1);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                newarray[x + y * width] = array[x, y];

        return newarray;
    }

    /// <summary>
    /// Converts a linear array into a rectangular array of the
    /// same length, using the following pattern:
    /// Raise(array)[x, y] == array[x + y * width]
    /// </summary>
    public static T[,] Raise<T>(T[] array, int width, int height)
    {
        if (width * height != array.Length)
            throw new System.InvalidOperationException();
        T[,] newarray = new T[width, height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                newarray[x, y] = array[x + y * width];

        return newarray;
    }
    #endregion

    #region Cutting, Stretching, Rotating
    /*public static bool[,][,] Split(bool[,] bitmap, int sections = 2)
	{
		float sectionWidth = bitmap.GetLength(0) / (float)sections,
			  sectionHeight = bitmap.GetLength(1) / (float)sections;

		bool[,][,] result = new bool[sections, sections][,];
		int X, Y;
		for (int x = 0; x < sections; x++)
			for (int y = 0; y < sections; y++)
			{
				X = Mathf.RoundToInt(sectionWidth * x);
				Y = Mathf.RoundToInt(sectionHeight * y);
				result[x, y] = Cut(bitmap, X, Y, Mathf.RoundToInt(sectionWidth * (x + 1)) - X, Mathf.RoundToInt(sectionHeight * (y + 1)) - Y);
			}
		return result;
	}*/

    /*public static T[,] Cut<T>(T[,] array, int left, int bottom, int width, int height)
	{
		int maxwidth = array.GetLength(0), maxheight = array.GetLength(1);
		left = Mathf.Max(0, left);
		bottom = Mathf.Max(0, bottom);
		width = Mathf.Min(maxwidth - left, width);
		height = Mathf.Min(maxheight - bottom, height);
		var result = new T[width, height];

		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
				result[x, y] = array[left + x, bottom + y];

		return result;
	}*/

    /// <summary>
    /// Cuts a section from the given array.
    /// </summary>
    /// <param name="array">The original array, typically a linear representation of a bitmap.</param>
    /// <param name="arrayWidth">The width of the image in the array.</param>
    /// <param name="arrayHeight">The height of the image in the array.</param>
    /// <param name="left">The left border of the area to be cut (inclusive).</param>
    /// <param name="bottom">The bottom border of the area to be cut (inclusive).</param>
    /// <param name="width">The number of pixels to be cut horizontally.</param>
    /// <param name="height">The number of pixels to be cut vertically.</param>
    /// <returns>The cut elements from the array.</returns>
    public static T[] Cut<T>(T[] array, int arrayWidth, int arrayHeight, int left, int bottom, int width, int height)
    {
        left = Mathf.Max(0, left);
        bottom = Mathf.Max(0, bottom);
        width = Mathf.Min(arrayWidth - left, width);
        height = Mathf.Min(arrayHeight - bottom, height);
        var result = new T[width * height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[x + y * width] = array[left + x + (bottom + y) * arrayWidth];

        return result;
    }

    /// <summary>
    /// Same functionality and parameters as Cut(), but returns a rectangular array instead of a linear one.
    /// </summary>
    public static T[,] CutAndRaise<T>(T[] array, int arrayWidth, int arrayHeight, int left, int bottom, int width, int height)
    {
        left = Mathf.Max(0, left);
        bottom = Mathf.Max(0, bottom);
        width = Mathf.Min(arrayWidth - left, width);
        height = Mathf.Min(arrayHeight - bottom, height);
        var result = new T[width, height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                result[x, y] = array[left + x + (bottom + y) * arrayWidth];

        return result;
    }

    /// <summary>
    /// Stretches the arrays so that they have the same size.
    /// </summary>
    /// <param name="alpha">One of the arrays.</param>
    /// <param name="beta">The other array.</param>
    /// <param name="alphaResult">The modified version of alpha.</param>
    /// <param name="betaResult">The modified version of beta.</param>
    public static void StretchToMatch<T>(T[,] alpha, T[,] beta, out T[,] alphaResult, out T[,] betaResult)
    {
        int maxWidth = Mathf.Max(alpha.GetLength(0), beta.GetLength(0)),
            maxHeight = Mathf.Max(alpha.GetLength(1), beta.GetLength(1));

        if (!(alpha.GetLength(0) == maxWidth && alpha.GetLength(1) == maxHeight))
            alphaResult = Stretch(alpha, maxWidth, maxHeight);
        else
            alphaResult = alpha;

        if (!(beta.GetLength(0) == maxWidth && beta.GetLength(1) == maxHeight))
            betaResult = Stretch(beta, maxWidth, maxHeight);
        else
            betaResult = beta;
    }

    /// <summary>
    /// Stretches an array to the specified size.
    /// </summary>
    /// <param name="array">The array to be stretched.</param>
    /// <param name="newWidth">The width of the stretched array.</param>
    /// <param name="newHeight">The height of the stretched array.</param>
    /// <returns>The modified version of the array.</returns>
    public static T[,] Stretch<T>(T[,] array, int newWidth, int newHeight)
    {
        var result = new T[newWidth, newHeight];
        int oldWidth = array.GetLength(0), oldHeight = array.GetLength(1);
        for (int i = 0; i < newWidth; i++)
            for (int j = 0; j < newHeight; j++)
                result[i, j] = array[(int)(oldWidth * i / (float)newWidth), (int)(oldHeight * j / (float)newHeight)];

        return result;
    }

    /// <summary>
    /// Rotates a texture.
    /// </summary>
    /// <param name="texture">The texture as a linear array of colors.</param>
    /// <param name="width">The width of the texture.</param>
    /// <param name="height">The height of the texture.</param>
    /// <param name="degreesClockWise">The rotation clockwise in degrees as a multiple of 90.</param>
    public static void RotateTexture(ref Color[] texture, int width, int height, int degreesClockWise)
    {
        Color[] r;
        switch ((360 + degreesClockWise) % 360) //make sure -90 falls under 270
        {
            case 0:
                return;

            case 90:
                r = new Color[texture.Length];
                for (int i = 0; i < width; i++)
                    for (int j = 0; j < height; j++)
                        r[j + (width - 1 - i) * height] = texture[i + width * j];
                texture = r;
                return;

            case 180:
                r = new Color[texture.Length];
                for (int i = 0; i < width; i++)
                    for (int j = 0; j < height; j++)
                        r[(width - 1 - i) + (height - 1 - j) * width] = texture[i + width * j];
                texture = r;
                return;

            case 270:
                r = new Color[texture.Length];
                for (int i = 0; i < width; i++)
                    for (int j = 0; j < height; j++)
                        r[(height - 1 - j) + (width - 1 - i) * height] = texture[i + width * j];
                texture = r;
                return;

            default: throw new System.ArgumentException("Invalid rotation: " + degreesClockWise + " degrees. Expected multiple of 90.");
        }
    }
    #endregion

    #region Sudoku-specific
    /// <summary>
    /// Solves a sudoku using a brute-force algorithm. <!-- Based on the one in https://en.wikipedia.org/wiki/Sudoku_solving_algorithms -->
    /// </summary>
    /// <param name="unsolved">The initial sudoku with zeroes representing blank tiles.</param>
    /// <param name="success">Whether or not the sudoku could be solved.</param>
    /// <returns>A solution to the specified sudoku, if it exists. Returns unsolved if it doesn't find a solution.</returns>
    //very inefficient, but seems to work well enough
    public static int[,] Solve(int[,] unsolved, ref bool success)
    {
        //check if the sudoku actually can be solved
        for (int x = 0; x < 9; x++)
            for (int y = 0; y < 9; y++)
                if (!AllowedPlacement(unsolved, x, y))
                {
                    success = false;
                    return unsolved;
                }

        int[,] solved = new int[9, 9];
        System.Array.Copy(unsolved, solved, 81);

        for (int y = 0; y < 9; )
            for (int x = 0; x < 9; )
            {
                if (unsolved[x, y] > 0)
                {
                    if (++x == 9)
                        y++;
                    continue;
                }

                do
                    solved[x, y]++;
                while (solved[x, y] <= 9 && !AllowedPlacement(solved, x, y));

                if (solved[x, y] > 9)
                {
                    solved[x, y] = 0;
                    do
                    {
                        x--;
                        if (x == -1)
                        {
                            x = 8;
                            y--;
                        }
                        if (x == 8 && y == -1)
                        {
                            MonoBehaviour.print("Couldn't solve");
                            success = false;
                            return unsolved;
                        }
                    } while (unsolved[x, y] > 0);
                }
                else if (++x == 9)
                    y++;
            }

        success = true;
        return solved;
    }

    /// <summary>
    /// Checks if a placement does not invalidate the sudoku.
    /// </summary>
    /// <param name="sudoku">The sudoku to be checked.</param>
    /// <param name="x">The x coordinate of the number to check.</param>
    /// <param name="y">The y coordinate of the number to check.</param>
    /// <returns>If the sudoku still is valid.</returns>
    private static bool AllowedPlacement(int[,] sudoku, int x, int y)
    {
        //check the column
        bool[] used = new bool[10];
        for (int i = 0; i < 9; i++)
        {
            if (sudoku[x, i] == 0)
                continue;
            if (used[sudoku[x, i]])
                return false;
            else
                used[sudoku[x, i]] = true;
        }

        //check the row
        used = new bool[10];
        for (int i = 0; i < 9; i++)
        {
            if (sudoku[i, y] == 0)
                continue;
            if (used[sudoku[i, y]])
                return false;
            else
                used[sudoku[i, y]] = true;
        }

        //check the square
        used = new bool[10];
        int xcoord = (x / 3) * 3, ycoord = (y / 3) * 3;
        for (int i = xcoord; i < xcoord + 3; i++)
            for (int j = ycoord; j < ycoord + 3; j++)
            {
                if (sudoku[i, j] == 0)
                    continue;
                if (used[sudoku[i, j]])
                    return false;
                else
                    used[sudoku[i, j]] = true;
            }

        return true;
    }

    /// <summary>
    /// Checks if an array is filled with zeroes.
    /// </summary>
    /// <param name="array">The array to check.</param>
    /// <returns>True if every element in the array equals zero, otherwise false.</returns>
    public static bool OnlyZeroes(int[,] array)
    {
        foreach (int i in array)
            if (i != 0)
                return false;
        return true;
    }
    #endregion
}