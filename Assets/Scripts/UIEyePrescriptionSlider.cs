using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIEyePrescriptionSlider : MonoBehaviour
{
    [Header("References")]
    [Tooltip("UI Slider reference")]
    public Slider slider;

    [Tooltip("TextMeshProUGUI reference")]
    public TMP_Text valueText;

    [Tooltip("Aberration Config reference")]
    [SerializeField] AberrationConfig _aberrationConfig;

    [Header("Eye Prescription Target")]
    [SerializeField] EyeSide _eye;
    [SerializeField] EyeParameter _parameter;

    [Header("Slider Settings")]
    [Tooltip("Slider step snap interval (2 decimals)")]
    public float stepSize = 0.25f;
    [Tooltip("Turn step snapping on/off")]
    public bool snapToStep = true;

    void Awake()
    {
        if (slider != null)
            slider.onValueChanged.AddListener(OnSliderChanged);
            #if UNITY_EDITOR
                slider.onValueChanged.AddListener(OnSliderChanged);  
            #endif
    }

    void Start()
    {
        float savedValue = GetCurrentConfigValue();
        slider.value = savedValue;
    }

    float GetCurrentConfigValue()
    {
        var eye = _eye == EyeSide.Right ? _aberrationConfig.OD : _aberrationConfig.OS;

        switch (_parameter)
        {
            case EyeParameter.Sphere:           return eye.Sphere;
            case EyeParameter.Cylinder:         return eye.Cylinder;
            case EyeParameter.Axis:             return eye.Axis;
            case EyeParameter.PupilRadius:      return eye.PupilRadius;
            case EyeParameter.ViewingDistance:  return eye.ViewingDistance;
            default:                            return 0f;
        }
    }

    void SetConfigValue(float value)
    {
        if (_eye == EyeSide.Left)
        {
            switch (_parameter)
            {
                case EyeParameter.Sphere:
                    _aberrationConfig.osSphere = value;
                    break;
                case EyeParameter.Cylinder:
                    _aberrationConfig.osCylinder = value;
                    break;
                case EyeParameter.Axis:
                    _aberrationConfig.osAxis = value;
                    break;
                case EyeParameter.PupilRadius:
                    _aberrationConfig.osPupilRadius = value;
                    break;
                case EyeParameter.ViewingDistance:
                    _aberrationConfig.osViewingDistance = value;
                    break;
            }
        } else {
            switch (_parameter)
            {
                case EyeParameter.Sphere:
                    _aberrationConfig.odSphere = value;
                    break;
                case EyeParameter.Cylinder:
                    _aberrationConfig.odCylinder = value;
                    break;
                case EyeParameter.Axis:
                    _aberrationConfig.odAxis = value;
                    break;
                case EyeParameter.PupilRadius:
                    _aberrationConfig.odPupilRadius = value;
                    break;
                case EyeParameter.ViewingDistance:
                    _aberrationConfig.odViewingDistance = value;
                    break;
            }
        }
    }

    void OnSliderChanged(float value)
    {
        float finalValue = value;

        if (snapToStep)
        {
            finalValue = Mathf.Round(value / stepSize) * stepSize;
            if (!Mathf.Approximately(finalValue, slider.value))
                slider.SetValueWithoutNotify(finalValue);
        }

        SetConfigValue(finalValue);

        if (valueText != null)
            valueText.text = finalValue.ToString("F2");
    }
}
