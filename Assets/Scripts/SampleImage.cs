using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Renderer))]
public class SampleImage : MonoBehaviour
{
    [Header("Original Assets")]
    [Tooltip("Original texture associated with this sample image.")]
    public Texture2D originalTexture;

    [Tooltip("Original display material for this sample image.")]
    public Material originalMaterial;

    [Header("External References")]
    [Tooltip("Reference to SampleImages parent object")]
    [SerializeField] SampleImages sampleImages;

    [Header("Internal Components")]
    [SerializeField] Renderer _renderer;
    [SerializeField] XRSimpleInteractable _interactable;

    [Header("Hover Parameters")]
    [SerializeField] Color _originalColor;
    [SerializeField] Color _hoverColor = new Color(0.75f, 0.85f, 1.3f);
    [SerializeField] bool _hasColorProperty = false;

    void Awake()
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();

        if (_interactable == null)
            _interactable = GetComponent<XRSimpleInteractable>();

        if (originalMaterial == null)
            originalMaterial = _renderer.material;

        if (originalTexture == null && originalMaterial != null)
            originalTexture = originalMaterial.mainTexture as Texture2D;

        // Cache original color (if shader supports _Color)
        if (_renderer.material.HasProperty("_Color"))
        {
            _originalColor = _renderer.material.color;
            _hasColorProperty = true;
        }

        // XR event subscriptions
        if (_interactable != null)
        {
            _interactable.selectEntered.AddListener(OnGrabbed);
            _interactable.selectExited.AddListener(OnReleased);

            _interactable.hoverEntered.AddListener(OnHoverEnteredXR);
            _interactable.hoverExited.AddListener(OnHoverExitedXR);
        }
    }

    void Start()
    {
        if (sampleImages == null)
            sampleImages = FindFirstObjectByType<SampleImages>();
    }

    // ---------------- XR Callbacks ----------------

    void OnGrabbed(SelectEnterEventArgs args)
    {
        HandleGrab();
    }

    void OnReleased(SelectExitEventArgs args)
    {
        HandleRelease();
    }

    void OnHoverEnteredXR(HoverEnterEventArgs args)
    {
        Highlight(true);
    }

    void OnHoverExitedXR(HoverExitEventArgs args)
    {
        Highlight(false);
    }

    // ---------------- Logic ----------------

    void HandleGrab()
    {
        if (sampleImages != null && originalTexture != null && originalMaterial != null)
        {
            sampleImages.SetActiveSample(originalTexture, originalMaterial, transform.localPosition);
        }
        else
        {
            Debug.LogWarning("SampleImage: Could not set active sample (SampleImages.Instance or assets missing).");
        }
    }

    void HandleRelease()
    {
    }

    void Highlight(bool enable)
    {
        if (!_hasColorProperty) return;

        if (enable)
        {
            _renderer.material.color = _originalColor * _hoverColor;
        }
        else
        {
            _renderer.material.color = _originalColor;
        }
    }

    // ---------------- Inspector Simulation Helpers ----------------

    [ContextMenu("Simulate Grab")]
    void SimGrab() => HandleGrab();

    [ContextMenu("Simulate Release")]
    void SimRelease() => HandleRelease();

    [ContextMenu("Simulate Hover Enter")]
    void SimHoverEnter() => Highlight(true);

    [ContextMenu("Simulate Hover Exit")]
    void SimHoverExit() => Highlight(false);
}
