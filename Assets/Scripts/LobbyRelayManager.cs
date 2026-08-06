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
    private const string SessionTimestampKey = "sessionTs";

    [Header("Lobby")]
    [SerializeField] private string lobbyName = "OvercookedPetriLobby";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private bool showLobbyOverlay = true;

    [Header("Heartbeat")]
    [SerializeField] private float heartbeatIntervalSeconds = 15f;

    private Lobby currentLobby;
    private string joinCodeInput = string.Empty;
    private string currentJoinCode = string.Empty;
    private string statusMessage = "Idle";
    private float heartbeatTimer;
    private bool servicesReady;
    private bool busy;
    private Vector2 lobbyOverlayScrollPosition;
    private bool wasNetworkListening;
    private bool wasTwoPlayersConnected;

    private async void Start()
    {
        EnsureNetworkManagerExists();

        await InitializeServicesAsync();
    }

    private void Update()
    {
        HandleEmergencyStopHotkey();
        HandleUnexpectedNetworkDisconnect();

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

    private void HandleUnexpectedNetworkDisconnect()
    {
        bool isListening = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool bothPlayersConnected = isListening && AreBothPlayersConnected();
        if (!busy && currentLobby != null && wasNetworkListening && !isListening)
        {
            _ = LeaveBecausePeerDisconnectedAsync();
        }
        else if (!busy && currentLobby != null && wasTwoPlayersConnected && isListening && !bothPlayersConnected)
        {
            _ = LeaveBecausePeerDisconnectedAsync();
        }

        wasNetworkListening = isListening;
        wasTwoPlayersConnected = bothPlayersConnected;
    }

    private void OnGUI()
    {
        if (!showLobbyOverlay)
        {
            return;
        }

        GameManager gameManager = FindAnyObjectByType<GameManager>();
        bool showLevelSelectionLobbyOverlay = gameManager != null && gameManager.ShouldShowLevelSelectionLobbyOverlay();
        if (showLevelSelectionLobbyOverlay)
        {
            DrawConnectedLevelSelectionLobbyOverlay();
            return;
        }

        if (gameManager != null && gameManager.ShouldSuppressLobbyOverlay() && !showLevelSelectionLobbyOverlay)
        {
            return;
        }

        if (currentLobby != null && AreBothPlayersConnected())
        {
            if (showLevelSelectionLobbyOverlay)
            {
                DrawConnectedLevelSelectionLobbyOverlay();
            }

            return;
        }

        float uiScale = GetLobbyUiScale();
        GUIStyle boxStyle = CreateLobbyGuiStyle(GUI.skin.box, 44f, uiScale, TextAnchor.UpperCenter, FontStyle.Bold, false);
        GUIStyle labelStyle = CreateLobbyGuiStyle(GUI.skin.label, 36f, uiScale, TextAnchor.MiddleLeft, FontStyle.Normal, true);
        GUIStyle statusStyle = CreateLobbyGuiStyle(GUI.skin.label, 28f, uiScale, TextAnchor.MiddleLeft, FontStyle.Normal, true);
        GUIStyle buttonStyle = CreateLobbyGuiStyle(GUI.skin.button, 40f, uiScale, TextAnchor.MiddleCenter, FontStyle.Bold, false);
        GUIStyle textFieldStyle = CreateLobbyGuiStyle(GUI.skin.textField, 40f, uiScale, TextAnchor.MiddleLeft, FontStyle.Normal, false);

        Rect panel = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.Box(panel, "Lobby", boxStyle);

        float outerPadding = 34f * uiScale;
        Rect scrollViewRect = new Rect(
            outerPadding,
            86f * uiScale,
            Mathf.Max(1f, panel.width - outerPadding * 2f),
            Mathf.Max(1f, panel.height - 104f * uiScale));
        float buttonHeight = 78f * uiScale;
        float textFieldHeight = 68f * uiScale;
        float gap = 22f * uiScale;
        bool canStartLobbyAction = servicesReady && !busy && currentLobby == null;
        string statusDisplayText = GetLobbyStatusDisplayText();
        bool showExitButton = ShouldShowExitToDesktopButton();
        float fixedExitButtonHeight = showExitButton ? 58f * uiScale : 0f;
        float fixedExitButtonGap = showExitButton ? 14f * uiScale : 0f;
        scrollViewRect.height = Mathf.Max(
            1f,
            scrollViewRect.height - fixedExitButtonHeight - fixedExitButtonGap);

        float measuredY = 10f * uiScale;
        if (!string.IsNullOrEmpty(statusDisplayText))
        {
            measuredY += 78f * uiScale + gap;
        }

        measuredY += buttonHeight + gap;
        measuredY += 56f * uiScale;
        measuredY += textFieldHeight + gap;
        measuredY += buttonHeight + gap;
        measuredY += buttonHeight + gap;
        float contentBottom = measuredY + buttonHeight;

        measuredY += buttonHeight + gap;
        if (!string.IsNullOrEmpty(currentJoinCode))
        {
            contentBottom = measuredY + 48f * uiScale;
        }

        float naturalContentHeight = contentBottom + 10f * uiScale;
        bool needsVerticalScroll = naturalContentHeight > scrollViewRect.height;
        float scrollbarWidth = needsVerticalScroll ? 18f * uiScale : 0f;
        Rect scrollContentRect = new Rect(
            0f,
            0f,
            Mathf.Max(1f, scrollViewRect.width - scrollbarWidth),
            Mathf.Max(scrollViewRect.height, naturalContentHeight));
        float contentWidth = Mathf.Clamp(scrollContentRect.width - 24f * uiScale, 320f * uiScale, 980f * uiScale);
        float x = (scrollContentRect.width - contentWidth) * 0.5f;
        float y = 10f * uiScale;
        float maximumScrollY = Mathf.Max(0f, naturalContentHeight - scrollViewRect.height);
        lobbyOverlayScrollPosition.x = 0f;
        lobbyOverlayScrollPosition.y = Mathf.Clamp(lobbyOverlayScrollPosition.y, 0f, maximumScrollY);

        lobbyOverlayScrollPosition = GUI.BeginScrollView(
            scrollViewRect,
            lobbyOverlayScrollPosition,
            scrollContentRect,
            false,
            false);
        if (!string.IsNullOrEmpty(statusDisplayText))
        {
            Rect statusRect = new Rect(x, y, contentWidth, 78f * uiScale);
            GUI.Box(statusRect, "");
            GUI.Label(new Rect(statusRect.x + 16f * uiScale, statusRect.y, statusRect.width - 32f * uiScale, statusRect.height), statusDisplayText, statusStyle);
            y += statusRect.height + gap;
        }

        GUI.enabled = canStartLobbyAction;

        if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Create Game", buttonStyle))
        {
            _ = CreateLobbyAndStartHostAsync();
        }

        y += buttonHeight + gap;
        GUI.enabled = true;
        GUI.Label(new Rect(x, y, contentWidth, 48f * uiScale), "Join Code:", labelStyle);
        y += 56f * uiScale;
        GUI.enabled = canStartLobbyAction;
        joinCodeInput = GUI.TextField(new Rect(x, y, contentWidth, textFieldHeight), joinCodeInput ?? string.Empty, textFieldStyle);
        y += textFieldHeight + gap;

        GUI.enabled = canStartLobbyAction && !string.IsNullOrWhiteSpace(joinCodeInput);
        if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Join Game", buttonStyle))
        {
            _ = JoinLobbyAndStartClientAsync(joinCodeInput);
        }

        y += buttonHeight + gap;
        GUI.enabled = currentLobby != null && !busy;
        if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Return to Lobby", buttonStyle))
        {
            _ = LeaveLobbyAsync();
        }

        y += buttonHeight + gap;
        GUI.enabled = !busy && currentLobby == null;
        if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Single Player Game", buttonStyle))
        {
            _ = StartSinglePlayerGameAsync();
        }

        GUI.enabled = true;
        y += buttonHeight + gap;
        if (!string.IsNullOrEmpty(currentJoinCode))
        {
            GUI.Label(new Rect(x, y, contentWidth, 48f * uiScale), "Game Code: " + currentJoinCode, labelStyle);
            y += 58f * uiScale;
        }

        GUI.EndScrollView();

        if (showExitButton)
        {
            GUI.enabled = !busy;
            float exitButtonWidth = Mathf.Min(360f * uiScale, panel.width - outerPadding * 2f);
            Rect exitButtonRect = new Rect(
                outerPadding,
                panel.height - fixedExitButtonHeight - 18f * uiScale,
                exitButtonWidth,
                fixedExitButtonHeight);
            if (GUI.Button(exitButtonRect, "Exit Game", buttonStyle))
            {
                RequestExitToDesktop();
            }

            GUI.enabled = true;
        }
    }

    private void DrawConnectedLevelSelectionLobbyOverlay()
    {
        float uiScale = Mathf.Clamp(Screen.height / 900f, 0.9f, 1.35f);
        GUIStyle buttonStyle = CreateLobbyGuiStyle(GUI.skin.button, 20f, uiScale, TextAnchor.MiddleCenter, FontStyle.Bold, false);
        float buttonWidth = Mathf.Min(190f * uiScale, Screen.width - 24f);
        float buttonHeight = 36f * uiScale;
        float gap = 8f * uiScale;
        bool showExitButton = ShouldShowExitToDesktopButton();
        Rect exitButtonRect = new Rect(12f, Screen.height - buttonHeight - 12f, buttonWidth, buttonHeight);
        Rect leaveButtonRect = showExitButton
            ? new Rect(12f, exitButtonRect.y - buttonHeight - gap, buttonWidth, buttonHeight)
            : exitButtonRect;

        GUI.enabled = !busy;
        if (GUI.Button(leaveButtonRect, "Return to Lobby", buttonStyle))
        {
            _ = LeaveLobbyAsync();
        }

        if (showExitButton)
        {
            GUI.enabled = !busy;
            if (GUI.Button(exitButtonRect, "Exit Game", buttonStyle))
            {
                RequestExitToDesktop();
            }
        }

        GUI.enabled = true;
    }

    private float GetLobbyUiScale()
    {
        return Mathf.Clamp(Screen.height / 720f, 1.15f, 2.4f);
    }

    private string GetLobbyStatusDisplayText()
    {
        if (string.IsNullOrWhiteSpace(statusMessage) || statusMessage == "Idle" || statusMessage == "Signed in.")
        {
            return string.Empty;
        }

        return "Status: " + ShortenLongStatusWords(statusMessage, 30);
    }

    private string ShortenLongStatusWords(string text, int maxWordLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = ShortenStatusWord(words[i], maxWordLength);
        }

        return string.Join(" ", words);
    }

    private string ShortenStatusWord(string word, int maxWordLength)
    {
        if (string.IsNullOrEmpty(word) || word.Length <= maxWordLength)
        {
            return word;
        }

        int startLength = Mathf.Max(8, maxWordLength - 12);
        int endLength = Mathf.Max(4, maxWordLength - startLength - 3);
        startLength = Mathf.Min(startLength, word.Length);
        endLength = Mathf.Min(endLength, word.Length - startLength);
        return word.Substring(0, startLength) + "..." + word.Substring(word.Length - endLength);
    }

    private GUIStyle CreateLobbyGuiStyle(GUIStyle baseStyle, float fontSize, float uiScale, TextAnchor alignment, FontStyle fontStyle, bool wordWrap)
    {
        return new GUIStyle(baseStyle)
        {
            alignment = alignment,
            fontSize = Mathf.RoundToInt(fontSize * uiScale),
            fontStyle = fontStyle,
            wordWrap = wordWrap
        };
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
            statusMessage = "Signed in.";
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
            await CleanupExistingSessionAsync();

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
            joinCodeInput = currentJoinCode;

            EnsureNetworkManagerExists();
            await ResetNetworkSessionAsync();
            EnsureNetworkManagerExists();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (!NetworkManager.Singleton.StartHost())
            {
                throw new InvalidOperationException("Failed to start host.");
            }

            EnterGameManagerNetworkSession();
            heartbeatTimer = heartbeatIntervalSeconds;
            statusMessage = "Host";
        }
        catch (Exception ex)
        {
            statusMessage = "Create game failed: " + ex.Message;
            Debug.LogException(ex);
            await CleanupExistingSessionAsync();
        }
        finally
        {
            busy = false;
        }
    }

    private async Task StartSinglePlayerGameAsync()
    {
        if (busy || currentLobby != null)
        {
            return;
        }

        busy = true;
        try
        {
            await CleanupExistingSessionAsync();
            EnterGameManagerSinglePlayerSession();
            wasNetworkListening = false;
            wasTwoPlayersConnected = false;
            statusMessage = "Single Player";
        }
        catch (Exception ex)
        {
            statusMessage = "Single player start failed: " + ex.Message;
            Debug.LogException(ex);
            ResetGameManagerToLobbyStartScreen();
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
            statusMessage = "Enter join code first.";
            return;
        }

        busy = true;
        try
        {
            await CleanupExistingSessionAsync();

            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim().ToUpperInvariant());
            currentJoinCode = currentLobby.LobbyCode;

            if (!currentLobby.Data.TryGetValue("relayJoinCode", out DataObject relayData) || string.IsNullOrEmpty(relayData.Value))
            {
                throw new InvalidOperationException("Relay join code missing in game data.");
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayData.Value);

            EnsureNetworkManagerExists();
            await ResetNetworkSessionAsync();
            EnsureNetworkManagerExists();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!NetworkManager.Singleton.StartClient())
            {
                throw new InvalidOperationException("Failed to start client.");
            }

            EnterGameManagerNetworkSession();
            statusMessage = "Client joined game " + currentJoinCode;
        }
        catch (Exception ex)
        {
            statusMessage = "Join game failed: " + ex.Message;
            Debug.LogException(ex);
            await CleanupExistingSessionAsync();
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
                statusMessage = "No open game found.";
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
                statusMessage = "No valid game found.";
                return;
            }

            await CleanupExistingSessionAsync();

            currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(selectedLobby.Id);
            currentJoinCode = currentLobby.LobbyCode;
            joinCodeInput = string.Empty;

            if (!currentLobby.Data.TryGetValue("relayJoinCode", out DataObject selectedRelayData) || string.IsNullOrEmpty(selectedRelayData.Value))
            {
                throw new InvalidOperationException("Relay join code missing in selected game data.");
            }

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(selectedRelayData.Value);

            EnsureNetworkManagerExists();
            await ResetNetworkSessionAsync();
            EnsureNetworkManagerExists();
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!NetworkManager.Singleton.StartClient())
            {
                throw new InvalidOperationException("Failed to start client.");
            }

            EnterGameManagerNetworkSession();
            statusMessage = "Client auto-joined game " + currentJoinCode;
        }
        catch (Exception ex)
        {
            statusMessage = "Auto-join game failed: " + ex.Message;
            Debug.LogException(ex);
            await CleanupExistingSessionAsync();
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
            bool wasHost = IsHost() || IsCurrentLobbyHost();
            Lobby lobbyToLeave = currentLobby;
            currentLobby = null;
            currentJoinCode = string.Empty;
            joinCodeInput = string.Empty;
            statusMessage = "Leaving game...";

            await ResetNetworkSessionAsync();

            if (lobbyToLeave != null)
            {
                if (wasHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyToLeave.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(lobbyToLeave.Id, AuthenticationService.Instance.PlayerId);
                }
            }

            wasNetworkListening = false;
            wasTwoPlayersConnected = false;
            statusMessage = "Left game.";
        }
        catch (Exception ex)
        {
            statusMessage = "Leave game failed: " + ex.Message;
            Debug.LogException(ex);
        }
        finally
        {
            busy = false;
        }
    }

    public void RequestExitToDesktop()
    {
        if (busy)
        {
            return;
        }

        _ = ExitToDesktopAsync();
    }

    private async Task ExitToDesktopAsync()
    {
        busy = true;
        statusMessage = "Exiting...";
        try
        {
            await CleanupExistingSessionAsync();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Exit cleanup failed: " + ex.Message);
        }
        finally
        {
            busy = false;
            QuitApplicationNow();
        }
    }

    private bool ShouldShowExitToDesktopButton()
    {
#if UNITY_WEBGL
        return false;
#else
        return true;
#endif
    }

    private void QuitApplicationNow()
    {
#if UNITY_EDITOR
        ForceStopPlayMode();
#else
        Application.Quit();
#endif
    }

    private async Task CleanupExistingSessionAsync()
    {
        bool wasHost = IsHost() || IsCurrentLobbyHost();
        Lobby lobbyToCleanUp = currentLobby;
        currentLobby = null;
        currentJoinCode = string.Empty;
        joinCodeInput = string.Empty;

        await ResetNetworkSessionAsync();

        if (lobbyToCleanUp == null)
        {
            return;
        }

        try
        {
            if (wasHost)
            {
                await LobbyService.Instance.DeleteLobbyAsync(lobbyToCleanUp.Id);
            }
            else if (AuthenticationService.Instance.IsSignedIn)
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyToCleanUp.Id, AuthenticationService.Instance.PlayerId);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Existing game cleanup failed: " + ex.Message);
        }
    }

    private async Task LeaveBecausePeerDisconnectedAsync()
    {
        busy = true;
        try
        {
            await CleanupExistingSessionAsync();
            statusMessage = "Other player left game.";
        }
        finally
        {
            wasNetworkListening = false;
            wasTwoPlayersConnected = false;
            busy = false;
        }
    }

    private async Task ResetNetworkSessionAsync()
    {
        ResetGameManagerToLobbyStartScreen();
        EnsureNetworkManagerExists();
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            wasNetworkListening = false;
            wasTwoPlayersConnected = false;
            return;
        }

        NetworkManager.Singleton.Shutdown();
        for (int i = 0; i < 60; i++)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                await Task.Delay(100);
                wasNetworkListening = false;
                wasTwoPlayersConnected = false;
                return;
            }

            await Task.Delay(50);
        }

        await Task.Delay(100);
        wasNetworkListening = false;
        wasTwoPlayersConnected = false;
    }

    private void ResetGameManagerToLobbyStartScreen()
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.ResetToLobbyStartScreen();
        }
    }

    private void EnterGameManagerNetworkSession()
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.EnterNetworkGameSession();
        }
    }

    private void EnterGameManagerSinglePlayerSession()
    {
        GameManager gameManager = FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.EnterSinglePlayerGameSession();
        }
    }

    private bool IsHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
    }

    private bool IsCurrentLobbyHost()
    {
        return currentLobby != null
            && AuthenticationService.Instance.IsSignedIn
            && currentLobby.HostId == AuthenticationService.Instance.PlayerId;
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

        if (transport != null && manager.NetworkConfig.NetworkTransport != transport)
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
        joinCodeInput = string.Empty;
        busy = false;
    }
}
