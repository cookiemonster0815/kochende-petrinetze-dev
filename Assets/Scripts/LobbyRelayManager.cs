using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class LobbyRelayManager : MonoBehaviour
{
    private const string LastLobbyCodePrefsKey = "BAOvercooked_LastLobbyCode";
    private const string SessionTimestampKey = "sessionTs";

    [Header("Lobby")]
    [SerializeField] private string lobbyName = "OvercookedPetriLobby";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private bool showLobbyOverlay = true;
    [SerializeField] private bool autoFillLastLobbyCode = true;
    [SerializeField] private float lobbyZoomSpeed = 0.08f;
    [SerializeField] private float minLobbyZoom = 0.7f;
    [SerializeField] private float maxLobbyZoom = 1.8f;
    [SerializeField] private float lobbyPanSpeed = 6f;

    [Header("Heartbeat")]
    [SerializeField] private float heartbeatIntervalSeconds = 15f;

    private Lobby currentLobby;
    private string joinCodeInput = string.Empty;
    private string currentJoinCode = string.Empty;
    private string statusMessage = "Idle";
    private float heartbeatTimer;
    private bool servicesReady;
    private bool busy;
    private float lobbyZoomScale = 1f;

    private async void Start()
    {
        EnsureNetworkManagerExists();

        if (autoFillLastLobbyCode)
        {
            string savedCode = LoadLastLobbyCode();
            if (!string.IsNullOrEmpty(savedCode))
            {
                joinCodeInput = savedCode;
            }
        }

        await InitializeServicesAsync();
    }

    private void Update()
    {
        HandleEmergencyStopHotkey();
        HandleLobbyZoom();
        HandleLobbyKeyboardControls();

        if (currentLobby == null || !IsHost())
        {
            return;
        }

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer <= 0f)
        {
            heartbeatTimer = heartbeatIntervalSeconds;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
    }

    private void OnGUI()
    {
        if (!showLobbyOverlay)
        {
            return;
        }

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null && gameManager.ShouldSuppressLobbyOverlay())
        {
            return;
        }

        bool connected = AreBothPlayersConnected();
        Matrix4x4 oldMatrix = GUI.matrix;
        if (!connected)
        {
            Vector2 pivot = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUIUtility.ScaleAroundPivot(new Vector2(lobbyZoomScale, lobbyZoomScale), pivot);
        }

        Rect panel = connected
            ? new Rect(12f, Screen.height - 136f, 420f, 124f)
            : new Rect(0f, 0f, Screen.width, Screen.height);

        GUI.Box(panel, connected ? "Lobby + Relay (Connected)" : "Lobby + Relay");
        float x = connected ? panel.x + 16f : panel.x + Mathf.Max(24f, panel.width * 0.18f);
        float y = connected ? panel.y + 26f : panel.y + Mathf.Max(48f, panel.height * 0.28f);
        float contentWidth = connected ? panel.width - 32f : Mathf.Min(820f, panel.width * 0.64f);

        GUI.Label(new Rect(x, y, contentWidth, 22f), "Status: " + statusMessage);
        y += 30f;

        if (!connected)
        {
            GUI.enabled = servicesReady && !busy;

            if (GUI.Button(new Rect(x, y, 240f, 38f), "Create Lobby (Host)"))
            {
                _ = CreateLobbyAndStartHostAsync();
            }

            GUI.Label(new Rect(x + 260f, y + 8f, 90f, 22f), "Join Code:");
            joinCodeInput = GUI.TextField(new Rect(x + 350f, y + 6f, 150f, 28f), joinCodeInput ?? string.Empty);

            if (GUI.Button(new Rect(x + 510f, y, 180f, 38f), "Join Lobby"))
            {
                _ = JoinLobbyAndStartClientAsync(joinCodeInput);
            }

            y += 44f;
            GUI.enabled = servicesReady && !busy && !string.IsNullOrWhiteSpace(LoadLastLobbyCode());
            if (GUI.Button(new Rect(x + 350f, y, 340f, 30f), "Join Last Code (J)"))
            {
                _ = JoinLobbyAndStartClientAsync(LoadLastLobbyCode());
            }

            y += 36f;
            GUI.enabled = servicesReady && !busy;
            if (GUI.Button(new Rect(x + 350f, y, 340f, 30f), "Auto Join Test Lobby (T)"))
            {
                _ = JoinLatestLobbyByNameAsync();
            }

            y += 36f;
            GUI.enabled = currentLobby != null && !busy;
            if (GUI.Button(new Rect(x, y, 150f, 30f), "Leave Lobby"))
            {
                _ = LeaveLobbyAsync();
            }

            GUI.enabled = true;
            y += 38f;
            if (!string.IsNullOrEmpty(currentJoinCode))
            {
                GUI.Label(new Rect(x, y, contentWidth, 22f), "Current Join Code: " + currentJoinCode);
            }

            y += 24f;
            GUI.Label(new Rect(x, y, contentWidth, 20f), "Shortcuts: H Host | Enter Join | J Join Last | T Auto Join | L Leave");
        }
        else
        {
            GUI.enabled = currentLobby != null && !busy;
            if (GUI.Button(new Rect(x, y, 150f, 28f), "Leave Lobby"))
            {
                _ = LeaveLobbyAsync();
            }

            GUI.enabled = true;
            if (!string.IsNullOrEmpty(currentJoinCode))
            {
                GUI.Label(new Rect(x + 170f, y + 4f, panel.width - 190f, 22f), "Code: " + currentJoinCode);
            }
        }

        GUI.matrix = oldMatrix;
    }

    private void HandleLobbyZoom()
    {
        if (AreBothPlayersConnected())
        {
            return;
        }

        float scroll = 0f;
        if (Mouse.current != null)
        {
            scroll = Mouse.current.scroll.ReadValue().y;
        }

        if (Mathf.Abs(scroll) < 0.001f)
        {
            return;
        }

        lobbyZoomScale = Mathf.Clamp(lobbyZoomScale + scroll * lobbyZoomSpeed * 0.01f, minLobbyZoom, maxLobbyZoom);
    }

    private void HandleLobbyKeyboardControls()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || busy)
        {
            return;
        }

        if (!AreBothPlayersConnected())
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 move = Vector3.zero;
                if (keyboard.leftArrowKey.isPressed) { move.x -= 1f; }
                if (keyboard.rightArrowKey.isPressed) { move.x += 1f; }
                if (keyboard.upArrowKey.isPressed) { move.y += 1f; }
                if (keyboard.downArrowKey.isPressed) { move.y -= 1f; }

                if (move.sqrMagnitude > 0f)
                {
                    float speed = lobbyPanSpeed * cam.orthographicSize * 0.2f;
                    cam.transform.position += move.normalized * speed * Time.unscaledDeltaTime;
                }
            }
        }

        if (!AreBothPlayersConnected())
        {
            if (servicesReady && keyboard.hKey.wasPressedThisFrame)
            {
                _ = CreateLobbyAndStartHostAsync();
            }

            if (servicesReady && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && !string.IsNullOrWhiteSpace(joinCodeInput))
            {
                _ = JoinLobbyAndStartClientAsync(joinCodeInput);
            }

            if (servicesReady && keyboard.jKey.wasPressedThisFrame)
            {
                string savedCode = LoadLastLobbyCode();
                if (!string.IsNullOrWhiteSpace(savedCode))
                {
                    _ = JoinLobbyAndStartClientAsync(savedCode);
                }
            }

            if (servicesReady && keyboard.tKey.wasPressedThisFrame)
            {
                _ = JoinLatestLobbyByNameAsync();
            }

            if (keyboard.minusKey.wasPressedThisFrame)
            {
                lobbyZoomScale = Mathf.Clamp(lobbyZoomScale - 0.08f, minLobbyZoom, maxLobbyZoom);
            }

            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
            {
                lobbyZoomScale = Mathf.Clamp(lobbyZoomScale + 0.08f, minLobbyZoom, maxLobbyZoom);
            }
        }

        if (currentLobby != null && keyboard.lKey.wasPressedThisFrame)
        {
            _ = LeaveLobbyAsync();
        }
    }

    private bool AreBothPlayersConnected()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return false;
        }

        return NetworkManager.Singleton.ConnectedClientsIds != null && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2;
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesReady = true;
            statusMessage = "Signed in as " + AuthenticationService.Instance.PlayerId;
        }
        catch (Exception ex)
        {
            servicesReady = false;
            statusMessage = "Service init failed: " + ex.Message;
            Debug.LogException(ex);
        }
    }

    private async Task CreateLobbyAndStartHostAsync()
    {
        if (!servicesReady)
        {
            statusMessage = "Services not ready.";
            return;
        }

        busy = true;
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    {
                        "relayJoinCode",
                        new DataObject(DataObject.VisibilityOptions.Public, relayJoinCode)
                    },
                    {
                        SessionTimestampKey,
                        new DataObject(DataObject.VisibilityOptions.Public, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
                    }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            currentJoinCode = currentLobby.LobbyCode;
            SaveLastLobbyCode(currentJoinCode);
            joinCodeInput = currentJoinCode;

            EnsureNetworkManagerExists();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (!NetworkManager.Singleton.StartHost())
            {
                throw new InvalidOperationException("Failed to start host.");
            }

            heartbeatTimer = heartbeatIntervalSeconds;
            statusMessage = "Host started. Lobby code: " + currentJoinCode;
        }
        catch (Exception ex)
        {
            statusMessage = "Create host failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task JoinLobbyAndStartClientAsync(string lobbyCode)
    {
        if (!servicesReady)
        {
            statusMessage = "Services not ready.";
            return;
        }

        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            statusMessage = "Enter lobby code first.";
            return;
        }

        busy = true;
        try
        {
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim().ToUpperInvariant());
            currentJoinCode = currentLobby.LobbyCode;
            SaveLastLobbyCode(currentJoinCode);
            joinCodeInput = currentJoinCode;

            if (!currentLobby.Data.TryGetValue("relayJoinCode", out DataObject relayData) || string.IsNullOrEmpty(relayData.Value))
            {
                throw new InvalidOperationException("Relay join code missing in lobby data.");
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayData.Value);

            EnsureNetworkManagerExists();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!NetworkManager.Singleton.StartClient())
            {
                throw new InvalidOperationException("Failed to start client.");
            }

            statusMessage = "Client connected to lobby " + currentJoinCode;
        }
        catch (Exception ex)
        {
            statusMessage = "Join failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task JoinLatestLobbyByNameAsync()
    {
        if (!servicesReady)
        {
            statusMessage = "Services not ready.";
            return;
        }

        busy = true;
        try
        {
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.Name, lobbyName, QueryFilter.OpOptions.EQ),
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                },
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            if (response == null || response.Results == null || response.Results.Count == 0)
            {
                statusMessage = "No open test lobby found.";
                return;
            }

            Lobby selectedLobby = null;
            long selectedTimestamp = long.MinValue;
            for (int i = 0; i < response.Results.Count; i++)
            {
                Lobby candidate = response.Results[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate.Data == null || !candidate.Data.TryGetValue("relayJoinCode", out DataObject relayData) || string.IsNullOrEmpty(relayData.Value))
                {
                    continue;
                }

                long timestamp = 0;
                if (candidate.Data.TryGetValue(SessionTimestampKey, out DataObject tsData))
                {
                    long.TryParse(tsData.Value, out timestamp);
                }

                if (selectedLobby == null || timestamp > selectedTimestamp)
                {
                    selectedLobby = candidate;
                    selectedTimestamp = timestamp;
                }
            }

            if (selectedLobby == null)
            {
                statusMessage = "No valid test lobby with relay data found.";
                return;
            }

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(selectedLobby.Id);
            currentJoinCode = currentLobby.LobbyCode;
            SaveLastLobbyCode(currentJoinCode);
            joinCodeInput = currentJoinCode;

            if (!currentLobby.Data.TryGetValue("relayJoinCode", out DataObject selectedRelayData) || string.IsNullOrEmpty(selectedRelayData.Value))
            {
                throw new InvalidOperationException("Relay join code missing in selected lobby data.");
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(selectedRelayData.Value);

            EnsureNetworkManagerExists();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!NetworkManager.Singleton.StartClient())
            {
                throw new InvalidOperationException("Failed to start client.");
            }

            statusMessage = "Client auto-joined lobby " + currentJoinCode;
        }
        catch (Exception ex)
        {
            statusMessage = "Auto-join failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private async Task LeaveLobbyAsync()
    {
        busy = true;
        try
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            if (currentLobby != null)
            {
                if (IsHost())
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
                }
            }

            currentLobby = null;
            currentJoinCode = string.Empty;
            statusMessage = "Left lobby.";
        }
        catch (Exception ex)
        {
            statusMessage = "Leave failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    private bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }

    private void HandleEmergencyStopHotkey()
    {
#if UNITY_EDITOR
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // F12 is a simple emergency kill switch when Play Mode gets stuck.
        if (!keyboard.f12Key.wasPressedThisFrame)
        {
            return;
        }

        ForceStopPlayMode();
#endif
    }

#if UNITY_EDITOR
    private void ForceStopPlayMode()
    {
        try
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        catch
        {
        }

        busy = false;
        currentLobby = null;
        currentJoinCode = string.Empty;
        statusMessage = "Emergency stop requested.";
        EditorApplication.isPlaying = false;
    }
#endif

    private void EnsureNetworkManagerExists()
    {
        if (NetworkManager.Singleton != null)
        {
            UnityTransport existingTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (existingTransport == null)
            {
                existingTransport = NetworkManager.Singleton.gameObject.AddComponent<UnityTransport>();
            }

            EnsureNetworkConfig(NetworkManager.Singleton, existingTransport);

            return;
        }

        GameObject netObj = new GameObject("NetworkManager");
        NetworkManager manager = netObj.AddComponent<NetworkManager>();
        UnityTransport transport = netObj.AddComponent<UnityTransport>();
        EnsureNetworkConfig(manager, transport);
        try
        {
            DontDestroyOnLoad(netObj);
        }
        catch
        {
        }
    }

    private static void EnsureNetworkConfig(NetworkManager manager, UnityTransport transport)
    {
        if (manager == null)
        {
            return;
        }

        if (manager.NetworkConfig == null)
        {
            manager.NetworkConfig = new NetworkConfig();
        }

        if (manager.NetworkConfig.NetworkTransport == null)
        {
            manager.NetworkConfig.NetworkTransport = transport;
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        catch
        {
        }

        currentLobby = null;
        currentJoinCode = string.Empty;
        busy = false;
    }

    private static void SaveLastLobbyCode(string lobbyCode)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            return;
        }

        PlayerPrefs.SetString(LastLobbyCodePrefsKey, lobbyCode.Trim().ToUpperInvariant());
        PlayerPrefs.Save();
    }

    private static string LoadLastLobbyCode()
    {
        if (!PlayerPrefs.HasKey(LastLobbyCodePrefsKey))
        {
            return string.Empty;
        }

        return PlayerPrefs.GetString(LastLobbyCodePrefsKey, string.Empty).Trim().ToUpperInvariant();
    }
}
