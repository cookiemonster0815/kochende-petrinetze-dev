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

		ConfigurePerformanceDefaults();
		EnsureBaseSceneComponents();
		gameplayInitialized = false;

		Debug.Log("Petri-Net Editor active: 1 Select, 2 Place, 3 Transition, 4 Connect, 5 Delete, 6 +Token, 7 -Token");
	}

	private void ConfigurePerformanceDefaults()
	{
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
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

	private void LateUpdate()
	{
		if (!gameplayInitialized || mainCamera == null)
		{
			return;
		}

		UpdateCameraFollowAvatar();
	}

	public override void OnDestroy()
	{
		UnregisterNetworkHandlers();
		base.OnDestroy();
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
			BroadcastSnapshotToClients();
			return;
		}

		if (nodesById.Count > 0)
		{
			EnsureLocalAvatarStartPosition();
			gameplayInitialized = true;
			SendAvatarUpdate(avatarPosition, avatarRotation, heldTransitionId);
		}
	}
}
