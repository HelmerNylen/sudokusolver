#define WRITE_IMAGES_TO_DISK //färgar viktiga pixlar. Se TakePicture
using UnityEngine;
using System.Collections.Generic;

class OCR
{
    //used for keeping track of the bitmaps associated with each sudoku tile, so that the user can correct the scan
    public static bool[][,] identifiedBitmaps = new bool[81][,];
    //error messages
    public const string NoSudokuMessage = "Did not find any sudoku. Press <i>Incorrect Scan</i> to try again.";
    public const string SinglePixelMessage = "No black pixels in any direction.";
    public const string NoEntryPointMessage = "Found no appropriate entry point for the corner search.";

    #region Bitmap Analysis
    /// <summary>
    /// Determines whether the bitmap is all true or false, or has mixed values.
    /// </summary>
    /// <param name="bitmap">The bitmap to check</param>
    /// <returns>True if the bitmap is a single color, otherwise false.</returns>
    public static bool SingleColor(bool[,] bitmap)
    {
        bool skip = true;
        bool first = false;
        foreach (bool b in bitmap)
            if (skip)
            {
                first = b;
                skip = false;
            }
            else if (b ^ first)
                return false;

        return true;
    }

    /// <summary>
    /// Determines how many elements have the same value in two equally sized bitmaps.
    /// </summary>
    /// <param name="alpha">One of the bitmaps.</param>
    /// <param name="beta">The bitmap with which alpha is compared.</param>
    /// <returns>A 0-100 float measurement of how well the bitmaps match.</returns>
    public static float MatchPercent(bool[,] alpha, bool[,] beta)
    {
        int width = alpha.GetLength(0), height = alpha.GetLength(1);
        if (width != beta.GetLength(0) || height != beta.GetLength(1))
            return 0;

        int matchCounter = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (alpha[x, y] == beta[x, y])
                    matchCounter++;

        return (100 * matchCounter) / (float)alpha.Length;
    }
    #endregion

    #region Finding Digits

    /// <summary>
    /// Locates the corners of the sudoku in the image.
    /// </summary>
    /// <param name="bitmap"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="queueSize">
    /// The length of the pixel queue to use when detecting corners.
    /// A smaller queue is more vulnerable to irregularities in the image,
    /// while a longer queue may yield a less accurate result.
    /// </param>
    /// <returns>A jagged array with {x, y} pairs, starting in the lower left corner and going counter-clockwise.</returns>
    //not used
    public static int[][] FindCorners(bool[] bitmap, int width, int height, int queueSize)
    {
        //börja i mitten till vänster
        int x = 0, y = 0, majorDirection = 0, currentDirection = 0;
        bool breakout = false;

        for (int direction = 0; !breakout && direction < 5; direction++)
        {
            //init
            int tmp;
            switch (direction)
            {
                case 0: x = 0; y = height / 2; break; //vänsterifrån
                case 1: x = width / 2; y = 0; break; //nedifrån
                case 2: x = width - 1; y = height / 2; break; //högerifrån
                case 3: x = width / 2; y = height - 1; break; //uppifrån
                case 4:
                    MonoBehaviour.print(NoEntryPointMessage);
                    x = width / 2; y = height / 2;
                    return new int[][] { new int[] { x, y }, new int[] { x, y }, new int[] { x, y }, new int[] { x, y } };
            }

            //hitta ramen
            switch (direction)
            {
                case 0: //vänsterifrån
                    tmp = y * width;
                    //passera eventuella svarta pixlar i kanten
                    while (x < width && !bitmap[x + tmp])
                        x++;
                    //passera alla vita pixlar till ramen
                    while (x < width && bitmap[x + tmp])
                        x++;
                    if (x >= width)
                        continue;
                    majorDirection = currentDirection = 1;
                    breakout = true;
                    break;

                case 1: //nedifrån
                    while (y < height && !bitmap[x + y * width])
                        y++;
                    while (y < height && bitmap[x + y * width])
                        y++;
                    if (y >= height)
                        continue;
                    majorDirection = currentDirection = 2;
                    breakout = true;
                    break;

                case 2: //högerifrån
                    tmp = y * width;
                    while (x >= 0 && !bitmap[x + tmp])
                        x--;
                    while (x >= 0 && bitmap[x + tmp])
                        x--;
                    if (x < 0)
                        continue;
                    majorDirection = currentDirection = 3;
                    breakout = true;
                    break;

                case 3: //uppifrån
                    while (y >= 0 && !bitmap[x + y * width])
                        y--;
                    while (y >= 0 && bitmap[x + y * width])
                        y--;
                    if (y < 0)
                        continue;
                    majorDirection = currentDirection = 0;
                    breakout = true;
                    break;
            }
        }

        #if WRITE_IMAGES_TO_DISK
        TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(x + y * width, Color.magenta));
        #endif

        int[][] result = { new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 } };
        Queue<long> visited = new Queue<long>(queueSize);
        for (int i = 0; i < queueSize; i++)
            visited.Enqueue(0L);

        int tmpx = 0, tmpy = 0, dx, dy;
        byte assigned = 0;
        while (true)
        {
            if (visited.Peek() != 0)
            {
                //hitta eventuell böj
                dy = y - (int)(visited.Peek() & ((1L << 32) - 1L));
                dx = x - (int)((visited.Dequeue() >> 32) & ((1L << 32) - 1L));
                switch (majorDirection)
                {
                    case 0:
                        if (dy < -dx)
                        {
                            Assign(2, result, visited.ToArray());
                            assigned |= (byte)1 << 0;
                            majorDirection = 3;
                        }
                        break;

                    case 1:
                        if (dy < dx)
                        {
                            Assign(3, result, visited.ToArray());
                            assigned |= (byte)1 << 1;
                            majorDirection = 0;
                        }
                        break;

                    case 2:
                        if (-dx < dy)
                        {
                            Assign(0, result, visited.ToArray());
                            assigned |= (byte)1 << 2;
                            majorDirection = 1;
                        }
                        break;

                    case 3:
                        if (-dy < -dx)
                        {
                            Assign(1, result, visited.ToArray());
                            assigned |= (byte)1 << 3;
                            majorDirection = 2;
                        }
                        break;
                }

                if (assigned == ((byte)1 << 4) - (byte)1)
                    return result;
            }
            else visited.Dequeue();

            for (int i = -1; i < 4; i++)
            {
                tmpx = x; tmpy = y;
                switch ((4 + currentDirection - i) & 3)
                {
                    case 0: tmpx++; break;
                    case 1: tmpy++; break;
                    case 2: tmpx--; break;
                    case 3: tmpy--; break;
                }
                if (i == 3)
                {
                    MonoBehaviour.print(SinglePixelMessage);
                    return new int[][] { new int[] { x, y }, new int[] { x, y }, new int[] { x, y }, new int[] { x, y } };
                }

                if (tmpx >= 0 && tmpx < width && tmpy >= 0 && tmpy < height && !bitmap[tmpx + tmpy * width])
                {
                    currentDirection = (4 + currentDirection - i) & 3;
                    break;
                }
            }
            x = tmpx; y = tmpy;

            visited.Enqueue(((long)x << 32) + (long)y);
        }
    }
    
    /// <summary>
    /// Represents directions. Incrementing rotates counter-clockwise, decrementing rotates clockwise.
    /// </summary>
    enum Direction
    {
        Up, Left, Down, Right
    }

    /// <summary>
    /// Represents corners of a rectangle. Incrementing rotates counter-clockwise, decrementing rotates clockwise.
    /// </summary>
    enum Corner
    {
        LowerLeft, LowerRight, UpperRight, UpperLeft
    }

    /// <summary>
    /// Finds the corners of the sudoku in a bitmap. Will attempt different entry points and directions.
    /// </summary>
    /// <param name="bitmap">The bitmap in which to search for corners.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="queueSize">
    /// The length of the pixel queue to use when detecting corners.
    /// A smaller queue is more vulnerable to irregularities in the image,
    /// while a longer queue may yield a less accurate result.
    /// </param>
    /// <returns>A jagged array with {x, y} pairs, starting in the lower left corner and going counter-clockwise.</returns>
    public static int[][] FindCorners2(bool[] bitmap, int width, int height, int queueSize)
    {
        int x, y;
        int[][] corners;

        //try edge detecting at the middle and 1/3 in
        foreach (float t in new float[] { 0.5f, 1 / 3f, 2 / 3f })
        {
            for (int direction = 0; direction < 4; direction++)
            {
                switch (direction)
                {
                    case (int)Direction.Up:
                        x = (int)(width * t);
                        y = 0;

                        //pass all black pixels
                        while (y < height && !bitmap[x + width * y])
                            y++;

                        //pass all white pixels
                        while (y < height && bitmap[x + width * y])
                            y++;

                        //try another direction if the edge wasn't found
                        if (y >= height)
                            continue;

                        break;

                    case (int)Direction.Left:
                        x = width - 1;
                        y = (int)(height * t);

                        while (x >= 0 && !bitmap[x + width * y])
                            x--;

                        while (x >= 0 && bitmap[x + width * y])
                            x--;

                        if (x < 0)
                            continue;

                        break;

                    case (int)Direction.Down:
                        x = (int)(width * t);
                        y = height - 1;

                        while (y >= 0 && !bitmap[x + width * y])
                            y--;

                        while (y >= 0 && bitmap[x + width * y])
                            y--;

                        if (y < 0)
                            continue;

                        break;

                    case (int)Direction.Right:
                        x = 0;
                        y = (int)(height * t);

                        while (x < width && !bitmap[x + width * y])
                            x++;

                        while (x < width && bitmap[x + width * y])
                            x++;

                        if (x >= width)
                            continue;

                        break;

                    default: throw new System.Exception("Undefined direction: " + direction);
                }

                MonoBehaviour.print((Direction)direction + ", x: " + x + ", y: " + y + ", t: " + t);
                corners = CornerSearch(x, y, (Direction)((direction + 1) & 3), bitmap, width, height, queueSize);
                MonoBehaviour.print(corners != null ? "(" + corners[0][0] + ", " + corners[0][1] + "), " + "(" + corners[1][0] + ", " + corners[1][1] + "), "  + "(" + corners[2][0] + ", " + corners[2][1] + "), "  + "(" + corners[3][0] + ", " + corners[3][1] + ")" : "null");

                if (ValidCorners(corners, width, height))
                    return corners;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Traces the edge clockwise and returns the corners it finds.
    /// </summary>
    /// <param name="x">The horizontal starting position.</param>
    /// <param name="y">The vertical starting position.</param>
    /// <param name="startDirection">
    /// The direction in which to start searching.
    /// Depends on which edge the starting position is on,
    /// and should make sure the search starts off clockwise.
    /// ex. If it's on the left edge, startDirection should be Direction.up
    /// </param>
    /// <param name="bitmap">The bitmap to trace.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="queueSize">
    /// The length of the pixel queue to use when detecting corners.
    /// A smaller queue is more vulnerable to irregularities in the image,
    /// while a longer queue may yield a less accurate result.
    /// </param>
    /// <returns>A jagged array with {x, y} pairs, starting in the lower left corner and going counter-clockwise.</returns>
    private static int[][] CornerSearch(int x, int y, Direction startDirection, bool[] bitmap, int width, int height, int queueSize)
    {
        TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(x + y * width, Color.magenta));

        ulong start = PackULong(x, y);
        int[][] result = { new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 } };

        Queue<ulong> visited = new Queue<ulong>(queueSize);
        for (int i = 0; i < queueSize; i++)
            visited.Enqueue(ulong.MaxValue);

        Direction latest = startDirection;
        int dx, dy, oldx, oldy;
        Corner nextCorner;
        ulong next, old;
        byte assigned = 0;

        switch (startDirection)
        {
            case Direction.Up: nextCorner = Corner.UpperLeft; break;
            case Direction.Left: nextCorner = Corner.LowerLeft; break;
            case Direction.Down: nextCorner = Corner.LowerRight; break;
            case Direction.Right: nextCorner = Corner.UpperRight; break;
            default: throw new System.Exception("Undefined direction: " + startDirection);
        }

        do
        {
            try
            {
                Move(ref x, ref y, latest, out latest, bitmap, width, height);
            }
            catch (System.Exception e)
            {
                MonoBehaviour.print(e);
                MonoBehaviour.print("Returning null - position was (" + x + ", " + y + ")");
                TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(x + y * width, Color.cyan));
                return null;
            }
            old = visited.Dequeue();

            UnpackULong(old, out oldx, out oldy);

            next = PackULong(x, y);
            visited.Enqueue(next);

            if (old == ulong.MaxValue) //the queue has not yet been filled
                continue;

            dx = x - oldx;
            dy = y - oldy;

            if ((nextCorner == Corner.LowerLeft && dy >= -dx)
                || (nextCorner == Corner.LowerRight && -dx >= -dy)
                || (nextCorner == Corner.UpperRight && -dy >= dx)
                || (nextCorner == Corner.UpperLeft && dx >= dy))
            {
                MonoBehaviour.print("Met conditions for corner " + nextCorner + " when at (" + x + ", " + y + ")");
                Assign(nextCorner, result, visited.ToArray());
                assigned |= (byte)(1 << (int)nextCorner);

                //take next clockwise corner
                nextCorner = (Corner)(((int)nextCorner + 3) & 3);

                if (assigned == 0xF)
                {
                    MonoBehaviour.print("Assigned all corners");
                    TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(x + y * width, Color.cyan));
                    return result;
                }
            }

        } while (next != start);

        MonoBehaviour.print("Returning null - exited loop");
        TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(x + y * width, Color.cyan));

        return null;
    }

    /// <summary>
    /// Helper function for CornerSearch(). Moves to the next point along the edge. Will throw an exception if there are no other pixels.
    /// </summary>
    /// <param name="x">The current x-coordinate.</param>
    /// <param name="y">The current y-coordinate.</param>
    /// <param name="latest">The direction last travelled in.</param>
    /// <param name="resultingDirection">The direction travelled in this time.</param>
    /// <param name="bitmap">The bitmap on which the edge detection is done.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    private static void Move(ref int x, ref int y, Direction latest, out Direction resultingDirection, bool[] bitmap, int width, int height)
    {
        int xres = -1, yres = -1;
        Direction next = (Direction)(((int)latest + 1) & 3);
        for (int i = 0; i < 4; i++)
        {
            switch (next)
            {
                case Direction.Up:
                    xres = x;
                    yres = y + 1;
                    break;
                case Direction.Left:
                    xres = x - 1;
                    yres = y;
                    break;
                case Direction.Down:
                    xres = x;
                    yres = y - 1;
                    break;
                case Direction.Right:
                    xres = x + 1;
                    yres = y;
                    break;
            }
            if (xres >= 0 && xres < width && yres >= 0 && yres < height && !bitmap[xres + width * yres])
            {
                resultingDirection = next;
                x = xres;
                y = yres;
                return;
            }

            next = (Direction)(((int)next + 3) & 3);
        }
        
        throw new System.Exception(SinglePixelMessage);
    }

    /// <summary>
    /// Stores two 32-bit integers in one 64-bit unsigned integer. See UnpackULong().
    /// </summary>
    /// <param name="x">One of the integers.</param>
    /// <param name="y">The other integer.</param>
    /// <returns>The combination of the two integers.</returns>
    public static ulong PackULong(int x, int y)
    {
        return ((ulong)x << 32) | (uint)y;
    }

    /// <summary>
    /// Retrieves to 32-bit integers from one 64-bit integer. See PackULong().
    /// </summary>
    /// <param name="value">The 64-bit integer in which the information is stored.</param>
    /// <param name="x">One of the resulting integers.</param>
    /// <param name="y">The other resulting integer.</param>
    public static void UnpackULong(ulong value, out int x, out int y)
    {
        y = (int)(value & 0xFFFFFFFF);
        x = (int)((value >> 32) & 0xFFFFFFFF);
    }

    /// <summary>
    /// Checks if a corner array contains valid data,
    /// making sure they are far enough apart and in the right places.
    /// </summary>
    /// <param name="corners">The corner array to check.</param>
    /// <param name="width">The width of the bitmap that was used to generate the array.</param>
    /// <param name="height">The height of the bitmap that was used to generate the array.</param>
    /// <returns>true if all conditions are met, otherwise false.</returns>
    //a bit ugly hard-coding every little check like this, but generalizing it would probably get even messier
    private static bool ValidCorners(int[][] corners, int width, int height)
    {
        try
        {
            return corners != null
                && corners.Length == 4

                //all corners are in the correct quadrant relative to one another
                && corners[(int)Corner.LowerLeft][0]  < corners[(int)Corner.LowerRight][0]
                && corners[(int)Corner.LowerLeft][0]  < corners[(int)Corner.UpperRight][0]
                && corners[(int)Corner.UpperLeft][0]  < corners[(int)Corner.LowerRight][0]
                && corners[(int)Corner.UpperLeft][0]  < corners[(int)Corner.UpperRight][0]
                && corners[(int)Corner.LowerLeft][1]  < corners[(int)Corner.UpperRight][1]
                && corners[(int)Corner.LowerLeft][1]  < corners[(int)Corner.UpperLeft][1]
                && corners[(int)Corner.LowerRight][1] < corners[(int)Corner.UpperRight][1]
                && corners[(int)Corner.LowerRight][1] < corners[(int)Corner.UpperLeft][1]

                //the smallest distance between any two corners is at least three times larger than the smallest dimension of the bitmap
                && Mathf.Pow(Mathf.Min(width, height) / 3, 2)
                        < Mathf.Min(
                            Mathf.Pow(corners[(int)Corner.LowerLeft][0]  - corners[(int)Corner.LowerRight][0], 2) + Mathf.Pow(corners[(int)Corner.LowerLeft][1]  - corners[(int)Corner.LowerRight][1], 2),
                            Mathf.Pow(corners[(int)Corner.LowerRight][0] - corners[(int)Corner.UpperRight][0], 2) + Mathf.Pow(corners[(int)Corner.LowerRight][1] - corners[(int)Corner.UpperRight][1], 2),
                            Mathf.Pow(corners[(int)Corner.UpperRight][0] - corners[(int)Corner.UpperLeft][0], 2)  + Mathf.Pow(corners[(int)Corner.UpperRight][1] - corners[(int)Corner.UpperLeft][1], 2),
                            Mathf.Pow(corners[(int)Corner.UpperLeft][0]  - corners[(int)Corner.LowerLeft][0], 2)  + Mathf.Pow(corners[(int)Corner.UpperLeft][1]  - corners[(int)Corner.LowerLeft][1], 2),
                            Mathf.Pow(corners[(int)Corner.LowerLeft][0]  - corners[(int)Corner.UpperRight][0], 2) + Mathf.Pow(corners[(int)Corner.LowerLeft][1]  - corners[(int)Corner.UpperRight][1], 2),
                            Mathf.Pow(corners[(int)Corner.LowerRight][0] - corners[(int)Corner.UpperLeft][0], 2)  + Mathf.Pow(corners[(int)Corner.LowerRight][1] - corners[(int)Corner.UpperLeft][1], 2)
                        );
        }
        catch (System.Exception e)
        {
            MonoBehaviour.print("Exception when validating array: " + e);
            return false;
        }
    }

    /// <summary>
    /// Helper function for CornerSearch().
    /// Assigns the value in the middle of the queue to the corner with the specified index.
    /// </summary>
    /// <param name="index">
    /// The corner to assign to.
    /// 0 is lower left, 1 is lower right,
    /// 2 is upper right and 3 is upper left.
    /// </param>
    /// <param name="result">The array with the corner values.</param>
    /// <param name="queue">The corner scan queue, as an array.</param>
    private static void Assign(Corner index, int[][] result, ulong[] queue)
    {
        result[(int)index] = new int[] {
            (int)((queue[queue.Length / 2] >> 32) & 0xFFFFFFFF),
            (int)(queue[queue.Length / 2] & 0xFFFFFFFF)
        };
    }

    private static void Assign(int index, int[][] result, long[] queue)
    {
        result[index] = new int[] {
            (int)((queue[queue.Length / 2] >> 32) & 0xFFFFFFFF),
            (int)(queue[queue.Length / 2] & 0xFFFFFFFF)
        };
    }

    /// <summary>
    /// Calculates where the digits should be, given a corner array from FindCorners().
    /// </summary>
    /// <param name="corners">
    /// A jagged array with {x, y} pairs,
    /// starting in the lower left corner and going counter-clockwise.
    /// </param>
    /// <param name="bitmapWidth">The width of the bitmap from which the corners were calculated.</param>
    /// <returns>
    /// An array of 81 elements containing digit indexes,
    /// starting from the bottom left corner.
    /// </returns>
    public static int[] FindDigitCoordinates(int[][] corners, int bitmapWidth)
    {
        int[] result = new int[81];
        int xresult, yresult;
        float xfrac, yfrac;

        for (int y = 0; y < 9; y++)
        {
            yfrac = (y + 0.5f) / 9f;
            for (int x = 0; x < 9; x++)
            {
                xfrac = (x + 0.5f) / 9f;
                xresult = (int)Mathf.Lerp(Mathf.Lerp(corners[(int)Corner.LowerLeft][0], corners[(int)Corner.LowerRight][0], xfrac),
                                          Mathf.Lerp(corners[(int)Corner.UpperLeft][0], corners[(int)Corner.UpperRight][0], xfrac),
                                          yfrac);
                yresult = (int)Mathf.Lerp(Mathf.Lerp(corners[(int)Corner.LowerLeft][1],  corners[(int)Corner.UpperLeft][1], yfrac),
                                          Mathf.Lerp(corners[(int)Corner.LowerRight][1], corners[(int)Corner.UpperRight][1], yfrac),
                                          xfrac);

                result[x + y * 9] = xresult + yresult * bitmapWidth;
            }
        }

        #if WRITE_IMAGES_TO_DISK
		foreach (int i in result)
			TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(i, Color.red));
        #endif

        return result;
    }
    #endregion

    #region Identification
    /// <summary>
    /// Finds and identifies the digits, given the bitmap image and search settings.
    /// </summary>
    /// <param name="bitmap">The bitmap for which the search coordinates were calculated.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="searchCoords">An array with 81 probable digit indexes. See FindDigitCoordinates().</param>
    /// <param name="searchRadius">
    /// The maximum distance from a calculated digit index
    /// to its actual match.Too high values may result in accidentally selecting
    /// the entire sudoku wireframe, while too low values may result in the algorithm
    /// not actually finding anything, assuming a zero.
    /// </param>
    /// <param name="digitBitmaps">
    /// A length 10 array of arbitrarily sized arrays of bitmaps, to be used when identifying the digits.
    /// This array can be modified by the function call, inserting and removing new bitmaps.
    /// </param>
    /// <param name="instantMatch">A 0-100 threshold of when two bitmaps should be considered identical.</param>
    /// <param name="maxBitmapsPerDigit">The maximum number of bitmaps each digit should be allowed to store.</param>
    /// <returns>A 9x9 sudoku where 0 represents an empty tile.</returns>
    public static int[,] GetSudoku(bool[] bitmap, int width, int height, int[] searchCoords, float searchRadius, bool[][][,] digitBitmaps, float instantMatch, int maxBitmapsPerDigit)
    {
        int[] result = new int[81];
        int x, y, xscan = 0, yscan = 0;
        bool breakout = false;
        bool[,] digit = new bool[0, 0];
        identifiedBitmaps = new bool[81][,];

        //try to find a hit in the desired area for each coordinate
        for (int i = 0; i < 81; i++)
        {
            if (!bitmap[searchCoords[i]])
            {
                result[i] = Identify(bitmap, width, height, searchCoords[i], digitBitmaps, instantMatch, maxBitmapsPerDigit, ref digit);
                identifiedBitmaps[i] = digit;
                continue;
            }

            x = searchCoords[i] % width;
            y = searchCoords[i] / width;
            //Search in an octagonal pattern
            breakout = false;
            for (int j = 1; j <= searchRadius && !breakout; j++)
            {
                for (int k = 0; k < 8; k++)
                {
                    switch (k)
                    {
                        case 0:
                            xscan = x + j;
                            yscan = y;
                            break;

                        case 1:
                            xscan = x;
                            yscan = y + j;
                            break;

                        case 2:
                            xscan = x - j;
                            yscan = y;
                            break;

                        case 3:
                            xscan = x;
                            yscan = y - j;
                            break;

                        case 4:
                            xscan = x + j;
                            yscan = y - j;
                            break;

                        case 5:
                            xscan = x + j;
                            yscan = y + j;
                            break;

                        case 6:
                            xscan = x - j;
                            yscan = y + j;
                            break;

                        case 7:
                            xscan = x - j;
                            yscan = y - j;
                            break;
                    }

                    if (xscan >= 0 && xscan < width && yscan >= 0 && yscan < height)
                    {
                        if (!bitmap[xscan + yscan * width])
                        {
                            result[i] = Identify(bitmap, width, height, xscan + yscan * width, digitBitmaps, instantMatch, maxBitmapsPerDigit, ref digit);
                            identifiedBitmaps[i] = digit;
                            breakout = true;
                            break;
                        }
                    }
                }
            }
        }

        FailCounter = 0;
        return ArrayHandling.Raise(result, 9, 9);
    }

    public static System.Random random = new System.Random();   //used for randomly replacing a bitmap - the identification algorithm itself is not random
    private static int FailCounter = 0;                         //counts how many times Identify() is called with a bitmap not resembling a digit
    /// <summary>
    /// Compares the cutout at a certain index to all stored digit bitmaps, and returns the best match.
    /// If the digit is different enough from its best match, it is inserted into the digit bitmap array.
    /// </summary>
    /// <param name="bitmap">The bitmap image of the whole sudoku.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="index">The index at which the digit was found.</param>
    /// <param name="digits">
    /// A length 10 array of arbitrarily sized arrays of bitmaps, to be used when identifying the digits.
    /// This array can be modified by the function call, inserting and removing new bitmaps.
    /// </param>
    /// <param name="instantMatch">A 0-100 threshold of when two bitmaps should be considered identical.</param>
    /// <param name="maxBitmapsPerDigit">The maximum number of bitmaps each digit should be allowed to store.</param>
    /// <param name="digitout">The extracted digit bitmap, which can be inserted into identifiedBitmaps.</param>
    /// <returns>The digit which was found at the indicated position.</returns>
    public static int Identify(bool[] bitmap, int width, int height, int index, bool[][][,] digits, float instantMatch, int maxBitmapsPerDigit, ref bool[,] digitout)
    {
        #if WRITE_IMAGES_TO_DISK
        TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(index, Color.blue));
        #endif
        
        bool[,] digit = ExtractDigit(bitmap, width, height, index); //cut out the digit as a 2d bitmap
        digitout = digit;

        TakePicture.Visualize(digit);

        //check if every digit is invalid, and if so, alert the user
        if (digit.Length <= 1 || SingleColor(digit))
        {
            if (++FailCounter == 81)
            {
                Popup.queued = new KeyValuePair<string, System.Action>(NoSudokuMessage, null);
                FailCounter = 0;
            }
            MonoBehaviour.print(FailCounter);
            return 0;
        }

        //identify the digit by checking it against all stored bitmaps
        bool[][,] memoryArray;
        bool[,] digitStretched = new bool[0, 0], memoryStretched = new bool[0, 0];
        float matchpercent, bestPercent = 0;
        int bestmatch = 0;

        #if CREATE_NEW_DEFAULT
		bestmatch = orderedDigits.Dequeue();
        #else
        for (int i = 1; i <= 9; i++)
        {
            memoryArray = digits[i];
            foreach (var memMap in memoryArray)
            {
                ArrayHandling.StretchToMatch(digit, memMap, out digitStretched, out memoryStretched);
                matchpercent = MatchPercent(digitStretched, memoryStretched);

                if (matchpercent >= instantMatch)
                    return i;
                if (matchpercent > bestPercent)
                {
                    bestmatch = i;
                    bestPercent = matchpercent;
                }
            }
        }
        #endif

        //save the bitmap if it is different enough
        bool[][,] bitmaps = digits[bestmatch] ?? new bool[0][,];
        if (bitmaps.Length < maxBitmapsPerDigit)
        {
            MonoBehaviour.print("Adding digit " + bestmatch + " to storage");
            var temp = new bool[bitmaps.Length + 1][,];
            for (int i = 0; i < bitmaps.Length; i++)
                temp[i] = bitmaps[i];

            temp[bitmaps.Length] = digit;
            digits[bestmatch] = temp;
        }
        else
        {
            MonoBehaviour.print("Replacing random stored bitmap for digit " + bestmatch);
            bitmaps[Mathf.FloorToInt((float)random.NextDouble() * bitmaps.Length)] = digit;
        }

        return bestmatch;
    }

    /// <summary>
    /// Uses a flood fill implementation to cut out an area of adjacently connected false elements.
    /// </summary>
    /// <param name="bitmap">The bitmap in which to look.</param>
    /// <param name="width">The width of the bitmap.</param>
    /// <param name="height">The height of the bitmap.</param>
    /// <param name="startIndex">The index of a false element from which to start the search.</param>
    /// <returns>The bitmap cutout.</returns>
    public static bool[,] ExtractDigit(bool[] bitmap, int width, int height, int startIndex)
    {
        int x = startIndex % width,
            xmin = x,
            xmax = x,
            y = startIndex / width,
            ymin = y,
            ymax = y;
        int widthLimit = width / 9, heightLimit = height / 9;
        ulong next;

        Queue<ulong> toVisit = new Queue<ulong>(512);
        HashSet<ulong> visited = new HashSet<ulong>();

        toVisit.Enqueue(PackULong(x, y));

        while (toVisit.Count > 0)
        {
            next = toVisit.Dequeue();
            if (visited.Contains(next))
                continue;
            visited.Add(next);

            UnpackULong(next, out x, out y);

            if (x >= 0 && y >= 0 && x < width && y < height
                && !bitmap[x + y * width])
            {
                xmax = Mathf.Max(x, xmax);
                xmin = Mathf.Min(x, xmin);
                ymax = Mathf.Max(y, ymax);
                ymin = Mathf.Min(y, ymin);

                //make sure the selected area is not a significant portion of the image -
                //this would most likely mean we've accidentally started selecting the wireframe of the sudoku
                if (xmax - xmin > widthLimit || ymax - ymin > heightLimit)
                    return new bool[,] { { false } };

                toVisit.Enqueue(PackULong(x + 1, y));
                toVisit.Enqueue(PackULong(x - 1, y));
                toVisit.Enqueue(PackULong(x, y + 1));
                toVisit.Enqueue(PackULong(x, y - 1));
            }
        }

        #if WRITE_IMAGES_TO_DISK
        TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(xmin + ymin * width, Color.cyan));
        TakePicture.colorq.Enqueue(new KeyValuePair<int, Color>(xmax + ymax * width, Color.cyan));
        #endif

        return ArrayHandling.CutAndRaise(bitmap, width, height, xmin, ymin, xmax - xmin + 1, ymax - ymin + 1);
    }
    #endregion
}