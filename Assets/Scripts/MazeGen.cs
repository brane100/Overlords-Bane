using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public static class ListExtensions
{
    public static void Shuffle<T>(this List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            // Swap
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}


public class MapLocation{
    public int x;
    public int z;

    public MapLocation(int _x, int _z)
    {
        x = _x;
        z = _z;
    }

    public Vector2 ToVector()
    {
        return new Vector2(x, z);
    }

    public static MapLocation operator +(MapLocation a, MapLocation b)
        => new MapLocation(a.x + b.x, a.z + b.z);

    public override bool Equals(object obj){
        if ((obj == null) || !this.GetType().Equals(obj.GetType())) return false;
        else
            return x == ((MapLocation)obj).x && z == ((MapLocation)obj).z;
    }

    public override int GetHashCode()
    {
        return 0;
    }

}

public class MazeGen : MonoBehaviour
{
    [SerializeField] private int _width = 10;
    [SerializeField] private int _height = 10;

    public byte[,] map;

    [SerializeField] private int _scale = 6;

    public List<MapLocation> directions = new List<MapLocation>()
    {
        new MapLocation(0, 1), // Up
        new MapLocation(0, -1), // Down
        new MapLocation(-1, 0), // Left
        new MapLocation(1, 0) // Right
    };

    void Start()
    {
        InitializeMap();
        Generate();
        DrawMap();
    }

    void InitializeMap()
    {
        map = new byte[_width, _height];
        for (int z = 0; z < _width; z++)
        {
            for (int x = 0; x < _height; x++)
            {
                map[x, z] = 1; // 1 represents a wall
            }
        }
    }

    void Generate(){
        Generate(5, 5);
    }

    void Generate(int x, int z){
        // Check bounds first
        if (x <= 0 || x >= _width - 1 || z <= 0 || z >= _height - 1) return;
        
        if (CountSquareNeighbours(x, z) > 1) return; // Too many neighbours, stop carving
        map[x, z] = 0; // 0 represents a passage

        directions.Shuffle(); // Shuffle directions to ensure randomness

        Generate(x + directions[0].x, z + directions[0].z); // Move in the first direction
        Generate(x + directions[1].x, z + directions[1].z); // Move in the second direction
        Generate(x + directions[2].x, z + directions[2].z); // Move in the third direction
        Generate(x + directions[3].x, z + directions[3].z); // Move in the fourth direction
    }

    void DrawMap(){
        for (int z = 0; z < _height; z++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (map[x, z] == 1)
                {
                    Vector3 pos = new Vector3(x * _scale, 0, z * _scale);
                    GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.localScale = new Vector3(_scale, _scale, _scale);
                    wall.transform.position = pos;
                }
            }
        }
    }

    public int CountSquareNeighbours(int x, int z){
        int count = 0;
        if (x <= 0 || x >= _width - 1 || z <= 0 || z >= _height - 1) return count; // Out of bounds, return 0
        if (map[x - 1, z] == 0) count++; // Left
        if (map[x + 1, z] == 0) count++; // Right
        if (map[x, z - 1] == 0) count++; // Down
        if (map[x, z + 1] == 0) count++; // Up
        return count;
    }
}