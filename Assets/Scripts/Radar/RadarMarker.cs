using UnityEngine;
public class RadarMarker : MonoBehaviour
{
    public float radarRadius = 100f; // Straal van je UI cirkel
    public float maxWorldDistance = 500f; // Pas dit aan naar de schaal van je wereld!

    public void UpdatePosition(Vector3 targetWorldPos, Vector3 playerWorldPos, float playerRotationY)
    {
        // 1. Bereken de relatieve positie in de wereld
        Vector3 relativePos = targetWorldPos - playerWorldPos;

        // 2. Draai de positie mee met de helikopter (zodat 'vooruit' op de radar ook echt 'vooruit' is)
        Vector3 rotatedPos = Quaternion.Euler(0, -playerRotationY, 0) * relativePos;

        // 3. Converteer naar 2D radar coördinaten (X en Z uit wereld worden X en Y in UI)
        Vector2 radarPos = new Vector2(rotatedPos.x, rotatedPos.z);

        // 4. Schalen: Hoe ver staat het doel t.o.v. de maximale afstand?
        // Als maxWorldDistance 500 is en de target is op 250m, komt hij op de helft van de radarRadius.
        float distanceRatio = radarPos.magnitude / maxWorldDistance;
        
        // Beperk de marker tot de rand van de radar
        if (distanceRatio > 1f)
        {
            radarPos = radarPos.normalized * radarRadius;
        }
        else
        {
            radarPos = radarPos.normalized * (distanceRatio * radarRadius);
        }

        // 5. Toepassen op de UI
        transform.localPosition = new Vector3(radarPos.x, radarPos.y, 0);
    }
}