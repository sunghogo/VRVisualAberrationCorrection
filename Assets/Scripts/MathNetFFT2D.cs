using System.Numerics;
using MathNet.Numerics.IntegralTransforms;

/// <summary>
/// 2D FFT helper built on top of Math.NET's 1D Fourier transforms.
/// Works on square Complex[,] arrays, e.g. 256x256, 512x512.
/// </summary>
public static class MathNetFFT2D
{
    /// <summary>
    /// In-place forward 2D FFT (unscaled) using Math.NET. 
    /// Uses FourierOptions.Matlab (forward unscaled, inverse scaled).
    /// </summary>
    public static void Forward2D(Complex[,] data)
    {
        int width  = data.GetLength(0);
        int height = data.GetLength(1);

        // Row-wise FFT
        var row = new Complex[width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                row[x] = data[x, y];

            Fourier.Forward(row, FourierOptions.Matlab);

            for (int x = 0; x < width; x++)
                data[x, y] = row[x];
        }

        // Column-wise FFT
        var col = new Complex[height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                col[y] = data[x, y];

            Fourier.Forward(col, FourierOptions.Matlab);

            for (int y = 0; y < height; y++)
                data[x, y] = col[y];
        }
    }

    /// <summary>
    /// In-place inverse 2D FFT using Math.NET.
    /// With FourierOptions.Matlab, scaling is handled internally.
    /// </summary>
    public static void Inverse2D(Complex[,] data)
    {
        int width  = data.GetLength(0);
        int height = data.GetLength(1);

        // Row-wise inverse
        var row = new Complex[width];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                row[x] = data[x, y];

            Fourier.Inverse(row, FourierOptions.Matlab);

            for (int x = 0; x < width; x++)
                data[x, y] = row[x];
        }

        // Column-wise inverse
        var col = new Complex[height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                col[y] = data[x, y];

            Fourier.Inverse(col, FourierOptions.Matlab);

            for (int y = 0; y < height; y++)
                data[x, y] = col[y];
        }
    }
}
