using UnityEngine;
using System.Numerics;

public static class ImageProcessingFunctions
{
    /// <summary>
    /// Applies a PSF blur to a Texture2D using frequency-domain convolution:
    /// blurred = IFFT( FFT(image) * FFT(PSF) ).
    /// Processes luminance only and outputs a grayscale blurred texture.
    /// </summary>
   public static Texture2D ApplyPsfBlur(Texture2D src, Texture2D psfTex, int kernelSize)
    {
        int size = kernelSize;

        if (src.width != size || src.height != size)
        {
            Debug.LogWarning($"ApplyPsfBlurCPU: src is {src.width}x{src.height}, kernelSize={size}.");
        }
        if (psfTex.width != size || psfTex.height != size)
        {
            Debug.LogWarning($"ApplyPsfBlurCPU: psfTex is {psfTex.width}x{psfTex.height}, kernelSize={size}.");
        }

        // --- 1) Pack image into Complex[,] as luminance ---
        Complex[,] I = new Complex[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Color c = src.GetPixel(x, y);
                float L = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                I[x, y] = new Complex(L, 0.0);
            }
        }

        // --- 2) Pack PSF into Complex[,] ---
        Complex[,] K = new Complex[size, size];
        float maxPsf = 0f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float k = psfTex.GetPixel(x, y).r;
                if (k > maxPsf) maxPsf = k;
                K[x, y] = new Complex(k, 0.0);
            }
        }
        Debug.Log($"ApplyPsfBlurCPU: PSF max value = {maxPsf}");

        // --- 3) FFT both: I -> Ifreq, K -> H ---
        MathNetFFT2D.Forward2D(I);
        MathNetFFT2D.Forward2D(K);

        // --- 4) Multiply spectra: Y = Ifreq * H ---
        Complex[,] Y = new Complex[size, size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Y[x, y] = I[x, y] * K[x, y];
            }
        }

        // --- 5) Inverse FFT: Y -> Iblur (blurred luminance) ---
        MathNetFFT2D.Inverse2D(Y);

        // --- 6) Find min/max of blurred luminance ---
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double v = Y[x, y].Real;
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }
        }

        Debug.Log($"ApplyPsfBlurCPU: Iblur min={minVal}, max={maxVal}");

        double range = maxVal - minVal;
        if (range <= 1e-8)
        {
            Debug.LogWarning("ApplyPsfBlurCPU: Iblur is nearly constant; output may look flat.");
            range = 1.0;
        }

        // --- 7) Use blurred luminance to scale original RGB (color-preserving blur) ---
        Texture2D blurred = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Normalized blurred luminance in [0,1]
                float Lblur = (float)((Y[x, y].Real - minVal) / range);
                Lblur = Mathf.Clamp01(Lblur);

                // Original color + original luminance
                Color orig = src.GetPixel(x, y);
                float Lorig = 0.2126f * orig.r + 0.7152f * orig.g + 0.0722f * orig.b;
                float denom = Mathf.Max(Lorig, 1e-4f); // avoid divide-by-zero

                // Scale original RGB to match new luminance
                float factor = Lblur / denom;
                float r = Mathf.Clamp01(orig.r * factor);
                float g = Mathf.Clamp01(orig.g * factor);
                float b = Mathf.Clamp01(orig.b * factor);

                blurred.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }

        blurred.Apply();
        return blurred;
    }

     /// <summary>
    /// Applies frequency-domain deconvolution using a precomputed M filter:
    /// corrected = IFFT( FFT(image) * M ).
    ///
    /// - Uses luminance (Y) as the signal to deconvolve.
    /// - Rebuilds color by scaling original RGB to match new luminance.
    /// </summary>
    /// <param name="src">Observed (blurred) source texture, size x size, Read/Write enabled.</param>
    /// <param name="M">Frequency-domain deconvolution filter M(u,v) from GenerateMFilterFromPsf.</param>
    /// <param name="size">Resolution (e.g. 512).</param>
    public static Texture2D ApplyDeconvolution(Texture2D src, Complex[,] M, int size)
    {
        if (src == null)
        {
            Debug.LogError("ApplyDeconvolutionCPU: src is null.");
            return null;
        }
        if (M == null)
        {
            Debug.LogError("ApplyDeconvolutionCPU: M filter is null.");
            return null;
        }

        if (src.width != size || src.height != size)
        {
            Debug.LogWarning($"ApplyDeconvolutionCPU: src is {src.width}x{src.height}, size={size}.");
        }

        int w = size;
        int h = size;

        // --- 1) Pack image into Complex[,] as luminance ---
        Complex[,] I = new Complex[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = src.GetPixel(x, y);
                float L = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                I[x, y] = new Complex(L, 0.0);
            }
        }

        // --- 2) FFT image: I(x,y) -> Ifreq(u,v) ---
        MathNetFFT2D.Forward2D(I); // in-place: I becomes Ifreq

        // --- 3) Multiply spectra: Yfreq = Ifreq * M ---
        Complex[,] Yfreq = new Complex[w, h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Yfreq[x, y] = I[x, y] * M[x, y];
            }
        }

        // --- 4) Inverse FFT: Yfreq(u,v) -> Y(x,y) (corrected luminance) ---
        MathNetFFT2D.Inverse2D(Yfreq); // in-place or returning scaled; depends on your wrapper

        // --- 5) Inspect corrected luminance range ---
        double minVal = double.MaxValue;
        double maxVal = double.MinValue;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double v = Yfreq[x, y].Real;
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }
        }

        Debug.Log($"ApplyDeconvolutionCPU: Lcorr min={minVal}, max={maxVal}");

        // Optional: if range is tiny, avoid divide-by-zero
        double range = maxVal - minVal;
        if (range <= 1e-8)
        {
            Debug.LogWarning("ApplyDeconvolutionCPU: corrected luminance is nearly constant; output may look flat.");
            range = 1.0;
        }

        // --- 6) Build color image using corrected luminance ---
        Texture2D corrected = new Texture2D(w, h, TextureFormat.RGBA32, false);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Option A: direct clamped luminance (no global remap)
                double LcorrRaw = Yfreq[x, y].Real;
                float Lcorr = Mathf.Clamp01((float)LcorrRaw);

                // Optionally, if you prefer global remap to [0,1]:
                // float Lcorr = Mathf.Clamp01((float)((LcorrRaw - minVal) / range));

                Color orig = src.GetPixel(x, y);
                float Lorig = 0.2126f * orig.r + 0.7152f * orig.g + 0.0722f * orig.b;
                float denom = Mathf.Max(Lorig, 1e-4f);

                float factor = Lcorr / denom;
                float r = Mathf.Clamp01(orig.r * factor);
                float g = Mathf.Clamp01(orig.g * factor);
                float b = Mathf.Clamp01(orig.b * factor);

                corrected.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }

        corrected.Apply();

        // Debug: sample center pixel
        Color mid = corrected.GetPixel(w / 2, h / 2);
        Debug.Log($"ApplyDeconvolutionCPU: corrected center pixel = {mid}");

        return corrected;
    }

    public static Texture2D ApplyPreCorrection(Texture2D src, Complex[,] M, int size)
    {
        // This is basically ApplyDeconvolution, but conceptually we call it
        // "pre-correction" because we apply it to the *original* image.
        return ApplyDeconvolution(src, M, size);
    }
}
