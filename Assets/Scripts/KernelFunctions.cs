using UnityEngine;
using System.Numerics;

public static class KernelFunctions
{
    /// <summary>
    /// Generates a normalized PSF texture from an eye prescription.
    /// PSF is normalized so that sum(K) = 1 (energy-preserving).
    /// <para>
    /// Pipeline:
    /// <br/>1. Convert prescription → Zernike coefficients.
    /// <br/>2. Generate complex pupil function P(x, y) · exp(-i·2π·W/λ).
    /// <br/>3. Apply 2D FFT to obtain the optical transfer.
    /// <br/>4. Take magnitude squared to get the PSF K(x, y).
    /// <br/>5. Normalize PSF to [0, 1] and pack into an RFloat texture.
    /// </para>
    /// 
    /// </summary>
    /// <param name="p">Eye prescription used to compute aberrations.</param>
    /// <param name="size">Resolution of the PSF (e.g., 256 or 512).</param>
    /// <param name="wavelength">Light wavelength in nanometers (e.g., 550 nm for green).</param>
    /// <returns>A Texture2D with the normalized PSF in the red channel.</returns>
    public static Texture2D GeneratePsfKernelCpu(EyePrescription p, int size, float wavelengthNm)
    {
        // 1) Prescription → Zernike coefficients
        ZernikeCoefficients z = AberrationFunctions.ComputeZernikeCoeffs(p);

        // 2) Zernike → complex pupil function
        Complex[,] pupil = AberrationFunctions.GeneratePupilFunction(z, size, wavelengthNm);

        // 3) Forward 2D FFT of pupil
        MathNetFFT2D.Forward2D(pupil); // in-place

        // 4) Magnitude squared => PSF K(x, y)
        float[,] psf = new float[size, size];
        double sum = 0.0;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Complex c = pupil[x, y];
                double mag2 = c.Real * c.Real + c.Imaginary * c.Imaginary;

                // Clamp tiny negative numerical garbage
                if (mag2 < 0.0) mag2 = 0.0;

                float v = (float)mag2;
                psf[x, y] = v;
                sum += v;
            }
        }

        // Guard: if the PSF is degenerate, avoid division by zero
        if (sum <= 0.0)
            sum = 1.0;

        // 5) Normalize PSF so that sum(K) = 1
        Texture2D psfTex = new Texture2D(size, size, TextureFormat.RFloat, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = psf[x, y] / (float)sum; // now sum over all pixels ≈ 1
                psfTex.SetPixel(x, y, new Color(v, 0f, 0f, 1f));
            }
        }

        psfTex.Apply();
        return psfTex;
    }

    /// <summary>
    /// Generates ONLY the PSF from the optical prescription, matching
    /// the process described in aberration-correction papers:
    ///
    /// 1. Convert eye prescription -> Zernike coefficients
    /// 2. Compute wavefront W(x,y)
    /// 3. Build complex pupil function
    /// 4. FFT -> optical transfer
    /// 5. Magnitude squared -> PSF
    /// 6. Normalize (∑ PSF = 1) → return PSF texture
    ///
    /// No blurring or deconvolution is performed.
    /// </summary>
    public static Texture2D GeneratePsfOnly(EyePrescription p, int size, float wavelength, float blurStrength = 1f)
    {
        // 1) Prescription → Zernike coefficients
        ZernikeCoefficients z = AberrationFunctions.ComputeZernikeCoeffs(p);

        // 2) Build pupil function (complex)
        Complex[,] pupil = AberrationFunctions.GeneratePupilFunction(z, size, wavelength, blurStrength);

        // 3) FFT of pupil (in-place)
        MathNetFFT2D.Forward2D(pupil);

        // 4) Compute PSF = |FFT|²
        float[,] psf = new float[size, size];
        double sum = 0.0;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Complex c = pupil[x, y];
                double mag2 = c.Real * c.Real + c.Imaginary * c.Imaginary;
                float m2 = (float)mag2;

                psf[x, y] = m2;
                sum += mag2;
            }
        }

        // Guard against degenerate PSF
        if (sum <= 1e-12)
            sum = 1.0;

        // 5) Normalize so that ∑ PSF(x,y) = 1, then pack into texture
        float invSum = (float)(1.0 / sum);

        // You can keep RFloat since we only need one channel numerically.
        Texture2D tex = new Texture2D(size, size, TextureFormat.RFloat, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float v = psf[x, y] * invSum; // now energy-normalized
                tex.SetPixel(x, y, new Color(v, 0f, 0f, 1f)); // R = PSF
                // If you want a nicer visual debug: new Color(v, v, v, 1f);
            }
        }

        tex.Apply();
        return tex;
    }
}
