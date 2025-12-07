using UnityEngine;
using System.Numerics;

/// <summary>
/// Applies aberration blur and correction to a 2D texture using the PSF
/// derived from an EyePrescription stored in an AberrationConfig.
/// 
/// Designed as a debug/preview tool:
/// - ApplyBlur(): simulate the aberrated (blurred) image
/// - ApplyCorrection(): try to undo that blur via deconvolution filter M
/// </summary>
[RequireComponent(typeof(Renderer))]
public class ProcessedImage : MonoBehaviour
{
    [Header("Inputs")]
    [Tooltip("Source texture to blur (should be kernelSize x kernelSize, Read/Write enabled).")]
    public Texture2D sourceTexture;

    [Tooltip("Aberration configuration containing OD/OS prescriptions.")]
    public AberrationConfig aberrationConfig;

    [Tooltip("If true, use the right eye (OD) prescription, otherwise left eye (OS).")]
    public bool useRightEye = true;

    [Tooltip("Prescription actually used for the last operation (for debug/inspection).")]
    public EyePrescription activeEyePrescription;

    [Header("Optics Settings")]
    [Tooltip("PSF / kernel resolution. Should match source texture size (e.g. 512).")]
    public int kernelSize = 512;

    [Tooltip("Wavelength in nanometers (e.g. 550 nm for green).")]
    public float wavelengthNm = 550f;

    [Header("Output")]
    [Tooltip("Optional: material to receive the output texture. If null, uses this object's Renderer.material.")]
    public Material targetMaterialOverride;

    [SerializeField] Renderer _renderer;

    [Header("Aberration Debug Values")]
    [Tooltip("Adjusted sphere S(d) for the active prescription.")]
    public float sd;

    [Tooltip("Zernike coefficients computed from the active prescription.")]
    public ZernikeCoefficients zernikeCoeffs;

    // Cached intermediates
    Texture2D _psfTexture;
    Texture2D _blurredTexture;
    Texture2D _correctedTexture;
    Complex[,] _mFilter;

    void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();
    }

    void Start()
    {
        if (aberrationConfig != null)
        {
            UpdateActivePrescription();
        }
    }

    /// <summary>
    /// Updates activeEyePrescription, sd, and zernikeCoeffs based on
    /// useRightEye and aberrationConfig.
    /// </summary>
    void UpdateActivePrescription()
    {
        if (aberrationConfig == null)
        {
            Debug.LogWarning("ProcessedImage: aberrationConfig is null; cannot update prescription.");
            return;
        }

        activeEyePrescription = useRightEye ? aberrationConfig.OD : aberrationConfig.OS;

        sd = AberrationFunctions.AdjustSphereForDistance(
            activeEyePrescription.Sphere,
            activeEyePrescription.ViewingDistance);

        zernikeCoeffs = AberrationFunctions.ComputeZernikeCoeffs(activeEyePrescription);
    }

    /// <summary>
    /// Common validation checks before doing any processing.
    /// </summary>
    bool ValidateInputs()
    {
        if (sourceTexture == null)
        {
            Debug.LogError("ProcessedImage: sourceTexture is null.");
            return false;
        }

        if (aberrationConfig == null)
        {
            Debug.LogError("ProcessedImage: aberrationConfig is not assigned.");
            return false;
        }

        if (!sourceTexture.isReadable)
        {
            Debug.LogError("ProcessedImage: sourceTexture is not readable. Enable Read/Write in import settings.");
            return false;
        }

        if (sourceTexture.width != kernelSize || sourceTexture.height != kernelSize)
        {
            Debug.LogWarning($"ProcessedImage: sourceTexture is {sourceTexture.width}x{sourceTexture.height}, " +
                             $"but kernelSize is {kernelSize}. Results may be incorrect.");
        }

        return true;
    }

    // ------------------------------------------------------------------------
    // BLUR STEP
    // ------------------------------------------------------------------------

    /// <summary>
    /// Generates a PSF from the active prescription and applies blur to
    /// sourceTexture. The result is assigned to the material and cached
    /// in _blurredTexture.
    /// </summary>
    public void ApplyBlur()
    {
        if (!ValidateInputs())
            return;

        // 1) Update prescription + debug fields
        UpdateActivePrescription();

        // 2) Generate PSF via Math.NET-based KernelFunctions
        _psfTexture = KernelFunctions.GeneratePsfKernelCpu(
            activeEyePrescription,
            kernelSize,
            wavelengthNm);

        if (_psfTexture == null)
        {
            Debug.LogError("ProcessedImage: GeneratePsfKernelCpu returned null.");
            return;
        }

        Debug.Log($"ProcessedImage: Generated PSF {_psfTexture.width}x{_psfTexture.height}");

        // 3) Apply blur using frequency-domain convolution
        _blurredTexture = ImageProcessingFunctions.ApplyPsfBlur(
            sourceTexture,
            _psfTexture,
            kernelSize);

        if (_blurredTexture == null)
        {
            Debug.LogError("ProcessedImage: ApplyPsfBlur returned null.");
            return;
        }

        // Optional: inspect center pixel
        Color mid = _blurredTexture.GetPixel(kernelSize / 2, kernelSize / 2);
        Debug.Log($"ProcessedImage: blurred center pixel = {mid}");

        // 4) Assign blurred texture to material
        Material mat = targetMaterialOverride != null
            ? targetMaterialOverride
            : _renderer.material;

        mat.mainTexture = _blurredTexture;

        if (mat.HasProperty("_Color"))
            mat.color = Color.white;
    }

    // ------------------------------------------------------------------------
    // CORRECTION STEP
    // ------------------------------------------------------------------------

    /// <summary>
    /// Builds a deconvolution filter M from the current PSF and applies
    /// frequency-domain correction to the blurred image.
    /// 
    /// Expects that ApplyBlur() has already been called so that:
    /// - _psfTexture is valid
    /// - _blurredTexture is the observed image
    /// </summary>
    public void ApplyCorrection(double epsilon = 1e-3)
    {
        if (!ValidateInputs())
            return;

        if (_psfTexture == null)
        {
            Debug.LogError("ProcessedImage: _psfTexture is null. Run ApplyBlur() first.");
            return;
        }

        if (_blurredTexture == null)
        {
            Debug.LogError("ProcessedImage: _blurredTexture is null. Run ApplyBlur() first.");
            return;
        }

        // 1) Build deconvolution filter M from PSF (if not cached or if you want to rebuild)
        _mFilter = DeconvolutionFunctions.GenerateMFilterFromPsf(
            _psfTexture,
            kernelSize,
            epsilon);

        if (_mFilter == null)
        {
            Debug.LogError("ProcessedImage: GenerateMFilterFromPsf returned null.");
            return;
        }

        // 2) Apply deconvolution to the blurred image
        _correctedTexture = ImageProcessingFunctions.ApplyDeconvolution(
            _blurredTexture,
            _mFilter,
            kernelSize);

        if (_correctedTexture == null)
        {
            Debug.LogError("ProcessedImage: ApplyDeconvolution returned null.");
            return;
        }

        Color mid = _correctedTexture.GetPixel(kernelSize / 2, kernelSize / 2);
        Debug.Log($"ProcessedImage: corrected center pixel = {mid}");

        // 3) Show corrected result
        Material mat = targetMaterialOverride != null
            ? targetMaterialOverride
            : _renderer.material;

        mat.mainTexture = _correctedTexture;

        if (mat.HasProperty("_Color"))
            mat.color = Color.white;
    }

    // ------------------------------------------------------------------------
    // CONVENIENCE CONTEXT MENUS
    // ------------------------------------------------------------------------

    [ContextMenu("Apply Blur Only")]
    void ApplyBlurContextMenu()
    {
        ApplyBlur();
    }

    [ContextMenu("Apply Correction (use last blur)")]
    void ApplyCorrectionContextMenu()
    {
        ApplyCorrection();
    }

    [ContextMenu("Apply Blur Then Correction")]
    void ApplyBlurThenCorrectionContextMenu()
    {
        ApplyBlur();
        ApplyCorrection();
    }
}
