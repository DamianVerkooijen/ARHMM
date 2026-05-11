using System.Collections.Generic;
using UnityEngine;

public class LocationRegistry: MonoBehaviour
{
    [System.Serializable]
    public class NamedLocation
    {
        public string locationName;
        public Vector2 gridPosition;
    }

    public List<NamedLocation> locations = new List<NamedLocation>();

    // Helper to find a position by its name
    public Vector2 GetPosition(string name)
    {
        var found = locations.Find(l => l.locationName == name);
        if (found != null) return found.gridPosition;

        Debug.LogWarning($"Location '{name}' not found in Registry!");
        return Vector2.zero;
    }
}