using UnityEngine;

public class InvisibleWallTrigger : MonoBehaviour
{
    public ScreenEventController controller;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            controller.TriggerEvent();
    }
}