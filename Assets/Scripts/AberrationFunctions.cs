using UnityEngine;
using System.Numerics;

/// <summary>
/// Provides mathematical utilities for adjusting optical aberration
/// parameters used in VR visual correction.
/// </summary>
public static class AberrationFunctions
{
    /// <summary>
    /// Computes the effective sphere value S(d) for a given viewing distance
    /// <paramref name="d"/> from the user’s measured sphere <paramref name="Sm"/>.
    ///
    /// <para>
    /// The piecewise function implemented is:
    ///   <br/> S(d) = Sm + 1/d,     if 1/d &lt; |Sm|
    ///   <br/> S(d) = Sm − (8 − 1/d), if (8 − 1/d) &lt; |Sm|
    ///   <br/> S(d) = 0,             otherwise
    /// </para>
    ///
    /// where:
    /// <list type="bullet">
    ///   <item><description>Sm is the optometrist-measured sphere (myopia &lt; 0, hyperopia &gt; 0), in diopters.</description></item>
    ///   <item><description>d is the viewing distance in meters (e.g., virtual screen distance of an HMD).</description></item>
    ///   <item><description>1/d is in diopters and 8 corresponds to a near point of 0.125 m (8 diopters).</description></item>
    /// </list>
    ///
    /// Intuitively:
    /// <list type="bullet">
    ///   <item><description>For far distances (small 1/d), the eye cannot fully compensate and S(d) tends toward Sm (maximal defocus).</description></item>
    ///   <item><description>For some intermediate distances, residual accommodation reduces the effective</description></item>
    ///   <item><description>For distances where the eye can focus naturally, the effective defocus is S(d) = 0.</description></item>
    /// </list>
    /// </summary>
    /// 
    /// <param name="Sm">Measured sphere Sm in diopters (negative for myopia, positive for hyperopia).</param>
    /// <param name="d">Viewing distance d in meters.</param>
    /// <returns>The distance-adjusted effective sphere S(d) in diopters.</returns>
    public static float AdjustSphereForDistance(float Sm, float d)
    {
        float invD = 1.0f / d;
        float absSm = Mathf.Abs(Sm);

        // Case 1: S(d) = Sm + 1/d  when 1/d < |Sm|
        if (invD < absSm)
        {
            return Sm + invD;
        }

        // Case 2: S(d) = Sm − (8 − 1/d)  when (8 − 1/d) < |Sm|
        float term = 8.0f - invD;
        if (term < absSm)
        {
            return Sm - term;
        }

        // Case 3: S(d) = 0  otherwise (eye can focus at this distance)
        return 0.0f;
    }

    /// <summary>
    /// Computes the low-order Zernike coefficients (Z₋₂², Z₀², Z₂²) for a given eye prescription, following the formulation from the aberration-correction paper on VR HMDs.
    /// 
    /// The coefficients are computed using:
    /// <para>
    ///     <br/> c₋₂² = (R² · C · sin(2A)) / (4√6)
    ///     <br/> c₀²  = −(R² · (S + C / 2)) / (4√3)
    ///     <br/> c₂²  = (R² · C · cos(2A)) / (4√6)
    /// </para>
    /// 
    /// <para>
    /// where:
    ///     <br/>• S = adjusted sphere value S(d) using the HMD viewing distance  
    ///     <br/>• C = cylinder in diopters  
    ///     <br/>• A = cylinder axis in radians  
    ///     <br/>• R = pupil radius in millimeters  
    /// </para>
    /// <para>
    /// These coefficients describe the aberrated wavefront W(x, y) that is later transformed into the point-spread function (PSF) for kernel generation.
    /// </para>
    /// </summary>
    /// 
    /// <param name="p">Eye prescription containing sphere, cylinder, axis, pupil radius, and viewing distance.</param>
    /// <returns>A <see cref="ZernikeCoefficients"/> struct containing c₋₂², c₀², and c₂².</returns>
    public static ZernikeCoefficients ComputeZernikeCoeffs(EyePrescription p)
    {
        // Convert axis to radians
        float A = p.Axis * Mathf.Deg2Rad;

        // Adjusted sphere for the given viewing distance
        float S = AberrationFunctions.AdjustSphereForDistance(p.Sphere, p.ViewingDistance);
        float C = p.Cylinder;
        float R = p.PupilRadius;

        float R2 = R * R;

        float sqrt6 = Mathf.Sqrt(6.0f);
        float sqrt3 = Mathf.Sqrt(3.0f);

        ZernikeCoefficients z;
        z.cMinus2_2 = R2 * C * Mathf.Sin(2.0f * A) / (4.0f * sqrt6);
        z.c0_2      = -R2 * (S + C * 0.5f) / (4.0f * sqrt3);
        z.c2_2      = R2 * C * Mathf.Cos(2.0f * A) / (4.0f * sqrt6);

        return z;
    }

    /// <summary>
    /// Computes the wavefront W(x, y) for normalized pupil coordinates x, y ∈ [-1, 1], using the low-order Zernike coefficients:
    /// <para>
    /// The resulting wavefront is:
    ///     <br/> W(x, y) = c₋₂² · Z₋₂²(x, y)
    ///                   + c₀²  · Z₀²(x, y)
    ///                   + c₂²  · Z₂²(x, y)
    /// </para>
    ///
    /// <para>
    /// where the Zernike polynomials are:
    ///     <br/> Z₋₂²(x, y) = 2√6 · x · y
    ///     <br/> Z₀²(x, y)  = √3 · (2(x² + y²) − 1)
    ///     <br/> Z₂²(x, y)  = √6 · (x² − y²)
    /// </para>
    /// Only valid when x² + y² ≤ 1 (inside the pupil).
    /// </summary>
    /// 
    /// <param name="z">Zernike coefficients.</param>
    /// <param name="x">Normalized x-coordinate (−1..1).</param>
    /// <param name="y">Normalized y-coordinate (−1..1).</param>
    /// <returns>The scalar wavefront value W(x, y).</returns>
    public static float ComputeWavefront(ZernikeCoefficients z, float x, float y, float strength = 1f)
    {
        float r2 = x * x + y * y;
        if (r2 > 1f)
            return 0f;  // outside pupil: wavefront undefined → treat as zero

        float sqrt6 = Mathf.Sqrt(6f);
        float sqrt3 = Mathf.Sqrt(3f);

        // Zernike basis terms
        float Z_m2 = 2f * sqrt6 * x * y;
        float Z_0  = sqrt3 * (2f * r2 - 1f);
        float Z_2  = sqrt6 * (x * x - y * y);

        // Wavefront W(x,y)
        float W =
        z.cMinus2_2 * Z_m2 +
        z.c0_2      * Z_0  +
        z.c2_2      * Z_2;

        return strength * W;
    }

    /// <summary>
    /// Generates the generalized pupil function:
    /// <para>
    ///     P(x, y) · exp(-i · 2π · W(x, y) / λ)
    /// </para>
    /// where W(x, y) is obtained from the Zernike coefficients.
    /// <para>
    /// Returns a complex-valued 2D array representing the pupil function,
    /// which can later be Fourier transformed into the PSF.
    /// </para>
    /// </summary>
    /// <param name="z">Zernike coefficients (c₋₂², c₀², c₂²).</param>
    /// <param name="size">Resolution of the pupil grid (e.g., 512×512).</param>
    /// <param name="wavelength">Light wavelength in nanometers (e.g. 550 nm).</param>
    public static Complex[,] GeneratePupilFunction(ZernikeCoefficients z, int size, float wavelength, float blurStrength = 1f)
    {
        Complex[,] pupil = new Complex[size, size];

        // Convert wavelength from nanometers to millimeters (to match W units).
        float lambda = wavelength * 1e-6f;
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                // Normalized pupil coordinates in range [-1, 1]
                float nx = (x - half) / half;
                float ny = (y - half) / half;

                float r2 = nx * nx + ny * ny;
                if (r2 <= 1f)
                {
                    // Wavefront W(x, y) from Zernike coefficients
                    float W = ComputeWavefront(z, nx, ny, blurStrength);

                    // Complex phase term exp(-i 2π W / λ)
                    float phase = -2f * Mathf.PI * W / lambda;
                    pupil[x, y] = new Complex(Mathf.Cos(phase), Mathf.Sin(phase));
                }
                else
                {
                    // Outside the pupil aperture
                    pupil[x, y] = Complex.Zero;
                }
            }
        }

        return pupil;
    }
}
