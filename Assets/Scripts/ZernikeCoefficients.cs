/// <summary>
/// Represents the low-order Zernike polynomial coefficients used to
/// describe the wavefront aberration of an eye.
/// 
/// These coefficients correspond to:
/// <list type="bullet">
///   <item><description><c>c₋₂²</c> — Oblique astigmatism term (Z₋₂²)</description></item>
///   <item><description><c>c₀²</c>  — Defocus term (Z₀²)</description></item>
///   <item><description><c>c₂²</c>  — Vertical astigmatism term (Z₂²)</description></item>
/// </list>
/// </summary>
[System.Serializable]
public struct ZernikeCoefficients
{
    /// <summary>
    /// Coefficient for Zernike term Z₋₂² (oblique astigmatism).
    /// </summary>
    public float cMinus2_2;

    /// <summary>
    /// Coefficient for Zernike term Z₀² (defocus), based on sphere and cylinder.
    /// </summary>
    public float c0_2;

    /// <summary>
    /// Coefficient for Zernike term Z₂² (vertical astigmatism).
    /// </summary>
    public float c2_2;
}
