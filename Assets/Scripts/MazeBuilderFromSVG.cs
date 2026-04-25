using UnityEngine;
using UnityEditor;
using System.Globalization;
using System.Text.RegularExpressions;

public class MazeBuilderFromSVG : EditorWindow
{
    private float cellSize = 4f;
    private float wallHeight = 5f;
    private float wallThickness = 1f;
    private GameObject wallPrefab;
    private Material wallMaterial;
    private string wallTag = "Wall";
    private TextAsset svgAsset;

    // When false: walls butt together with ZERO overlap (no z-fighting).
    // When true: walls extend half a thickness each side to seal corners.
    private bool sealCorners = false;

    private const float SVG_STEP = 16f;
    private const float SVG_MIN = 2f;
    private const int GRID = 11;

    [MenuItem("Tools/Maze Builder From SVG")]
    public static void ShowWindow() => GetWindow<MazeBuilderFromSVG>("Maze Builder");

    private void OnGUI()
    {
        GUILayout.Label("SVG Maze Builder", EditorStyles.boldLabel);
        svgAsset = (TextAsset)EditorGUILayout.ObjectField("SVG file", svgAsset, typeof(TextAsset), false);
        cellSize = EditorGUILayout.FloatField("Cell Size (corridor width)", cellSize);
        wallHeight = EditorGUILayout.FloatField("Wall Height", wallHeight);
        wallThickness = EditorGUILayout.FloatField("Wall Thickness", wallThickness);
        wallPrefab = (GameObject)EditorGUILayout.ObjectField("Wall Prefab (optional)", wallPrefab, typeof(GameObject), false);
        wallMaterial = (Material)EditorGUILayout.ObjectField("Wall Material (if no prefab)", wallMaterial, typeof(Material), false);
        wallTag = EditorGUILayout.TextField("Wall Tag", wallTag);
        sealCorners = EditorGUILayout.Toggle("Seal Corners (overlap)", sealCorners);

        EditorGUILayout.Space();
        if (GUILayout.Button("Build Maze")) BuildMaze();
        if (GUILayout.Button("Clear Existing MazeRoot")) ClearMaze();
    }

    private void BuildMaze()
    {
        if (svgAsset == null)
        {
            EditorUtility.DisplayDialog("Missing SVG",
                "Assign your SVG file as a TextAsset first.", "OK");
            return;
        }

        ClearMaze();
        GameObject root = new GameObject("MazeRoot");
        float totalWorld = GRID * cellSize;
        float half = totalWorld / 2f;

        var matches = Regex.Matches(svgAsset.text,
            @"<line\s+x1=""([\d.]+)""\s+y1=""([\d.]+)""\s+x2=""([\d.]+)""\s+y2=""([\d.]+)""");

        int count = 0;
        foreach (Match m in matches)
        {
            float x1 = ToCell(m.Groups[1].Value);
            float y1 = ToCell(m.Groups[2].Value);
            float x2 = ToCell(m.Groups[3].Value);
            float y2 = ToCell(m.Groups[4].Value);

            float wx1 = x1 * cellSize - half;
            float wz1 = (GRID - y1) * cellSize - half;
            float wx2 = x2 * cellSize - half;
            float wz2 = (GRID - y2) * cellSize - half;

            CreateWallSegment(new Vector3(wx1, 0f, wz1),
                              new Vector3(wx2, 0f, wz2), root.transform);
            count++;
        }

        Debug.Log($"Maze built: {count} wall segments under MazeRoot.");
        Selection.activeGameObject = root;
    }

    private float ToCell(string raw)
        => (float.Parse(raw, CultureInfo.InvariantCulture) - SVG_MIN) / SVG_STEP;

    private void CreateWallSegment(Vector3 a, Vector3 b, Transform parent)
    {
        Vector3 mid = (a + b) / 2f;
        float length = Vector3.Distance(a, b);
        bool horizontal = Mathf.Abs(a.x - b.x) > Mathf.Abs(a.z - b.z);

        GameObject wall;
        if (wallPrefab != null)
            wall = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, parent);
        else
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(parent);
            if (wallMaterial != null)
                wall.GetComponent<MeshRenderer>().sharedMaterial = wallMaterial;
        }

        wall.name = horizontal ? "Wall_H" : "Wall_V";

        // Snap midpoint to a clean grid so adjacent walls share an exact edge.
        mid = new Vector3(
            Mathf.Round(mid.x * 1000f) / 1000f,
            wallHeight / 2f,
            Mathf.Round(mid.z * 1000f) / 1000f);
        wall.transform.position = mid;

        // Length: with sealCorners off, the wall spans exactly the segment so
        // it meets its neighbour edge-to-edge with 0% overlap.
        float span = sealCorners ? length + wallThickness : length;

        if (horizontal)
            wall.transform.localScale = new Vector3(span, wallHeight, wallThickness);
        else
            wall.transform.localScale = new Vector3(wallThickness, wallHeight, span);

        if (!string.IsNullOrEmpty(wallTag))
        {
            try { wall.tag = wallTag; }
            catch { Debug.LogWarning($"Tag '{wallTag}' not defined."); }
        }
    }

    private void ClearMaze()
    {
        var existing = GameObject.Find("MazeRoot");
        if (existing != null) DestroyImmediate(existing);
    }
}