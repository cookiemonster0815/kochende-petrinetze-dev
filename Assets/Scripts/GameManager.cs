using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public partial class GameManager : NetworkBehaviour
{
	private void Start()
	{
		if (FindAnyObjectByType<LobbyRelayManager>() == null)
		{
			gameObject.AddComponent<LobbyRelayManager>();
		}

		EnsureBaseSceneComponents();
		gameplayInitialized = false;

		Debug.Log("Petri-Net Editor active: 1 Select, 2 Place, 3 Transition, 4 Connect, 5 Delete, 6 +Token, 7 -Token");
	}

	private void Update()
	{
		HandleNetworkHooks();
		HandleCameraControls();

		if (!IsGameplayConnectionReady())
		{
			return;
		}

		TryInitializeGameplayAfterConnection();
		if (!gameplayInitialized)
		{
			return;
		}

		HandleModeHotkeys();
		if (!buildPetriNetOnStart)
		{
			return;
		}

		HandleAvatarInput();
	}

	public override void OnDestroy()
	{
		UnregisterNetworkHandlers();
		base.OnDestroy();
	}

	private void OnGUI()
	{
		if (!showEditorOverlay || !IsGameplayConnectionReady() || !gameplayInitialized)
		{
			return;
		}

		GUI.Box(new Rect(12f, 12f, 560f, 240f), "Petri-Net Runtime Editor");
		GUI.Label(new Rect(24f, 40f, 520f, 20f), "Mode: " + currentMode);
		GUI.Label(new Rect(24f, 62f, 520f, 20f), "Role: " + GetNetworkRoleLabel());
		GUI.Label(new Rect(24f, 84f, 520f, 20f), "1 Select | 2 Place | 4 Connect | 5 Delete | 6 +Token | 7 -Token");
		GUI.Label(new Rect(24f, 106f, 520f, 20f), "Select mode: Pool-Transition klicken = nehmen, ins Poolfeld ziehen = zuruecklegen.");
		GUI.Label(new Rect(24f, 128f, 520f, 20f), "Connect: Startnode klicken, dann Zielnode klicken.");
		GUI.Label(new Rect(24f, 150f, 520f, 20f), "Kamera: Mausrad Zoom, MMB Pan, WASD/Pfeile bewegen.");
		GUI.Label(new Rect(24f, 172f, 520f, 20f), "Network: Host autoritativ, Clients senden Commands.");
		GUI.Label(new Rect(24f, 194f, 520f, 20f), "Jeder Spieler sieht nur seinen Bereich + gemeinsamen Transition-Pool.");
	}

	private bool IsGameplayConnectionReady()
	{
		if (!enableNetworkAuthoritativeSync)
		{
			return true;
		}

		NetworkManager net = Unity.Netcode.NetworkManager.Singleton;
		if (net == null || !net.IsListening)
		{
			return false;
		}

		return net.ConnectedClientsIds != null && net.ConnectedClientsIds.Count >= 2;
	}

	private void TryInitializeGameplayAfterConnection()
	{
		if (gameplayInitialized)
		{
			return;
		}

		if (IsHostOrOffline())
		{
			if (!buildPetriNetOnStart)
			{
				gameplayInitialized = true;
				return;
			}

			EnsureGraphRootExists();
			BuildInitialPetriNet();
			gameplayInitialized = true;
			return;
		}

		if (nodesById.Count > 0)
		{
			gameplayInitialized = true;
		}
	}
}
