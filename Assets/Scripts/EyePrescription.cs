using UnityEngine;

[System.Serializable]
public struct EyePrescription: System.IEquatable<EyePrescription>
{
    public const float QUEST3_VIEWING_DISTANCE = 1.25f; // ~1.2–1.3 m
    public const float DEFAULT_PUPIL_RADIUS = 2.5f; // ~2-3 mm indoors

    /// <summary>
    /// Spherical refractive correction for the eye, measured in diopters.
    /// </summary>
    public float Sphere;

    /// <summary>
    /// Cylindrical correction used to compensate for astigmatism, measured in diopters.
    /// </summary>
    public float Cylinder;

    /// <summary>
    /// Axis of the cylindrical correction, measured in degrees.
    /// </summary>
    public float Axis;

    /// <summary>
    /// Radius of the pupil in millimeters (mm).
    /// </summary>
    public float PupilRadius;

    /// <summary>
    /// Effective viewing distance of the headset or virtual display in meters (m). Use constants above.
    /// </summary>
    public float ViewingDistance;

    public bool Equals(EyePrescription other)
    {
        return Mathf.Approximately(Sphere, other.Sphere) &&
               Mathf.Approximately(Cylinder, other.Cylinder) &&
               Mathf.Approximately(Axis, other.Axis) &&
               Mathf.Approximately(PupilRadius, other.PupilRadius) &&
               Mathf.Approximately(ViewingDistance, other.ViewingDistance);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + Sphere.GetHashCode();
            h = h * 31 + Cylinder.GetHashCode();
            h = h * 31 + Axis.GetHashCode();
            h = h * 31 + PupilRadius.GetHashCode();
            h = h * 31 + ViewingDistance.GetHashCode();
            return h;
        }
    }
}
