using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(LocationNameAttribute))]
public class LocationNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Find the Registry in the scene
        LocationRegistry registry = Object.FindFirstObjectByType<LocationRegistry>();

        if (registry == null || registry.locations.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // Get the list of names from the registry
        List<string> names = new List<string>();
        foreach (var loc in registry.locations)
        {
            names.Add(loc.locationName);
        }

        // Find which name is currently selected
        int currentIndex = names.IndexOf(property.stringValue);
        if (currentIndex < 0) currentIndex = 0;

        // Draw the Dropdown
        currentIndex = EditorGUI.Popup(position, label.text, currentIndex, names.ToArray());

        // Save the selected name back to the string
        property.stringValue = names[currentIndex];
    }
}