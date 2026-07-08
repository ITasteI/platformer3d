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
    private float connectDeadline = -1f;
    private const float ConnectTimeoutSeconds = 8f;

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

    void Update()
    {
        // A LAN join attempt across networks without port-forwarding never fails or succeeds -
        // it just hangs forever. Give it a hard timeout with a clear message instead.
        if (connectDeadline < 0f || Time.time < connectDeadline)
            return;

        connectDeadline = -1f;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsConnectedClient)
        {
            NetworkManager.Singleton.Shutdown();
            statusMessage = "Verbindung fehlgeschlagen. Seid ihr im selben Netzwerk? Nutzt sonst Relay.";
            ShowToast("Verbindung fehlgeschlagen");
        }
    }

    void OnClientConnected(ulong clientId)
    {
        wasConnected = true;
        bool isSelf = NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId;
        if (isSelf)
        {
            connectDeadline = -1f;
            statusMessage = "Verbunden!";
        }
        else
        {
            ShowToast("Spieler beigetreten");
        }
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
        UITheme.BeginUI();
        DrawToasts();

        if (MainMenu.Current != MenuScreen.Play)
            return;

        UITheme.EnsureInit();

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, MainMenu.FadeAlpha);

        // Relay UI removed on request - co-op is LAN-only (plus solo), which is all that's used.
        float w = 360f;
        float h = 386f;
        float x = (UITheme.ScreenW - w) / 2f;
        float y = (UITheme.ScreenH - h) / 2f;

        MainMenu.DrawScreenChrome(new Rect(x, y, w, h), "Spielmodus & Koop");

        // Mode selector (host/solo picks how to play the tower) - 2x2 grid, four modes.
        GUI.Label(new Rect(x + 20, y + 58, w - 40, 18), "Modus:", UITheme.LabelStyle);
        float mw = (w - 40f - 6f) / 2f;
        DrawModeButton(new Rect(x + 20, y + 78, mw, 30), GameMode.Klassisch);
        DrawModeButton(new Rect(x + 26 + mw, y + 78, mw, 30), GameMode.Zeitrennen);
        DrawModeButton(new Rect(x + 20, y + 114, mw, 30), GameMode.Endlos);
        DrawModeButton(new Rect(x + 26 + mw, y + 114, mw, 30), GameMode.Hardcore);

        GUI.Label(new Rect(x + 20, y + 150, w - 40, 20), "LAN / direkte IP (nur selbes Netzwerk):", UITheme.LabelStyle);
        ipAddress = GUI.TextField(new Rect(x + 20, y + 170, w - 40, 25), ipAddress);

        if (GUI.Button(new Rect(x + 20, y + 205, (w - 50) / 2, 30), "Hosten (LAN)", UITheme.ButtonStyle))
            StartLanHost();
        if (GUI.Button(new Rect(x + 30 + (w - 50) / 2, y + 205, (w - 50) / 2, 30), "Beitreten", UITheme.ButtonStyle))
            StartLanClient();

        if (GUI.Button(new Rect(x + 20, y + 242, w - 40, 28), "Solo spielen", UITheme.ButtonStyle))
            StartSolo();

        GUI.Label(new Rect(x + 20, y + 278, w - 40, 30), statusMessage, UITheme.LabelStyle);

        var hintStyle = new GUIStyle(UITheme.LabelStyle) { fontSize = 11 };
        GUI.Label(new Rect(x + 20, y + 308, w - 40, 32), "Hinweis: Münzen werden pro Spieler getrennt gezählt.", hintStyle);

        if (GUI.Button(new Rect(x + 20, y + 344, w - 40, 30), "↩ Zurück", UITheme.ButtonStyle))
        {
            AudioManager.Instance?.PlayClick();
            MainMenu.SetScreen(MenuScreen.Main);
        }

        GUI.color = prevColor;
    }

    void DrawModeButton(Rect r, GameMode m)
    {
        bool sel = GameModeState.Current == m;
        if (GUI.Button(r, GameModeState.Name(m), sel ? UITheme.TabActiveStyle : UITheme.TabStyle))
        {
            AudioManager.Instance?.PlayClick();
            GameModeState.Current = m;
            // Keep an existing save's mode in sync, so resuming doesn't flip back silently.
            SaveSystem.UpdateSavedMode();
        }
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
            GUI.Box(new Rect(UITheme.ScreenW - 260, y, 240, 32), toast.Message, UITheme.PanelStyle);
            GUI.color = prev;
            y += 38f;
        }
    }

    void StartLanHost()
    {
        transport.SetConnectionData("0.0.0.0", port);
        bool started = NetworkManager.Singleton.StartHost();
        statusMessage = started
            ? "Host gestartet auf Port " + port
            : "Hosten fehlgeschlagen (Port " + port + " evtl. belegt).";
    }

    void StartLanClient()
    {
        transport.SetConnectionData(ipAddress, port);
        bool started = NetworkManager.Singleton.StartClient();
        if (started)
        {
            statusMessage = "Verbinde mit " + ipAddress + " ...";
            connectDeadline = Time.time + ConnectTimeoutSeconds;
        }
        else
        {
            statusMessage = "Verbindung konnte nicht gestartet werden.";
        }
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
