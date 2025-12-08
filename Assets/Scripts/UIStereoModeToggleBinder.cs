using UnityEngine;
using UnityEngine.UI;

public class StereoModeToggleBinder : MonoBehaviour
{
    [Header("References")]
    [Tooltip("UI Toggle that controls stereo mode.")]
    [SerializeField] Toggle _toggle;

    [Tooltip("AberrationCorrectionRenderer that will be controlled.")]
    [SerializeField] AberrationCorrectionRenderer _aberrationCorrectionRenderer;

    private void Awake()
    {
        // Auto-assign Toggle if not set
        if (_toggle == null)
            _toggle = GetComponent<Toggle>();

        if (_toggle == null)
        {
            Debug.LogWarning("StereoModeToggleBinder: No Toggle assigned or found on this GameObject.");
            return;
        }

        // Register callback
        _toggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    private void OnDestroy()
    {
        if (_toggle != null)
            _toggle.onValueChanged.RemoveListener(OnToggleValueChanged);
    }

    /// <summary>
    /// Called by the Toggle when its value changes.
    /// </summary>
    /// <param name="isOn">True if toggle is on (stereo enabled).</param>
    public void OnToggleValueChanged(bool isOn)
    {
        if (_aberrationCorrectionRenderer == null)
        {
            Debug.LogWarning("StereoModeToggleBinder: correctionRenderer is not assigned.");
            return;
        }

        _aberrationCorrectionRenderer.SetStereoMode(isOn);
    }
}
