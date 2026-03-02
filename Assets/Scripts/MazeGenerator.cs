using System.Collections;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField] private MazeCell _mazeCellPrefab;
    [SerializeField] private int _mazeWidth;
    [SerializeField] private int _mazeDepth;

    private MazeCell[,] _mazeGrid;
    private Vector3 _exitPosition;
    private string _exitWall;
<<<<<<< HEAD
    private GameObject _mazeParent; // Parent object to hold all maze cells
=======
>>>>>>> 6154507 (improve logic and materials)
    
    [SerializeField] private int _hideoutRoomSize = 3; // Size of hiding rooms (e.g., 3x3)
    [SerializeField] private int _numberOfHideouts = 5; // Number of hiding rooms to create
    [SerializeField] private int _minDistanceBetweenHideouts = 6; // Minimum distance between hideouts

    public Transform mazeRoot; // assign in Inspector (MazeRoot)

    [SerializeField] private GameObject _mazeStartPosition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
<<<<<<< HEAD
        // Create a parent GameObject to hold all maze cells
        _mazeParent = new GameObject("Maze_" + _mazeWidth + "x" + _mazeDepth);
        _mazeParent.transform.SetParent(mazeRoot);
        _mazeParent.transform.localPosition = Vector3.zero;

=======
>>>>>>> 6154507 (improve logic and materials)
        _mazeGrid = new MazeCell[_mazeWidth, _mazeDepth];
        Vector3 cellSize = _mazeCellPrefab.transform.localScale;
        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeDepth; z++)
            {
<<<<<<< HEAD
                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, new Vector3(x * cellSize.x, 0, z * cellSize.z), Quaternion.identity, _mazeParent.transform);
=======
                _mazeGrid[x, z] = Instantiate(_mazeCellPrefab, new Vector3(x * cellSize.x, 0, z * cellSize.z), Quaternion.identity, mazeRoot);
>>>>>>> 6154507 (improve logic and materials)
            }
        }

        GenerateMaze(null, _mazeGrid[0, 0]); // Start maze generation from the first cell
        CreateRandomExit(); // Create the random exit after maze generation
        CreateHideoutRooms(); // Create hiding/rooting rooms inside the maze

        // Spawn the player at a random position
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            player.transform.position = GetRandomSpawnPosition();
        }
    }

    private /*IEnumerator*/ void GenerateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();
        ClearWalls(previousCell, currentCell);

        // yield return new WaitForSeconds(0.05f);

        MazeCell nextCell;

        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);
            if (nextCell != null)
            {
                /*yield return*/
                GenerateMaze(currentCell, nextCell);
            }
        } while (nextCell != null);
    }

    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
    {
        var unvisitedCells = GetUnvisitedCells(currentCell);

        return unvisitedCells.OrderBy(_ => Random.Range(1, 10)).FirstOrDefault();
    }

    private IEnumerable<MazeCell> GetUnvisitedCells(MazeCell currentCell)
    {
        Vector3 cellSize = _mazeCellPrefab.transform.localScale;
        int x = Mathf.RoundToInt(currentCell.transform.position.x / cellSize.x);
        int z = Mathf.RoundToInt(currentCell.transform.position.z / cellSize.z);

        if (x + 1 < _mazeWidth)
        {
            var cellToRight = _mazeGrid[x + 1, z];
            if (!cellToRight.IsVisited)
                yield return cellToRight;
        }
        if (x - 1 >= 0)
        {
            var cellToLeft = _mazeGrid[x - 1, z];
            if (!cellToLeft.IsVisited)
                yield return cellToLeft;
        }
        if (z + 1 < _mazeDepth)
        {
            var cellToFront = _mazeGrid[x, z + 1];
            if (!cellToFront.IsVisited)
                yield return cellToFront;
        }
        if (z - 1 >= 0)
        {
            var cellToBack = _mazeGrid[x, z - 1];
            if (!cellToBack.IsVisited)
                yield return cellToBack;
        }
    }

    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
    {
        if (previousCell == null) return;
        if (previousCell.transform.position.x < currentCell.transform.position.x)
        {
            previousCell.ClearRightWall();
            currentCell.ClearLeftWall();
            return;
        }
        if (previousCell.transform.position.x > currentCell.transform.position.x)
        {
            previousCell.ClearLeftWall();
            currentCell.ClearRightWall();
            return;
        }
        if (previousCell.transform.position.z < currentCell.transform.position.z)
        {
            previousCell.ClearFrontWall();
            currentCell.ClearBackWall();
            return;
        }
        if (previousCell.transform.position.z > currentCell.transform.position.z)
        {
            previousCell.ClearBackWall();
            currentCell.ClearFrontWall();
            return;
        }
    }

    private void CreateRandomExit()
    {
        // Randomly choose which wall of the maze will have the exit
        int wallChoice = Random.Range(0, 4); // 0: left, 1: right, 2: front, 3: back
        int x, z;
        Vector3 cellSize = _mazeCellPrefab.transform.localScale;

        switch (wallChoice)
        {
            case 0: // Left wall
                x = 0;
                z = Random.Range(0, _mazeDepth);
                _exitWall = "left";
                _mazeGrid[x, z].ClearLeftWall();
                break;
            case 1: // Right wall
                x = _mazeWidth - 1;
                z = Random.Range(0, _mazeDepth);
                _exitWall = "right";
                _mazeGrid[x, z].ClearRightWall();
                break;
            case 2: // Front wall
                x = Random.Range(0, _mazeWidth);
                z = _mazeDepth - 1;
                _exitWall = "front";
                _mazeGrid[x, z].ClearFrontWall();
                break;
            default: // Back wall
                x = Random.Range(0, _mazeWidth);
                z = 0;
                _exitWall = "back";
                _mazeGrid[x, z].ClearBackWall();
                break;
        }

        _exitPosition = new Vector3(x * cellSize.x, 0, z * cellSize.z);
    }

    public Vector3 GetRandomSpawnPosition()
    {
        int attempts = 0;
        const int maxAttempts = 100; // Prevent infinite loop
        Vector3 cellSize = _mazeCellPrefab.transform.localScale;

        while (attempts < maxAttempts)
        {
            int x = Random.Range(0, _mazeWidth);
            int z = Random.Range(0, _mazeDepth);
            Vector3 position = new Vector3(x * cellSize.x, 0, z * cellSize.z);

            // Make sure spawn point is not at or adjacent to the exit
            if (Vector3.Distance(position, _exitPosition) > 2f)
            {
                // Return the position with a small y offset to prevent floor clipping
                return new Vector3(x * cellSize.x, 0.1f, z * cellSize.z);
            }
            attempts++;
        }

        // Fallback position if no suitable position found
        return new Vector3((_mazeWidth / 2) * cellSize.x, 0.1f, (_mazeDepth / 2) * cellSize.z);
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void CreateHideoutRooms()
    {
        List<Vector2Int> hideoutCenters = new List<Vector2Int>();
        int attempts = 0;
        const int maxAttempts = 100;

        // Try to place the desired number of hideout rooms
        while (hideoutCenters.Count < _numberOfHideouts && attempts < maxAttempts)
        {
            int centerX = Random.Range(_hideoutRoomSize, _mazeWidth - _hideoutRoomSize);
            int centerZ = Random.Range(_hideoutRoomSize, _mazeDepth - _hideoutRoomSize);
            Vector2Int newCenter = new Vector2Int(centerX, centerZ);

            // Check if this location is far enough from other hideouts
            bool isFarEnough = true;
            foreach (var existingCenter in hideoutCenters)
            {
                float distance = Vector2Int.Distance(newCenter, existingCenter);
                if (distance < _minDistanceBetweenHideouts)
                {
                    isFarEnough = false;
                    break;
                }
            }

            if (isFarEnough)
            {
                CarveHideoutRoom(centerX, centerZ);
                hideoutCenters.Add(newCenter);
            }

            attempts++;
        }

        Debug.Log($"Created {hideoutCenters.Count} hideout rooms out of {_numberOfHideouts} requested");
    }

    private void CarveHideoutRoom(int centerX, int centerZ)
    {
        // Carve out the room by clearing interior walls
        int halfSize = _hideoutRoomSize / 2;

        for (int x = centerX - halfSize; x <= centerX + halfSize; x++)
        {
            for (int z = centerZ - halfSize; z <= centerZ + halfSize; z++)
            {
                if (x >= 0 && x < _mazeWidth && z >= 0 && z < _mazeDepth)
                {
                    MazeCell cell = _mazeGrid[x, z];
                    
                    // Clear interior walls to create open space
                    if (x > centerX - halfSize)
                        cell.ClearLeftWall();
                    
                    if (x < centerX + halfSize)
                        cell.ClearRightWall();
                    
                    if (z > centerZ - halfSize)
                        cell.ClearBackWall();
                    
                    if (z < centerZ + halfSize)
                        cell.ClearFrontWall();
                }
            }
        }

        // Create an opening to the hideout room from the maze
        CreateHideoutEntrance(centerX, centerZ, halfSize);
    }

    private void CreateHideoutEntrance(int centerX, int centerZ, int halfSize)
    {
        // Randomly choose which side of the hideout to open
        int sideChoice = Random.Range(0, 4);
        MazeCell entranceCell = null;

        switch (sideChoice)
        {
            case 0: // Left side
                if (centerX - halfSize - 1 >= 0)
                    entranceCell = _mazeGrid[centerX - halfSize - 1, centerZ];
                break;
            case 1: // Right side
                if (centerX + halfSize + 1 < _mazeWidth)
                    entranceCell = _mazeGrid[centerX + halfSize + 1, centerZ];
                break;
            case 2: // Front side
                if (centerZ + halfSize + 1 < _mazeDepth)
                    entranceCell = _mazeGrid[centerX, centerZ + halfSize + 1];
                break;
            case 3: // Back side
                if (centerZ - halfSize - 1 >= 0)
                    entranceCell = _mazeGrid[centerX, centerZ - halfSize - 1];
                break;
        }

        // Open the entrance by removing the wall between the hideout and the maze
        if (entranceCell != null)
        {
            MazeCell hideoutCell = null;
            switch (sideChoice)
            {
                case 0:
                    hideoutCell = _mazeGrid[centerX - halfSize, centerZ];
                    entranceCell.ClearRightWall();
                    hideoutCell.ClearLeftWall();
                    break;
                case 1:
                    hideoutCell = _mazeGrid[centerX + halfSize, centerZ];
                    entranceCell.ClearLeftWall();
                    hideoutCell.ClearRightWall();
                    break;
                case 2:
                    hideoutCell = _mazeGrid[centerX, centerZ + halfSize];
                    entranceCell.ClearBackWall();
                    hideoutCell.ClearFrontWall();
                    break;
                case 3:
                    hideoutCell = _mazeGrid[centerX, centerZ - halfSize];
                    entranceCell.ClearFrontWall();
                    hideoutCell.ClearBackWall();
                    break;
            }
        }
    }
}
