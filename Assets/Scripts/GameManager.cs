using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public partial class GameManager : NetworkBehaviour
{
	private const string RealisticDronePrefabPath = "Assets/Realistic Drone/drone/3dModel/Drone.prefab";
	private const string LegacyDronePrefabPath = "Assets/Prefabs/Drone_fbx_7.4_binary.fbx";
	private const string HarborChainPrefabPath = "Assets/Harbor Props Pack vol.1/Prefabs/chain.prefab";
	private const string HarborChainHookPrefabPath = "Assets/Harbor Props Pack vol.1/Prefabs/chain_hook.prefab";

	private void Start()
	{
		if (FindAnyObjectByType<LobbyRelayManager>() == null)
		{
			gameObject.AddComponent<LobbyRelayManager>();
		}

		TryAutoAssignActivityPrefabsInEditor();
		UpgradeActivityPrefabDefaults();
		ConfigurePerformanceDefaults();
		EnsureBaseSceneComponents();
		gameplayInitialized = false;

		Debug.Log("Petri-Net Editor active: 1 Select, 2 Place, 3 Transition, 4 Connect, 5 Delete, 6 +Token, 7 -Token");
	}

	private void ConfigurePerformanceDefaults()
	{
		renderResolutionChecksRemaining = 12;
		nextRenderResolutionCheckTime = 0f;
		ForceCrispRenderingDefaults();
		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
	}

	private void ForceCrispRenderingDefaults()
	{
		int pcQualityIndex = Array.IndexOf(QualitySettings.names, "PC");
		if (pcQualityIndex >= 0 && QualitySettings.GetQualityLevel() != pcQualityIndex)
		{
			QualitySettings.SetQualityLevel(pcQualityIndex, true);
		}

		QualitySettings.globalTextureMipmapLimit = 0;
		QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
		ScalableBufferManager.ResizeBuffers(1f, 1f);
		EnsureMinimumWindowResolution();
		Debug.Log("Render quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()] + ", resolution " + Screen.width + "x" + Screen.height);
	}

	private void EnsureMinimumWindowResolution()
	{
		if (!enforceMinimumWindowResolution || Application.isBatchMode)
		{
			return;
		}

		int targetWidth = Mathf.Max(1, minimumWindowWidth);
		int targetHeight = Mathf.Max(1, minimumWindowHeight);
		if (Display.main != null && Display.main.systemWidth > 0 && Display.main.systemHeight > 0)
		{
			targetWidth = Mathf.Min(targetWidth, Display.main.systemWidth);
			targetHeight = Mathf.Min(targetHeight, Display.main.systemHeight);
		}

		if (Screen.width >= targetWidth && Screen.height >= targetHeight)
		{
			return;
		}

#if !UNITY_WEBGL
		Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
#endif
	}

	private void TryAutoAssignActivityPrefabsInEditor()
	{
#if UNITY_EDITOR
		string currentCuttingToolPath = cuttingTransitionPrefab != null
			? UnityEditor.AssetDatabase.GetAssetPath(cuttingTransitionPrefab)
			: "";
		if (cuttingTransitionPrefab == null || currentCuttingToolPath.Contains("utensil-knife"))
		{
			cuttingTransitionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sprites/FoodKenney/Models/FBX format/cooking-knife-chopping.fbx");
			if (cuttingTransitionPrefab == null)
			{
				cuttingTransitionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sprites/FoodKenney/Models/FBX format/utensil-knife.fbx");
			}
		}

		if (cuttingToolColorMap == null)
		{
			cuttingToolColorMap = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/FoodKenney/Models/FBX format/Textures/colormap.png");
		}

		if (cuttingToolAnimatorController == null)
		{
			cuttingToolAnimatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/cooking-knife-chopping.controller");
		}

		string currentDronePath = avatarDronePrefab != null
			? UnityEditor.AssetDatabase.GetAssetPath(avatarDronePrefab)
			: "";
		GameObject realisticDronePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RealisticDronePrefabPath);
		if (realisticDronePrefab != null)
		{
			avatarDronePrefab = realisticDronePrefab;
			currentDronePath = RealisticDronePrefabPath;
		}
		else if (avatarDronePrefab == null
			|| currentDronePath.IndexOf("Drone_fbx_7.4_binary", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			avatarDronePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(LegacyDronePrefabPath);
			currentDronePath = LegacyDronePrefabPath;
		}

		if (avatarDroneUseImportedAnimationClips && (avatarDroneAnimationClips == null || avatarDroneAnimationClips.Length <= 0))
		{
			avatarDroneAnimationClips = LoadAnimationClipsFromModel(currentDronePath);
		}

		if (avatarCraneChainPrefab == null)
		{
			avatarCraneChainPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HarborChainPrefabPath);
		}

		if (avatarCraneHookPrefab == null)
		{
			avatarCraneHookPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HarborChainHookPrefabPath);
		}
#endif
	}

#if UNITY_EDITOR
	private AnimationClip[] LoadAnimationClipsFromModel(string modelPath)
	{
		UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(modelPath);
		List<AnimationClip> clips = new List<AnimationClip>();
		for (int i = 0; i < assets.Length; i++)
		{
			AnimationClip clip = assets[i] as AnimationClip;
			if (!IsAllowedAvatarDroneClip(clip))
			{
				continue;
			}

			clips.Add(clip);
		}

		return clips.ToArray();
	}

	private bool IsAllowedAvatarDroneClip(AnimationClip clip)
	{
		if (clip == null || clip.length <= 0.001f || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string clipName = clip.name ?? "";
		if (!string.IsNullOrWhiteSpace(avatarDroneAnimationClipNameContains)
			&& clipName.IndexOf(avatarDroneAnimationClipNameContains, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}

		if (!string.IsNullOrWhiteSpace(avatarDroneAnimationClipNameExcludes)
			&& clipName.IndexOf(avatarDroneAnimationClipNameExcludes, StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return false;
		}

		return true;
	}
#endif

	private void UpgradeActivityPrefabDefaults()
	{
		Vector3 oldSmallScale = new Vector3(0.75f, 0.75f, 0.75f);
		Vector3 olderSmallScale = new Vector3(0.55f, 0.55f, 0.55f);
		if (IsApproximatelyAnyVector(cuttingTransitionPrefabLocalScale, oldSmallScale, olderSmallScale))
		{
			cuttingTransitionPrefabLocalScale = new Vector3(1.5f, 1.5f, 1.5f);
		}

		Vector3 oldEmbeddedPosition = new Vector3(0f, 0f, -0.2f);
		Vector3 oldLiftedPosition = new Vector3(0f, 0.32f, -0.48f);
		if (IsApproximatelyAnyVector(cuttingTransitionPrefabLocalPosition, oldEmbeddedPosition, oldLiftedPosition))
		{
			cuttingTransitionPrefabLocalPosition = new Vector3(0f, 0.5f, -0.62f);
		}

		Vector3 oldEdgeOnEuler = new Vector3(18f, -35f, 25f);
		if (IsApproximatelyVector(cuttingTransitionPrefabLocalEuler, oldEdgeOnEuler))
		{
			cuttingTransitionPrefabLocalEuler = new Vector3(90f, 0f, -35f);
		}

		Vector3 oldSmallDroneScale = new Vector3(0.42f, 0.42f, 0.42f);
		Vector3 oldMediumDroneScale = new Vector3(1.35f, 1.35f, 1.35f);
		Vector3 oldLargeDroneScale = new Vector3(2.1f, 2.1f, 2.1f);
		if (IsApproximatelyAnyVector(avatarDroneLocalScale, oldSmallDroneScale, oldMediumDroneScale, oldLargeDroneScale))
		{
			avatarDroneLocalScale = new Vector3(0.675f, 0.675f, 0.675f);
		}

		Vector3 oldHookEuler = new Vector3(-90f, 0f, 0f);
		Vector3 oldRotatedHookEuler = new Vector3(-90f, 0f, 90f);
		Vector3 oldFlippedHookEuler = new Vector3(90f, 0f, 90f);
		Vector3 oldStraightHookEuler = new Vector3(90f, 0f, 0f);
		Vector3 oldTiltedHookEuler = new Vector3(90f, -10f, 90f);
		Vector3 oldRaisedHookEuler = new Vector3(100f, 0f, 90f);
		Vector3 oldMoreRaisedHookEuler = new Vector3(130f, 0f, 90f);
		Vector3 oldSideTiltedHookEuler = new Vector3(90f, -30f, 90f);
		Vector3 oldLowZHookEuler = new Vector3(90f, 0f, 12f);
		Vector3 oldFlatHookEuler = new Vector3(0f, 0f, 90f);
		Vector3 oldTurnedHookEuler = new Vector3(90f, 180f, 90f);
		Vector3 oldZTurnedHookEuler = new Vector3(90f, 0f, 180f);
		Vector3 oldFullyFlippedHookEuler = new Vector3(180f, 0f, 90f);
		Vector3 oldPartlyFlippedHookEuler = new Vector3(120f, 0f, 90f);
		if (IsApproximatelyAnyVector(
			avatarCraneHookLocalEuler,
			oldHookEuler,
			oldRotatedHookEuler,
			oldFlippedHookEuler,
			oldStraightHookEuler,
			oldTiltedHookEuler,
			oldRaisedHookEuler,
			oldMoreRaisedHookEuler,
			oldSideTiltedHookEuler,
			oldLowZHookEuler,
			oldFlatHookEuler,
			oldTurnedHookEuler,
			oldZTurnedHookEuler,
			oldFullyFlippedHookEuler,
			oldPartlyFlippedHookEuler))
		{
			avatarCraneHookLocalEuler = new Vector3(90f, 0f, 90f);
		}

		if (avatarCraneChainLocalScale.y <= 0.13f
			|| avatarCraneChainLocalScale.x <= 0.13f
			|| avatarCraneChainLocalScale.z <= 0.13f
			|| avatarCraneChainLocalScale.y > 0.25f
			|| avatarCraneChainLocalScale.x > 0.25f
			|| avatarCraneChainLocalScale.z > 0.25f)
		{
			avatarCraneChainLocalScale = new Vector3(0.18f, 0.18f, 0.18f);
		}

		if (avatarCraneChainLinkSpacing <= 0.001f || Mathf.Abs(avatarCraneChainLinkSpacing - 0.13f) <= 0.001f)
		{
			avatarCraneChainLinkSpacing = 0.095f;
		}

		if (avatarCraneChainMaxLinks <= 24)
		{
			avatarCraneChainMaxLinks = 36;
		}

		if (avatarCraneHookHangDistance <= 0.75f)
		{
			avatarCraneHookHangDistance = 0.95f;
		}

		if (avatarCraneHookVisualDrop <= 0.001f || avatarCraneHookVisualDrop >= 0.15f)
		{
			avatarCraneHookVisualDrop = 0.015f;
		}

		Vector3 oldHugeHookScale = new Vector3(3f, 3f, 3f);
		Vector3 oldLargeHookScale = new Vector3(1.35f, 1.35f, 1.35f);
		Vector3 oldTinyHookScale = new Vector3(0.24f, 0.24f, 0.24f);
		if (IsApproximatelyAnyVector(avatarCraneHookLocalScale, oldTinyHookScale, oldHugeHookScale, oldLargeHookScale))
		{
			avatarCraneHookLocalScale = new Vector3(0.75f, 0.75f, 0.75f);
		}

		float oldCraneRestHeight = 0.95f;
		if (avatarCraneRestHeight <= 1.45f || Mathf.Abs(avatarCraneRestHeight - oldCraneRestHeight) <= 0.001f)
		{
			avatarCraneRestHeight = 1.75f;
		}

		avatarCraneLoweredHeight = GetCraneHeightForHookTarget(NodeVisualTopZ);
		avatarCraneDipTargetHeight = avatarCraneLoweredHeight;

		if (avatarCraneCurrentHeight <= 1.45f || Mathf.Abs(avatarCraneCurrentHeight - oldCraneRestHeight) <= 0.001f)
		{
			avatarCraneCurrentHeight = avatarCraneRestHeight;
		}

		if (lastAvatarNetworkSyncCraneHeight <= 1.45f || Mathf.Abs(lastAvatarNetworkSyncCraneHeight - oldCraneRestHeight) <= 0.001f)
		{
			lastAvatarNetworkSyncCraneHeight = avatarCraneRestHeight;
		}
	}

	private bool IsApproximatelyVector(Vector3 left, Vector3 right)
	{
		return (left - right).sqrMagnitude <= 0.0001f;
	}

	private bool IsApproximatelyAnyVector(Vector3 value, params Vector3[] candidates)
	{
		for (int i = 0; i < candidates.Length; i++)
		{
			if (IsApproximatelyVector(value, candidates[i]))
			{
				return true;
			}
		}

		return false;
	}

	private void Update()
	{
		MaintainCrispRenderingDefaults();
		HandleNetworkHooks();
		HandleCameraControls();

		if (!IsGameplayConnectionReady())
		{
			return;
		}

		if (showLevelSelection && !gameplayInitialized)
		{
			EnsureLevelSelectionScreen();
			HandleLevelSelectionInput();
			TryInitializeGameplayAfterConnection();
			if (!gameplayInitialized)
			{
				return;
			}
		}
		else
		{
			TryInitializeGameplayAfterConnection();
		}

		if (!gameplayInitialized)
		{
			return;
		}

		HandleGameplayMenuHotkey();
		if (IsGameplayMenuOpen())
		{
			UpdateAvatarVisuals();
			return;
		}

		HandleModeHotkeys();
		if (!buildPetriNetOnStart)
		{
			return;
		}

		HandleAvatarInput();
	}

	private void MaintainCrispRenderingDefaults()
	{
		if (renderResolutionChecksRemaining <= 0 || Time.unscaledTime < nextRenderResolutionCheckTime)
		{
			return;
		}

		renderResolutionChecksRemaining--;
		nextRenderResolutionCheckTime = Time.unscaledTime + 0.5f;
		QualitySettings.globalTextureMipmapLimit = 0;
		ScalableBufferManager.ResizeBuffers(1f, 1f);
	}

	private void LateUpdate()
	{
		if (!gameplayInitialized || mainCamera == null)
		{
			return;
		}

		UpdateCameraFollowAvatar();
		UpdateLevelOrderDisplay();
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

			if (showLevelSelection && !levelSelectionConfirmed)
			{
				return;
			}

			DestroyLevelSelectionScreen();
			EnsureGraphRootExists();
			BuildInitialPetriNet();
			gameplayInitialized = true;
			StartLevelOrderTimeline();
			BroadcastSnapshotToClients();
			return;
		}

		if (nodesById.Count > 0)
		{
			EnsureLocalAvatarStartPosition();
			gameplayInitialized = true;
			StartLevelOrderTimeline();
			SendAvatarUpdate(avatarPosition, avatarRotation, heldTransitionId);
		}
	}
}
