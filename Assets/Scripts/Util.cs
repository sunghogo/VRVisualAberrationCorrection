using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(menuName = "Custom/Util")]
public class Util : ScriptableObject
{
    static Util _instance;
    public static Util Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<Util>("Util"); // Util.asset must be under Resources/
            return _instance;
        }
    }

    [Header("Database")]
    public PSFDatabase psfDatabase;

    /// <summary>
    /// Save a texture as a PNG on disk (optional helper, works in Editor & builds).
    /// </summary>
    public void SaveTextureAsPNG(Texture2D tex, string filePath)
    {
        if (tex == null)
        {
            Debug.LogError("SaveTextureAsPNG: texture is null.");
            return;
        }

        byte[] bytes = tex.EncodeToPNG();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
        System.IO.File.WriteAllBytes(filePath, bytes);
        Debug.Log($"Saved texture to: {filePath}");
    }

    /// <summary>
    /// Assigns a material instance to a renderer if both exist.
    /// </summary>
    public void ApplyMaterial(Renderer r, Material mat)
    {
        if (r == null || mat == null) return;

        // Instantiate so each renderer keeps its own texture slot
        r.material = new Material(mat);
    }

    /// <summary>
    /// Saves the PSF texture and blurred material as actual Unity assets (if not already),
    /// then adds/updates an entry in the PSFDatabase referencing those assets.
    /// 
    /// This is Editor-only (uses AssetDatabase). At runtime in builds it will still
    /// update the database entry with whatever references you pass in, but will not
    /// create new asset files.
    /// </summary>
    public void SaveToDatabase(EyePrescription p, Texture2D psf, Material blurredMat)
    {
        if (psfDatabase == null)
        {
            Debug.LogError("Util.SaveToDatabase: psfDatabase is not assigned.");
            return;
        }

        if (psf == null || blurredMat == null)
        {
            Debug.LogError("Util.SaveToDatabase: psf or blurredMat is null.");
            return;
        }

        #if UNITY_EDITOR
            // -----------------------------
            // Ensure base folder exists
            // -----------------------------
            const string baseFolder = "Assets/GeneratedPSF";

            if (!AssetDatabase.IsValidFolder(baseFolder))
            {
                string parent = "Assets";
                string child = "GeneratedPSF";

                // If "GeneratedPSF" already exists at some other path, you may want
                // to adjust this, but for a fresh project this is fine.
                AssetDatabase.CreateFolder(parent, child);
            }

            // Key used for asset names (based on prescription)
            string safeKey = MakePrescriptionKey(p);

            // -----------------------------
            // Save PSF texture as an asset (if not already an asset)
            // -----------------------------
            string psfPath = AssetDatabase.GetAssetPath(psf);
            if (string.IsNullOrEmpty(psfPath))
            {
                psfPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{baseFolder}/PSF_{safeKey}.asset");
                AssetDatabase.CreateAsset(psf, psfPath);
                Debug.Log($"Created PSF texture asset at: {psfPath}");
            }

            // -----------------------------
            // Save blurred material as an asset (if not already an asset)
            // -----------------------------
            string matPath = AssetDatabase.GetAssetPath(blurredMat);
            if (string.IsNullOrEmpty(matPath))
            {
                matPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{baseFolder}/BlurredMat_{safeKey}.mat");
                AssetDatabase.CreateAsset(blurredMat, matPath);
                Debug.Log($"Created blurred material asset at: {matPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        #endif

        // -----------------------------
        // Update database entry
        // -----------------------------
        PSFEntry entry = new PSFEntry
        {
            prescription = p,
            psfTexture = psf,
            blurredMaterial = blurredMat
        };

        psfDatabase.AddOrReplace(entry);
        Debug.Log("Saved prescription to PSF database.");
    }

    #if UNITY_EDITOR
        /// <summary>
        /// Builds a filesystem-safe key string from an EyePrescription
        /// to use in asset filenames.
        /// </summary>
        static string MakePrescriptionKey(EyePrescription p)
        {
            // Round to 2 decimals to avoid super-long filenames
            string s = p.Sphere.ToString("0.00");
            string c = p.Cylinder.ToString("0.00");
            string a = p.Axis.ToString("0");
            string r = p.PupilRadius.ToString("0.00");
            string d = p.ViewingDistance.ToString("0.00");

            // Replace minus signs and dots to keep filenames safe
            string sanitize(string v) =>
                v.Replace("-", "m").Replace(".", "p");

            return $"S{sanitize(s)}_C{sanitize(c)}_A{sanitize(a)}_R{sanitize(r)}_D{sanitize(d)}";
        }
    #endif
}
