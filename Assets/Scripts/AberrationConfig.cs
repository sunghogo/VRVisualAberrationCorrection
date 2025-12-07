using UnityEngine;

public enum EyeSide
{
    Right,
    Left
}

public enum EyeParameter
{
    Sphere,
    Cylinder,
    Axis,
    PupilRadius,
    ViewingDistance
}

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
    public float odSphere = 0f;
    public float odCylinder = 0f;
    public float odAxis = 0f;
    public float odPupilRadius = EyePrescription.DEFAULT_PUPIL_RADIUS;
    public float odViewingDistance = EyePrescription.QUEST3_VIEWING_DISTANCE;

    [Header("Left Eye (OS) Parameters")]
    public float osSphere = 0f;
    public float osCylinder = 0f;
    public float osAxis = 0f;
    public float osPupilRadius = EyePrescription.DEFAULT_PUPIL_RADIUS;
    public float osViewingDistance = EyePrescription.QUEST3_VIEWING_DISTANCE;

    /// <summary>
    /// Sets the prescription for the right eye (OD).
    /// </summary>
    public void SetRightEye()
    {
        OD.Sphere          = odSphere;
        OD.Cylinder        = odCylinder;
        OD.Axis            = odAxis;
        OD.PupilRadius     = odPupilRadius;
        OD.ViewingDistance = odViewingDistance;
    }
    
    public void SetRightEye(
        float sphere,
        float cylinder,
        float axis,
        float pupilRadius,
        float viewingDistance)
    {
        odSphere          = sphere;
        odCylinder        = cylinder;
        odAxis            = axis;
        odPupilRadius     = pupilRadius;
        odViewingDistance = viewingDistance;
        SetRightEye();
    }

    public void SetRightEye(EyePrescription od)
    {
        odSphere            = od.Sphere;
        odCylinder          = od.Cylinder;
        odAxis              = od.Axis;
        odPupilRadius       = od.PupilRadius;
        odViewingDistance   = od.ViewingDistance;
        SetRightEye();
    }

    /// <summary>
    /// Sets the prescription for the left eye (OS).
    /// </summary>
    public void SetLeftEye()
    {
        OS.Sphere          = osSphere;
        OS.Cylinder        = osCylinder;
        OS.Axis            = osAxis;
        OS.PupilRadius     = osPupilRadius;
        OS.ViewingDistance = osViewingDistance;
    }

    public void SetLeftEye(
        float sphere,
        float cylinder,
        float axis,
        float pupilRadius,
        float viewingDistance)
    {
        osSphere          = sphere;
        osCylinder        = cylinder;
        osAxis            = axis;
        osPupilRadius     = pupilRadius;
        osViewingDistance = viewingDistance;
        SetLeftEye();
    }

    public void SetLeftEye(EyePrescription os)
    {
        osSphere            = os.Sphere;
        osCylinder          = os.Cylinder;
        osAxis              = os.Axis;
        osPupilRadius       = os.PupilRadius;
        osViewingDistance   = os.ViewingDistance;
        SetLeftEye();
    }

    /// <summary>
    /// Convenience function to set both eye prescriptions at once.
    /// </summary>
    public void SetBothEyes()
    {
        SetRightEye();
        SetLeftEye();
    }

    public void SetBothEyes(EyePrescription od, EyePrescription os)
    {
        SetRightEye(od);
        SetLeftEye(os);
    }

    /// <summary>
    /// Applies the serialized inspector parameters (odSphere, osSphere, etc.)
    /// to the OD and OS <see cref="EyePrescription"/> fields.
    /// </summary>
    [ContextMenu("Apply Inspector Parameters")]
    public void ApplyInspectorParameters()
    {
        SetRightEye();
        SetLeftEye();
    }

    void OnValidate()
    {
        ApplyInspectorParameters();
    }
}
