//#define CREATE_NEW_DEFAULT //aktivera om en ny default-fil ska skapas. Även i TakePicture.cs
using UnityEngine;
using System.IO;

/*
    --The file format--
    Repeated for 9 digits:
        2 bytes representing the number of bitmaps for this digit
        Repeated said number of times:
            2 bytes representing the width of this bitmap
            2 bytes representing the height of this bitmap
            (width * height) / 8 (rounded up) bytes representing the bitmap
*/

/// <summary>
/// A class containing functions for saving, loading and converting bitmaps.
/// </summary>
public static class BitmapEncoding
{
    public const string BitmapFileName = "DigitBitmaps.bytes";  //the file in which the bitmap data is saved
	public static string PersistentPath;                        //the folder in which everything is saved (set in TakePicture.Start)

    /// <summary>
    /// Reads the bitmap file from disk.
    /// </summary>
    /// <returns>An array of length 10 with arrays of the bitmaps for the corresponding digit.</returns>
    //  example: LoadBitmaps()[4][0] becomes the first bitmap stored for the digit 4
    public static bool[][][,] LoadBitmaps()
	{
		string filename = Path.Combine(PersistentPath, BitmapFileName);
		bool loadDefault = !File.Exists(filename);
		byte[] defaultFile = null;
		if (loadDefault)
		{
			MonoBehaviour.print("No bitmap file found, loading default");
			#if CREATE_NEW_DEFAULT
			defaultFile = new byte[18]; //start out with a blank file if a new default is being created
			#else
			defaultFile = (Resources.Load("DigitBitmaps") as TextAsset).bytes;
			#endif
		}
        
		var result = new bool[10][][,];
        //open a file stream if the data is being read from disk, and open a memorystream of the default file if the default data is being read
		using (Stream stream = (loadDefault ? new MemoryStream(defaultFile) : (Stream)File.OpenRead(filename)))
		{
			bool[][,] memarray;
			byte[] buffer;
			int width, height;
            //read arrays for each digit (0 has no saved digits since it is very rarely explicitly printed)
			for (int i = 1; i <= 9; i++)
			{
                //read how many bitmaps are stored for this digit
				int mapslength = FromDoubleByte((byte)stream.ReadByte(), (byte)stream.ReadByte());
				memarray = new bool[mapslength][,];

				for (int mapNumber = 0; mapNumber < mapslength; mapNumber++)
				{
                    //first read the width and height of the next bitmap, then read the whole bitmap as a byte array
					width = FromDoubleByte((byte)stream.ReadByte(), (byte)stream.ReadByte());
					height = FromDoubleByte((byte)stream.ReadByte(), (byte)stream.ReadByte());
					buffer = new byte[Mathf.CeilToInt(width * height / 8f)];
					stream.Read(buffer, 0, buffer.Length);

					memarray[mapNumber] = FromByteArray(buffer, width, height); //decode the byte array to a proper bitmap
				}

				result[i] = memarray;
			}
		}

		return result;
	}

    /// <summary>
    /// Saves the bitmaps to disk.
    /// </summary>
    /// <param name="contents">An array of length 10 with arrays of the bitmaps for the corresponding digit.</param>
    /// <param name="maxBitmapsCount">The maximum number of bitmaps to save.</param>
    //  for the file structure, see the top of the file
	public static void SaveBitmaps(bool[][][,] contents, int maxBitmapsCount)
	{
		string filename = Path.Combine(PersistentPath, BitmapFileName);
		if (File.Exists(filename))
			File.Delete(filename);

		using (var stream = File.OpenWrite(filename))
		{
			bool[][,] memarray;
			byte[] temp;
			for (int i = 1; i <= 9; i++)
			{
				memarray = contents[i];
				int length = Mathf.Min(memarray.Length, maxBitmapsCount);
                //write number of bitmaps
				stream.Write(ToDoubleByte(length), 0, 2);
                MonoBehaviour.print("Storage for " + i + ": " + length + " bitmaps");

				for (int j = 0; j < length; j++)
				{
                    //write bitmap dimensions
					stream.Write(ToDoubleByte(memarray[j].GetLength(0)), 0, 2);
					stream.Write(ToDoubleByte(memarray[j].GetLength(1)), 0, 2);

					temp = ToByteArray(memarray[j]);
                    //write bitmap
					stream.Write(temp, 0, temp.Length);
				}
			}
		}
	}

    /// <summary>
    /// Converts an 32 bit int to two 8 bit bytes.
    /// </summary>
    /// <param name="number">The int to be converted. Cannot be larger than 2^16 - 1.</param>
    /// <returns>A byte array of length 2 with the least significant bits first.</returns>
    //  since the numbers encoded with this function are usually several magnitudes
    //  smaller than 2^16, only two bytes are used instead of four
    public static byte[] ToDoubleByte(int number)
	{
		if (number >= (1 << 16))
			throw new System.ArgumentOutOfRangeException("number");
		byte[] result = new byte[2];
        //the least significant bits come first
		result[0] = (byte)(number & (int)byte.MaxValue);
		result[1] = (byte)((number >> 8) & (int)byte.MaxValue);
		return result;
	}
	
    /// <summary>
    /// Converts the byte array obtained with ToDoubleByte() back to an int.
    /// </summary>
    /// <param name="leastSignificant">The least significant byte.</param>
    /// <param name="mostSignificant">The most significant byte.</param>
    /// <returns>The corresponding int.</returns>
	public static int FromDoubleByte(byte leastSignificant, byte mostSignificant)
	{
		return (((int)mostSignificant) << 8) | (int)leastSignificant;
	}
    
    /// <summary>
    /// Converts a rectangular bitmap to a linear byte array.
    /// </summary>
    /// <param name="bitmap">The bitmap to be converted.</param>
    /// <returns>A byte array that is 1/8 (rounded up) as long as the given bitmap.</returns>
	public static byte[] ToByteArray(bool[,] bitmap)
	{
		byte[] result = new byte[Mathf.CeilToInt(bitmap.Length / 8f)];

		int bitcounter = 0, totalcounter = 0;
		for (int y = 0; y < bitmap.GetLength(1); y++)
			for (int x = 0; x < bitmap.GetLength(0); x++)
			{
                //put each bit in the resulting array, starting from MSB
				result[totalcounter] |= bitmap[x, y] ? (byte)1 : (byte)0;
				if (++bitcounter == 8)
				{
					totalcounter++;
					bitcounter = 0;
				}
				else
					result[totalcounter] <<= 1;
			}
        //pad with zeroes if needed
        if (bitcounter != 0)
		    result[result.Length - 1] <<= 7 - bitcounter;

		return result;
	}


    /// <summary>
    /// Convert the linear byte array back to a rectangular bitmap.
    /// </summary>
    /// <param name="array">The byte array to be converted.</param>
    /// <param name="width">The desired width of the returned bitmap.</param>
    /// <param name="height">The desired height of the returned bitmap.</param>
    /// <returns>A width x height bitmap.</returns>
	public static bool[,] FromByteArray(byte[] array, int width, int height)
	{
		bool[,] result = new bool[width, height];

		int bitcounter = 0, totalcounter = 0;
		for (int y = 0; y < height; y++)
			for (int x = 0; x < width; x++)
			{
                //get the MSB
				result[x, y] = (array[totalcounter] & ((byte)1 << 7)) != 0;
				if (++bitcounter == 8)
				{
					totalcounter++;
					bitcounter = 0;
				}
				else
					array[totalcounter] <<= 1;
			}

		return result;
	}
}