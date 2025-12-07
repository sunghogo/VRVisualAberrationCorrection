using UnityEngine;

/// <summary>
/// ScriptableObject that holds the optical correction settings for both eyes,
/// including prescription data used to configure aberration or distortion correction
/// in the rendering pipeline.
/// </summary>
[CreateAssetMenu(
    fileName = "NewAberrationConfig",
    menuName = "Custom/Aberration Config",
    order = 0)]
public class AberrationConfig : ScriptableObject
{
    /// <summary>
    /// Optical prescription for the right eye (OD).
    /// </summary>
    public EyePrescription OD;

    /// <summary>
    /// Optical prescription for the left eye (OS).
    /// </summary>
    public EyePrescription OS;

    [Header("Right Eye (OD) Parameters")]
    [SerializeField] float odSphere = 0f;
    [SerializeField] float odCylinder = 0f;
    [SerializeField] float odAxis = 0f;
    [SerializeField] float odPupilRadius = EyePrescription.DEFAULT_PUPIL_RADIUS;
    [SerializeField] float odViewingDistance = EyePrescription.QUEST3_VIEWING_DISTANCE;

    [Header("Left Eye (OS) Parameters")]
    [SerializeField] float osSphere = 0f;
    [SerializeField] float osCylinder = 0f;
    [SerializeField] float osAxis = 0f;
    [SerializeField] float osPupilRadius = EyePrescription.DEFAULT_PUPIL_RADIUS;
    [SerializeField] float osViewingDistance = EyePrescription.QUEST3_VIEWING_DISTANCE;

    /// <summary>
    /// Sets the prescription for the right eye (OD).
    /// </summary>
    public void SetRightEye(
        float sphere,
        float cylinder,
        float axis,
        float pupilRadius,
        float viewingDistance)
    {
        OD.Sphere          = sphere;
        OD.Cylinder        = cylinder;
        OD.Axis            = axis;
        OD.PupilRadius     = pupilRadius;
        OD.ViewingDistance = viewingDistance;
    }

    /// <summary>
    /// Sets the prescription for the left eye (OS).
    /// </summary>
    public void SetLeftEye(
        float sphere,
        float cylinder,
        float axis,
        float pupilRadius,
        float viewingDistance)
    {
        OS.Sphere          = sphere;
        OS.Cylinder        = cylinder;
        OS.Axis            = axis;
        OS.PupilRadius     = pupilRadius;
        OS.ViewingDistance = viewingDistance;
    }

    /// <summary>
    /// Convenience function to set both eye prescriptions at once.
    /// </summary>
    public void SetBothEyes(EyePrescription od, EyePrescription os)
    {
        OD = od;
        OS = os;
    }

    /// <summary>
    /// Applies the serialized inspector parameters (odSphere, osSphere, etc.)
    /// to the OD and OS <see cref="EyePrescription"/> fields.
    /// </summary>
    [ContextMenu("Apply Inspector Parameters")]
    public void ApplyInspectorParameters()
    {
        SetRightEye(
            odSphere,
            odCylinder,
            odAxis,
            odPupilRadius,
            odViewingDistance);

        SetLeftEye(
            osSphere,
            osCylinder,
            osAxis,
            osPupilRadius,
            osViewingDistance);
    }

    void OnValidate()
    {
        ApplyInspectorParameters();
    }
}
