using UnityEngine;
using System.Numerics;

public static class DeconvolutionFunctions
{
    /// <summary>
    /// Builds a frequency-domain deconvolution filter M(u, v)
    /// from a spatial-domain PSF texture K(x, y).
    ///
    /// Steps:
    /// 1. Read PSF K(x,y) from psfTex (R channel).
    /// 2. Compute H = FFT2D(K) using MathNet.
    /// 3. Compute M(u,v) = conj(H) / (|H|^2 + epsilon).
    ///
    /// Returns M as Complex[size,size] in frequency domain.
    /// </summary>
    /// <param name="psfTex">Spatial-domain PSF (R channel, size x size).</param>
    /// <param name="size">Resolution (must match psfTex size).</param>
    /// <param name="epsilon">Regularization parameter, e.g. 1e-3 to 1e-4.</param>
    public static Complex[,] GenerateMFilterFromPsf(Texture2D psfTex, int size, double epsilon = 1e-3)
    {
        if (psfTex == null)
        {
            Debug.LogError("GenerateMFilterFromPsf: psfTex is null.");
            return null;
        }

        if (psfTex.width != size || psfTex.height != size)
        {
            Debug.LogWarning($"GenerateMFilterFromPsf: psfTex is {psfTex.width}x{psfTex.height}, " +
                             $"size parameter is {size}. This may cause artifacts.");
        }

        // 1) Load PSF K(x,y) into a Complex[,] with real=k, imag=0
        Complex[,] K = new Complex[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float k = psfTex.GetPixel(x, y).r;
                K[x, y] = new Complex(k, 0.0);
            }
        }

        // 2) Forward FFT: K(x,y) -> H(u,v)
        MathNetFFT2D.Forward2D(K); // in-place: K becomes H

        // Debugging sanity check after Forward2D(K) in GenerateMFilterFromPsf
        double hMin = double.MaxValue, hMax = double.MinValue;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            var H = K[x, y];
            double mag = H.Magnitude;
            if (mag < hMin) hMin = mag;
            if (mag > hMax) hMax = mag;
        }
        Debug.Log($"GenerateMFilterFromPsf: |H| min={hMin}, max={hMax}");

        // 3) Build M(u,v) = conj(H) / (|H|^2 + epsilon)
        Complex[,] M = new Complex[size, size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Complex H = K[x, y];
                double Hr = H.Real;
                double Hi = H.Imaginary;

                double mag2 = Hr * Hr + Hi * Hi;
                double denom = mag2 + epsilon;

                if (denom <= 0.0)
                    denom = epsilon;

                // conj(H) = (Hr, -Hi)
                double Mr = Hr / denom;
                double Mi = -Hi / denom;

                M[x, y] = new Complex(Mr, Mi);
            }
        }
        
        return M;
    }
}
