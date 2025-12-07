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
/// Shows (for the currently selected eye):
/// - Original
/// - Blurred (no correction)
/// - Pre-corrected image (what the display would show)
/// - Retinal image (pre-corrected image blurred by PSF)
/// - PSF
///
/// Internally computes PSFs (and M filters) for *both* eyes (OD/OS),
/// but only previews one at a time controlled by useRightEye.
///
/// Intended for offline / debug use, not real-time VR.
/// </summary>
public class AberrationCorrectionRenderer : MonoBehaviour
{
    [Header("Inputs")]
    [Tooltip("Sharp source texture to test with (should be kernelSize x kernelSize, Read/Write enabled).")]
    public Texture2D sourceTexture;

    [Tooltip("Aberration configuration containing OD/OS prescriptions.")]
    public AberrationConfig aberrationConfig;

    [Tooltip("If true, preview the right eye (OD); otherwise left eye (OS).")]
    public bool useRightEye = true;

    [Header("Optics Settings")]
    [Tooltip("PSF / kernel resolution (should match source texture size, e.g. 512).")]
    public int kernelSize = 512;

    [Tooltip("Wavelength in nanometers (e.g. 550 nm for green).")]
    public float wavelengthNm = 550f;

    [Tooltip("Regularization epsilon used when building the deconvolution filter M.")]
    public double deconvEpsilon = 1e-3;

    [Header("Blur Strength")]
    [Tooltip("Scalar applied to the wavefront / Zernike amplitudes when building the PSF. " +
             "Smaller values weaken blur; 2.5e-05f roughly matched the paper visually.")]
    public float blurStrength = 2.5e-05f;

    [Header("Output Renderers (preview for the currently selected eye)")]
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

    [Header("Debug / Inspection (active eye preview)")]
    [Tooltip("Prescription actually used for the last *preview* run (based on useRightEye).")]
    public EyePrescription activeEyePrescription;

    [Tooltip("Adjusted sphere S(d) for the active prescription.")]
    public float sd;

    [Tooltip("Zernike coefficients computed from the active prescription.")]
    public ZernikeCoefficients zernikeCoeffs;

    [Tooltip("Last generated PSF texture for the currently selected eye.")]
    public Texture2D psfTexture;

    [Tooltip("Last blurred texture (source * PSF, no pre-correction) for the selected eye.")]
    public Texture2D blurredTexture;

    [Tooltip("Last pre-corrected texture (what the display would show) for the selected eye.")]
    public Texture2D preCorrectedTexture;

    [Tooltip("Last simulated retinal texture (pre-corrected then blurred by PSF) for the selected eye.")]
    public Texture2D retinalTexture;

    // ------------------------------------------------------------------------
    // Internal per-eye data (stereo support)
    // ------------------------------------------------------------------------

    // Per-eye PSFs (OD/OS)
    Texture2D _psfRight;
    Texture2D _psfLeft;

    // Per-eye deconvolution filters M (frequency-domain)
    Complex[,] _mFilterRight;
    Complex[,] _mFilterLeft;

    // Optional: store per-eye S(d) and Zernike for debugging if needed
    float _sdRight, _sdLeft;
    ZernikeCoefficients _zernikeRight, _zernikeLeft;

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

    void UpdateActivePrescriptionForPreview()
    {
        activeEyePrescription = useRightEye ? aberrationConfig.OD : aberrationConfig.OS;

        sd = AberrationFunctions.AdjustSphereForDistance(
            activeEyePrescription.Sphere,
            activeEyePrescription.ViewingDistance);

        zernikeCoeffs = AberrationFunctions.ComputeZernikeCoeffs(activeEyePrescription);
    }

    /// <summary>
    /// Replaces the sourceTexture AND assigns a new display material
    /// to all available renderers (original, blurred, precorrected, retina, PSF).
    /// Useful when changing test images or applying a unified material at runtime.
    /// </summary>
    public void SetSourceAndMaterial(Texture2D newSource, Material newMat)
    {
        if (newSource == null)
        {
            Debug.LogError("SetSourceAndMaterial: newSource is null.");
            return;
        }

        if (!newSource.isReadable)
        {
            Debug.LogWarning("SetSourceAndMaterial: newSource is not readable. PSF/FFT operations may fail.");
        }

        // ---- 1) Replace source texture ----
        sourceTexture = newSource;

        // If renderer for original image exists, assign immediately
        if (originalRenderer != null)
        {
            Util.Instance.ApplyMaterial(originalRenderer, newMat);
            originalRenderer.material.mainTexture = newSource;
        }

        // ---- 2) Replace materials for all output renderers ----
        Util.Instance.ApplyMaterial(blurredRenderer, newMat);
        Util.Instance.ApplyMaterial(preCorrectedRenderer, newMat);
        Util.Instance.ApplyMaterial(retinalRenderer, newMat);
        Util.Instance.ApplyMaterial(psfRenderer, newMat);

        // NOTE: textures assigned after re-running pipeline

        Debug.Log("AberrationValidationTool: Source texture and renderer materials updated.");
    }

    void AssignTextureToRenderer(Renderer r, Texture2D tex)
    {
        if (r == null || tex == null) return;

        // For debug PSF/retina, use a simple unlit texture shader if available.
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

    Texture2D GetCurrentPsfTexture()
    {
        return useRightEye ? _psfRight : _psfLeft;
    }

    Complex[,] GetCurrentMFilter()
    {
        return useRightEye ? _mFilterRight : _mFilterLeft;
    }

    void SetCurrentMFilter(Complex[,] M)
    {
        if (useRightEye)
            _mFilterRight = M;
        else
            _mFilterLeft = M;
    }

    // ------------------------------------------------------------------------
    // STEP 1: PSF GENERATION (for both eyes, preview one)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Generates PSFs for both eyes (OD/OS) using the paper-style pipeline:
    ///
    /// EyePrescription -> Zernike -> Wavefront -> Pupil -> FFT -> PSF
    ///
    /// Then selects either OD or OS PSF to preview based on useRightEye.
    /// </summary>
    public void GeneratePsfOnly()
    {
        if (!ValidateInputs())
            return;

        // ----- Right eye (OD) -----
        EyePrescription pRight = aberrationConfig.OD;
        _sdRight = AberrationFunctions.AdjustSphereForDistance(
            pRight.Sphere, pRight.ViewingDistance);
        _zernikeRight = AberrationFunctions.ComputeZernikeCoeffs(pRight);

        _psfRight = KernelFunctions.GeneratePsfOnly(
            pRight,
            kernelSize,
            wavelengthNm,
            blurStrength);

        if (_psfRight == null)
        {
            Debug.LogError("AberrationValidationTool: GeneratePsfOnly (right eye) returned null.");
        }

        // ----- Left eye (OS) -----
        EyePrescription pLeft = aberrationConfig.OS;
        _sdLeft = AberrationFunctions.AdjustSphereForDistance(
            pLeft.Sphere, pLeft.ViewingDistance);
        _zernikeLeft = AberrationFunctions.ComputeZernikeCoeffs(pLeft);

        _psfLeft = KernelFunctions.GeneratePsfOnly(
            pLeft,
            kernelSize,
            wavelengthNm,
            blurStrength);

        if (_psfLeft == null)
        {
            Debug.LogError("AberrationValidationTool: GeneratePsfOnly (left eye) returned null.");
        }

        // ----- Update active-eye debug info -----
        UpdateActivePrescriptionForPreview();

        // Choose which PSF to preview
        psfTexture = GetCurrentPsfTexture();
        if (psfTexture == null)
        {
            Debug.LogError("AberrationValidationTool: current PSF texture is null. Check OD/OS PSF generation.");
            return;
        }

        Debug.Log(
            $"AberrationValidationTool: Generated PSFs {kernelSize}x{kernelSize} for OD & OS. " +
            $"Previewing {(useRightEye ? "Right (OD)" : "Left (OS)")}.");

        AssignTextureToRenderer(psfRenderer, psfTexture);
        AssignTextureToRenderer(originalRenderer, sourceTexture);
    }

    // ------------------------------------------------------------------------
    // STEP 2: BLUR USING PSF (reference retinal image WITHOUT pre-correction)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Applies PSF blur to the source texture using the PSF of the
    /// currently selected eye. This corresponds to the "simulated retinal
    /// image" without any pre-correction.
    /// Expects GeneratePsfOnly() to have been called first.
    /// </summary>
    public void ApplyBlurWithCurrentPsf()
    {
        if (!ValidateInputs())
            return;

        Texture2D currentPsf = GetCurrentPsfTexture();
        if (currentPsf == null)
        {
            Debug.LogError("AberrationValidationTool: current PSF texture is null. Run GeneratePsfOnly() first.");
            return;
        }

        blurredTexture = ImageProcessingFunctions.ApplyPsfBlur(
            sourceTexture,
            currentPsf,
            kernelSize);

        if (blurredTexture == null)
        {
            Debug.LogError("AberrationValidationTool: ApplyPsfBlur returned null.");
            return;
        }

        Color mid = blurredTexture.GetPixel(kernelSize / 2, kernelSize / 2);
        Debug.Log($"AberrationValidationTool: blurred (no pre-corr, {(useRightEye ? "OD" : "OS")}) center pixel = {mid}");

        AssignTextureToRenderer(blurredRenderer, blurredTexture);
        AssignTextureToRenderer(originalRenderer, sourceTexture);
    }

    // ------------------------------------------------------------------------
    // STEP 3: BUILD M FILTER & PRE-CORRECT ORIGINAL IMAGE (for current eye)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Builds the deconvolution filter M for the currently selected eye
    /// from its PSF and applies pre-correction to the ORIGINAL source image:
    ///
    /// I_pre = IFFT( FFT(I_source) * M ).
    ///
    /// This is the "pre-corrected" display image for that eye (top row in the paper).
    /// Expects GeneratePsfOnly() to have been called first.
    /// </summary>
    public void ApplyPreCorrectionWithCurrentPsf()
    {
        if (!ValidateInputs())
            return;

        Texture2D currentPsf = GetCurrentPsfTexture();
        if (currentPsf == null)
        {
            Debug.LogError("AberrationValidationTool: current PSF texture is null. Run GeneratePsfOnly() first.");
            return;
        }

        // 1) Build M from PSF for this eye
        Complex[,] M = DeconvolutionFunctions.GenerateMFilterFromPsf(
            currentPsf,
            kernelSize,
            deconvEpsilon);

        if (M == null)
        {
            Debug.LogError("AberrationValidationTool: GenerateMFilterFromPsf returned null.");
            return;
        }

        SetCurrentMFilter(M);

        // 2) Pre-correct the ORIGINAL image (not the blurred one)
        preCorrectedTexture = ImageProcessingFunctions.ApplyDeconvolution(
            sourceTexture,
            M,
            kernelSize);

        if (preCorrectedTexture == null)
        {
            Debug.LogError("AberrationValidationTool: ApplyDeconvolution (pre-correction) returned null.");
            return;
        }

        Color mid = preCorrectedTexture.GetPixel(kernelSize / 2, kernelSize / 2);
        Debug.Log($"AberrationValidationTool: pre-corrected ({(useRightEye ? "OD" : "OS")}) center pixel = {mid}");

        AssignTextureToRenderer(preCorrectedRenderer, preCorrectedTexture);
    }

    // ------------------------------------------------------------------------
    // STEP 4: SIMULATE RETINAL IMAGE FROM PRE-CORRECTED IMAGE (current eye)
    // ------------------------------------------------------------------------

    /// <summary>
    /// Simulates the retinal image for the currently selected eye by blurring
    /// the pre-corrected display image with that eye's PSF:
    ///
    /// I_ret = I_pre * PSF.
    ///
    /// This should look close to the original if pre-correction works well.
    /// Expects:
    /// - GeneratePsfOnly() has produced per-eye PSFs
    /// - ApplyPreCorrectionWithCurrentPsf() has produced preCorrectedTexture
    /// </summary>
    public void ApplyRetinalSimulationWithCurrentPsf()
    {
        if (!ValidateInputs())
            return;

        Texture2D currentPsf = GetCurrentPsfTexture();
        if (currentPsf == null)
        {
            Debug.LogError("AberrationValidationTool: current PSF texture is null. Run GeneratePsfOnly() first.");
            return;
        }

        if (preCorrectedTexture == null)
        {
            Debug.LogError("AberrationValidationTool: preCorrectedTexture is null. Run ApplyPreCorrectionWithCurrentPsf() first.");
            return;
        }

        retinalTexture = ImageProcessingFunctions.ApplyPsfBlur(
            preCorrectedTexture,
            currentPsf,
            kernelSize);

        if (retinalTexture == null)
        {
            Debug.LogError("AberrationValidationTool: ApplyPsfBlur (retinal) returned null.");
            return;
        }

        AssignTextureToRenderer(retinalRenderer, retinalTexture);

        double mse = ComputeMse(sourceTexture, retinalTexture);
        Debug.Log($"AberrationValidationTool: MSE(original, retinal, {(useRightEye ? "OD" : "OS")}) = {mse}");
    }

    // ------------------------------------------------------------------------
    // FULL PIPELINE (paper-style) for currently selected eye
    // ------------------------------------------------------------------------

    /// <summary>
    /// Runs the full validation pipeline **for the currently selected eye**:
    /// 1. Generate PSFs for OD & OS (preview one)
    /// 2. Blur the source with the selected eye's PSF (uncorrected retinal image)
    /// 3. Pre-correct the original image using the selected eye's M
    /// 4. Blur the pre-corrected image with that eye's PSF (simulated retinal image)
    /// </summary>
    public void RunFullValidation()
    {
        GeneratePsfOnly();                         // builds PSFs for both eyes, previews one
        ApplyBlurWithCurrentPsf();                 // reference: uncorrected retinal image
        ApplyPreCorrectionWithCurrentPsf();        // pre-corrected display image (current eye)
        ApplyRetinalSimulationWithCurrentPsf();    // simulated retinal image (current eye)
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

    [ContextMenu("1) Generate PSFs (OD+OS) & Preview Current Eye")]
    private void Context_GeneratePsfOnly()
    {
        GeneratePsfOnly();
    }

    [ContextMenu("2) Blur Source With Current Eye PSF (no pre-correction)")]
    private void Context_ApplyBlurWithCurrentPsf()
    {
        ApplyBlurWithCurrentPsf();
    }

    [ContextMenu("3) Pre-Correct Original With Current Eye PSF (build M)")]
    private void Context_ApplyPreCorrectionWithCurrentPsf()
    {
        ApplyPreCorrectionWithCurrentPsf();
    }

    [ContextMenu("4) Simulate Retina From Pre-Corrected Image (Current Eye)")]
    private void Context_ApplyRetinalSimulationWithCurrentPsf()
    {
        ApplyRetinalSimulationWithCurrentPsf();
    }

    [ContextMenu("Run Full Validation (Current Eye, OD/OS toggled by useRightEye)")]
    private void Context_RunFullValidation()
    {
        RunFullValidation();
    }

    [ContextMenu("Run Full Validation for Right Eye (OD)")]
    private void Context_RunFullValidationRight()
    {
        useRightEye = true;
        RunFullValidation();
    }

    [ContextMenu("Run Full Validation for Left Eye (OS)")]
    private void Context_RunFullValidationLeft()
    {
        useRightEye = false;
        RunFullValidation();
    }
}
