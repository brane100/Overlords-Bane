using Unity.Netcode;
using UnityEngine;


public class AutoStartHost : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;


    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.StartHost();
    }


    private void OnServerStarted()
    {
        if (spawnPoint == null) return;


        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
                client.PlayerObject.transform.position = spawnPoint.position;
        }
    }


    private void OnDestroy()
    {
        // Always unsubscribe to avoid memory leaks
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
    }
}