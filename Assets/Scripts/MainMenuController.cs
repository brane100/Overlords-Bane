using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main menu button handlers for Overlords' Bane. Wire each public method to the
/// matching button's onClick in the MainMenu scene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public void OnBeginAscent()
    {
        SceneManager.LoadScene("Level1");
    }

    public void OnContinue()
    {
        Debug.Log("Continue: not yet implemented");
    }

    public void OnSettings()
    {
        Debug.Log("Settings: not yet implemented");
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
