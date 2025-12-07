using UnityEngine;

[System.Serializable]
public struct PSFEntry
{
    public EyePrescription prescription;
    public Texture2D psfTexture;
    public Material blurredMaterial;
}
