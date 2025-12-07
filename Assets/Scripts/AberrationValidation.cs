using UnityEngine;
using System.Numerics;

/// <summary>
/// Full pipeline validation tool (paper-style):
///
/// 1. EyePrescription -> PSF
/// 2. PSF -> blur(source)              [reference blur]
/// 3. PSF -> M filter
/// 4. PRE-CORRECT:  I_pre = IFFT(FFT(source) * M)
/// 5. RETINA:       I_ret = I_pre * PSF
///
/// Shows:
/// - Original
/// - Blurred (no correction)
/// - Pre-corrected image (what the display would show)
/// - Retinal image (pre-corrected image blurred by PSF)
/// - PSF
///
/// Intended for offline / debug use, not real-time VR.
/// </summary>
public class AberrationValidationTool : MonoBehaviour
{
    [Header("Inputs")]
    [Tooltip("Sharp source texture to test with (should be kernelSize x kernelSize, Read/Write enabled).")]
    public Texture2D sourceTexture;

    [Tooltip("Aberration configuration containing OD/OS prescriptions.")]
    public AberrationConfig aberrationConfig;

    [Tooltip("If true, use the right eye (OD) prescription; otherwise left eye (OS).")]
    public bool useRightEye = true;

    [Header("Optics Settings")]
    [Tooltip("PSF / kernel resolution (should match source texture size, e.g. 512).")]
    public int kernelSize = 512;

    [Tooltip("Wavelength in nanometers (e.g. 550 nm for green).")]
    public float wavelengthNm = 550f;

    [Tooltip("Regularization epsilon used when building the deconvolution filter M.")]
    public double deconvEpsilon = 1e-3;

    [Header("Output Renderers (optional)")]
    [Tooltip("Renderer that will show the original source texture.")]
    public Renderer originalRenderer;

    [Tooltip("Renderer that will show the blurred image (no correction).")]
    public Renderer blurredRenderer;

    [Tooltip("Renderer that will show the pre-corrected image (what the display would show).")]
    public Renderer preCorrectedRenderer;

    [Tooltip("Renderer that will show the simulated retinal image (pre-corrected + PSF).")]
    public Renderer retinalRenderer;

    [Tooltip("Renderer that will show the PSF texture (for visualization).")]
    public Renderer psfRenderer;

    [Header("Debug / Inspection")]
    [Tooltip("Prescription actually used for the last run.")]
    public EyePrescription activeEyePrescription;

    [Tooltip("Strength to scale the blur")]
    // 2.5e-05f most closely matched the papers
    public float blurStrength = 2.5e-05f;

    [Tooltip("Adjusted sphere S(d) for the active prescription.")]
    public float sd;

    [Tooltip("Zernike coefficients computed from the active prescription.")]
    public ZernikeCoefficients zernikeCoeffs;

    [Tooltip("Last generated PSF texture.")]
    public Texture2D psfTexture;

    [Tooltip("Last blurred texture (source * PSF, no pre-correction).")]
    public Texture2D blurredTexture;

    [Tooltip("Last pre-corrected texture (what the display would show).")]
    public Texture2D preCorrectedTexture;

    [Tooltip("Last simulated retinal texture (pre-corrected then blurred by PSF).")]
    public Texture2D retinalTexture;

    private Complex[,] _mFilter;

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    bool ValidateInputs()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("AberrationValidationTool: sourceTexture is null.");
            return false;
        }

        if (aberrationConfig == null)
        {
            Debug.LogError("AberrationValidationTool: aberrationConfig is not assigned.");
            return false;
        }

        if (!sourceTexture.isReadable)
        {
            Debug.LogError("AberrationValidationTool: sourceTexture is not readable. Enable Read/Write in import settings.");
            return false;
        }

        if (sourceTexture.width != kernelSize || sourceTexture.height != kernelSize)
        {
            Debug.LogWarning(
                $"AberrationValidationTool: sourceTexture is {sourceTexture.width}x{sourceTexture.height}, " +
                $"but kernelSize is {kernelSize}. This may cause artifacts.");
        }

        return true;
    }

    void UpdateActivePrescription()
    {
        activeEyePrescription = useRightEye ? aberrationConfig.OD : aberrationConfig.OS;

        sd = AberrationFunctions.AdjustSphereForDistance(
            activeEyePrescription.Sphere,
            activeEyePrescription.ViewingDistance);

        zernikeCoeffs = AberrationFunctions.ComputeZernikeCoeffs(activeEyePrescription);
    }

    void AssignTextureToRenderer(Renderer r, Texture2D tex)
    {
        if (r == null || tex == null) return;

        // For debug PSF/retina, use a simple unlit texture shader.
        Shader unlitTex = Shader.Find("Unlit/Texture");
        if (unlitTex != null)
        {
            var mat = new Material(unlitTex);
            mat.mainTexture = tex;
            r.material = mat;
        }
        else
        {
            var mat = r.material;
            mat.mainTexture = tex;
            if (mat.HasProperty("_Color"))
                mat.color = Color.white;
        }
    }

    // ------------------------------------------------------------------------
    // STEP 1: PSF GENERATION (like in the paper)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Generates the PSF from the current eye prescription using the
    /// paper-style pipeline:
    ///
    /// EyePrescription -> Zernike -> Wavefront -> Pupil -> FFT -> PSF
    /// </summary>
    public void GeneratePsfOnly()
    {
        if (!ValidateInputs())
            return;

        UpdateActivePrescription();

        psfTexture = KernelFunctions.GeneratePsfOnly(
            activeEyePrescription,
            kernelSize,
            wavelengthNm,
            blurStrength);

        if (psfTexture == null)
        {
            Debug.LogError("AberrationValidationTool: GeneratePsfOnly returned null.");
            return;
        }

        Debug.Log($"AberrationValidationTool: Generated PSF {psfTexture.width}x{psfTexture.height}");

        AssignTextureToRenderer(psfRenderer, psfTexture);
        AssignTextureToRenderer(originalRenderer, sourceTexture);
    }

    // ------------------------------------------------------------------------
    // STEP 2: BLUR USING PSF (reference retinal image WITHOUT pre-correction)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Applies PSF blur to the source texture using the currently generated PSF.
    /// This corresponds to the "simulated retinal image" without any pre-correction.
    /// Expects GeneratePsfOnly() to have been called first.
    /// </summary>
    public void ApplyBlurWithCurrentPsf()
    {
        if (!ValidateInputs())
            return;

        if (psfTexture == null)
        {
            Debug.LogError("AberrationValidationTool: psfTexture is null. Run GeneratePsfOnly() first.");
            return;
        }

        blurredTexture = ImageProcessingFunctions.ApplyPsfBlur(
            sourceTexture,
            psfTexture,
            kernelSize);

        if (blurredTexture == null)
        {
            Debug.LogError("AberrationValidationTool: ApplyPsfBlur returned null.");
            return;
        }

        Color mid = blurredTexture.GetPixel(kernelSize / 2, kernelSize / 2);
        Debug.Log($"AberrationValidationTool: blurred (no pre-corr) center pixel = {mid}");

        AssignTextureToRenderer(blurredRenderer, blurredTexture);
        AssignTextureToRenderer(originalRenderer, sourceTexture);
    }

    // ------------------------------------------------------------------------
    // STEP 3: BUILD M FILTER & PRE-CORRECT ORIGINAL IMAGE (top row)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Builds the deconvolution filter M from the current PSF and applies
    /// pre-correction to the ORIGINAL source image:
    ///
    /// I_pre = IFFT( FFT(I_source) * M ).
    ///
    /// This is the "pre-corrected" display image (top row in the paper).
    /// Expects GeneratePsfOnly() to have been called first.
    /// </summary>
    public void ApplyPreCorrectionWithCurrentPsf()
    {
        if (!ValidateInputs())
            return;

        if (psfTexture == null)
        {
            Debug.LogError("AberrationValidationTool: psfTexture is null. Run GeneratePsfOnly() first.");
            return;
        }

        // 1) Build M from PSF (if not already or if you want to always rebuild)
        _mFilter = DeconvolutionFunctions.GenerateMFilterFromPsf(
            psfTexture,
            kernelSize,
            deconvEpsilon);

        if (_mFilter == null)
        {
            Debug.LogError("AberrationValidationTool: GenerateMFilterFromPsf returned null.");
            return;
        }

        // 2) Pre-correct the ORIGINAL image (not the blurred one)
        preCorrectedTexture = ImageProcessingFunctions.ApplyDeconvolution(
            sourceTexture,
            _mFilter,
            kernelSize);

        if (preCorrectedTexture == null)
        {
            Debug.LogError("AberrationValidationTool: ApplyDeconvolution (pre-correction) returned null.");
            return;
        }

        Color mid = preCorrectedTexture.GetPixel(kernelSize / 2, kernelSize / 2);
        Debug.Log($"AberrationValidationTool: pre-corrected center pixel = {mid}");

        AssignTextureToRenderer(preCorrectedRenderer, preCorrectedTexture);
    }

    // ------------------------------------------------------------------------
    // STEP 4: SIMULATE RETINAL IMAGE FROM PRE-CORRECTED IMAGE (bottom row)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Simulates the retinal image by blurring the pre-corrected display image
    /// with the PSF:
    ///
    /// I_ret = I_pre * PSF.
    ///
    /// This should look close to the original if pre-correction works well.
    /// Expects:
    /// - GeneratePsfOnly() has produced psfTexture
    /// - ApplyPreCorrectionWithCurrentPsf() has produced preCorrectedTexture
    /// </summary>
    public void ApplyRetinalSimulationWithCurrentPsf()
    {
        if (!ValidateInputs())
            return;

        if (psfTexture == null)
        {
            Debug.LogError("AberrationValidationTool: psfTexture is null. Run GeneratePsfOnly() first.");
            return;
        }

        if (preCorrectedTexture == null)
        {
            Debug.LogError("AberrationValidationTool: preCorrectedTexture is null. Run ApplyPreCorrectionWithCurrentPsf() first.");
            return;
        }

        retinalTexture = ImageProcessingFunctions.ApplyPsfBlur(
            preCorrectedTexture,
            psfTexture,
            kernelSize);

        if (retinalTexture == null)
        {
            Debug.LogError("AberrationValidationTool: ApplyPsfBlur (retinal) returned null.");
            return;
        }

        AssignTextureToRenderer(retinalRenderer, retinalTexture);

        double mse = ComputeMse(sourceTexture, retinalTexture);
        Debug.Log($"AberrationValidationTool: MSE(original, retinal) = {mse}");
    }

    // ------------------------------------------------------------------------
    // FULL PIPELINE (paper-style)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Runs the full validation pipeline:
    /// 1. Generate PSF from prescription
    /// 2. Blur the source (reference retinal image without pre-correction)
    /// 3. Pre-correct the original image using M
    /// 4. Blur the pre-corrected image with PSF (simulated retinal image)
    /// </summary>
    public void RunFullValidation()
    {
        GeneratePsfOnly();
        ApplyBlurWithCurrentPsf();          // reference: uncorrected retinal image
        ApplyPreCorrectionWithCurrentPsf(); // top row: pre-corrected display image
        ApplyRetinalSimulationWithCurrentPsf(); // bottom row: simulated retinal image
    }

    // ------------------------------------------------------------------------
    // SIMPLE ERROR METRIC
    // ------------------------------------------------------------------------

    double ComputeMse(Texture2D a, Texture2D b)
    {
        if (a == null || b == null) return double.NaN;
        if (a.width != b.width || a.height != b.height)
        {
            Debug.LogWarning("ComputeMse: texture sizes differ.");
            return double.NaN;
        }

        int w = a.width;
        int h = a.height;
        double sum = 0.0;
        int count = w * h;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color ca = a.GetPixel(x, y);
                Color cb = b.GetPixel(x, y);

                // Compare per-pixel luminance
                float La = 0.2126f * ca.r + 0.7152f * ca.g + 0.0722f * ca.b;
                float Lb = 0.2126f * cb.r + 0.7152f * cb.g + 0.0722f * cb.b;

                double diff = La - Lb;
                sum += diff * diff;
            }
        }

        return sum / count;
    }

    // ------------------------------------------------------------------------
    // CONTEXT MENUS FOR EASY USE FROM INSPECTOR
    // ------------------------------------------------------------------------

    [ContextMenu("1) Generate PSF Only")]
    private void Context_GeneratePsfOnly()
    {
        GeneratePsfOnly();
    }

    [ContextMenu("2) Blur Source With Current PSF (no pre-correction)")]
    private void Context_ApplyBlurWithCurrentPsf()
    {
        ApplyBlurWithCurrentPsf();
    }

    [ContextMenu("3) Pre-Correct Original With Current PSF (build M)")]
    private void Context_ApplyPreCorrectionWithCurrentPsf()
    {
        ApplyPreCorrectionWithCurrentPsf();
    }

    [ContextMenu("4) Simulate Retina From Pre-Corrected Image")]
    private void Context_ApplyRetinalSimulationWithCurrentPsf()
    {
        ApplyRetinalSimulationWithCurrentPsf();
    }

    [ContextMenu("Run Full Validation (1→2→3→4)")]
    private void Context_RunFullValidation()
    {
        RunFullValidation();
    }
}
