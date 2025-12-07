using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "PSFDatabase",
    menuName = "Custom/PSF Database",
    order = 1)]
public class PSFDatabase : ScriptableObject
{
    [Tooltip("List of stored PSF/blur results keyed by EyePrescription.")]
    public List<PSFEntry> entries = new List<PSFEntry>();

    // Runtime dictionary for fast lookup
    private Dictionary<EyePrescription, PSFEntry> lookup;

    private void OnEnable()
    {
        lookup = new Dictionary<EyePrescription, PSFEntry>();

        foreach (var e in entries)
        {
            if (!lookup.ContainsKey(e.prescription))
                lookup.Add(e.prescription, e);
        }
    }

    public bool TryGet(EyePrescription p, out PSFEntry entry)
    {
        if (lookup == null)
            OnEnable();
        return lookup.TryGetValue(p, out entry);
    }

    public void AddOrReplace(PSFEntry entry)
    {
        // Remove previous entry
        entries.RemoveAll(e => e.prescription.Equals(entry.prescription));
        entries.Add(entry);

        // Update runtime lookup
        lookup[entry.prescription] = entry;
    }
}
