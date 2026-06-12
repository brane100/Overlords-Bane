using System.Collections;
using Unity.Netcode;
using UnityEngine;


public class AutoStartHost : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;


    private void Start()
    {
        StartCoroutine(StartHostWhenReady());
    }


    private IEnumerator StartHostWhenReady()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[AutoStartHost] No NetworkManager in scene; cannot start host.");
            yield break;
        }

        // If a host/server/client from a previous session or scene is still
        // running, shut it down first so the transport socket (port 7777) is
        // released before we bind again. Without this, a leftover host causes
        // "address already in use" and StartHost fails -> the player never spawns.
        if (nm.IsListening || nm.IsHost || nm.IsServer || nm.IsClient)
        {
            nm.Shutdown();
            while (nm.IsListening)
                yield return null;
            yield return null; // give the transport a frame to release the socket
        }

        nm.OnServerStarted += OnServerStarted;

        if (!nm.StartHost())
        {
            // Bind failed — a previous host likely leaked the socket.
            nm.OnServerStarted -= OnServerStarted;
            Debug.LogError("[AutoStartHost] StartHost failed: the transport could not bind port 7777. " +
                           "A previous host left the socket open. Restart the Unity Editor to release it, " +
                           "then play again.");
        }
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
