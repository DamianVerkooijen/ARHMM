using UnityEngine;

public class RadarMarker : MonoBehaviour
{
    public Transform radarCenter; // Het midden van je radar
    public float radarRadius = 100f; // Hoe groot is je radar in pixels?
    public float maxWorldDistance = 5000f; // Hoe ver is "de rand" van je radar in Unity units?

    public void UpdatePosition(Vector3 targetWorldPos, Vector3 playerWorldPos)
    {
        Debug.Log("UpdatePosition aangeroepen!");
        // Bereken de vector van speler naar doel
        Vector3 relativePos = targetWorldPos - playerWorldPos;

        // Converteer naar 2D voor de radar (X=X, Z=Y)
        Vector2 radarPos = new Vector2(relativePos.x, relativePos.z);

        // Bereken de afstand en beperk deze tot de radar straal
        float distance = radarPos.magnitude;
        if (distance > maxWorldDistance)
        {
            radarPos = radarPos.normalized * radarRadius;
        }
        else
        {
            radarPos = (radarPos / maxWorldDistance) * radarRadius;
        }

        // Zet de positie in lokale ruimte van de RadarUI
        transform.localPosition = radarPos;
    }
}