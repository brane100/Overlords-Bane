using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters <see cref="EyeWatcher"/> prefabs flush against the maze walls.
///
/// The maze is sculpted into a Unity <see cref="Terrain"/> (corridors low,
/// walls high), so there are no "Wall"-tagged meshes to raycast. Instead this
/// samples the terrain heightmap on a grid: for every walkable corridor cell
/// that borders a wall cell, it raycasts the TerrainCollider toward the wall and
/// pins an eye on the exact face, honoring a minimum spacing.
///
/// All candidates are collected then shuffled before selection so eyes spread
/// evenly across the whole maze rather than clustering at one edge.
///
/// Idempotent and re-runnable: each spawn clears the previously spawned eyes
/// first. Runs on Start (Level 1), and can be re-run from the context menu.
/// </summary>
public class EyeSpawner : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] GameObject eyePrefab;
    [Tooltip("Left null -> Terrain.activeTerrain is used.")]
    [SerializeField] Terrain terrain;
    [Tooltip("Optional: pushes the LevelTheme sigil accent onto every spawned eye.")]
    [SerializeField] LevelTheme theme;

    [Header("Heightmap classification (world Y)")]
    [Tooltip("Cells at or below this height are walkable corridor.")]
    [SerializeField] float corridorMaxHeight = 1.5f;
    [Tooltip("Neighbor cells at or above this height count as a wall.")]
    [SerializeField] float wallMinHeight = 3.5f;

    [Header("Sampling & placement")]
    [Tooltip("Grid step in world units when scanning the maze.")]
    [SerializeField] float sampleStep = 2f;
    [SerializeField] float eyeMinHeight = 1.4f;
    [SerializeField] float eyeMaxHeight = 3.0f;
    [Tooltip("Distance the eye is pushed off the wall face toward the corridor (avoids clipping).")]
    [SerializeField] float surfaceOffset = 0.5f;
    [Tooltip("Minimum distance between any two eyes.")]
    [SerializeField] float minSpacing = 6f;
    [SerializeField] int maxEyes = 40;
    [SerializeField] float eyeScale = 2.0f;

    [Header("Behaviour")]
    [SerializeField] bool spawnOnStart = true;
    [Tooltip("Only spawn when the active scene name contains this (blank = any).")]
    [SerializeField] string sceneNameContains = "Level1";

    struct Candidate { public Vector3 pos; public Vector3 dir; }

    const string ContainerName = "EyeWatchers";

    void Start()
    {
        if (!spawnOnStart) return;
        if (!string.IsNullOrEmpty(sceneNameContains) &&
            !gameObject.scene.name.Contains(sceneNameContains)) return;
        Spawn();
    }

    [ContextMenu("Respawn Eyes")]
    public void Spawn()
    {
        var t = terrain != null ? terrain : Terrain.activeTerrain;
        if (t == null) { Debug.LogWarning("[EyeSpawner] No Terrain found; nothing spawned."); return; }
        if (eyePrefab == null) { Debug.LogWarning("[EyeSpawner] No eye prefab assigned; nothing spawned."); return; }

        var container = ResetContainer();

        TerrainData td = t.terrainData;
        Vector3 origin = t.GetPosition();
        float sizeX = td.size.x, sizeZ = td.size.z;
        float baseY = origin.y;

        Color accent = theme != null ? theme.sigilAccentColor : new Color(0.545f, 0.361f, 0.965f);

        Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };

        // --- Pass 1: collect all valid raycast hits across the whole maze ---
        var candidates = new List<Candidate>();

        for (float x = origin.x; x <= origin.x + sizeX; x += sampleStep)
        {
            for (float z = origin.z; z <= origin.z + sizeZ; z += sampleStep)
            {
                var corridorPos = new Vector3(x, 0f, z);
                float h = baseY + t.SampleHeight(corridorPos);
                if (h > corridorMaxHeight) continue;

                foreach (var dir in dirs)
                {
                    Vector3 nPos = corridorPos + dir * sampleStep;
                    float nh = baseY + t.SampleHeight(nPos);
                    if (nh < wallMinHeight) continue;

                    float eyeY = Random.Range(eyeMinHeight, eyeMaxHeight);
                    Vector3 rayOrigin = new Vector3(x, eyeY, z);

                    if (!Physics.Raycast(rayOrigin, dir, out var hit, sampleStep * 1.5f)) continue;
                    if (!(hit.collider is TerrainCollider)) continue;

                    // Offset toward the corridor (away from wall) for reliable clearance on slopes.
                    Vector3 pos = hit.point + (-dir) * surfaceOffset;
                    pos.y = eyeY;
                    candidates.Add(new Candidate { pos = pos, dir = dir });
                }
            }
        }

        // --- Pass 2: shuffle then greedily pick with minSpacing ---
        Shuffle(candidates);

        var placed = new List<Vector3>();
        foreach (var c in candidates)
        {
            if (placed.Count >= maxEyes) break;
            if (TooClose(placed, c.pos, minSpacing)) continue;

            Quaternion rot = Quaternion.LookRotation(-c.dir, Vector3.up);
            var eye = Instantiate(eyePrefab, c.pos, rot, container);
            eye.transform.localScale = Vector3.one * eyeScale;

            var watcher = eye.GetComponent<EyeWatcher>();
            if (watcher != null) watcher.SetAccent(accent);

            placed.Add(c.pos);
        }

        Debug.Log($"[EyeSpawner] Spawned {placed.Count} eyes from {candidates.Count} candidates (cap {maxEyes}).");
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    bool TooClose(List<Vector3> placed, Vector3 p, float spacing)
    {
        float sq = spacing * spacing;
        for (int i = 0; i < placed.Count; i++)
            if ((placed[i] - p).sqrMagnitude < sq) return true;
        return false;
    }

    Transform ResetContainer()
    {
        var existing = transform.Find(ContainerName);
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        var go = new GameObject(ContainerName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }
}
