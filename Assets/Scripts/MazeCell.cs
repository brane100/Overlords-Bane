using UnityEngine;

public class MazeCell : MonoBehaviour
{
    [SerializeField] private GameObject _leftWall;
    [SerializeField] private GameObject _rightWall;
    [SerializeField] private GameObject _frontWall;
    [SerializeField] private GameObject _backWall;
    [SerializeField] private GameObject _unvisitedBlock;

<<<<<<< HEAD
<<<<<<< HEAD
=======
=======
>>>>>>> 615f63b (adding multiplayer functionality)
    public int X { get; private set; }
    public int Z { get; private set; }

    public void Init(int x, int z)
    {
        X = x;
        Z = z;
    }


<<<<<<< HEAD
>>>>>>> 6154507 (improve logic and materials)
=======
>>>>>>> 615f63b (adding multiplayer functionality)
    public bool IsVisited { get; private set; }

    public void Visit()
    {
        IsVisited = true;
        _unvisitedBlock.SetActive(false);
    }

    public void ClearRightWall()
    {
        _rightWall.SetActive(false);
    }
    public void ClearLeftWall()
    {
        _leftWall.SetActive(false);
    }
    public void ClearFrontWall()
    {
        _frontWall.SetActive(false);
    }
    public void ClearBackWall()
    {
        _backWall.SetActive(false);
<<<<<<< HEAD
<<<<<<< HEAD
    }   
}
=======
=======
>>>>>>> 615f63b (adding multiplayer functionality)
    }

    public void ApplyWallOwnership(bool showLeft, bool showBack, bool showRight, bool showFront)
    {
        _leftWall.SetActive(showLeft);
        _backWall.SetActive(showBack);
        _rightWall.SetActive(showRight);
        _frontWall.SetActive(showFront);
    }

    public bool IsLeftWallActive()  => _leftWall.activeSelf;
public bool IsRightWallActive() => _rightWall.activeSelf;
public bool IsFrontWallActive() => _frontWall.activeSelf;
public bool IsBackWallActive()  => _backWall.activeSelf;

<<<<<<< HEAD
}
>>>>>>> 6154507 (improve logic and materials)
=======
}
>>>>>>> 615f63b (adding multiplayer functionality)
