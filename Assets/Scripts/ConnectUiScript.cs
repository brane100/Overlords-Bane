using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ConnectUiScript : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        hostButton.onClick.AddListener(HostButtonOnClick);
        clientButton.onClick.AddListener(ClientButtonOnClick);
    }

    private void HostButtonOnClick()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Host button clicked");
    }

    private void ClientButtonOnClick()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("Client button clicked");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
