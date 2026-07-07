using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class LobbyBootstrap : MonoBehaviour
{
    public static LobbyBootstrap Instance { get; private set; }

    public Camera lobbyCamera;
    public UnityTransport transport;

    private string ipAddress = "127.0.0.1";
    private readonly ushort port = 7777;
    private string relayJoinCodeInput = "";
    private string statusMessage = "";
    private bool wasConnected;

    private class Toast
    {
        public string Message;
        public float ExpiryTime;
    }

    private readonly List<Toast> toasts = new List<Toast>();

    void Awake()
    {
        Instance = this;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientConnected(ulong clientId)
    {
        wasConnected = true;
        bool isSelf = NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId;
        if (!isSelf)
            ShowToast("Spieler beigetreten");
    }

    void OnClientDisconnected(ulong clientId)
    {
        bool isSelf = NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId;
        if (isSelf && wasConnected)
        {
            wasConnected = false;
            ShowToast("Verbindung verloren");
            MainMenu.ReturnToMainAfterDisconnect();
        }
        else if (!isSelf)
        {
            ShowToast("Spieler getrennt");
        }
    }

    void ShowToast(string message)
    {
        toasts.Add(new Toast { Message = message, ExpiryTime = Time.time + 3f });
    }

    public static void HideLobbyCamera()
    {
        if (Instance != null && Instance.lobbyCamera != null)
            Instance.lobbyCamera.gameObject.SetActive(false);
    }

    void OnGUI()
    {
        DrawToasts();

        if (MainMenu.Current != MenuScreen.Play)
            return;

        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, MainMenu.FadeAlpha);

        float w = 340f;
        float h = 370f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        GUI.Box(new Rect(x, y, w, h), "", UITheme.PanelStyle);
        GUI.Label(new Rect(x, y + 8, w, 28), "Multiplayer", UITheme.TitleStyle);

        GUI.Label(new Rect(x + 20, y + 45, w - 40, 20), "LAN / direkte IP:", UITheme.LabelStyle);
        ipAddress = GUI.TextField(new Rect(x + 20, y + 65, w - 40, 25), ipAddress);

        if (GUI.Button(new Rect(x + 20, y + 100, (w - 50) / 2, 30), "Hosten (LAN)", UITheme.ButtonStyle))
            StartLanHost();
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + 100, (w - 50) / 2, 30), "Beitreten", UITheme.ButtonStyle))
            StartLanClient();

        if (GUI.Button(new Rect(x + 20, y + 137, w - 40, 28), "Solo spielen", UITheme.ButtonStyle))
            StartSolo();

        GUI.Label(new Rect(x + 20, y + 173, w - 40, 20), "Unity Relay Beitrittscode:", UITheme.LabelStyle);
        relayJoinCodeInput = GUI.TextField(new Rect(x + 20, y + 193, w - 40, 25), relayJoinCodeInput);

        if (GUI.Button(new Rect(x + 20, y + 225, (w - 50) / 2, 28), "Relay hosten", UITheme.ButtonStyle))
            _ = StartRelayHost();
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + 225, (w - 50) / 2, 28), "Relay beitreten", UITheme.ButtonStyle))
            _ = StartRelayJoin();

        GUI.Label(new Rect(x + 20, y + 258, w - 40, 30), statusMessage, UITheme.LabelStyle);

        var hintStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 11 };
        GUI.Label(new Rect(x + 20, y + 288, w - 40, 32), "Hinweis: Shards werden pro Spieler getrennt gezählt.", hintStyle);

        if (GUI.Button(new Rect(x + 20, y + 325, w - 40, 30), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            MainMenu.SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    void DrawToasts()
    {
        if (toasts.Count == 0)
            return;

        UITheme.EnsureInit();
        float y = 20f;
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            if (Time.time > toasts[i].ExpiryTime)
            {
                toasts.RemoveAt(i);
                continue;
            }
        }

        foreach (var toast in toasts)
        {
            float alpha = Mathf.Clamp01(toast.ExpiryTime - Time.time);
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(new Rect(Screen.width - 260, y, 240, 32), toast.Message, UITheme.PanelStyle);
            GUI.color = prev;
            y += 38f;
        }
    }

    void StartLanHost()
    {
        transport.SetConnectionData("0.0.0.0", port);
        NetworkManager.Singleton.StartHost();
        statusMessage = "Host gestartet auf Port " + port;
    }

    void StartLanClient()
    {
        transport.SetConnectionData(ipAddress, port);
        NetworkManager.Singleton.StartClient();
        statusMessage = "Verbinde mit " + ipAddress + " ...";
    }

    void StartSolo()
    {
        transport.SetConnectionData("127.0.0.1", port);
        NetworkManager.Singleton.StartHost();
        statusMessage = "Solo gestartet";
    }

    async Task EnsureSignedIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    async Task StartRelayHost()
    {
        try
        {
            statusMessage = "Verbinde mit Unity Services...";
            await EnsureSignedIn();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            transport.SetRelayServerData(BuildRelayServerData(
                allocation.ServerEndpoints, allocation.AllocationIdBytes, allocation.ConnectionData, allocation.ConnectionData, allocation.Key));
            NetworkManager.Singleton.StartHost();
            statusMessage = "Beitrittscode: " + joinCode;
        }
        catch (System.Exception e)
        {
            statusMessage = "Relay-Fehler (Unity Cloud verknüpft?): " + e.Message;
        }
    }

    async Task StartRelayJoin()
    {
        try
        {
            statusMessage = "Verbinde mit Unity Services...";
            await EnsureSignedIn();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCodeInput);
            transport.SetRelayServerData(BuildRelayServerData(
                joinAllocation.ServerEndpoints, joinAllocation.AllocationIdBytes, joinAllocation.ConnectionData, joinAllocation.HostConnectionData, joinAllocation.Key));
            NetworkManager.Singleton.StartClient();
            statusMessage = "Über Relay verbunden!";
        }
        catch (System.Exception e)
        {
            statusMessage = "Relay-Fehler (Unity Cloud verknüpft?): " + e.Message;
        }
    }

    static RelayServerData BuildRelayServerData(
        System.Collections.Generic.List<RelayServerEndpoint> endpoints,
        byte[] allocationId, byte[] connectionData, byte[] hostConnectionData, byte[] key)
    {
        RelayServerEndpoint endpoint = null;
        foreach (var ep in endpoints)
        {
            if (ep.ConnectionType == "dtls")
            {
                endpoint = ep;
                break;
            }
        }
        if (endpoint == null)
            endpoint = endpoints[0];

        bool isSecure = endpoint.ConnectionType == "dtls" || endpoint.ConnectionType == "wss";
        bool isWebSocket = endpoint.ConnectionType == "wss";

        return new RelayServerData(endpoint.Host, (ushort)endpoint.Port,
            allocationId, connectionData, hostConnectionData, key, isSecure, isWebSocket);
    }
}
