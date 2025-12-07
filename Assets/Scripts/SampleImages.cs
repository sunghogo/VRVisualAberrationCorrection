using UnityEngine;
using UnityEngine.SocialPlatforms;

public class SampleImages : MonoBehaviour
{
    [Header("Current Active Sample (Read Only)")]
    public Texture2D currentTexture;
    public Material currentMaterial;

    [Header("External References")]
    [Tooltip("Reference to AberrationValidationTool object")]
    [SerializeField] AberrationValidationTool _aberrationValidationTool;

    [Header("Child References")]
    [Tooltip("Reference to Child objects")]
    [SerializeField] GameObject _selectedHeader;
    [SerializeField] float _selectedHeaderXOffest = -0.4f;


    void Awake()
    {
        if (_aberrationValidationTool == null)
            _aberrationValidationTool = FindFirstObjectByType<AberrationValidationTool>();
    }

    void Start()
    {
        if (_selectedHeader != null)
            _selectedHeaderXOffest = _selectedHeader.transform.localPosition.x;
    }

    /// <summary>
    /// Sets the active sample and forwards it to AberrationValidationTool.
    /// </summary>
    public void SetActiveSample(Texture2D texture, Material material, Vector3 localPosition)
    {
        currentTexture = texture;
        currentMaterial = material;
        if (_aberrationValidationTool != null && currentTexture != null && currentMaterial != null)
        {
            _aberrationValidationTool.SetSourceAndMaterial(currentTexture, currentMaterial);
            _selectedHeader.transform.localPosition = new Vector3(localPosition.x +_selectedHeaderXOffest, _selectedHeader.transform.localPosition.y, _selectedHeader.transform.localPosition.z );
        }
        else
        {
            Debug.LogWarning("SampleImages: Missing texture/material or AberrationValidationTool.");
        }
    }
}
