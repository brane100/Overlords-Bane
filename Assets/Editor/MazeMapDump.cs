using UnityEngine;
using UnityEditor;
using System.Text;

public class MazeMapDump
{
    // Samples terrain height on a world-space grid over the maze area and
    // returns an ASCII map: '#' = wall (high), '.' = floor (low).
    public static object Execute()
    {
        Terrain t = Object.FindFirstObjectByType<Terrain>();
        if (t == null) return "NO TERRAIN";

        Vector3 tp = t.transform.position;
        Vector3 ts = t.terrainData.size;

        float x0 = -12.13f, x1 = 97.87f;
        float z0 = -0.76f, z1 = 109.24f;
        int cols = 110, rows = 110;

        var sb = new StringBuilder();
        sb.AppendLine($"terrainPos={tp} size={ts}");

        // find min/max height in region first
        float hMin = float.MaxValue, hMax = float.MinValue;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            float wx = Mathf.Lerp(x0, x1, c / (float)(cols - 1));
            float wz = Mathf.Lerp(z0, z1, r / (float)(rows - 1));
            float h = t.SampleHeight(new Vector3(wx, 0, wz)) + tp.y;
            if (h < hMin) hMin = h;
            if (h > hMax) hMax = h;
        }
        sb.AppendLine($"hMin={hMin:F2} hMax={hMax:F2}");
        float thresh = (hMin + hMax) * 0.5f;

        // print top row = max z so map reads like a plan (north up)
        for (int r = rows - 1; r >= 0; r -= 2) // step 2 to halve output
        {
            for (int c = 0; c < cols; c += 2)
            {
                float wx = Mathf.Lerp(x0, x1, c / (float)(cols - 1));
                float wz = Mathf.Lerp(z0, z1, r / (float)(rows - 1));
                float h = t.SampleHeight(new Vector3(wx, 0, wz)) + tp.y;
                sb.Append(h > thresh ? '#' : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
