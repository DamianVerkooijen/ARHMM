using System.Collections.Generic;
using UnityEngine;

public class LocationRegistry : MonoBehaviour
{
    [System.Serializable]
    public class LocationData
    {
        [Tooltip("De unieke naam van de locatie (moet exact overeenkomen met de missie-instellingen)")]
        public string locationName;
        
        [Tooltip("De coördinaten op de grid")]
        public Vector2 gridPosition;
        
        [Tooltip("De foto van de locatie die in de teardrop marker verschijnt")]
        public Sprite locationPreviewImage; 
    }

    [Header("Geregistreerde Locaties")]
    public List<LocationData> locations = new List<LocationData>();

    /// <summary>
    /// Zoekt de gekoppelde foto van een locatie op basis van de naam string.
    /// </summary>
    public Sprite GetLocationSprite(string name)
    {
        LocationData data = locations.Find(x => x.locationName == name);
        if (data != null)
        {
            return data.locationPreviewImage;
        }
        
        Debug.LogWarning($"[LocationRegistry] Geen foto gevonden voor locatie: '{name}'. Controleer de spelling!");
        return null;
    }

    /// <summary>
    /// Zoekt de grid positie op van een locatie op basis van de naam string.
    /// </summary>
    public Vector2 GetPosition(string name)
    {
        LocationData data = locations.Find(x => x.locationName == name);
        return data != null ? data.gridPosition : Vector2.zero;
    }
}