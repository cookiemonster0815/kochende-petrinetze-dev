using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public partial class GameManager
{
	private const string CuttingActivityVisualName = "CuttingActivityPrefab3D";
	private const string AvatarDroneVisualName = "AvatarDroneVisual";
	private const string GroundPlaneName = "PetriNetGroundXY";
	private const float GroundZ = 0f;
	private const float OverlayZ = -0.02f;
	private const float ArcZ = -0.08f;
	private const float NodeVisualFootprint = 1f;
	private const float NodeVisualHeight = 0.52f;
	private const float NodeVisualCenterZ = -NodeVisualHeight * 0.5f;
	private const float NodeVisualTopZ = -NodeVisualHeight;
	private const float PlaceVisualHeightScale = NodeVisualHeight * 0.5f;
	private const float TransitionLabelCharacterSize = 0.058f;
	private const float TransitionLabelEstimatedCharacterWidth = TransitionLabelCharacterSize * 2.45f;
	private const float TransitionLabelHorizontalPadding = 0.85f;
	private const float TransitionMultilineMinimumVisualWidth = 1.95f;
	private const float CompositeBlockNodeGap = 0.68f;
	private const float CompositeBlockPaddingX = 0.06f;
	private const float CompositeBlockPaddingY = 0.06f;
	private const float CompositeBlockBaseShadowCasterDepth = 0.035f;
	private const float IngredientAreaTransitionEdgePadding = 0.08f;
	private const float IngredientPlaceGap = 0.55f;
	private const float HeldObjectUnderHookGap = 0.025f;
	private const float TokenLayerZ = NodeVisualTopZ - 0.08f;
	private const float NodeLabelLayerZ = NodeVisualTopZ - 0.12f;
	private const float ArcWeightLabelCharacterSize = 0.095f;
	private const float InhibitorCircleRadius = 0.23f;
	private const float GameplayCameraDistance = 10f;
	private const float GameplayCameraTiltPercent = 0.6f;
	private const int LiftedObjectRenderQueue = 3100;
	private readonly Dictionary<Material, int> liftedObjectOriginalRenderQueues = new Dictionary<Material, int>();

	private void EnsureBaseSceneComponents()
	{
		if (createDefaultCameraIfMissing)
		{
			mainCamera = Camera.main;
			if (mainCamera == null)
			{
				GameObject cameraObject = new GameObject("Main Camera");
				mainCamera = cameraObject.AddComponent<Camera>();
				mainCamera.tag = "MainCamera";
			}

			ConfigureCamera(mainCamera);
		}

		if (createDefaultLightIfMissing)
		{
			ConfigureSceneLight();
		}

		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}

		EnsureGroundPlane();
	}

	private void ConfigureCamera(Camera camera)
	{
		ResetCameraFollowVelocity();
		camera.transform.rotation = GetGameplayCameraRotation();
		SetCameraGroundCenter(camera, Vector2.zero);
		camera.orthographic = true;
		camera.orthographicSize = GetSharedScreenCameraSize();
		camera.nearClipPlane = 0.01f;
		camera.farClipPlane = 80f;
		camera.allowMSAA = true;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = new Color(0.95f, 0.96f, 0.98f);
	}

	private Quaternion GetGameplayCameraRotation()
	{
		Vector3 forward = new Vector3(0f, GameplayCameraTiltPercent, 1f).normalized;
		return Quaternion.LookRotation(forward, Vector3.up);
	}

	private void SetCameraGroundCenter(Camera camera, Vector2 groundCenter)
	{
		if (camera == null)
		{
			return;
		}

		camera.transform.rotation = GetGameplayCameraRotation();
		Vector3 center = new Vector3(groundCenter.x, groundCenter.y, GroundZ);
		camera.transform.position = center - camera.transform.forward * GameplayCameraDistance;
	}

	private void CenterGameplayCameraOnLocalAvatar()
	{
		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}

		if (mainCamera == null)
		{
			return;
		}

		manualCameraPanActive = false;
		isMiddlePanning = false;
		ResetCameraFollowVelocity();
		SetCameraGroundCenter(mainCamera, new Vector2(avatarPosition.x, avatarPosition.y));
	}

	private Vector2 GetCameraGroundCenter()
	{
		if (mainCamera == null)
		{
			return Vector2.zero;
		}

		Vector3 center = GetCameraGroundViewportPoint(new Vector2(0.5f, 0.5f));
		return new Vector2(center.x, center.y);
	}

	private Vector3 GetCameraGroundViewportPoint(Vector2 viewportPoint)
	{
		if (mainCamera == null)
		{
			return Vector3.zero;
		}

		Ray ray = mainCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
		Plane groundPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, GroundZ));
		if (groundPlane.Raycast(ray, out float distance))
		{
			Vector3 point = ray.GetPoint(distance);
			point.z = GroundZ;
			return point;
		}

		Vector3 fallback = mainCamera.transform.position + mainCamera.transform.forward * GameplayCameraDistance;
		fallback.z = GroundZ;
		return fallback;
	}

	private void ConfigureSceneLight()
	{
		Light light = FindAnyObjectByType<Light>();
		if (light == null)
		{
			GameObject lightObject = new GameObject("Top Directional Light");
			light = lightObject.AddComponent<Light>();
		}

		light.type = LightType.Directional;
		light.transform.rotation = Quaternion.LookRotation(new Vector3(0.22f, -0.28f, 1f).normalized, Vector3.up);
		light.intensity = 1.05f;
		light.shadows = LightShadows.Soft;
		light.shadowStrength = 0.65f;
		light.shadowBias = 0.02f;
		light.shadowNormalBias = 0.25f;

		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.58f, 0.6f, 0.64f);
	}

	private void EnsureGroundPlane()
	{
		Transform existing = transform.Find(GroundPlaneName);
		GameObject groundObject;
		if (existing == null)
		{
			groundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			groundObject.name = GroundPlaneName;
			groundObject.transform.SetParent(transform, false);
		}
		else
		{
			groundObject = existing.gameObject;
		}

		groundObject.transform.position = new Vector3(0f, 0f, GroundZ);
		groundObject.transform.rotation = Quaternion.identity;
		groundObject.transform.localScale = new Vector3(240f, 240f, 1f);

		Collider collider = groundObject.GetComponent<Collider>();
		if (collider != null)
		{
			Destroy(collider);
		}

		MeshRenderer renderer = groundObject.GetComponent<MeshRenderer>();
		if (renderer == null)
		{
			renderer = groundObject.AddComponent<MeshRenderer>();
		}

		renderer.sharedMaterial = CreatePrimitiveVisualMaterial(new Color(0.9f, 0.93f, 0.91f));
		renderer.shadowCastingMode = ShadowCastingMode.Off;
		renderer.receiveShadows = true;
		renderer.sortingOrder = -100;
	}

	private float GetSharedScreenCameraSize()
	{
		float size;
		if (!enableSharedTransitionPool)
		{
			size = 3.6f;
		}
		else
		{
			size = Mathf.Max(4.8f, playerZoneYSpacing + sharedPoolHalfHeight + 1.4f);
		}

#if UNITY_WEBGL && !UNITY_EDITOR
		size *= Mathf.Max(1f, webGlCameraSizeMultiplier);
#endif
		return size;
	}

	private void EnsureGraphRootExists()
	{
		if (petriNetRoot != null)
		{
			petriNetRoot.gameObject.SetActive(true);
			return;
		}

		Transform existing = transform.Find(petriNetRootName);
		if (existing != null)
		{
			petriNetRoot = existing;
			petriNetRoot.gameObject.SetActive(true);
			return;
		}

		petriNetRoot = new GameObject(petriNetRootName).transform;
		petriNetRoot.SetParent(transform, false);
	}

	private void BuildInitialPetriNet()
	{
		ClearGraph();
		EnsureGraphRootExists();

		if (enableSharedTransitionPool && IsHostOrOffline())
		{
			ulong topOwner = GetLocalActorClientId();
			ulong bottomOwner = singlePlayerMode ? topOwner : GetFirstOtherConnectedClientId(topOwner);
			BuildCollaborativeTwoPlayerLayout(topOwner, bottomOwner);
			return;
		}

		ulong ownerId = GetLocalActorClientId();
		CreatePlaceNode("P_Input", new Vector2(-4f, 0f), 1, true, ownerId, false, false);
		CreatePlaceNode("P_Cooking", new Vector2(0f, 0f), 0, true, ownerId, false, false);
		CreatePlaceNode("P_Done", new Vector2(4f, 0f), 0, true, ownerId, false, false);

		CreateTransitionNode("T_StartCook", new Vector2(-2f, 0f), true, ownerId, false, false);
		CreateTransitionNode("T_FinishCook", new Vector2(2f, 0f), true, ownerId, false, false);

		CreateArcInternal("A_1", "P_Input", "T_StartCook", 1, true, ownerId);
		CreateArcInternal("A_2", "T_StartCook", "P_Cooking", 1, true, ownerId);
		CreateArcInternal("A_3", "P_Cooking", "T_FinishCook", 1, true, ownerId);
		CreateArcInternal("A_4", "T_FinishCook", "P_Done", 1, true, ownerId);

		placeCounter = 4;
		transitionCounter = 3;
		arcCounter = 5;

		ResetLocalAvatarToGameplayStartPosition();
		RefreshPetriNetVisuals();
		UpdateAllArcVisuals();
	}

	private void BuildCollaborativeTwoPlayerLayout(ulong topOwnerClientId, ulong bottomOwnerClientId)
	{
		ClearGraph();
		EnsureGraphRootExists();
		ApplySharedScreenLayoutDefaults();
		RebuildSharedPoolVisual();

		CreateSharedPoolCompositeBlocks(UnassignedOwnerClientId);
		CreateSharedPoolTrashTransition(UnassignedOwnerClientId);
		CreatePlayerCompositeBlocks(true, topOwnerClientId);
		CreatePlayerCompositeBlocks(false, bottomOwnerClientId);

		float topY = sharedPoolY + playerZoneYSpacing;
		float bottomY = sharedPoolY - playerZoneYSpacing;
		float horizontalOffset = Mathf.Min(2.2f, playerZoneXOffset * 0.35f);

		if (!singlePlayerMode)
		{
			CreatePlaceNode("P_Top_In", new Vector2(-horizontalOffset, topY), 0, false, topOwnerClientId, false, false);
			CreateTransitionNode("T_Top_Out", new Vector2(horizontalOffset, topY), false, topOwnerClientId, false, false);

			CreatePlaceNode("P_Bottom_In", new Vector2(horizontalOffset, bottomY), 0, false, bottomOwnerClientId, false, false);
			CreateTransitionNode("T_Bottom_Out", new Vector2(-horizontalOffset, bottomY), false, bottomOwnerClientId, false, false);

			CreateArcInternal("A_Top_1", "T_Top_Out", "P_Bottom_In", 1, false, topOwnerClientId);
			CreateArcInternal("A_Bottom_1", "T_Bottom_Out", "P_Top_In", 1, false, bottomOwnerClientId);
		}

		CreateIngredientSourceNodes(true, topOwnerClientId);
		CreateIngredientSourceNodes(false, bottomOwnerClientId);
		CreateTransitionNode("T_Bottom_Ausliefern", GetDeliveryTransitionPosition(), false, bottomOwnerClientId, false, false);
		CreateConfiguredInhibitorArcs();

		placeCounter = 1;
		transitionCounter = 1;
		arcCounter = 1;
		createdBlockCounter = 1;
		collaborativeLayoutApplied = true;

		ResetLocalAvatarToGameplayStartPosition();
		ResetRemoteAvatarToGameplayStartPosition(topOwnerClientId);
		ResetRemoteAvatarToGameplayStartPosition(bottomOwnerClientId);

		RefreshPetriNetVisuals();
	}

	private void CreateIngredientSourceNodes(bool topSide, ulong ownerClientId)
	{
		string side = topSide ? "Top" : "Bottom";
		int count = GetIngredientCount(topSide);
		for (int i = 0; i < count; i++)
		{
			int number = i + 1;
			string transitionId = "T_" + side + "_Zutat_" + number;
			string placeId = "P_" + side + "_Zutat_" + number;
			CreateTransitionNode(transitionId, GetIngredientTransitionPosition(topSide, i), false, ownerClientId, false, false);
			if (nodesById.TryGetValue(transitionId, out NodeRuntime transitionNode))
			{
				transitionNode.displayName = GetIngredientDisplayName(topSide, i);
			}

			CreatePlaceNode(placeId, GetIngredientPlacePosition(topSide, i), 0, false, ownerClientId, false, false);
			CreateArcInternal("A_" + side + "_Zutat_" + number, transitionId, placeId, 1, false, ownerClientId);
		}
	}

	private void CreateSharedPoolCompositeBlocks(ulong ownerClientId)
	{
		int count = GetPoolBlockCount();
		for (int i = 0; i < count; i++)
		{
			CreateCompositeSequenceBlock(
				GetCompositeBlockIdByIndex(i),
				GetPoolBlockDefinition(i),
				GetSharedPoolBlockSlotPositionByIndex(i),
				ownerClientId,
				true,
				true);
		}
	}

	private void CreateSharedPoolTrashTransition(ulong ownerClientId)
	{
		if (!HasSharedPoolTrashTransition())
		{
			return;
		}

		string transitionId = GetSharedPoolTrashTransitionId();
		CreateTransitionNode(transitionId, GetSharedPoolTrashTransitionPosition(), false, ownerClientId, true, true);
		if (nodesById.TryGetValue(transitionId, out NodeRuntime transitionNode))
		{
			transitionNode.displayName = GetSharedPoolTrashTransitionDisplayName();
		}
	}

	private void CreatePlayerCompositeBlocks(bool topSide, ulong ownerClientId)
	{
		int count = GetPlayerBlockCount(topSide);
		for (int i = 0; i < count; i++)
		{
			CreateCompositeSequenceBlock(
				GetPlayerCompositeBlockIdByIndex(topSide, i),
				GetPlayerBlockDefinition(topSide, i),
				GetPlayerCompositeBlockPosition(topSide, i),
				ownerClientId,
				false,
				false);
		}
	}

	private void CreateCompositeSequenceBlock(string blockId, PoolBlockDefinition definition, Vector2 center, ulong ownerClientId, bool isSharedPoolBlock, bool isSharedPoolAvailable)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		string[] arcIds = GetCompositeBlockArcIds(blockId);
		if (nodeIds == null || arcIds == null)
		{
			return;
		}

		float[] nodeOffsets = GetCompositeBlockNodeXOffsets(blockId, definition);
		if (nodeOffsets.Length < nodeIds.Length)
		{
			return;
		}

		if (IsSingleTransitionBlockDefinition(definition))
		{
			CreateTransitionNode(nodeIds[0], center + new Vector2(nodeOffsets[0], 0f), false, ownerClientId, isSharedPoolBlock, isSharedPoolAvailable);
			CreatePlaceNode(nodeIds[1], center + new Vector2(nodeOffsets[1], 0f), 0, false, ownerClientId, false, false);

			if (nodesById.TryGetValue(nodeIds[0], out NodeRuntime singleTransition))
			{
				singleTransition.displayName = GetPoolBlockFirstTransitionName(definition);
			}

			UpdateCompositeBlockTransitionDimensions(blockId);
			CreateArcInternal(arcIds[0], nodeIds[0], nodeIds[1], GetPoolBlockOutputTokenCount(definition), false, ownerClientId);
			EnsureCompositeBlockVisuals();
			return;
		}

		CreateTransitionNode(nodeIds[0], center + new Vector2(nodeOffsets[0], 0f), false, ownerClientId, isSharedPoolBlock, isSharedPoolAvailable);
		CreatePlaceNode(nodeIds[1], center + new Vector2(nodeOffsets[1], 0f), 0, false, ownerClientId, false, false);
		CreateTransitionNode(nodeIds[2], center + new Vector2(nodeOffsets[2], 0f), false, ownerClientId, isSharedPoolBlock, isSharedPoolAvailable);
		CreatePlaceNode(nodeIds[3], center + new Vector2(nodeOffsets[3], 0f), 0, false, ownerClientId, false, false);

		if (nodesById.TryGetValue(nodeIds[0], out NodeRuntime firstTransition))
		{
			firstTransition.displayName = GetPoolBlockFirstTransitionName(definition);
		}

		if (nodesById.TryGetValue(nodeIds[2], out NodeRuntime secondTransition))
		{
			secondTransition.displayName = GetPoolBlockSecondTransitionName(definition);
		}

		if (nodesById.TryGetValue(nodeIds[1], out NodeRuntime bufferPlace))
		{
			bufferPlace.processingDuration = GetPoolBlockProcessingSeconds(definition);
			EnsureTimedPlaceProcessingVisual(bufferPlace);
		}

		UpdateCompositeBlockTransitionDimensions(blockId);
		CreateArcInternal(arcIds[0], nodeIds[0], nodeIds[1], 1, false, ownerClientId);
		CreateArcInternal(arcIds[1], nodeIds[1], nodeIds[2], 1, false, ownerClientId);
		CreateArcInternal(arcIds[2], nodeIds[2], nodeIds[3], GetPoolBlockOutputTokenCount(definition), false, ownerClientId);
		EnsureCompositeBlockVisuals();
	}

	private string CreatePlaceTransitionBlock(Vector2 center, ulong ownerClientId)
	{
		string blockId = GetNextCreatedCompositeBlockId();
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		string[] arcIds = GetCompositeBlockArcIds(blockId);
		if (nodeIds == null || nodeIds.Length < 2 || arcIds == null || arcIds.Length < 1)
		{
			return null;
		}

		float[] nodeOffsets = GetCompositeBlockNodeXOffsets(blockId, null);
		if (nodeOffsets.Length < nodeIds.Length)
		{
			return null;
		}

		CreateTransitionNode(nodeIds[0], center + new Vector2(nodeOffsets[0], 0f), false, ownerClientId, false, false);
		CreatePlaceNode(nodeIds[1], center + new Vector2(nodeOffsets[1], 0f), 0, false, ownerClientId, false, false);
		if (nodesById.TryGetValue(nodeIds[0], out NodeRuntime storageTransition))
		{
			storageTransition.displayName = "Lager";
		}

		UpdateCompositeBlockTransitionDimensions(blockId);
		CreateArcInternal(arcIds[0], nodeIds[0], nodeIds[1], 1, false, ownerClientId);
		EnsureCompositeBlockVisuals();
		RefreshPetriNetVisuals();
		return blockId;
	}

	private void CreateConfiguredInhibitorArcs()
	{
		if (levelInhibitorArcs == null || levelInhibitorArcs.Count <= 0)
		{
			return;
		}

		for (int i = 0; i < levelInhibitorArcs.Count; i++)
		{
			PetriNetLevelInhibitorArcDefinition inhibitor = levelInhibitorArcs[i];
			if (inhibitor == null)
			{
				continue;
			}

			if (!TryResolveInhibitorSourcePlaceId(inhibitor, out string sourcePlaceId)
				|| !TryResolveInhibitorTargetTransitionId(inhibitor, out string targetTransitionId))
			{
				Debug.LogWarning("Inhibitor arc could not be resolved: " + inhibitor.sourceBlockFirstTransitionName + " -> " + inhibitor.targetTransitionName);
				continue;
			}

			ulong ownerClientId = nodesById.TryGetValue(targetTransitionId, out NodeRuntime targetTransition)
				? targetTransition.ownerClientId
				: UnassignedOwnerClientId;
			CreateArcInternal("A_Inhibitor_" + (i + 1), sourcePlaceId, targetTransitionId, 1, false, ownerClientId, ArcKind.Inhibitor);
		}
	}

	private bool TryResolveInhibitorSourcePlaceId(PetriNetLevelInhibitorArcDefinition inhibitor, out string sourcePlaceId)
	{
		sourcePlaceId = null;
		if (inhibitor == null || !TryFindCompositeBlockByFirstTransitionName(inhibitor.sourceBlockFirstTransitionName, out string blockId))
		{
			return false;
		}

		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		sourcePlaceId = inhibitor.sourcePlace == PetriNetLevelBlockPlace.ausgabe || nodeIds.Length <= 2
			? nodeIds[nodeIds.Length - 1]
			: nodeIds[1];
		return nodesById.TryGetValue(sourcePlaceId, out NodeRuntime sourcePlace) && sourcePlace.type == NodeType.Place;
	}

	private bool TryResolveInhibitorTargetTransitionId(PetriNetLevelInhibitorArcDefinition inhibitor, out string targetTransitionId)
	{
		targetTransitionId = null;
		if (inhibitor == null)
		{
			return false;
		}

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.type != NodeType.Transition)
			{
				continue;
			}

			if (NamesMatch(GetNodeDisplayName(node), inhibitor.targetTransitionName))
			{
				targetTransitionId = node.id;
		return true;
	}
		}

		return false;
	}

	private bool TryFindCompositeBlockByFirstTransitionName(string firstTransitionName, out string blockId)
	{
		blockId = null;
		List<string> blockIds = GetAllCompositeBlockIds();
		for (int i = 0; i < blockIds.Count; i++)
		{
			PoolBlockDefinition definition = GetCompositeBlockDefinition(blockIds[i]);
			if (definition != null && NamesMatch(GetPoolBlockFirstTransitionName(definition), firstTransitionName))
			{
				blockId = blockIds[i];
			return true;
		}
		}

		return false;
	}

	private bool NamesMatch(string left, string right)
	{
		return string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
	}

	private void SeedRemoteAvatarStartPosition(ulong clientId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return;
		}

		if (clientId == GetLocalActorClientId() || remoteAvatarPositions.ContainsKey(clientId))
		{
			return;
		}

		remoteAvatarPositions[clientId] = GetDefaultAvatarStartPosition(clientId);
		remoteAvatarRotations[clientId] = 0f;
		remoteAvatarInventories[clientId] = new RemoteHeldObjectState { kind = HeldObjectKind.None, id = "", offset = Vector2.zero };
		remoteAvatarCraneHeights[clientId] = avatarCraneRestHeight;
		remoteCraneConnectStates.Remove(clientId);
	}

	private void ResetRemoteAvatarToGameplayStartPosition(ulong clientId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening || clientId == GetLocalActorClientId())
		{
			return;
		}

		remoteAvatarPositions[clientId] = GetDefaultAvatarStartPosition(clientId);
		remoteAvatarRotations[clientId] = 0f;
		remoteAvatarInventories[clientId] = new RemoteHeldObjectState { kind = HeldObjectKind.None, id = "", offset = Vector2.zero };
		remoteAvatarCraneHeights[clientId] = avatarCraneRestHeight;
		remoteCraneConnectStates.Remove(clientId);
	}

	private ulong GetFirstOtherConnectedClientId(ulong fallbackBaseClientId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return fallbackBaseClientId + 1;
		}

		foreach (ulong clientId in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds)
		{
			if (clientId != fallbackBaseClientId)
			{
				return clientId;
			}
		}

		return fallbackBaseClientId + 1;
	}

	private Vector3 GetDefaultAvatarStartPosition(ulong actorClientId)
	{
		ApplySharedScreenLayoutDefaults();
		float y = IsActorTopSide(actorClientId)
			? sharedPoolY + playerZoneYSpacing
			: sharedPoolY - playerZoneYSpacing;
		return new Vector3(0f, y, 0f);
	}

	private void ApplySharedScreenLayoutDefaults()
	{
		if (enableSharedTransitionPool)
		{
			sharedPoolY = 0f;
		}
	}

	private void EnsureLocalAvatarStartPosition()
	{
		if (avatarStartPositionApplied)
		{
			return;
		}

		ResetLocalAvatarToGameplayStartPosition();
	}

	private void ResetLocalAvatarToGameplayStartPosition()
	{
		avatarPosition = GetDefaultAvatarStartPosition(GetLocalActorClientId());
		avatarRotation = 0f;
		avatarCraneCurrentHeight = avatarCraneRestHeight;
		avatarCraneDipTargetHeight = avatarCraneLoweredHeight;
		avatarCraneAnimationStartTime = -10f;
		heldTransitionId = null;
		heldPlaceId = null;
		heldCompositeBlockId = null;
		heldCompositeBlockOffset = Vector2.zero;
		craneConnectStartNodeId = null;
		CancelCraneConnectPreview();
		pendingClaimedTransitionId = null;
		draggedNodeId = null;
		draggedCompositeBlockId = null;
		pendingCreatedBlockPickup = false;
		pendingCreatedBlockExistingIds.Clear();
		avatarStartPositionApplied = true;
		lastAvatarPosition = avatarPosition;
		lastAvatarNetworkSyncRotation = avatarRotation;
		lastAvatarNetworkSyncHeldId = "";
		lastAvatarNetworkSyncCraneHeight = avatarCraneCurrentHeight;
		nextAvatarNetworkSyncTime = 0f;
		nextReliableAvatarNetworkSyncTime = 0f;
		CenterGameplayCameraOnLocalAvatar();
	}

	private void ClearGraph()
	{
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			if (pair.Value.gameObject != null)
			{
				Destroy(pair.Value.gameObject);
			}
		}

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			if (pair.Value.transform != null)
			{
				Destroy(pair.Value.transform.gameObject);
			}
		}

		nodesById.Clear();
		arcsById.Clear();
		nodeByCollider.Clear();
		arcByCollider.Clear();
		foreach (KeyValuePair<string, CompositeBlockRuntime> pair in compositeBlocksById)
		{
			if (pair.Value.gameObject != null)
			{
				Destroy(pair.Value.gameObject);
			}
		}
		compositeBlocksById.Clear();
		compositeBlockByCollider.Clear();
		if (sharedPoolVisualRoot != null)
		{
			Destroy(sharedPoolVisualRoot.gameObject);
			sharedPoolVisualRoot = null;
		}
		connectStartNodeId = null;
		CancelCraneConnectPreview();
		draggedNodeId = null;
		heldTransitionId = null;
		heldPlaceId = null;
		heldCompositeBlockId = null;
		heldCompositeBlockOffset = Vector2.zero;
		pendingCreatedBlockPickup = false;
		pendingCreatedBlockExistingIds.Clear();
		DestroyCraneConnectPreviewVisual();
		DestroyRemoteCraneConnectPreviewVisuals();
		DestroyCraneHoverSelectionVisual();
		DestroyLevelTutorialVisuals();
	}

	private void DestroyAvatarVisuals()
	{
		if (localAvatarVisual != null)
		{
			Destroy(localAvatarVisual);
			localAvatarVisual = null;
		}

		if (localAvatarArrow != null)
		{
			Destroy(localAvatarArrow);
			localAvatarArrow = null;
		}

		if (localAvatarShadow != null)
		{
			Destroy(localAvatarShadow);
			localAvatarShadow = null;
		}

		if (localAvatarCable != null)
		{
			Destroy(localAvatarCable);
			localAvatarCable = null;
		}

		if (localHeldNodeShadow != null)
		{
			Destroy(localHeldNodeShadow);
			localHeldNodeShadow = null;
		}

		foreach (KeyValuePair<ulong, GameObject> pair in remoteAvatarVisuals)
		{
			if (pair.Value != null)
			{
				Destroy(pair.Value);
			}
		}

		remoteAvatarVisuals.Clear();
		DestroyRemoteCraneHoverVisuals();
		DestroyRemoteCraneConnectPreviewVisuals();
		remoteAvatarPositions.Clear();
		remoteAvatarRotations.Clear();
		remoteAvatarInventories.Clear();
		remoteAvatarCraneHeights.Clear();
		remoteCraneConnectStates.Clear();
	}

	private void CreatePlaceNode(string id, Vector2 position, int initialTokens, bool refreshVisuals, ulong ownerClientId, bool isSharedPoolTransition, bool isSharedPoolAvailable)
	{
		if (nodesById.ContainsKey(id))
		{
			return;
		}

		GameObject nodeObject = new GameObject(id);
		nodeObject.transform.SetParent(petriNetRoot, false);
		nodeObject.transform.position = new Vector3(position.x, position.y, 0f);
		nodeObject.transform.localScale = Vector3.one;

		SpriteRenderer renderer = nodeObject.AddComponent<SpriteRenderer>();
		renderer.sprite = GetCircleSprite();
		renderer.sortingOrder = 30;
		renderer.color = placeColor;
		if (placeMaterial != null)
		{
			renderer.sharedMaterial = placeMaterial;
		}
		MakeSpriteRendererInvisible(renderer);

		MeshRenderer visual3DRenderer = CreatePrimitiveVisual3D(
			nodeObject.transform,
			"PlaceCylinder3D",
			PrimitiveType.Cylinder,
			placeColor,
			new Vector3(0f, 0f, NodeVisualCenterZ),
			new Vector3(NodeVisualFootprint, PlaceVisualHeightScale, NodeVisualFootprint),
			Quaternion.Euler(-90f, 0f, 0f));

		CircleCollider2D collider = nodeObject.AddComponent<CircleCollider2D>();

		Transform tokenRoot = new GameObject("Tokens").transform;
		tokenRoot.SetParent(nodeObject.transform, false);
		tokenRoot.localPosition = new Vector3(0f, 0f, TokenLayerZ);

		TextMesh label = CreateNodeLabel(nodeObject.transform, new Vector3(0f, -1.1f, 0f), 0.08f);
		TextMesh capacityLabel = CreateNodeLabel(nodeObject.transform, new Vector3(0f, 0.78f, 0f), ArcWeightLabelCharacterSize);
		capacityLabel.gameObject.name = "CapacityLabel";
		capacityLabel.gameObject.SetActive(false);

		NodeRuntime node = new NodeRuntime
		{
			id = id,
			type = NodeType.Place,
			ownerClientId = ownerClientId,
			isSharedPoolTransition = isSharedPoolTransition,
			isSharedPoolAvailable = isSharedPoolAvailable,
			processingDuration = GetTimedPlaceProcessingDuration(id),
			transform = nodeObject.transform,
			renderer = renderer,
			visual3D = visual3DRenderer != null ? visual3DRenderer.gameObject : null,
			visual3DRenderer = visual3DRenderer,
			collider = collider,
			label = label,
			capacityLabel = capacityLabel,
			tokenRoot = tokenRoot,
		};

		SetUntypedTokenCount(node, Mathf.Max(0, initialTokens));
		nodesById[id] = node;
		nodeByCollider[collider] = id;
		EnsureTimedPlaceProcessingVisual(node);

		if (refreshVisuals)
		{
			RefreshPetriNetVisuals();
		}
	}

	private void CreateTransitionNode(string id, Vector2 position, bool refreshVisuals, ulong ownerClientId, bool isSharedPoolTransition, bool isSharedPoolAvailable)
	{
		if (nodesById.ContainsKey(id))
		{
			return;
		}

		GameObject nodeObject = new GameObject(id);
		nodeObject.transform.SetParent(petriNetRoot, false);
		nodeObject.transform.position = new Vector3(position.x, position.y, 0f);
		nodeObject.transform.localScale = Vector3.one;

		SpriteRenderer renderer = nodeObject.AddComponent<SpriteRenderer>();
		renderer.sprite = GetSquareSprite();
		renderer.drawMode = SpriteDrawMode.Sliced;
		renderer.size = new Vector2(NodeVisualFootprint, NodeVisualFootprint);
		renderer.sortingOrder = 30;
		renderer.color = transitionEnabledColor;
		if (transitionMaterial != null)
		{
			renderer.sharedMaterial = transitionMaterial;
		}
		MakeSpriteRendererInvisible(renderer);

		MeshRenderer visual3DRenderer = CreatePrimitiveVisual3D(
			nodeObject.transform,
			"TransitionBlock3D",
			PrimitiveType.Cube,
			transitionEnabledColor,
			new Vector3(0f, 0f, NodeVisualCenterZ),
			new Vector3(NodeVisualFootprint, NodeVisualFootprint, NodeVisualHeight),
			Quaternion.identity);

		BoxCollider2D collider = nodeObject.AddComponent<BoxCollider2D>();
		collider.size = new Vector2(NodeVisualFootprint, NodeVisualFootprint);
		TextMesh label = CreateNodeLabel(nodeObject.transform, new Vector3(0f, 0f, 0f), 0.06f);

		NodeRuntime node = new NodeRuntime
		{
			id = id,
			type = NodeType.Transition,
			tokens = 0,
			ownerClientId = ownerClientId,
			isSharedPoolTransition = isSharedPoolTransition,
			isSharedPoolAvailable = isSharedPoolAvailable,
			transform = nodeObject.transform,
			renderer = renderer,
			visual3D = visual3DRenderer != null ? visual3DRenderer.gameObject : null,
			visual3DRenderer = visual3DRenderer,
			collider = collider,
			label = label,
			tokenRoot = null,
		};

		nodesById[id] = node;
		nodeByCollider[collider] = id;

		if (refreshVisuals)
		{
			RefreshPetriNetVisuals();
		}
	}

	private bool CreateArcInternal(string arcId, string fromId, string toId, int weight, bool refreshVisuals, ulong ownerClientId, ArcKind kind = ArcKind.Normal)
	{
		if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
		{
			return false;
		}

		if (arcsById.ContainsKey(arcId))
		{
			return false;
		}

		if (!nodesById.TryGetValue(fromId, out NodeRuntime fromNode) || !nodesById.TryGetValue(toId, out NodeRuntime toNode))
		{
			return false;
		}

		kind = GetEffectiveArcKind(fromId, toId, kind);

		if (kind == ArcKind.Inhibitor && (fromNode.type != NodeType.Place || toNode.type != NodeType.Transition))
		{
			Debug.LogWarning("Inhibitor arc rejected: only Place->Transition is allowed.");
			return false;
		}

		if (fromNode.type == toNode.type)
		{
			Debug.LogWarning("Arc rejected: only Place->Transition or Transition->Place is allowed.");
			return false;
		}

		if (kind != ArcKind.Inhibitor && !IsArcAllowedByIngredientRules(fromId, toId))
		{
			return false;
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime existing = pair.Value;
			if (existing.fromId == fromId && existing.toId == toId)
			{
				return false;
			}
		}

		GameObject arcObject = new GameObject(arcId);
		arcObject.transform.SetParent(petriNetRoot, false);

		Color arcColor = new Color(0.18f, 0.2f, 0.25f);
		LineRenderer body = arcObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(body, 2, arcWidth, 24, arcColor);

		GameObject arrowObject = new GameObject("Arrow");
		arrowObject.transform.SetParent(arcObject.transform, false);
		LineRenderer arrow = arrowObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(arrow, 3, arcWidth, 25, arcColor);

		GameObject resetArrowObject = new GameObject("ResetArrow");
		resetArrowObject.transform.SetParent(arcObject.transform, false);
		LineRenderer resetArrow = resetArrowObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(resetArrow, 3, arcWidth, 25, arcColor);
		resetArrowObject.SetActive(false);

		GameObject inhibitorCircleObject = new GameObject("InhibitorCircle");
		inhibitorCircleObject.transform.SetParent(arcObject.transform, false);
		LineRenderer inhibitorCircle = inhibitorCircleObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(inhibitorCircle, 32, arcWidth, 25, arcColor, 6, 8, true);
		inhibitorCircleObject.SetActive(false);

		GameObject weightLabelObject = new GameObject("WeightLabel");
		weightLabelObject.transform.SetParent(arcObject.transform, false);
		TextMesh weightLabel = weightLabelObject.AddComponent<TextMesh>();
		weightLabel.text = "";
		weightLabel.characterSize = ArcWeightLabelCharacterSize;
		weightLabel.fontSize = 64;
		weightLabel.anchor = TextAnchor.MiddleCenter;
		weightLabel.alignment = TextAlignment.Center;
		weightLabel.color = Color.black;
		MeshRenderer weightLabelRenderer = weightLabelObject.GetComponent<MeshRenderer>();
		if (weightLabelRenderer != null)
		{
			weightLabelRenderer.sortingOrder = 52;
		}

		EdgeCollider2D collider = arcObject.AddComponent<EdgeCollider2D>();
		collider.edgeRadius = 0.14f;
		collider.isTrigger = true;  // Allow avatars to pass through arcs

		ArcRuntime arc = new ArcRuntime
		{
			id = arcId,
			fromId = fromId,
			toId = toId,
			weight = Mathf.Max(1, weight),
			ownerClientId = ownerClientId,
			kind = kind,
			gameObject = arcObject,
			body = body,
			arrow = arrow,
			resetArrow = resetArrow,
			inhibitorCircle = inhibitorCircle,
			weightLabel = weightLabel,
			collider = collider,
		};

		arcsById[arcId] = arc;
		arcByCollider[collider] = arcId;
		UpdateArcVisual(arc);

		if (refreshVisuals)
		{
			RefreshPetriNetVisuals();
		}

		return true;
	}

	private void ConfigureGroundLineRenderer(LineRenderer line, int positionCount, float width, int sortingOrder, Color color, int capVertices = 6, int cornerVertices = 6, bool loop = false)
	{
		if (line == null)
		{
			return;
		}

		line.positionCount = positionCount;
		line.loop = loop;
		line.useWorldSpace = true;
		line.alignment = LineAlignment.TransformZ;
		line.sortingOrder = sortingOrder;
		line.startWidth = width;
		line.endWidth = width;
		line.numCapVertices = capVertices;
		line.numCornerVertices = cornerVertices;
		line.textureMode = LineTextureMode.Stretch;
		line.material = GetArcMaterial();
		line.startColor = color;
		line.endColor = color;
		line.generateLightingData = false;
		line.shadowCastingMode = ShadowCastingMode.Off;
		line.receiveShadows = false;
	}

	private bool RemoveNodeInternal(string nodeId)
	{
		if (!nodesById.TryGetValue(nodeId, out NodeRuntime node))
		{
			return false;
		}

		List<string> arcIdsToRemove = new List<string>();
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.fromId == nodeId || arc.toId == nodeId)
			{
				arcIdsToRemove.Add(arc.id);
			}
		}

		for (int i = 0; i < arcIdsToRemove.Count; i++)
		{
			RemoveArcInternal(arcIdsToRemove[i]);
		}

		nodeByCollider.Remove(node.collider);
		nodesById.Remove(nodeId);
		if (connectStartNodeId == nodeId)
		{
			connectStartNodeId = null;
		}

		if (craneConnectStartNodeId == nodeId)
		{
			CancelCraneConnectPreview();
		}

		if (heldTransitionId == nodeId)
		{
			heldTransitionId = null;
		}

		if (heldPlaceId == nodeId)
		{
			heldPlaceId = null;
		}

		Destroy(node.transform.gameObject);
		RefreshPetriNetVisuals();
		return true;
	}

	private bool RemoveArcInternal(string arcId, bool refreshVisuals = true)
	{
		if (!arcsById.TryGetValue(arcId, out ArcRuntime arc))
		{
			return false;
		}

		arcByCollider.Remove(arc.collider);
		arcsById.Remove(arcId);
		Destroy(arc.gameObject);
		if (refreshVisuals)
		{
			RefreshPetriNetVisuals();
		}
		return true;
	}

	private void RefreshPetriNetVisuals()
	{
		NormalizeArcKindsAndRemoveInvalidArcs();

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node.type == NodeType.Place)
			{
				node.label.text = GetPlaceDebugLabel(node);
				node.label.color = Color.black;
				node.label.characterSize = 0.04f;
				node.label.lineSpacing = 0.78f;
				SetNodeVisualColor(node, placeColor);
				UpdatePlaceCapacityLabel(node);
				RefreshTokenVisuals(node);
				SetPlaceSorting(node, node.id == heldPlaceId || IsHeldCompositeBlockNode(node));
			}
			else
			{
					string transitionLabel = FormatTransitionLabel(GetLocalizedNodeDisplayName(node));
				node.label.text = transitionLabel;
				node.label.color = Color.black;
				node.label.characterSize = GetTransitionLabelCharacterSize(transitionLabel);
				node.label.lineSpacing = transitionLabel.Contains("\n") ? 0.78f : 1f;
				UpdateTransitionVisualDimensions(node, transitionLabel);
				SetTransitionSorting(node, node.id == heldTransitionId || IsHeldCompositeBlockNode(node));
				SetNodeVisualColor(node, IsTransitionEnabled(node.id) ? transitionEnabledColor : transitionDisabledColor);
			}
		}

		EnsureCompositeBlockVisuals();
		NormalizeCompositeBlockSorting();
		UpdateAllArcVisuals();
		UpdateTimedPlaceProcessingVisuals();
		UpdateVisibilityForLocalPlayer();
	}

	private void NormalizeArcKindsAndRemoveInvalidArcs()
	{
		List<string> arcIdsToRemove = new List<string>();
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc == null || arc.kind == ArcKind.Inhibitor)
			{
				continue;
			}

			if (!IsArcAllowedByIngredientRules(arc.fromId, arc.toId))
			{
				arcIdsToRemove.Add(arc.id);
				continue;
			}

			arc.kind = GetEffectiveArcKind(arc.fromId, arc.toId, arc.kind);
		}

		for (int i = 0; i < arcIdsToRemove.Count; i++)
		{
			RemoveArcInternal(arcIdsToRemove[i], false);
		}
	}

	private void NormalizeCompositeBlockSorting()
	{
		if (compositeBlocksById == null || compositeBlocksById.Count == 0)
		{
			return;
		}

		List<string> blockIds = new List<string>(compositeBlocksById.Keys);
		for (int i = 0; i < blockIds.Count; i++)
		{
			string blockId = blockIds[i];
			SetCompositeBlockSorting(blockId, IsCompositeBlockLifted(blockId));
		}
	}

	private bool IsCompositeBlockLifted(string blockId)
	{
		return blockId == heldCompositeBlockId || IsCompositeBlockHeldByRemoteAvatar(blockId);
	}

	private bool IsCompositeBlockHeldByRemoteAvatar(string blockId)
	{
		if (string.IsNullOrEmpty(blockId) || remoteAvatarInventories == null)
		{
			return false;
		}

		foreach (KeyValuePair<ulong, RemoteHeldObjectState> pair in remoteAvatarInventories)
		{
			RemoteHeldObjectState heldState = pair.Value;
			if (heldState != null && heldState.kind == HeldObjectKind.CompositeBlock && heldState.id == blockId)
			{
				return true;
			}
		}

		return false;
	}

	private GameObject localAvatarVisual;
	private GameObject localAvatarArrow;
	private GameObject localAvatarShadow;
	private GameObject localAvatarCable;
	private GameObject localHeldNodeShadow;
	private GameObject localCraneHoverNodeShadow;
	private LineRenderer localCraneHoverNodeOutline;
	private GameObject localCraneHoverArcHighlight;
	private LineRenderer localCraneHoverArcBody;
	private LineRenderer localCraneHoverArcArrow;
	private GameObject localCraneConnectPreview;
	private LineRenderer localCraneConnectPreviewBody;
	private LineRenderer localCraneConnectPreviewArrow;
	private Dictionary<ulong, GameObject> remoteAvatarVisuals = new Dictionary<ulong, GameObject>();
	private Dictionary<ulong, CraneHoverSelectionVisual> remoteCraneHoverVisuals = new Dictionary<ulong, CraneHoverSelectionVisual>();
	private Dictionary<ulong, CraneConnectPreviewVisual> remoteCraneConnectPreviewVisuals = new Dictionary<ulong, CraneConnectPreviewVisual>();

	private class CraneHoverSelectionVisual
	{
		public GameObject root;
		public GameObject nodeRoot;
		public LineRenderer nodeOutline;
		public GameObject arcRoot;
		public LineRenderer arcBody;
		public LineRenderer arcArrow;
	}

	private class CraneConnectPreviewVisual
	{
		public GameObject root;
		public LineRenderer body;
		public LineRenderer arrow;
	}

	private Color GetAvatarColor(ulong clientId)
	{
		// Player 1 (ClientId 0) = Blau, Player 2 (ClientId 1) = Orange
		if (clientId == 0)
			return new Color(0.18f, 0.48f, 0.95f, 0.9f); // Blau
		else
			return new Color(0.95f, 0.48f, 0.12f, 0.9f); // Orange
	}

	private MeshRenderer CreatePrimitiveVisual3D(Transform parent, string name, PrimitiveType primitiveType, Color color, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
	{
		GameObject visual = GameObject.CreatePrimitive(primitiveType);
		visual.name = name;
		visual.transform.SetParent(parent, false);
		visual.transform.localPosition = localPosition;
		visual.transform.localRotation = localRotation;
		visual.transform.localScale = localScale;

		Collider collider = visual.GetComponent<Collider>();
		if (collider != null)
		{
			Destroy(collider);
		}

		MeshRenderer meshRenderer = visual.GetComponent<MeshRenderer>();
		if (meshRenderer != null)
		{
			meshRenderer.material = CreatePrimitiveVisualMaterial(color);
			meshRenderer.sortingOrder = 31;
			ConfigureMeshRendererFor3D(meshRenderer, true, true);
		}

		return meshRenderer;
	}

	private void UpdateTimedPlaceActivityVisual(NodeRuntime placeNode, bool processingActive)
	{
		if (placeNode == null || placeNode.type != NodeType.Place || placeNode.transform == null)
		{
			return;
		}

		Transform cuttingVisualTransform = placeNode.transform.Find(CuttingActivityVisualName);
		if (cuttingVisualTransform != null)
		{
			cuttingVisualTransform.gameObject.SetActive(false);
			Destroy(cuttingVisualTransform.gameObject);
		}
	}

	private bool IsCuttingName(string value)
	{
		return !string.IsNullOrEmpty(value)
			&& value.IndexOf("schneid", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void DestroyRuntimeColliders(GameObject root)
	{
		if (root == null)
		{
			return;
		}

		Collider[] colliders3D = root.GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < colliders3D.Length; i++)
		{
			Destroy(colliders3D[i]);
		}

		Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);
		for (int i = 0; i < colliders2D.Length; i++)
		{
			Destroy(colliders2D[i]);
		}
	}

	private void SetMeshRenderersSortingOrder(Transform root, int sortingOrder)
	{
		if (root == null)
		{
			return;
		}

		MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			renderers[i].sortingOrder = sortingOrder;
			ConfigureMeshRendererFor3D(renderers[i], true, true);
		}
	}

	private void EnsureRenderersMinimumSortingOrder(Transform root, int minimumSortingOrder)
	{
		if (root == null)
		{
			return;
		}

		Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			if (renderers[i] != null && renderers[i].sortingOrder < minimumSortingOrder)
			{
				renderers[i].sortingOrder = minimumSortingOrder;
			}
		}
	}

	private void SetRenderersAboveTutorialBubbles(Transform root, bool lifted)
	{
		if (root == null)
		{
			return;
		}

		Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
		for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
		{
			Renderer renderer = renderers[rendererIndex];
			if (renderer == null)
			{
				continue;
			}

			Material[] materials = lifted ? renderer.materials : renderer.sharedMaterials;
			for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
			{
				Material material = materials[materialIndex];
				if (material == null)
				{
					continue;
				}

				if (lifted)
				{
					if (!liftedObjectOriginalRenderQueues.ContainsKey(material))
					{
						liftedObjectOriginalRenderQueues[material] = material.renderQueue;
					}

					material.renderQueue = Mathf.Max(
						liftedObjectOriginalRenderQueues[material],
						LiftedObjectRenderQueue);
				}
				else if (liftedObjectOriginalRenderQueues.TryGetValue(material, out int originalRenderQueue))
				{
					material.renderQueue = originalRenderQueue;
					liftedObjectOriginalRenderQueues.Remove(material);
				}
			}
		}
	}

	private MeshRenderer EnsurePrimitiveVisual3D(Transform parent, string name, PrimitiveType primitiveType, Color color, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
	{
		Transform child = parent.Find(name);
		if (child == null)
		{
			return CreatePrimitiveVisual3D(parent, name, primitiveType, color, localPosition, localScale, localRotation);
		}

		child.localPosition = localPosition;
		child.localRotation = localRotation;
		child.localScale = localScale;

		MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
		if (meshRenderer == null)
		{
			meshRenderer = child.gameObject.AddComponent<MeshRenderer>();
		}

		SetPrimitiveVisualColor(meshRenderer, color);
		ConfigureMeshRendererFor3D(meshRenderer, true, true);
		return meshRenderer;
	}

	private void ConfigureMeshRendererFor3D(MeshRenderer meshRenderer, bool castShadows, bool receiveShadows)
	{
		if (meshRenderer == null)
		{
			return;
		}

		meshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
		meshRenderer.receiveShadows = receiveShadows;
	}

	private Material CreatePrimitiveVisualMaterial(Color color)
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}

		if (shader == null)
		{
			shader = Shader.Find("Sprites/Default");
		}

		Material material = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/Internal-Colored"));
		SetMaterialColor(material, color);
		return material;
	}

	private void SetPrimitiveVisualColor(MeshRenderer meshRenderer, Color color)
	{
		if (meshRenderer == null)
		{
			return;
		}

		if (meshRenderer.sharedMaterial == null)
		{
			meshRenderer.material = CreatePrimitiveVisualMaterial(color);
			return;
		}

		Material material = meshRenderer.material;
		SetMaterialColor(material, color);
		meshRenderer.material = material;
	}

	private void SetMaterialColor(Material material, Color color)
	{
		if (material == null)
		{
			return;
		}

		Color opaqueColor = new Color(color.r, color.g, color.b, 1f);
		material.color = opaqueColor;
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", opaqueColor);
		}

		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", opaqueColor);
		}
	}

	private void MakeSpriteRendererInvisible(SpriteRenderer renderer)
	{
		if (renderer == null)
		{
			return;
		}

		Color color = renderer.color;
		color.a = 0f;
		renderer.color = color;
		renderer.forceRenderingOff = true;
	}

	private void SetNodeVisualColor(NodeRuntime node, Color color)
	{
		if (node == null)
		{
			return;
		}

		if (node.renderer != null)
		{
			node.renderer.color = new Color(color.r, color.g, color.b, 0f);
		}

		SetPrimitiveVisualColor(node.visual3DRenderer, color);
	}

	private void EnsureAvatarVisuals()
	{
		if (petriNetRoot != null)
		{
			Transform existingArrow = petriNetRoot.Find("LocalAvatarArrow");
			if (existingArrow != null)
			{
				Destroy(existingArrow.gameObject);
			}

			Transform existingTransitionShadow = petriNetRoot.Find("LocalHeldTransitionShadow");
			if (existingTransitionShadow != null)
			{
				Destroy(existingTransitionShadow.gameObject);
			}
		}

		if (localAvatarArrow != null)
		{
			Destroy(localAvatarArrow);
			localAvatarArrow = null;
		}

		DestroyLocalFakeShadowVisuals();

		if (localAvatarCable == null)
		{
			localAvatarCable = new GameObject("LocalAvatarCable");
			localAvatarCable.transform.SetParent(petriNetRoot);
		}
		DisableLegacyCraneCableLine(localAvatarCable.transform);
		EnsureCraneAttachmentParts(localAvatarCable.transform);

		if (localAvatarVisual == null)
		{
			localAvatarVisual = new GameObject("LocalAvatar");
			localAvatarVisual.transform.SetParent(petriNetRoot);
			localAvatarVisual.transform.localScale = Vector3.one;
			MeshRenderer bodyRenderer = CreatePrimitiveVisual3D(
				localAvatarVisual.transform,
				"BodySphere3D",
				PrimitiveType.Sphere,
				GetAvatarColor(GetLocalActorClientId()),
				Vector3.zero,
				new Vector3(1f, 1f, 1f),
				Quaternion.identity);
			if (bodyRenderer != null)
			{
					bodyRenderer.sortingOrder = 70;
			}

			EnsureAvatarDroneVisual(localAvatarVisual.transform, GetLocalActorClientId(), true);

			// Trigger only: the crane flies over graph nodes and uses input logic for interactions.
			CircleCollider2D collider = localAvatarVisual.AddComponent<CircleCollider2D>();
			collider.radius = 0.4f;
			collider.isTrigger = true;
		}
	}

	private void DestroyLocalFakeShadowVisuals()
	{
		if (localAvatarShadow != null)
		{
			Destroy(localAvatarShadow);
			localAvatarShadow = null;
		}

		if (localHeldNodeShadow != null)
		{
			Destroy(localHeldNodeShadow);
			localHeldNodeShadow = null;
		}
	}

	private void UpdateAvatarVisuals()
	{
		EnsureAvatarVisuals();
		UpdateCraneHeightAnimation();
		UpdateTimedPlaceProcessingVisuals();

		Vector3 craneVisualPosition = GetCraneVisualPosition();
		bool isHoldingTransition = !string.IsNullOrEmpty(heldTransitionId) && nodesById.ContainsKey(heldTransitionId);
		bool isHoldingPlace = !string.IsNullOrEmpty(heldPlaceId) && nodesById.ContainsKey(heldPlaceId);
		bool isHoldingCompositeBlock = !string.IsNullOrEmpty(heldCompositeBlockId) && compositeBlocksById.ContainsKey(heldCompositeBlockId);
		bool isHoldingNode = isHoldingTransition || isHoldingPlace || isHoldingCompositeBlock;

		UpdateHeldTransitionVisual();
		UpdateHeldPlaceVisual();
		UpdateHeldCompositeBlockVisual();
		UpdateCraneHoverSelectionVisual(isHoldingNode);

		DestroyLocalFakeShadowVisuals();

		// Update local avatar dot
		if (localAvatarVisual != null)
		{
			localAvatarVisual.transform.position = craneVisualPosition;
			localAvatarVisual.transform.localScale = Vector3.one;
			EnsureAvatarDroneVisual(localAvatarVisual.transform, GetLocalActorClientId(), true);
		}

		if (localAvatarCable != null)
		{
			float hookTargetZ = GetLocalCraneHookTargetZ(isHoldingNode);
			UpdateCraneAttachmentVisual(localAvatarCable.transform, craneVisualPosition, hookTargetZ);
		}

		UpdateCraneConnectPreviewVisual();
		UpdateRemoteAvatarVisuals();
		UpdateRemoteCraneHoverSelectionVisuals();
		UpdateRemoteCraneConnectPreviewVisuals();
	}

	private void UpdateRemoteAvatarVisuals()
	{
		List<ulong> staleClientIds = new List<ulong>();
		foreach (KeyValuePair<ulong, GameObject> pair in remoteAvatarVisuals)
		{
			if (!remoteAvatarPositions.ContainsKey(pair.Key))
			{
				staleClientIds.Add(pair.Key);
			}
		}

		for (int i = 0; i < staleClientIds.Count; i++)
		{
			ulong clientId = staleClientIds[i];
			if (remoteAvatarVisuals.TryGetValue(clientId, out GameObject visual) && visual != null)
			{
				Destroy(visual);
			}

			remoteAvatarVisuals.Remove(clientId);
		}

		foreach (KeyValuePair<ulong, Vector3> pair in remoteAvatarPositions)
		{
			ulong clientId = pair.Key;
			if (clientId == GetLocalActorClientId())
			{
				continue;
			}

			if (!remoteAvatarVisuals.TryGetValue(clientId, out GameObject visual) || visual == null)
			{
				visual = new GameObject("RemoteAvatar_" + clientId);
				visual.transform.SetParent(petriNetRoot, false);
				visual.transform.position = pair.Value;
				remoteAvatarVisuals[clientId] = visual;
			}

			SpriteRenderer rootRenderer = visual.GetComponent<SpriteRenderer>();
			if (rootRenderer != null)
			{
				Destroy(rootRenderer);
			}

			Vector3 groundPosition = pair.Value;
			visual.transform.position = Vector3.Lerp(visual.transform.position, groundPosition, Time.deltaTime * 8f);
			visual.transform.localScale = Vector3.one;

			UpdateRemoteAvatarPartVisuals(visual, clientId);
		}
	}

	private void UpdateRemoteAvatarPartVisuals(GameObject root, ulong clientId)
	{
		if (root == null)
		{
			return;
		}

		Transform fakeShadow = root.transform.Find("Shadow");
		if (fakeShadow != null)
		{
			Destroy(fakeShadow.gameObject);
		}

		Vector3 ground = root.transform.position;
		float remoteCraneHeight = GetRemoteAvatarCraneHeight(clientId);
		Vector3 crane = ground + new Vector3(0f, 0f, -remoteCraneHeight);
		Transform cableRoot = EnsureRemoteAvatarCable(root.transform);
		UpdateCraneAttachmentVisual(cableRoot, crane, GetRemoteCraneHookTargetZ(clientId));

		Transform oldFlatBody = root.transform.Find("Body");
		if (oldFlatBody != null)
		{
			oldFlatBody.gameObject.SetActive(false);
		}

		MeshRenderer body = EnsurePrimitiveVisual3D(
			root.transform,
			"BodySphere3D",
			PrimitiveType.Sphere,
			GetAvatarColor(clientId),
			new Vector3(0f, 0f, -remoteCraneHeight),
			new Vector3(0.86f, 0.86f, 0.86f),
			Quaternion.identity);
		if (body != null)
		{
				body.sortingOrder = 70;
		}

		Transform drone = EnsureAvatarDroneVisual(root.transform, clientId, false);
		if (drone != null)
		{
			drone.localPosition = new Vector3(avatarDroneLocalPosition.x, avatarDroneLocalPosition.y, -remoteCraneHeight + avatarDroneLocalPosition.z);
		}

		UpdateRemoteHeldObjectVisual(clientId, ground);
	}

	private void UpdateRemoteHeldObjectVisual(ulong clientId, Vector3 remoteGroundPosition)
	{
		if (!remoteAvatarInventories.TryGetValue(clientId, out RemoteHeldObjectState heldState)
			|| heldState == null
			|| heldState.kind == HeldObjectKind.None
			|| string.IsNullOrEmpty(heldState.id))
		{
			return;
		}

		Vector2 heldCenter = new Vector2(remoteGroundPosition.x, remoteGroundPosition.y);
		if (heldState.kind == HeldObjectKind.CompositeBlock)
		{
			heldCenter += heldState.offset;
			if (MoveCompositeBlockInternal(heldState.id, heldCenter, false))
			{
				SetCompositeBlockNodeHeight(heldState.id, GetRemoteHeldObjectZ(clientId));
				UpdateAllArcVisuals();
				SetCompositeBlockSorting(heldState.id, true);
			}

			return;
		}

		if (!nodesById.TryGetValue(heldState.id, out NodeRuntime heldNode) || heldNode == null || heldNode.transform == null)
		{
			return;
		}

		if (heldState.kind == HeldObjectKind.Transition && IsDeliveryTransition(heldNode))
		{
			heldCenter = ClampDeliveryTransitionPositionToOwnSide(heldNode, heldCenter);
		}

		heldNode.transform.position = new Vector3(heldCenter.x, heldCenter.y, GetRemoteHeldObjectZ(clientId));
		if (heldState.kind == HeldObjectKind.Place)
		{
			SetPlaceSorting(heldNode, true);
		}
		else if (heldState.kind == HeldObjectKind.Transition)
		{
			SetTransitionSorting(heldNode, true);
		}

		UpdateAllArcVisuals();
	}

	private float GetRemoteHeldObjectZ(ulong clientId)
	{
		return GetHeldObjectZForCraneHeight(GetRemoteAvatarCraneHeight(clientId));
	}

	private float GetRemoteAvatarCraneHeight(ulong clientId)
	{
		if (remoteAvatarCraneHeights.TryGetValue(clientId, out float craneHeight))
		{
			return Mathf.Clamp(craneHeight, GroundZ, avatarCraneRestHeight);
		}

		return avatarCraneRestHeight;
	}

	private void UpdateRemoteCraneHoverSelectionVisuals()
	{
		if (petriNetRoot == null)
		{
			return;
		}

		List<ulong> staleClientIds = new List<ulong>();
		foreach (KeyValuePair<ulong, CraneHoverSelectionVisual> pair in remoteCraneHoverVisuals)
		{
			if (!remoteAvatarPositions.ContainsKey(pair.Key))
			{
				staleClientIds.Add(pair.Key);
			}
		}

		for (int i = 0; i < staleClientIds.Count; i++)
		{
			DestroyRemoteCraneHoverVisual(staleClientIds[i]);
		}

		foreach (KeyValuePair<ulong, Vector3> pair in remoteAvatarPositions)
		{
			ulong clientId = pair.Key;
			if (clientId == GetLocalActorClientId())
			{
				continue;
			}

			CraneHoverSelectionVisual visual = EnsureRemoteCraneHoverVisual(clientId);
			if (visual == null)
			{
				continue;
			}

			if (IsRemoteAvatarHoldingObject(clientId))
			{
				HideRemoteCraneHoverSelectionVisual(visual);
				continue;
			}

			Vector2 craneTarget = new Vector2(pair.Value.x, pair.Value.y);
			if (TryGetCompositeBlockAtPoint(craneTarget, clientId, out CompositeBlockRuntime block))
			{
				ShowRemoteCraneHoverCompositeBlockVisual(visual, block.id);
				HideRemoteCraneHoverArcVisual(visual);
				continue;
			}

			if (TryGetHoverSelectableNodeAtPoint(craneTarget, clientId, out NodeRuntime node))
			{
				ShowRemoteCraneHoverNodeVisual(visual, node);
				HideRemoteCraneHoverArcVisual(visual);
				continue;
			}

			HideRemoteCraneHoverNodeVisual(visual);
			if (TryGetArcAtPoint(craneTarget, clientId, out ArcRuntime arc))
			{
				ShowRemoteCraneHoverArcVisual(visual, arc, craneTarget);
				continue;
			}

			HideRemoteCraneHoverArcVisual(visual);
		}
	}

	private bool IsRemoteAvatarHoldingObject(ulong clientId)
	{
		if (!remoteAvatarInventories.TryGetValue(clientId, out RemoteHeldObjectState heldState) || heldState == null)
		{
			return false;
		}

		return heldState.kind != HeldObjectKind.None && !string.IsNullOrEmpty(heldState.id);
	}

	private CraneHoverSelectionVisual EnsureRemoteCraneHoverVisual(ulong clientId)
	{
		if (remoteCraneHoverVisuals.TryGetValue(clientId, out CraneHoverSelectionVisual visual)
			&& visual != null
			&& visual.root != null)
		{
			return visual;
		}

		visual = new CraneHoverSelectionVisual();
		visual.root = new GameObject("RemoteCraneHoverSelection_" + clientId);
		visual.root.transform.SetParent(petriNetRoot, false);

		visual.nodeRoot = new GameObject("NodeSelection");
		visual.nodeRoot.transform.SetParent(visual.root.transform, false);
		visual.nodeOutline = visual.nodeRoot.AddComponent<LineRenderer>();
		ConfigureCraneHoverNodeLine(visual.nodeOutline, 4, true);
		visual.nodeRoot.SetActive(false);

		visual.arcRoot = new GameObject("ArcHighlight");
		visual.arcRoot.transform.SetParent(visual.root.transform, false);
		visual.arcBody = visual.arcRoot.AddComponent<LineRenderer>();
		ConfigureCraneHoverArcLine(visual.arcBody, 2);

		GameObject arrowObject = new GameObject("Arrow");
		arrowObject.transform.SetParent(visual.arcRoot.transform, false);
		visual.arcArrow = arrowObject.AddComponent<LineRenderer>();
		ConfigureCraneHoverArcLine(visual.arcArrow, 3);
		visual.arcRoot.SetActive(false);

		remoteCraneHoverVisuals[clientId] = visual;
		return visual;
	}

	private void ShowRemoteCraneHoverNodeVisual(CraneHoverSelectionVisual visual, NodeRuntime node)
	{
		if (visual == null || visual.nodeRoot == null || node == null || node.transform == null)
		{
			HideRemoteCraneHoverNodeVisual(visual);
			return;
		}

		visual.nodeRoot.SetActive(true);
		Vector3 nodePosition = node.transform.position;
		if (node.type == NodeType.Place)
		{
			SetCraneHoverCircleOutline(visual.nodeOutline, nodePosition, NodeVisualFootprint * 0.58f);
		}
		else
		{
			SetCraneHoverRectOutline(visual.nodeOutline, ExpandRect(GetTransitionPlacementBounds(node, nodePosition), 0.08f));
		}
	}

	private void ShowRemoteCraneHoverCompositeBlockVisual(CraneHoverSelectionVisual visual, string blockId)
	{
		if (visual == null || visual.nodeRoot == null || !TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			HideRemoteCraneHoverNodeVisual(visual);
			return;
		}

		visual.nodeRoot.SetActive(true);
		SetCraneHoverRectOutline(visual.nodeOutline, new Rect(
			bounds.xMin - 0.08f,
			bounds.yMin - 0.08f,
			bounds.width + 0.16f,
			bounds.height + 0.16f));
	}

	private void ShowRemoteCraneHoverArcVisual(CraneHoverSelectionVisual visual, ArcRuntime arc, Vector2 craneTarget)
	{
		if (visual == null || visual.arcRoot == null || !TryGetArcHoverSegment(arc, craneTarget, out Vector3 start, out Vector3 end, out bool showArrowHead))
		{
			HideRemoteCraneHoverArcVisual(visual);
			return;
		}

		Vector3 dir = end - start;
		if (dir.sqrMagnitude < 0.0001f)
		{
			HideRemoteCraneHoverArcVisual(visual);
			return;
		}

		dir.Normalize();
		visual.arcRoot.SetActive(true);
		if (showArrowHead)
		{
			SetLineWithArrow(visual.arcBody, visual.arcArrow, start, end, dir);
		}
		else
		{
			Vector3 zOffset = new Vector3(0f, 0f, ArcZ);
			visual.arcBody.SetPosition(0, start + zOffset);
			visual.arcBody.SetPosition(1, end + zOffset);
			visual.arcArrow.SetPosition(0, end + zOffset);
			visual.arcArrow.SetPosition(1, end + zOffset);
			visual.arcArrow.SetPosition(2, end + zOffset);
		}
	}

	private void HideRemoteCraneHoverSelectionVisual(CraneHoverSelectionVisual visual)
	{
		HideRemoteCraneHoverNodeVisual(visual);
		HideRemoteCraneHoverArcVisual(visual);
	}

	private void HideRemoteCraneHoverNodeVisual(CraneHoverSelectionVisual visual)
	{
		if (visual != null && visual.nodeRoot != null)
		{
			visual.nodeRoot.SetActive(false);
		}
	}

	private void HideRemoteCraneHoverArcVisual(CraneHoverSelectionVisual visual)
	{
		if (visual != null && visual.arcRoot != null)
		{
			visual.arcRoot.SetActive(false);
		}
	}

	private void DestroyRemoteCraneHoverVisual(ulong clientId)
	{
		if (!remoteCraneHoverVisuals.TryGetValue(clientId, out CraneHoverSelectionVisual visual))
		{
			return;
		}

		if (visual != null && visual.root != null)
		{
			Destroy(visual.root);
		}

		remoteCraneHoverVisuals.Remove(clientId);
	}

	private void DestroyRemoteCraneHoverVisuals()
	{
		List<ulong> clientIds = new List<ulong>(remoteCraneHoverVisuals.Keys);
		for (int i = 0; i < clientIds.Count; i++)
		{
			DestroyRemoteCraneHoverVisual(clientIds[i]);
		}
	}

	private Transform EnsureAvatarDroneVisual(Transform root, ulong clientId, bool localAvatar)
	{
		GameObject dronePrefab = GetAvatarDronePrefab();
		if (root == null || dronePrefab == null)
		{
			return null;
		}

		Transform sphere = root.Find("BodySphere3D");
		if (sphere != null)
		{
			sphere.gameObject.SetActive(false);
		}

		Transform drone = root.Find(AvatarDroneVisualName);
		if (drone == null)
		{
			GameObject droneObject = Instantiate(dronePrefab, root);
			droneObject.name = AvatarDroneVisualName;
			drone = droneObject.transform;
			DestroyRuntimeColliders(droneObject);
			ApplyAvatarDroneTint(droneObject, clientId);
			SetAvatarDroneAnimation(droneObject, true);
		}

		drone.gameObject.SetActive(true);
		if (localAvatar)
		{
			drone.localPosition = avatarDroneLocalPosition;
		}

		drone.localRotation = GetAvatarDroneRotation();
		drone.localScale = avatarDroneLocalScale;
		SetMeshRenderersSortingOrder(drone, 70);
		return drone;
	}

	private Quaternion GetAvatarDroneRotation()
	{
		// The imported drone lies in XZ, so yaw must happen before tilting its top toward the XY game board camera.
		Quaternion inDronePlaneRotation = Quaternion.AngleAxis(avatarDroneLocalEuler.y, Vector3.up);
		Quaternion tiltTopTowardCamera = Quaternion.AngleAxis(avatarDroneLocalEuler.x, Vector3.right);
		Quaternion screenRoll = Quaternion.AngleAxis(avatarDroneLocalEuler.z, Vector3.forward);
		return screenRoll * tiltTopTowardCamera * inDronePlaneRotation;
	}

	private GameObject GetAvatarDronePrefab()
	{
#if UNITY_EDITOR
		GameObject realisticDronePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(RealisticDronePrefabPath);
		if (realisticDronePrefab != null)
		{
			avatarDronePrefab = realisticDronePrefab;
			return realisticDronePrefab;
		}
#endif

		return avatarDronePrefab;
	}

	private void SetAvatarDroneAnimation(GameObject droneObject, bool playing)
	{
		if (droneObject == null)
		{
			return;
		}

		bool useImportedAvatarAnimation = avatarDroneUseImportedAnimationClips && avatarDroneAnimatorController != null;
		Animator[] animators = droneObject.GetComponentsInChildren<Animator>(true);
		if (animators.Length <= 0 && useImportedAvatarAnimation)
		{
			animators = new[] { droneObject.AddComponent<Animator>() };
		}

		for (int i = 0; i < animators.Length; i++)
		{
			if (useImportedAvatarAnimation)
			{
				animators[i].runtimeAnimatorController = avatarDroneAnimatorController;
			}

			animators[i].enabled = useImportedAvatarAnimation && playing;
			animators[i].applyRootMotion = false;
		}

		Animation[] animations = droneObject.GetComponentsInChildren<Animation>(true);
		for (int i = 0; i < animations.Length; i++)
		{
			animations[i].Stop();
			animations[i].enabled = false;
		}

		PetriNetAvatarDroneAnimator droneAnimator = droneObject.GetComponent<PetriNetAvatarDroneAnimator>();
		if (droneAnimator == null)
		{
			droneAnimator = droneObject.AddComponent<PetriNetAvatarDroneAnimator>();
		}

		droneAnimator.Configure(
			avatarDroneAnimationClips,
			avatarDroneUseImportedAnimationClips,
			avatarDroneAnimationClipNameContains,
			avatarDroneAnimationClipNameExcludes,
			avatarDroneRotorDegreesPerSecond,
			avatarDroneRotorLocalAxis,
			playing);
	}

	private void ApplyAvatarDroneTint(GameObject droneObject, ulong clientId)
	{
		if (droneObject == null)
		{
			return;
		}

		CloneAvatarDroneMaterials(droneObject);
	}

	private void CloneAvatarDroneMaterials(GameObject droneObject)
	{
		if (droneObject == null)
		{
			return;
		}

		MeshRenderer[] renderers = droneObject.GetComponentsInChildren<MeshRenderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			MeshRenderer renderer = renderers[i];
			if (renderer == null)
			{
				continue;
			}

			Material[] sourceMaterials = renderer.sharedMaterials;
			Material[] materials = new Material[sourceMaterials.Length];
			for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
			{
				Material sourceMaterial = sourceMaterials[materialIndex];
				Material material = sourceMaterial != null ? new Material(sourceMaterial) : null;
				if (material != null)
				{
					// TextMesh labels render in the transparent queue. Put the drone there as well
					// so its higher sorting order can correctly draw it in front of labels.
					material.renderQueue = (int)RenderQueue.Transparent;
				}

				materials[materialIndex] = material;
			}

			renderer.sharedMaterials = materials;
		}
	}

	private SpriteRenderer EnsureRemoteAvatarSprite(Transform root, string name, Sprite sprite, Color color, int sortingOrder)
	{
		Transform child = root.Find(name);
		if (child == null)
		{
			child = new GameObject(name).transform;
			child.SetParent(root, false);
		}

		SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
		if (renderer == null)
		{
			renderer = child.gameObject.AddComponent<SpriteRenderer>();
		}

		renderer.sprite = sprite;
		renderer.color = color;
		renderer.sortingOrder = sortingOrder;
		return renderer;
	}

	private Transform EnsureRemoteAvatarCable(Transform root)
	{
		Transform child = root.Find("Cable");
		if (child == null)
		{
			child = new GameObject("Cable").transform;
			child.SetParent(root, false);
		}

		DisableLegacyCraneCableLine(child);
		EnsureCraneAttachmentParts(child);
		return child;
	}

	private void DisableLegacyCraneCableLine(Transform cableRoot)
	{
		if (cableRoot == null)
		{
			return;
		}

		LineRenderer line = cableRoot.GetComponent<LineRenderer>();
		if (line != null)
		{
			line.enabled = false;
		}
	}

	private void EnsureCraneAttachmentParts(Transform cableRoot)
	{
		if (cableRoot == null)
		{
			return;
		}

		EnsureCraneChainLinkRoot(cableRoot);
		EnsureCraneAttachmentPart(cableRoot, "ChainHook", GetAvatarCraneHookPrefab(), PrimitiveType.Capsule, new Color(0.16f, 0.16f, 0.17f));
	}

	private Transform EnsureCraneChainLinkRoot(Transform cableRoot)
	{
		if (cableRoot == null)
		{
			return null;
		}

		Transform oldSingleChain = cableRoot.Find("Chain");
		if (oldSingleChain != null)
		{
			oldSingleChain.gameObject.SetActive(false);
		}

		Transform chainRoot = cableRoot.Find("ChainLinks");
		if (chainRoot == null)
		{
			chainRoot = new GameObject("ChainLinks").transform;
			chainRoot.SetParent(cableRoot, false);
		}

		return chainRoot;
	}

	private Transform EnsureCraneAttachmentPart(Transform parent, string name, GameObject prefab, PrimitiveType fallbackPrimitive, Color fallbackColor)
	{
		Transform child = parent.Find(name);
		if (child == null)
		{
			GameObject partObject;
			if (prefab != null)
			{
				partObject = Instantiate(prefab, parent);
			}
			else
			{
				partObject = GameObject.CreatePrimitive(fallbackPrimitive);
				partObject.transform.SetParent(parent, false);
				MeshRenderer renderer = partObject.GetComponent<MeshRenderer>();
				if (renderer != null)
				{
					renderer.material = CreatePrimitiveVisualMaterial(fallbackColor);
				}
			}

			partObject.name = name;
			child = partObject.transform;
			DestroyRuntimeColliders(partObject);
		}

		child.gameObject.SetActive(true);
			SetMeshRenderersSortingOrder(child, 72);
		return child;
	}

	private void UpdateCraneAttachmentVisual(Transform cableRoot, Vector3 craneVisualPosition, float hookTargetZ)
	{
		if (cableRoot == null)
		{
			return;
		}

		DisableLegacyCraneCableLine(cableRoot);
		EnsureCraneAttachmentParts(cableRoot);

		float freeHookZ = GetHookZForCraneHeight(-craneVisualPosition.z);
		float contactHookZ = GetHookContactZ(hookTargetZ);
		float hookZ = Mathf.Clamp(Mathf.Min(freeHookZ, contactHookZ), craneVisualPosition.z, GroundZ);
		float chainLength = Mathf.Max(0.01f, hookZ - craneVisualPosition.z);
		Vector3 hookPosition = new Vector3(craneVisualPosition.x, craneVisualPosition.y, hookZ);
		UpdateCraneChainLinks(cableRoot, craneVisualPosition, hookZ, chainLength);

		Transform hook = cableRoot.Find("ChainHook");
		if (hook != null)
		{
			UpdateCraneHookVisual(hook, hookPosition);
		}
	}

	private void UpdateCraneHookVisual(Transform hook, Vector3 chainEndPosition)
	{
		if (hook == null)
		{
			return;
		}

		hook.gameObject.SetActive(true);
		hook.rotation = Quaternion.Euler(avatarCraneHookLocalEuler);
		hook.localScale = avatarCraneHookLocalScale;
		hook.position = GetCraneHookVisualPosition(chainEndPosition);
			SetMeshRenderersSortingOrder(hook, 73);

		if (!TryGetRendererBounds(hook, out Bounds bounds))
		{
			return;
		}

		Vector3 visibleAttachPoint = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
		Vector3 desiredAttachPoint = chainEndPosition + new Vector3(0f, 0f, GetCraneHookVisualDrop());
		hook.position += desiredAttachPoint - visibleAttachPoint;
	}

	private void UpdateCraneChainLinks(Transform cableRoot, Vector3 craneVisualPosition, float hookZ, float chainLength)
	{
		Transform chainRoot = EnsureCraneChainLinkRoot(cableRoot);
		if (chainRoot == null)
		{
			return;
		}

		bool showChain = chainLength > 0.035f;
		chainRoot.gameObject.SetActive(showChain);
		if (!showChain)
		{
			SetCraneChainLinkCount(chainRoot, 0, craneVisualPosition, hookZ);
			return;
		}

		float linkSpacing = Mathf.Max(0.035f, avatarCraneChainLinkSpacing * Mathf.Max(0.01f, avatarCraneChainLengthMultiplier));
		int maxLinks = Mathf.Max(1, avatarCraneChainMaxLinks);
		int linkCount = Mathf.Clamp(Mathf.CeilToInt(chainLength / linkSpacing), 1, maxLinks);
		SetCraneChainLinkCount(chainRoot, linkCount, craneVisualPosition, hookZ);
	}

	private void SetCraneChainLinkCount(Transform chainRoot, int linkCount, Vector3 craneVisualPosition, float hookZ)
	{
		if (chainRoot == null)
		{
			return;
		}

		for (int i = 0; i < linkCount; i++)
		{
			Transform link = EnsureCraneChainLink(chainRoot, i);
			if (link == null)
			{
				continue;
			}

				float t = linkCount == 1
					? 1f
					: i / (float)(linkCount - 1);
			Vector3 linkEuler = avatarCraneChainLocalEuler;
			linkEuler.z += (i % 2) * 90f;
			link.gameObject.SetActive(true);
			link.position = new Vector3(craneVisualPosition.x, craneVisualPosition.y, Mathf.Lerp(craneVisualPosition.z, hookZ, t));
			link.rotation = Quaternion.Euler(linkEuler);
			link.localScale = avatarCraneChainLocalScale;
				SetMeshRenderersSortingOrder(link, 72);
		}

		for (int i = linkCount; i < chainRoot.childCount; i++)
		{
			chainRoot.GetChild(i).gameObject.SetActive(false);
		}
	}

	private Transform EnsureCraneChainLink(Transform chainRoot, int index)
	{
		string linkName = "ChainLink_" + index.ToString("00");
		return EnsureCraneAttachmentPart(chainRoot, linkName, GetAvatarCraneChainPrefab(), PrimitiveType.Cylinder, new Color(0.18f, 0.16f, 0.14f));
	}

	private GameObject GetAvatarCraneChainPrefab()
	{
#if UNITY_EDITOR
		if (avatarCraneChainPrefab == null)
		{
			avatarCraneChainPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HarborChainPrefabPath);
		}
#endif
		return avatarCraneChainPrefab;
	}

	private GameObject GetAvatarCraneHookPrefab()
	{
#if UNITY_EDITOR
		if (avatarCraneHookPrefab == null)
		{
			avatarCraneHookPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HarborChainHookPrefabPath);
		}
#endif
		return avatarCraneHookPrefab;
	}

	private float GetLocalCraneHookTargetZ(bool isHoldingNode)
	{
		if (isHoldingNode)
		{
			return GetHeldObjectTopZ();
		}

		if (TryGetCompositeBlockAtCraneTarget(out CompositeBlockRuntime _) || IsCraneOverAnyNode())
		{
			return NodeVisualTopZ;
		}

		return GroundZ;
	}

	private float GetRemoteCraneHookTargetZ(ulong clientId)
	{
		if (remoteAvatarInventories.TryGetValue(clientId, out RemoteHeldObjectState heldState)
			&& heldState != null
			&& heldState.kind != HeldObjectKind.None
			&& !string.IsNullOrEmpty(heldState.id))
		{
			return GetRemoteHeldObjectZ(clientId) + NodeVisualTopZ;
		}

		return GroundZ;
	}

	private float GetCraneHookHangDistance()
	{
		return Mathf.Max(0.05f, avatarCraneHookHangDistance);
	}

	private float GetCraneHookVisualDrop()
	{
		return Mathf.Max(0f, avatarCraneHookVisualDrop);
	}

	private Vector3 GetCraneHookVisualPosition(Vector3 chainEndPosition)
	{
		float visualZ = Mathf.Clamp(chainEndPosition.z + GetCraneHookVisualDrop(), chainEndPosition.z, GroundZ);
		return new Vector3(chainEndPosition.x, chainEndPosition.y, visualZ);
	}

	private bool TryGetRendererBounds(Transform root, out Bounds bounds)
	{
		bounds = new Bounds();
		if (root == null)
		{
			return false;
		}

		Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
		bool hasBounds = false;
		for (int i = 0; i < renderers.Length; i++)
		{
			Renderer renderer = renderers[i];
			if (renderer == null || !renderer.enabled)
			{
				continue;
			}

			if (!hasBounds)
			{
				bounds = renderer.bounds;
				hasBounds = true;
			}
			else
			{
				bounds.Encapsulate(renderer.bounds);
			}
		}

		return hasBounds;
	}

	private float GetHookContactZ(float hookTargetZ)
	{
		return Mathf.Clamp(hookTargetZ - Mathf.Max(0f, avatarCraneHookClearance), -avatarCraneRestHeight, GroundZ);
	}

	private float GetHookZForCraneHeight(float craneHeight)
	{
		float craneZ = -Mathf.Max(0f, craneHeight);
		return Mathf.Clamp(craneZ + GetCraneHookHangDistance(), craneZ, GroundZ);
	}

	private float GetCraneHeightForHookTarget(float hookTargetZ)
	{
		float targetHookZ = GetHookContactZ(hookTargetZ);
		return Mathf.Clamp(GetCraneHookHangDistance() - targetHookZ, GroundZ, avatarCraneRestHeight);
	}

	private void StartCraneDipAnimation()
	{
		StartCraneDipAnimation(GetCraneHeightForHookTarget(NodeVisualTopZ));
	}

	private void StartCraneDipAnimation(float targetHeight)
	{
		avatarCraneDipTargetHeight = Mathf.Clamp(targetHeight, GroundZ, avatarCraneRestHeight);
		avatarCraneAnimationStartTime = Time.unscaledTime;
	}

	private void UpdateCraneHeightAnimation()
	{
		float elapsed = Time.unscaledTime - avatarCraneAnimationStartTime;
		if (elapsed < 0f || elapsed > avatarCraneAnimationDuration)
		{
			avatarCraneCurrentHeight = avatarCraneRestHeight;
			return;
		}

		float phase = Mathf.Clamp01(elapsed / avatarCraneAnimationDuration);
		float lowerAmount = Mathf.Sin(phase * Mathf.PI);
		avatarCraneCurrentHeight = Mathf.Lerp(avatarCraneRestHeight, avatarCraneDipTargetHeight, lowerAmount);
	}

	private Vector3 GetCraneVisualPosition()
	{
		return new Vector3(avatarPosition.x, avatarPosition.y, -avatarCraneCurrentHeight);
	}

	private void UpdateHeldTransitionVisual()
	{
		if (string.IsNullOrEmpty(heldTransitionId) || !nodesById.TryGetValue(heldTransitionId, out NodeRuntime heldNode))
		{
			return;
		}

		heldNode.transform.position = GetHeldTransitionVisualPosition();
		SetTransitionSorting(heldNode, true);
		UpdateAllArcVisuals();
	}

	private Vector3 GetHeldTransitionVisualPosition()
	{
		Vector3 position = GetHeldNodeVisualPosition();
		if (nodesById.TryGetValue(heldTransitionId, out NodeRuntime heldTransition)
			&& IsDeliveryTransition(heldTransition))
		{
			Vector2 clamped = ClampDeliveryTransitionPositionToOwnSide(
				heldTransition,
				new Vector2(position.x, position.y));
			position.x = clamped.x;
			position.y = clamped.y;
		}

		return position;
	}

	private void UpdateHeldPlaceVisual()
	{
		if (string.IsNullOrEmpty(heldPlaceId) || !nodesById.TryGetValue(heldPlaceId, out NodeRuntime heldNode))
		{
			return;
		}

		heldNode.transform.position = GetHeldNodeVisualPosition();
		SetPlaceSorting(heldNode, true);
		UpdateAllArcVisuals();
	}

	private void UpdateHeldCompositeBlockVisual()
	{
		if (string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return;
		}

		if (!compositeBlocksById.ContainsKey(heldCompositeBlockId))
		{
			heldCompositeBlockId = null;
			heldCompositeBlockOffset = Vector2.zero;
			return;
		}

		Vector2 heldCenter = GetHeldCompositeBlockVisualCenter();
		MoveCompositeBlockInternal(heldCompositeBlockId, heldCenter, false);
		SetCompositeBlockNodeHeight(heldCompositeBlockId, GetHeldObjectZ());
		UpdateAllArcVisuals();
		SetCompositeBlockSorting(heldCompositeBlockId, true);
	}

	private Vector2 GetHeldCompositeBlockGroundCenter()
	{
		return new Vector2(avatarPosition.x, avatarPosition.y) + heldCompositeBlockOffset;
	}

	private Vector2 GetHeldCompositeBlockVisualCenter()
	{
		return GetHeldCompositeBlockGroundCenter();
	}

	private void EnsureCraneConnectPreviewVisual()
	{
		if (localCraneConnectPreview != null)
		{
			return;
		}

		localCraneConnectPreview = new GameObject("LocalCraneConnectPreview");
		localCraneConnectPreview.transform.SetParent(petriNetRoot, false);

		localCraneConnectPreviewBody = localCraneConnectPreview.AddComponent<LineRenderer>();
		ConfigureCraneConnectPreviewLine(localCraneConnectPreviewBody, 56, 2);

		GameObject arrowObject = new GameObject("Arrow");
		arrowObject.transform.SetParent(localCraneConnectPreview.transform, false);
		localCraneConnectPreviewArrow = arrowObject.AddComponent<LineRenderer>();
		ConfigureCraneConnectPreviewLine(localCraneConnectPreviewArrow, 57, 3);

		localCraneConnectPreview.SetActive(false);
	}

	private void EnsureCraneHoverNodeVisual()
	{
		if (localCraneHoverNodeShadow != null && localCraneHoverNodeOutline != null)
		{
			return;
		}

		if (localCraneHoverNodeShadow == null)
		{
			localCraneHoverNodeShadow = new GameObject("LocalCraneHoverNodeSelection");
			localCraneHoverNodeShadow.transform.SetParent(petriNetRoot, false);
		}

		SpriteRenderer oldShadowRenderer = localCraneHoverNodeShadow.GetComponent<SpriteRenderer>();
		if (oldShadowRenderer != null)
		{
			oldShadowRenderer.enabled = false;
		}

		localCraneHoverNodeOutline = localCraneHoverNodeShadow.GetComponent<LineRenderer>();
		if (localCraneHoverNodeOutline == null)
		{
			localCraneHoverNodeOutline = localCraneHoverNodeShadow.AddComponent<LineRenderer>();
		}

		ConfigureCraneHoverNodeLine(localCraneHoverNodeOutline, 4, true);
		localCraneHoverNodeShadow.SetActive(false);
	}

	private void ConfigureCraneHoverNodeLine(LineRenderer line, int positionCount, bool loop)
	{
		ConfigureCraneHoverSelectionLine(line, positionCount, loop);
	}

	private void EnsureCraneHoverArcVisual()
	{
		if (localCraneHoverArcHighlight != null)
		{
			return;
		}

		localCraneHoverArcHighlight = new GameObject("LocalCraneHoverArcHighlight");
		localCraneHoverArcHighlight.transform.SetParent(petriNetRoot, false);

		localCraneHoverArcBody = localCraneHoverArcHighlight.AddComponent<LineRenderer>();
		ConfigureCraneHoverArcLine(localCraneHoverArcBody, 2);

		GameObject arrowObject = new GameObject("Arrow");
		arrowObject.transform.SetParent(localCraneHoverArcHighlight.transform, false);
		localCraneHoverArcArrow = arrowObject.AddComponent<LineRenderer>();
		ConfigureCraneHoverArcLine(localCraneHoverArcArrow, 3);

		localCraneHoverArcHighlight.SetActive(false);
	}

	private void ConfigureCraneHoverArcLine(LineRenderer line, int positionCount)
	{
		ConfigureCraneHoverSelectionLine(line, positionCount, false);
	}

	private void ConfigureCraneHoverSelectionLine(LineRenderer line, int positionCount, bool loop)
	{
		Color highlightColor = new Color(0.15f, 0.58f, 0.95f, 0.55f);
		ConfigureGroundLineRenderer(line, positionCount, arcWidth * 3.8f, 23, highlightColor, 8, 8, loop);
	}

	private void UpdateCraneHoverSelectionVisual(bool isHoldingNode)
	{
		if (isHoldingNode)
		{
			HideCraneHoverSelectionVisual();
			return;
		}

		if (TryGetCompositeBlockAtCraneTarget(out CompositeBlockRuntime block))
		{
			ShowCraneHoverCompositeBlockVisual(block.id);
			HideCraneHoverArcVisual();
			return;
		}

		if (TryGetHoverSelectableNodeAtCraneTarget(out NodeRuntime node))
		{
			ShowCraneHoverNodeVisual(node);
			HideCraneHoverArcVisual();
			return;
		}

		HideCraneHoverNodeVisual();
		if (TryGetArcAtCraneTarget(out ArcRuntime arc))
		{
			ShowCraneHoverArcVisual(arc);
			return;
		}

		HideCraneHoverArcVisual();
	}

	private void ShowCraneHoverNodeVisual(NodeRuntime node)
	{
		if (node == null || node.transform == null)
		{
			HideCraneHoverNodeVisual();
			return;
		}

		EnsureCraneHoverNodeVisual();
		localCraneHoverNodeShadow.SetActive(true);

		Vector3 nodePosition = node.transform.position;
		if (node.type == NodeType.Place)
		{
			SetCraneHoverCircleOutline(nodePosition, NodeVisualFootprint * 0.58f);
		}
		else
		{
			SetCraneHoverRectOutline(ExpandRect(GetTransitionPlacementBounds(node, nodePosition), 0.08f));
		}
	}

	private void ShowCraneHoverCompositeBlockVisual(string blockId)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			HideCraneHoverNodeVisual();
			return;
		}

		EnsureCraneHoverNodeVisual();
		localCraneHoverNodeShadow.SetActive(true);
		SetCraneHoverRectOutline(new Rect(
			bounds.xMin - 0.08f,
			bounds.yMin - 0.08f,
			bounds.width + 0.16f,
			bounds.height + 0.16f));
	}

	private void SetCraneHoverCircleOutline(Vector3 center, float radius)
	{
		SetCraneHoverCircleOutline(localCraneHoverNodeOutline, center, radius);
	}

	private void SetCraneHoverCircleOutline(LineRenderer outline, Vector3 center, float radius)
	{
		if (outline == null)
		{
			return;
		}

		const int pointCount = 48;
		ConfigureCraneHoverNodeLine(outline, pointCount, true);
		float z = ArcZ;
		for (int i = 0; i < pointCount; i++)
		{
			float angle = (i / (float)pointCount) * Mathf.PI * 2f;
			outline.SetPosition(i, new Vector3(
				center.x + Mathf.Cos(angle) * radius,
				center.y + Mathf.Sin(angle) * radius,
				z));
		}
	}

	private void SetCraneHoverRectOutline(Rect bounds)
	{
		SetCraneHoverRectOutline(localCraneHoverNodeOutline, bounds);
	}

	private void SetCraneHoverRectOutline(LineRenderer outline, Rect bounds)
	{
		if (outline == null)
		{
			return;
		}

		ConfigureCraneHoverNodeLine(outline, 4, true);
		float z = ArcZ;
		outline.SetPosition(0, new Vector3(bounds.xMin, bounds.yMax, z));
		outline.SetPosition(1, new Vector3(bounds.xMax, bounds.yMax, z));
		outline.SetPosition(2, new Vector3(bounds.xMax, bounds.yMin, z));
		outline.SetPosition(3, new Vector3(bounds.xMin, bounds.yMin, z));
	}

	private void ShowCraneHoverArcVisual(ArcRuntime arc)
	{
		if (!TryGetArcHoverSegment(arc, out Vector3 start, out Vector3 end, out bool showArrowHead))
		{
			HideCraneHoverArcVisual();
			return;
		}

		Vector3 dir = end - start;
		if (dir.sqrMagnitude < 0.0001f)
		{
			HideCraneHoverArcVisual();
			return;
		}

		dir.Normalize();
		EnsureCraneHoverArcVisual();
		localCraneHoverArcHighlight.SetActive(true);
		if (showArrowHead)
		{
			SetLineWithArrow(localCraneHoverArcBody, localCraneHoverArcArrow, start, end, dir);
		}
		else
		{
			Vector3 zOffset = new Vector3(0f, 0f, ArcZ);
			localCraneHoverArcBody.SetPosition(0, start + zOffset);
			localCraneHoverArcBody.SetPosition(1, end + zOffset);
			localCraneHoverArcArrow.SetPosition(0, end + zOffset);
			localCraneHoverArcArrow.SetPosition(1, end + zOffset);
			localCraneHoverArcArrow.SetPosition(2, end + zOffset);
		}
	}

	private bool TryGetArcHoverSegment(ArcRuntime arc, out Vector3 segmentStart, out Vector3 segmentEnd, out bool showArrowHead)
	{
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		return TryGetArcHoverSegment(arc, craneTarget, out segmentStart, out segmentEnd, out showArrowHead);
	}

	private bool TryGetArcHoverSegment(ArcRuntime arc, Vector2 craneTarget, out Vector3 segmentStart, out Vector3 segmentEnd, out bool showArrowHead)
	{
		segmentStart = Vector3.zero;
		segmentEnd = Vector3.zero;
		showArrowHead = false;
		if (!TryGetArcSegment(arc, out Vector3 start, out Vector3 end))
		{
			return false;
		}

		Vector3 middle = (start + end) * 0.5f;
		float distanceToStart = Vector2.Distance(craneTarget, new Vector2(start.x, start.y));
		float distanceToEnd = Vector2.Distance(craneTarget, new Vector2(end.x, end.y));
		if (distanceToStart <= distanceToEnd)
		{
			segmentStart = start;
			segmentEnd = middle;
			showArrowHead = false;
		}
		else
		{
			segmentStart = middle;
			segmentEnd = end;
			showArrowHead = true;
		}

		return true;
	}

	private void HideCraneHoverSelectionVisual()
	{
		HideCraneHoverNodeVisual();
		HideCraneHoverArcVisual();
	}

	private void HideCraneHoverNodeVisual()
	{
		if (localCraneHoverNodeShadow != null)
		{
			localCraneHoverNodeShadow.SetActive(false);
		}
	}

	private void HideCraneHoverArcVisual()
	{
		if (localCraneHoverArcHighlight != null)
		{
			localCraneHoverArcHighlight.SetActive(false);
		}
	}

	private void DestroyCraneHoverSelectionVisual()
	{
		if (localCraneHoverNodeShadow != null)
		{
			Destroy(localCraneHoverNodeShadow);
			localCraneHoverNodeShadow = null;
			localCraneHoverNodeOutline = null;
		}

		if (localCraneHoverArcHighlight != null)
		{
			Destroy(localCraneHoverArcHighlight);
			localCraneHoverArcHighlight = null;
			localCraneHoverArcBody = null;
			localCraneHoverArcArrow = null;
		}

		DestroyRemoteCraneHoverVisuals();
	}

	private void ConfigureCraneConnectPreviewLine(LineRenderer line, int sortingOrder, int positionCount)
	{
		Color previewColor = new Color(0.04f, 0.36f, 0.68f, 0.88f);
		ConfigureGroundLineRenderer(line, positionCount, arcWidth, sortingOrder, previewColor);
	}

	private CraneConnectPreviewVisual EnsureRemoteCraneConnectPreviewVisual(ulong clientId)
	{
		if (remoteCraneConnectPreviewVisuals.TryGetValue(clientId, out CraneConnectPreviewVisual visual)
			&& visual != null
			&& visual.root != null)
		{
			return visual;
		}

		visual = new CraneConnectPreviewVisual();
		visual.root = new GameObject("RemoteCraneConnectPreview_" + clientId);
		visual.root.transform.SetParent(petriNetRoot, false);

		visual.body = visual.root.AddComponent<LineRenderer>();
		ConfigureCraneConnectPreviewLine(visual.body, 56, 2);

		GameObject arrowObject = new GameObject("Arrow");
		arrowObject.transform.SetParent(visual.root.transform, false);
		visual.arrow = arrowObject.AddComponent<LineRenderer>();
		ConfigureCraneConnectPreviewLine(visual.arrow, 57, 3);

		visual.root.SetActive(false);
		remoteCraneConnectPreviewVisuals[clientId] = visual;
		return visual;
	}

	private void UpdateCraneConnectPreviewVisual()
	{
		if (string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			HideCraneConnectPreviewVisual();
			return;
		}

		if (!nodesById.TryGetValue(craneConnectStartNodeId, out NodeRuntime startNode) || startNode.transform == null || !startNode.transform.gameObject.activeInHierarchy)
		{
			CancelCraneConnectPreview();
			return;
		}

		Vector3 nodeCenter = startNode.transform.position;
		Vector3 cranePosition = avatarPosition;
		Vector3 nodeToCrane = cranePosition - nodeCenter;
		if (nodeToCrane.sqrMagnitude < 0.0001f)
		{
			HideCraneConnectPreviewVisual();
			return;
		}

		Vector3 directionToCrane = nodeToCrane.normalized;
		Vector3 nodeEdge = nodeCenter + directionToCrane * GetNodeOffsetAlongDirection(startNode, directionToCrane);
		Vector3 start = craneConnectReversed ? cranePosition : nodeEdge;
		Vector3 end = craneConnectReversed ? nodeEdge : cranePosition;
		Vector3 dir = end - start;
		if (dir.sqrMagnitude < 0.0001f)
		{
			HideCraneConnectPreviewVisual();
			return;
		}

		dir.Normalize();
		EnsureCraneConnectPreviewVisual();
		localCraneConnectPreview.SetActive(true);
		SetLineWithArrow(localCraneConnectPreviewBody, localCraneConnectPreviewArrow, start, end, dir);
	}

	private void UpdateRemoteCraneConnectPreviewVisuals()
	{
		List<ulong> staleClientIds = new List<ulong>();
		foreach (KeyValuePair<ulong, CraneConnectPreviewVisual> pair in remoteCraneConnectPreviewVisuals)
		{
			if (!remoteAvatarPositions.ContainsKey(pair.Key) || !remoteCraneConnectStates.ContainsKey(pair.Key))
			{
				staleClientIds.Add(pair.Key);
			}
		}

		for (int i = 0; i < staleClientIds.Count; i++)
		{
			DestroyRemoteCraneConnectPreviewVisual(staleClientIds[i]);
		}

		foreach (KeyValuePair<ulong, RemoteCraneConnectState> pair in remoteCraneConnectStates)
		{
			UpdateRemoteCraneConnectPreviewVisual(pair.Key, pair.Value);
		}
	}

	private void UpdateRemoteCraneConnectPreviewVisual(ulong clientId, RemoteCraneConnectState state)
	{
		if (state == null
			|| string.IsNullOrEmpty(state.startNodeId)
			|| !remoteAvatarPositions.TryGetValue(clientId, out Vector3 cranePosition)
			|| !nodesById.TryGetValue(state.startNodeId, out NodeRuntime startNode)
			|| startNode.transform == null
			|| !startNode.transform.gameObject.activeInHierarchy)
		{
			HideRemoteCraneConnectPreviewVisual(clientId);
			return;
		}

		Vector3 nodeCenter = startNode.transform.position;
		Vector3 nodeToCrane = cranePosition - nodeCenter;
		if (nodeToCrane.sqrMagnitude < 0.0001f)
		{
			HideRemoteCraneConnectPreviewVisual(clientId);
			return;
		}

		Vector3 directionToCrane = nodeToCrane.normalized;
		Vector3 nodeEdge = nodeCenter + directionToCrane * GetNodeOffsetAlongDirection(startNode, directionToCrane);
		Vector3 start = state.reversed ? cranePosition : nodeEdge;
		Vector3 end = state.reversed ? nodeEdge : cranePosition;
		Vector3 dir = end - start;
		if (dir.sqrMagnitude < 0.0001f)
		{
			HideRemoteCraneConnectPreviewVisual(clientId);
			return;
		}

		dir.Normalize();
		CraneConnectPreviewVisual visual = EnsureRemoteCraneConnectPreviewVisual(clientId);
		visual.root.SetActive(true);
		SetLineWithArrow(visual.body, visual.arrow, start, end, dir);
	}

	private void HideCraneConnectPreviewVisual()
	{
		if (localCraneConnectPreview != null)
		{
			localCraneConnectPreview.SetActive(false);
		}
	}

	private void DestroyCraneConnectPreviewVisual()
	{
		if (localCraneConnectPreview != null)
		{
			Destroy(localCraneConnectPreview);
			localCraneConnectPreview = null;
			localCraneConnectPreviewBody = null;
			localCraneConnectPreviewArrow = null;
		}
	}

	private void HideRemoteCraneConnectPreviewVisual(ulong clientId)
	{
		if (remoteCraneConnectPreviewVisuals.TryGetValue(clientId, out CraneConnectPreviewVisual visual)
			&& visual != null
			&& visual.root != null)
		{
			visual.root.SetActive(false);
		}
	}

	private void DestroyRemoteCraneConnectPreviewVisual(ulong clientId)
	{
		if (!remoteCraneConnectPreviewVisuals.TryGetValue(clientId, out CraneConnectPreviewVisual visual))
		{
			return;
		}

		if (visual != null && visual.root != null)
		{
			Destroy(visual.root);
		}

		remoteCraneConnectPreviewVisuals.Remove(clientId);
	}

	private void DestroyRemoteCraneConnectPreviewVisuals()
	{
		List<ulong> clientIds = new List<ulong>(remoteCraneConnectPreviewVisuals.Keys);
		for (int i = 0; i < clientIds.Count; i++)
		{
			DestroyRemoteCraneConnectPreviewVisual(clientIds[i]);
		}
	}

	private Vector3 GetHeldNodeVisualPosition()
	{
		return new Vector3(avatarPosition.x, avatarPosition.y, GetHeldObjectZ());
	}

	private float GetHeldObjectZ()
	{
		return GetHeldObjectZForCraneHeight(avatarCraneCurrentHeight);
	}

	private float GetHeldObjectTopZ()
	{
		return GetHeldObjectZ() + NodeVisualTopZ;
	}

	private float GetHeldObjectZForCraneHeight(float craneHeight)
	{
		float hookVisualZ = GetCraneHookVisualPosition(new Vector3(0f, 0f, GetHookZForCraneHeight(craneHeight))).z;
		float objectRootZ = hookVisualZ + HeldObjectUnderHookGap - NodeVisualTopZ;
		return Mathf.Min(GroundZ, objectRootZ);
	}

	private void SetCompositeBlockNodeHeight(string blockId, float z)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.transform == null)
			{
				continue;
			}

			Vector3 position = node.transform.position;
			node.transform.position = new Vector3(position.x, position.y, z);
		}

		if (compositeBlocksById.TryGetValue(blockId, out CompositeBlockRuntime block))
		{
			UpdateCompositeBlockVisual(block);
		}
	}

	private void SetPlaceSorting(NodeRuntime node, bool lifted)
	{
		if (node == null)
		{
			return;
		}

		if (node.renderer != null)
		{
			node.renderer.sortingOrder = lifted ? 61 : 30;
		}

		if (node.visual3DRenderer != null)
		{
			node.visual3DRenderer.sortingOrder = lifted ? 61 : 31;
		}

		Transform cuttingVisual = node.transform.Find(CuttingActivityVisualName);
		if (cuttingVisual != null)
		{
			SetMeshRenderersSortingOrder(cuttingVisual, lifted ? 61 : 31);
		}

		if (node.label != null)
		{
			MeshRenderer labelRenderer = node.label.GetComponent<MeshRenderer>();
			if (labelRenderer != null)
			{
				labelRenderer.sortingOrder = lifted ? 64 : 50;
			}
		}

		if (node.capacityLabel != null)
		{
			MeshRenderer capacityLabelRenderer = node.capacityLabel.GetComponent<MeshRenderer>();
			if (capacityLabelRenderer != null)
			{
				capacityLabelRenderer.sortingOrder = lifted ? 64 : 52;
			}
		}

		if (node.tokenRoot != null)
		{
			for (int i = 0; i < node.tokenRoot.childCount; i++)
			{
				SpriteRenderer tokenRenderer = node.tokenRoot.GetChild(i).GetComponent<SpriteRenderer>();
				if (tokenRenderer != null)
				{
					tokenRenderer.sortingOrder = lifted ? 64 : 40;
				}
			}
		}

		if (lifted)
		{
			EnsureRenderersMinimumSortingOrder(node.transform, 61);
		}

		SetRenderersAboveTutorialBubbles(node.transform, lifted);
	}

	private void SetTransitionSorting(NodeRuntime node, bool lifted)
	{
		if (node == null)
		{
			return;
		}

		if (node.renderer != null)
		{
			node.renderer.sortingOrder = lifted ? 61 : 30;
		}

		if (node.visual3DRenderer != null)
		{
			node.visual3DRenderer.sortingOrder = lifted ? 61 : 31;
		}

		if (node.label != null)
		{
			MeshRenderer labelRenderer = node.label.GetComponent<MeshRenderer>();
			if (labelRenderer != null)
			{
				labelRenderer.sortingOrder = lifted ? 64 : 50;
			}
		}

		if (lifted)
		{
			EnsureRenderersMinimumSortingOrder(node.transform, 61);
		}

		SetRenderersAboveTutorialBubbles(node.transform, lifted);
	}

	private void SetCompositeBlockSorting(string blockId, bool lifted)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node))
			{
				continue;
			}

			if (node.type == NodeType.Place)
			{
				SetPlaceSorting(node, lifted);
			}
			else
			{
				SetTransitionSorting(node, lifted);
			}
		}

		string[] arcIds = GetCompositeBlockArcIds(blockId);
		if (arcIds != null)
		{
			for (int i = 0; i < arcIds.Length; i++)
			{
				if (!arcsById.TryGetValue(arcIds[i], out ArcRuntime arc))
				{
					continue;
				}

				if (arc.body != null)
				{
						arc.body.sortingOrder = lifted ? 62 : 24;
				}

				if (arc.arrow != null)
				{
						arc.arrow.sortingOrder = lifted ? 63 : 25;
				}

				if (arc.resetArrow != null)
				{
						arc.resetArrow.sortingOrder = lifted ? 63 : 25;
				}

				if (arc.inhibitorCircle != null)
				{
						arc.inhibitorCircle.sortingOrder = lifted ? 63 : 25;
				}

				SetArcWeightLabelSorting(arc, lifted);
			}
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc == null || IsCompositeBlockInternalArc(arc))
			{
				continue;
			}

			if (GetCompositeBlockIdForNodeId(arc.fromId) != blockId && GetCompositeBlockIdForNodeId(arc.toId) != blockId)
			{
				continue;
			}

			if (arc.body != null)
			{
					arc.body.sortingOrder = lifted ? 62 : 24;
			}

			if (arc.arrow != null)
			{
					arc.arrow.sortingOrder = lifted ? 63 : 25;
			}

			if (arc.resetArrow != null)
			{
					arc.resetArrow.sortingOrder = lifted ? 63 : 25;
			}

			if (arc.inhibitorCircle != null)
			{
					arc.inhibitorCircle.sortingOrder = lifted ? 63 : 25;
			}

			SetArcWeightLabelSorting(arc, lifted);
		}

		if (compositeBlocksById.TryGetValue(blockId, out CompositeBlockRuntime block))
		{
			if (block.fill != null)
			{
					block.fill.sortingOrder = lifted ? 61 : 11;
			}

			if (block.border != null)
			{
					block.border.sortingOrder = lifted ? 63 : 14;
			}
		}
	}

	private void SetArcWeightLabelSorting(ArcRuntime arc, bool lifted)
	{
		if (arc == null || arc.weightLabel == null)
		{
			return;
		}

		MeshRenderer labelRenderer = arc.weightLabel.GetComponent<MeshRenderer>();
		if (labelRenderer != null)
		{
			labelRenderer.sortingOrder = lifted ? 64 : 52;
		}
	}

	private void RebuildSharedPoolVisual()
	{
		if (!enableSharedTransitionPool)
		{
			return;
		}

		if (sharedPoolVisualRoot != null)
		{
			Destroy(sharedPoolVisualRoot.gameObject);
		}

		sharedPoolVisualRoot = new GameObject("SharedTransitionPool").transform;
		sharedPoolVisualRoot.SetParent(petriNetRoot, false);

		float width = GetSharedPoolWidth();
		if (!singlePlayerMode)
		{
			CreateSharedBoundaryLine(10000f);
		}

		CreatePoolZoneVisual("PoolAvailable", sharedPoolY, width, new Color(0.82f, 0.92f, 1f, 0.35f));
		CreateIngredientAreaVisual(true);
		CreateIngredientAreaVisual(false);

		for (int i = 0; i < GetPoolBlockCount(); i++)
		{
			string blockId = GetCompositeBlockIdByIndex(i);
			Vector2 slot = GetSharedPoolBlockSlotPositionByIndex(i);
			GameObject slotObject = new GameObject("BlockSlot_" + (i + 1));
			slotObject.transform.SetParent(sharedPoolVisualRoot, false);
			slotObject.transform.position = new Vector3(slot.x, slot.y, OverlayZ);
			slotObject.transform.localScale = new Vector3(GetCompositeBlockLayoutWidth(blockId, GetPoolBlockDefinition(i)) + CompositeBlockPaddingX * 2f, GetCompositeBlockTemplateHeight(), 1f);

			SpriteRenderer slotRenderer = slotObject.AddComponent<SpriteRenderer>();
			slotRenderer.sprite = GetSquareSprite();
			slotRenderer.color = new Color(1f, 1f, 1f, 0.22f);
			slotRenderer.sortingOrder = 10;
		}

		if (HasSharedPoolTrashTransition())
		{
			Vector2 slot = GetSharedPoolTrashTransitionPosition();
			GameObject slotObject = new GameObject("TrashSlot");
			slotObject.transform.SetParent(sharedPoolVisualRoot, false);
			slotObject.transform.position = new Vector3(slot.x, slot.y, OverlayZ);
			slotObject.transform.localScale = new Vector3(GetSharedPoolTrashSlotWidth(), 1.1f, 1f);

			SpriteRenderer slotRenderer = slotObject.AddComponent<SpriteRenderer>();
			slotRenderer.sprite = GetSquareSprite();
			slotRenderer.color = new Color(1f, 1f, 1f, 0.22f);
			slotRenderer.sortingOrder = 10;
		}
	}

	private void CreateSharedBoundaryLine(float width)
	{
		float halfWidth = width * 0.5f;
		float poolHalfWidth = GetSharedPoolHalfWidth();
		if (halfWidth <= poolHalfWidth)
		{
			return;
		}

		CreateSharedBoundaryLineSegment("PlayerBoundaryLineLeft", -halfWidth, -poolHalfWidth);
		CreateSharedBoundaryLineSegment("PlayerBoundaryLineRight", poolHalfWidth, halfWidth);
	}

	private void CreateSharedBoundaryLineSegment(string name, float fromX, float toX)
	{
		GameObject lineObject = new GameObject(name);
		lineObject.transform.SetParent(sharedPoolVisualRoot, false);
		LineRenderer line = lineObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(line, 2, 0.08f, 13, new Color(0.08f, 0.12f, 0.16f, 0.78f));
		line.SetPosition(0, new Vector3(fromX, sharedPoolY, ArcZ));
		line.SetPosition(1, new Vector3(toX, sharedPoolY, ArcZ));
	}

	private void CreatePoolZoneVisual(string name, float centerY, float width, Color fillColor)
	{
		GameObject backgroundObject = new GameObject(name + "Background");
		backgroundObject.transform.SetParent(sharedPoolVisualRoot, false);
		backgroundObject.transform.position = new Vector3(0f, centerY, OverlayZ);
		backgroundObject.transform.localScale = new Vector3(width, sharedPoolHalfHeight * 2f, 1f);

		SpriteRenderer backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
		backgroundRenderer.sprite = GetSquareSprite();
		backgroundRenderer.color = fillColor;
		backgroundRenderer.sortingOrder = 5;

		GameObject borderObject = new GameObject(name + "Border");
		borderObject.transform.SetParent(sharedPoolVisualRoot, false);
		LineRenderer border = borderObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(border, 5, 0.08f, 12, new Color(0.07f, 0.34f, 0.56f, 0.95f), 6, 8);

		float halfWidth = width * 0.5f;
		float halfHeight = sharedPoolHalfHeight;
		Vector3 topLeft = new Vector3(-halfWidth, centerY + halfHeight, ArcZ);
		Vector3 topRight = new Vector3(halfWidth, centerY + halfHeight, ArcZ);
		Vector3 bottomRight = new Vector3(halfWidth, centerY - halfHeight, ArcZ);
		Vector3 bottomLeft = new Vector3(-halfWidth, centerY - halfHeight, ArcZ);
		border.SetPosition(0, topLeft);
		border.SetPosition(1, topRight);
		border.SetPosition(2, bottomRight);
		border.SetPosition(3, bottomLeft);
		border.SetPosition(4, topLeft);
	}

	private void CreateIngredientAreaVisual(bool topSide)
	{
		if (GetIngredientCount(topSide) <= 0)
		{
			return;
		}

		Rect bounds = GetIngredientAreaBounds(topSide);

		GameObject boxObject = new GameObject((topSide ? "Top" : "Bottom") + "IngredientBox");
		boxObject.transform.SetParent(sharedPoolVisualRoot, false);
		LineRenderer border = boxObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(border, 5, 0.07f, 12, new Color(0.24f, 0.18f, 0.08f, 0.9f), 6, 8);

		Vector3 topLeft = new Vector3(bounds.xMin, bounds.yMax, ArcZ);
		Vector3 topRight = new Vector3(bounds.xMax, bounds.yMax, ArcZ);
		Vector3 bottomRight = new Vector3(bounds.xMax, bounds.yMin, ArcZ);
		Vector3 bottomLeft = new Vector3(bounds.xMin, bounds.yMin, ArcZ);
		border.SetPosition(0, topLeft);
		border.SetPosition(1, topRight);
		border.SetPosition(2, bottomRight);
		border.SetPosition(3, bottomLeft);
		border.SetPosition(4, topLeft);

		GameObject labelObject = new GameObject("ZutatenLabel");
		labelObject.transform.SetParent(sharedPoolVisualRoot, false);
		float labelY = topSide ? bounds.yMin - 0.12f : bounds.yMax + 0.12f;
		labelObject.transform.position = new Vector3(bounds.xMin, labelY, ArcZ);
		TextMesh label = labelObject.AddComponent<TextMesh>();
			label.text = GameText("Zutaten", "Ingredients");
		label.characterSize = 0.046f;
		label.fontSize = 64;
		label.anchor = topSide ? TextAnchor.UpperLeft : TextAnchor.LowerLeft;
		label.alignment = TextAlignment.Left;
		label.color = new Color(0.24f, 0.18f, 0.08f, 1f);
		MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
		if (renderer != null)
		{
			renderer.sortingOrder = 50;
		}
	}

	private Rect GetIngredientAreaBounds(bool topSide)
	{
		int count = GetIngredientCount(topSide);
		if (count <= 0)
		{
			return new Rect();
		}

		Vector2 first = GetIngredientTransitionPosition(topSide, 0);
		Vector2 last = GetIngredientTransitionPosition(topSide, count - 1);
		float widestTransitionHalfWidth = GetWidestIngredientTransitionWidth(topSide) * 0.5f;
		float horizontalPadding = widestTransitionHalfWidth + IngredientAreaTransitionEdgePadding;
		float verticalPadding = 0.58f;
		float minX = Mathf.Min(first.x, last.x) - horizontalPadding;
		float maxX = Mathf.Max(first.x, last.x) + horizontalPadding;
		float minY = Mathf.Min(first.y, last.y) - verticalPadding;
		float maxY = Mathf.Max(first.y, last.y) + verticalPadding;

		return Rect.MinMaxRect(minX, minY, maxX, maxY);
	}

	private Vector2 GetIngredientTransitionPosition(bool topSide, int index)
	{
		int count = Mathf.Max(1, GetIngredientCount(topSide));
		int safeIndex = Mathf.Clamp(index, 0, count - 1);
		float yDirection = topSide ? 1f : -1f;
		float x = -GetSharedPoolHalfWidth() - 3.15f;
		float y = sharedPoolY + yDirection * (sharedPoolHalfHeight + 0.95f + safeIndex * ingredientTransitionSpacing);
		return new Vector2(x, y);
	}

	private Vector2 GetIngredientPlacePosition(bool topSide, int index)
	{
		Vector2 transitionPosition = GetIngredientTransitionPosition(topSide, index);
		float transitionWidth = GetIngredientTransitionWidth(topSide);
		float offset = transitionWidth * 0.5f + IngredientPlaceGap + NodeVisualFootprint * 0.5f;
		return transitionPosition + new Vector2(offset, 0f);
	}

	private float GetIngredientTransitionWidth(bool topSide)
	{
		return GetWidestIngredientTransitionWidth(topSide);
	}

	private float GetNaturalIngredientTransitionWidth(bool topSide, int index)
	{
		return GetTransitionVisualWidthForDisplayName(GetIngredientDisplayName(topSide, index));
	}

	private float GetWidestIngredientTransitionWidth(bool topSide)
	{
		int count = GetIngredientCount(topSide);
		float widest = NodeVisualFootprint;
		for (int i = 0; i < count; i++)
		{
			widest = Mathf.Max(widest, GetNaturalIngredientTransitionWidth(topSide, i));
		}

		return widest;
	}

	private int GetIngredientCount(bool topSide)
	{
		IList<string> names = GetIngredientNames(topSide);
		return names != null ? names.Count : 0;
	}

	private IList<string> GetIngredientNames(bool topSide)
	{
		List<string> names = topSide ? topIngredientNames : bottomIngredientNames;
		if (names == null || IsPlaceholderIngredientNameList(names))
		{
			return topSide ? DefaultTopIngredientNames : DefaultBottomIngredientNames;
		}

		return names;
	}

	private string GetIngredientDisplayName(bool topSide, int index)
	{
		IList<string> names = GetIngredientNames(topSide);
		if (names != null && index >= 0 && index < names.Count)
		{
			string name = names[index] != null ? names[index].Trim() : "";
			if (!string.IsNullOrEmpty(name))
			{
				return name;
			}
		}

		return GameText("Zutat ", "Ingredient ") + (index + 1);
	}

	private bool IsPlaceholderIngredientNameList(IList<string> names)
	{
		if (names == null || names.Count == 0)
		{
			return false;
		}

		for (int i = 0; i < names.Count; i++)
		{
			string actual = names[i] != null ? names[i].Trim() : "";
			string number = (i + 1).ToString();
			if (actual != "Zutat " + number && actual != "Zutat" + number)
			{
				return false;
			}
		}

		return true;
	}

	private string GetIngredientDisplayNameForNodeId(string nodeId)
	{
		if (string.IsNullOrEmpty(nodeId))
		{
			return null;
		}

		bool topSide;
		if (nodeId.StartsWith("T_Top_Zutat_") || nodeId.StartsWith("P_Top_Zutat_"))
		{
			topSide = true;
		}
		else if (nodeId.StartsWith("T_Bottom_Zutat_") || nodeId.StartsWith("P_Bottom_Zutat_"))
		{
			topSide = false;
		}
		else
		{
			return null;
		}

		return GetIngredientDisplayName(topSide, ExtractTrailingNumber(nodeId) - 1);
	}

	private float GetSharedPoolWidth()
	{
		return Mathf.Max(2.2f, GetSharedPoolContentWidth() + 0.9f);
	}

	private float GetSharedPoolHalfWidth()
	{
		return GetSharedPoolWidth() * 0.5f;
	}

	private float GetSharedPoolContentWidth()
	{
		int blockCount = GetPoolBlockCount();
		bool hasTrash = HasSharedPoolTrashTransition();
		int itemCount = blockCount + (hasTrash ? 1 : 0);
		if (itemCount <= 0)
		{
			return 0f;
		}

		float width = 0f;
		for (int i = 0; i < blockCount; i++)
		{
			width += GetCompositeBlockLayoutWidth(GetCompositeBlockIdByIndex(i), GetPoolBlockDefinition(i)) + CompositeBlockPaddingX * 2f;
		}

		if (hasTrash)
		{
			width += GetSharedPoolTrashSlotWidth();
		}

		width += Mathf.Max(0, itemCount - 1) * sharedPoolItemGap;
		return width;
	}

	private Vector2 GetSharedPoolBlockSlotPositionByIndex(int index)
	{
		int count = GetPoolBlockCount();
		int safeIndex = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
		float currentWidth = GetCompositeBlockLayoutWidth(GetCompositeBlockIdByIndex(safeIndex), GetPoolBlockDefinition(safeIndex)) + CompositeBlockPaddingX * 2f;
		float x = -GetSharedPoolContentWidth() * 0.5f;
		for (int i = 0; i < safeIndex; i++)
		{
			x += GetCompositeBlockLayoutWidth(GetCompositeBlockIdByIndex(i), GetPoolBlockDefinition(i)) + CompositeBlockPaddingX * 2f + sharedPoolItemGap;
		}

		x += currentWidth * 0.5f;
		return new Vector2(x, sharedPoolY);
	}

	private Vector2 GetSharedPoolTrashTransitionPosition()
	{
		float x = -GetSharedPoolContentWidth() * 0.5f;
		int blockCount = GetPoolBlockCount();
		if (blockCount > 0)
		{
			for (int i = 0; i < blockCount; i++)
			{
				x += GetCompositeBlockLayoutWidth(GetCompositeBlockIdByIndex(i), GetPoolBlockDefinition(i)) + CompositeBlockPaddingX * 2f;
			}

			x += blockCount * sharedPoolItemGap;
		}

		x += GetSharedPoolTrashSlotWidth() * 0.5f;
		return new Vector2(x, sharedPoolY);
	}

	private float GetCompositeBlockTemplateHeight()
	{
		return 1.35f;
	}

	private float GetSharedPoolTrashSlotWidth()
	{
		return GetTransitionVisualWidthForDisplayName(GetSharedPoolTrashTransitionDisplayName()) + 0.1f;
	}

	private bool HasSharedPoolTrashTransition()
	{
		return !string.IsNullOrEmpty(GetSharedPoolTrashTransitionDisplayName());
	}

	private string GetSharedPoolTrashTransitionId()
	{
		return "T_POOL_Trash";
	}

	private string GetSharedPoolTrashTransitionDisplayName()
	{
		return sharedPoolTrashTransitionName != null ? sharedPoolTrashTransitionName.Trim() : "";
	}

	private bool IsSharedPoolTrashTransitionId(string nodeId)
	{
		return nodeId == GetSharedPoolTrashTransitionId();
	}

	private ArcKind GetEffectiveArcKind(string fromId, string toId, ArcKind requestedKind)
	{
		if (requestedKind == ArcKind.Inhibitor)
		{
			return ArcKind.Inhibitor;
		}

		return IsSharedPoolTrashTransitionId(toId) ? ArcKind.Reset : ArcKind.Normal;
	}

	private bool IsActorTopSide(ulong actorClientId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return actorClientId == 0;
		}

		return actorClientId == NetworkManager.ServerClientId;
	}

	private bool IsIngredientTransitionId(string nodeId)
	{
		return !string.IsNullOrEmpty(nodeId)
			&& (nodeId.StartsWith("T_Top_Zutat_") || nodeId.StartsWith("T_Bottom_Zutat_"));
	}

	private bool IsIngredientPlaceId(string nodeId)
	{
		return !string.IsNullOrEmpty(nodeId)
			&& (nodeId.StartsWith("P_Top_Zutat_") || nodeId.StartsWith("P_Bottom_Zutat_"));
	}

	private bool IsIngredientTransition(NodeRuntime node)
	{
		return node != null && node.type == NodeType.Transition && IsIngredientTransitionId(node.id);
	}

	private bool IsIngredientSourceNode(NodeRuntime node)
	{
		return node != null && (IsIngredientTransitionId(node.id) || IsIngredientPlaceId(node.id));
	}

	private bool IsIngredientSourceArc(ArcRuntime arc)
	{
		return arc != null
			&& IsIngredientTransitionId(arc.fromId)
			&& arc.toId == GetIngredientPlaceIdForTransition(arc.fromId);
	}

	private bool IsPlayerExchangeArc(ArcRuntime arc)
	{
		return arc != null
			&& ((arc.fromId == "T_Top_Out" && arc.toId == "P_Bottom_In")
				|| (arc.fromId == "T_Bottom_Out" && arc.toId == "P_Top_In"));
	}

	private bool IsArcAllowedByPlayerExchangeRules(string fromId, string toId)
	{
		if (fromId == "T_Top_Out")
		{
			return toId == "P_Bottom_In";
		}

		if (fromId == "T_Bottom_Out")
		{
			return toId == "P_Top_In";
		}

		return true;
	}

	private bool IsDeliveryTransitionId(string nodeId)
	{
		return nodeId == "T_Bottom_Ausliefern";
	}

	private bool IsDeliveryTransition(NodeRuntime node)
	{
		return node != null && node.type == NodeType.Transition && IsDeliveryTransitionId(node.id);
	}

	private Vector2 GetDeliveryTransitionPosition()
	{
		return new Vector2(playerZoneXOffset, sharedPoolY - playerZoneYSpacing - 1.7f);
	}

	private int GetPoolBlockCount()
	{
		return sharedPoolBlocks != null ? sharedPoolBlocks.Count : 0;
	}

	private PoolBlockDefinition GetPoolBlockDefinition(int index)
	{
		if (sharedPoolBlocks != null && index >= 0 && index < sharedPoolBlocks.Count && sharedPoolBlocks[index] != null)
		{
			return sharedPoolBlocks[index];
		}

		return new PoolBlockDefinition("Start", "Ende", 5f, "");
	}

	private int GetPlayerBlockCount(bool topSide)
	{
		List<PoolBlockDefinition> blocks = topSide ? topPlayerBlocks : bottomPlayerBlocks;
		return blocks != null ? blocks.Count : 0;
	}

	private PoolBlockDefinition GetPlayerBlockDefinition(bool topSide, int index)
	{
		List<PoolBlockDefinition> blocks = topSide ? topPlayerBlocks : bottomPlayerBlocks;
		if (blocks != null && index >= 0 && index < blocks.Count && blocks[index] != null)
		{
			return blocks[index];
		}

		return new PoolBlockDefinition("Start", "Ende", 5f, "");
	}

	private string GetPlayerCompositeBlockIdByIndex(bool topSide, int index)
	{
		return topSide ? "B_TopBlock_" + (index + 1) : "B_BottomBlock_" + (index + 1);
	}

	private int GetPlayerCompositeBlockIndex(string blockId, bool topSide)
	{
		string prefix = topSide ? "B_TopBlock_" : "B_BottomBlock_";
		if (string.IsNullOrEmpty(blockId) || !blockId.StartsWith(prefix))
		{
			return -1;
		}

		int index = ExtractTrailingNumber(blockId) - 1;
		return index >= 0 && index < GetPlayerBlockCount(topSide) ? index : -1;
	}

	private Vector2 GetPlayerCompositeBlockPosition(bool topSide, int index)
	{
		int safeIndex = Mathf.Clamp(index, 0, Mathf.Max(0, GetPlayerBlockCount(topSide) - 1));
		float currentWidth = GetCompositeBlockLayoutWidth(GetPlayerCompositeBlockIdByIndex(topSide, safeIndex), GetPlayerBlockDefinition(topSide, safeIndex)) + CompositeBlockPaddingX * 2f;
		float x = GetSharedPoolHalfWidth() + 0.95f;
		for (int i = 0; i < safeIndex; i++)
		{
			x += GetCompositeBlockLayoutWidth(GetPlayerCompositeBlockIdByIndex(topSide, i), GetPlayerBlockDefinition(topSide, i)) + CompositeBlockPaddingX * 2f + sharedPoolItemGap;
		}

		x += currentWidth * 0.5f;
		float y = topSide ? sharedPoolY + playerZoneYSpacing : sharedPoolY - playerZoneYSpacing;
		return new Vector2(x, y);
	}

	private List<string> GetAllCompositeBlockIds()
	{
		List<string> blockIds = new List<string>();
		for (int i = 0; i < GetPoolBlockCount(); i++)
		{
			blockIds.Add(GetCompositeBlockIdByIndex(i));
		}

		for (int i = 0; i < GetPlayerBlockCount(true); i++)
		{
			blockIds.Add(GetPlayerCompositeBlockIdByIndex(true, i));
		}

		for (int i = 0; i < GetPlayerBlockCount(false); i++)
		{
			blockIds.Add(GetPlayerCompositeBlockIdByIndex(false, i));
		}

		List<string> createdBlockIds = GetCreatedCompositeBlockIds();
		for (int i = 0; i < createdBlockIds.Count; i++)
		{
			blockIds.Add(createdBlockIds[i]);
		}

		return blockIds;
	}

	private bool IsKnownCompositeBlockId(string blockId)
	{
		return GetCompositeBlockIndex(blockId) >= 0
			|| GetPlayerCompositeBlockIndex(blockId, true) >= 0
			|| GetPlayerCompositeBlockIndex(blockId, false) >= 0
			|| IsCreatedCompositeBlockId(blockId);
	}

	private bool IsPlayerBoundCompositeBlock(string blockId)
	{
		return GetPlayerCompositeBlockIndex(blockId, true) >= 0
			|| GetPlayerCompositeBlockIndex(blockId, false) >= 0;
	}

	private bool TryGetPlayerBoundCompositeBlockTopSide(string blockId, out bool topSide)
	{
		if (GetPlayerCompositeBlockIndex(blockId, true) >= 0)
		{
			topSide = true;
			return true;
		}

		if (GetPlayerCompositeBlockIndex(blockId, false) >= 0)
		{
			topSide = false;
			return true;
		}

		topSide = false;
		return false;
	}

	private bool IsCreatedCompositeBlockId(string blockId)
	{
		return !string.IsNullOrEmpty(blockId)
			&& blockId.StartsWith("B_CreatedBlock_")
			&& ExtractTrailingNumber(blockId) > 0;
	}

	private string GetNextCreatedCompositeBlockId()
	{
		while (nodesById.ContainsKey("T_CreatedBlock_" + createdBlockCounter + "_Start")
			|| nodesById.ContainsKey("P_CreatedBlock_" + createdBlockCounter + "_Output")
			|| arcsById.ContainsKey("A_CreatedBlock_" + createdBlockCounter + "_1"))
		{
			createdBlockCounter++;
		}

		string blockId = "B_CreatedBlock_" + createdBlockCounter;
		createdBlockCounter++;
		return blockId;
	}

	private List<string> GetCreatedCompositeBlockIds()
	{
		List<string> blockIds = new List<string>();
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			string blockId = GetCreatedCompositeBlockIdForNodeId(pair.Key);
			if (!string.IsNullOrEmpty(blockId) && !blockIds.Contains(blockId))
			{
				blockIds.Add(blockId);
			}
		}

		blockIds.Sort((left, right) => ExtractTrailingNumber(left).CompareTo(ExtractTrailingNumber(right)));
		return blockIds;
	}

	private string GetCreatedCompositeBlockIdForNodeId(string nodeId)
	{
		if (string.IsNullOrEmpty(nodeId))
		{
			return null;
		}

		const string transitionPrefix = "T_CreatedBlock_";
		const string placePrefix = "P_CreatedBlock_";
		if (nodeId.StartsWith(transitionPrefix) && nodeId.EndsWith("_Start"))
		{
			string number = nodeId.Substring(transitionPrefix.Length, nodeId.Length - transitionPrefix.Length - "_Start".Length);
			return "B_CreatedBlock_" + number;
		}

		if (nodeId.StartsWith(placePrefix) && nodeId.EndsWith("_Output"))
		{
			string number = nodeId.Substring(placePrefix.Length, nodeId.Length - placePrefix.Length - "_Output".Length);
			return "B_CreatedBlock_" + number;
		}

		return null;
	}

	private bool IsCreatedStoragePlaceId(string nodeId)
	{
		return !string.IsNullOrEmpty(nodeId)
			&& nodeId.StartsWith("P_CreatedBlock_")
			&& nodeId.EndsWith("_Output");
	}

	private int GetPlaceTokenCapacity(NodeRuntime place)
	{
		if (place == null || place.type != NodeType.Place)
		{
			return 0;
		}

		if (IsInhibitorCapacityLevel())
		{
			return 1;
		}

		if (IsCreatedStoragePlaceId(place.id))
		{
			return IsLastLevelSelected() ? 0 : 1;
		}

		return place.processingDuration > 0f ? 1 : 0;
	}

	private bool IsInhibitorCapacityLevel()
	{
		return levelInhibitorArcs != null && levelInhibitorArcs.Count > 0;
	}

	private bool CanAddTokensToPlace(NodeRuntime place, int amount)
	{
		if (place == null || place.type != NodeType.Place)
		{
			return false;
		}

		int capacity = GetPlaceTokenCapacity(place);
		return capacity <= 0 || place.tokens + Mathf.Max(1, amount) <= capacity;
	}

	private void UpdatePlaceCapacityLabel(NodeRuntime place)
	{
		if (place == null || place.capacityLabel == null)
		{
			return;
		}

		int capacity = GetPlaceTokenCapacity(place);
		bool showCapacity = capacity > 0 && (IsInhibitorCapacityLevel() || IsCreatedStoragePlaceId(place.id));
		if (!showCapacity)
		{
			place.capacityLabel.gameObject.SetActive(false);
			return;
		}

		place.capacityLabel.gameObject.SetActive(true);
		place.capacityLabel.text = capacity.ToString();
		place.capacityLabel.characterSize = ArcWeightLabelCharacterSize;
		place.capacityLabel.color = Color.black;
	}

	private PoolBlockDefinition GetCompositeBlockDefinition(string blockId)
	{
		int poolIndex = GetCompositeBlockIndex(blockId);
		if (poolIndex >= 0)
		{
			return GetPoolBlockDefinition(poolIndex);
		}

		int topIndex = GetPlayerCompositeBlockIndex(blockId, true);
		if (topIndex >= 0)
		{
			return GetPlayerBlockDefinition(true, topIndex);
		}

		int bottomIndex = GetPlayerCompositeBlockIndex(blockId, false);
		if (bottomIndex >= 0)
		{
			return GetPlayerBlockDefinition(false, bottomIndex);
		}

		return null;
	}

	private string GetPoolBlockFirstTransitionName(PoolBlockDefinition definition)
	{
		string name = definition != null && definition.firstTransitionName != null ? definition.firstTransitionName.Trim() : "";
		if (!string.IsNullOrEmpty(name))
		{
			return name;
		}

		return "Start";
	}

	private string GetPoolBlockSecondTransitionName(PoolBlockDefinition definition)
	{
		string name = definition != null && definition.secondTransitionName != null ? definition.secondTransitionName.Trim() : "";
		if (!string.IsNullOrEmpty(name))
		{
			return name;
		}

		return "Ende";
	}

	private float GetPoolBlockProcessingSeconds(PoolBlockDefinition definition)
	{
		return definition != null ? Mathf.Max(0f, definition.processingSeconds) : 0f;
	}

	private string GetPoolBlockResultState(PoolBlockDefinition definition)
	{
		string configuredState = definition != null && definition.resultState != null ? definition.resultState.Trim() : "";
		if (!string.IsNullOrEmpty(configuredState))
		{
			return configuredState;
		}

		string name = ((definition != null ? definition.firstTransitionName : "") + " " + (definition != null ? definition.secondTransitionName : "")).ToLowerInvariant();
		if (name.Contains("koch"))
		{
			return "gekocht";
		}

		if (name.Contains("schneid"))
		{
			return "geschnitten";
		}

		return "";
	}

	private int GetPoolBlockOutputTokenCount(PoolBlockDefinition definition)
	{
		return definition != null ? Mathf.Max(1, definition.outputTokenCount) : 1;
	}

	private bool IsSingleTransitionBlockDefinition(PoolBlockDefinition definition)
	{
		return definition != null && definition.singleTransition;
	}

	private float GetCompositeBlockLayoutWidth(string blockId, PoolBlockDefinition definition)
	{
		float[] widths = GetCompositeBlockNodeLayoutWidths(blockId, definition);
		if (widths.Length <= 0)
		{
			return 0f;
		}

		float total = 0f;
		for (int i = 0; i < widths.Length; i++)
		{
			total += widths[i];
		}

		total += Mathf.Max(0, widths.Length - 1) * CompositeBlockNodeGap;
		return total;
	}

	private float[] GetCompositeBlockNodeXOffsets(string blockId, PoolBlockDefinition definition)
	{
		float[] widths = GetCompositeBlockNodeLayoutWidths(blockId, definition);
		float[] offsets = new float[widths.Length];
		if (widths.Length <= 0)
		{
			return offsets;
		}

		float totalWidth = GetCompositeBlockLayoutWidth(blockId, definition);
		float cursor = -totalWidth * 0.5f;
		for (int i = 0; i < widths.Length; i++)
		{
			offsets[i] = cursor + widths[i] * 0.5f;
			cursor += widths[i] + CompositeBlockNodeGap;
		}

		return offsets;
	}

	private float[] GetCompositeBlockNodeLayoutWidths(string blockId, PoolBlockDefinition definition)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return new float[0];
		}

		float[] widths = new float[nodeIds.Length];
		for (int i = 0; i < nodeIds.Length; i++)
		{
			widths[i] = GetCompositeBlockNodeLayoutWidth(nodeIds[i], nodeIds, definition);
		}

		return widths;
	}

	private float GetCompositeBlockNodeLayoutWidth(string nodeId, string[] nodeIds, PoolBlockDefinition definition)
	{
		if (string.IsNullOrEmpty(nodeId) || nodeId.StartsWith("P_"))
		{
			return NodeVisualFootprint;
		}

		string displayName = GetCompositeBlockTransitionLayoutName(nodeId, nodeIds, definition);
		return GetTransitionVisualWidthForDisplayName(displayName);
	}

	private string GetCompositeBlockTransitionLayoutName(string nodeId, string[] nodeIds, PoolBlockDefinition definition)
	{
		if (!string.IsNullOrEmpty(GetCreatedCompositeBlockIdForNodeId(nodeId)) && nodeId.StartsWith("T_"))
		{
			return "Lager";
		}

		if (nodeIds != null && nodeIds.Length > 0 && nodeId == nodeIds[0] && definition != null)
		{
			return GetPoolBlockFirstTransitionName(definition);
		}

		if (nodeIds != null && nodeIds.Length > 2 && nodeId == nodeIds[2] && definition != null)
		{
			return GetPoolBlockSecondTransitionName(definition);
		}

		if (nodesById.TryGetValue(nodeId, out NodeRuntime node))
		{
			return GetNodeDisplayName(node);
		}

		return HumanizeId(nodeId);
	}

	private void UpdateCompositeBlockTransitionDimensions(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.type != NodeType.Transition || node.label == null)
			{
				continue;
			}

				string transitionLabel = FormatTransitionLabel(GetLocalizedNodeDisplayName(node));
			node.label.text = transitionLabel;
			node.label.characterSize = GetTransitionLabelCharacterSize(transitionLabel);
			node.label.lineSpacing = transitionLabel.Contains("\n") ? 0.78f : 1f;
			UpdateTransitionVisualDimensions(node, transitionLabel);
		}
	}

	private string GetCompositeBlockIdByIndex(int index)
	{
		return "B_PoolBlock_" + (index + 1);
	}

	private int GetCompositeBlockIndex(string blockId)
	{
		if (string.IsNullOrEmpty(blockId) || !blockId.StartsWith("B_PoolBlock_"))
		{
			return -1;
		}

		int index = ExtractTrailingNumber(blockId) - 1;
		return index >= 0 && index < GetPoolBlockCount() ? index : -1;
	}

	private string GetCompositeBlockNodePrefix(string blockId)
	{
		return blockId != null && blockId.StartsWith("B_") ? blockId.Substring(2) : blockId;
	}

	private string GetCompositeBlockDisplayNameForNodeId(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		if (IsCreatedCompositeBlockId(blockId))
		{
			string[] createdNodeIds = GetCompositeBlockNodeIds(blockId);
			return createdNodeIds != null && createdNodeIds.Length > 0 && nodeId == createdNodeIds[0] ? "Lager" : null;
		}

		PoolBlockDefinition definition = GetCompositeBlockDefinition(blockId);
		if (definition == null)
		{
			return null;
		}

		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return null;
		}

		if (nodeId == nodeIds[0])
		{
			return GetPoolBlockFirstTransitionName(definition);
		}

		if (nodeIds.Length > 2 && nodeId == nodeIds[2])
		{
			return GetPoolBlockSecondTransitionName(definition);
		}

		return null;
	}

	private string[] GetCompositeBlockNodeIds(string blockId)
	{
		if (!IsKnownCompositeBlockId(blockId))
		{
			return null;
		}

		string prefix = GetCompositeBlockNodePrefix(blockId);
		if (IsCreatedCompositeBlockId(blockId))
		{
			return new[] { "T_" + prefix + "_Start", "P_" + prefix + "_Output" };
		}

		if (IsSingleTransitionBlockDefinition(GetCompositeBlockDefinition(blockId)))
		{
			return new[] { "T_" + prefix + "_Start", "P_" + prefix + "_Output" };
		}

		return new[] { "T_" + prefix + "_Start", "P_" + prefix + "_Buffer", "T_" + prefix + "_End", "P_" + prefix + "_Output" };
	}

	private bool IsCompositeBlockBufferPlaceId(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		if (IsCreatedCompositeBlockId(blockId))
		{
			return false;
		}

		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		return nodeIds != null && nodeIds.Length > 2 && nodeId == nodeIds[1];
	}

	private float GetTimedPlaceProcessingDuration(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null || nodeIds.Length < 4 || nodeId != nodeIds[1])
		{
			return 0f;
		}

		return GetPoolBlockProcessingSeconds(GetCompositeBlockDefinition(blockId));
	}

	private string[] GetCompositeBlockArcIds(string blockId)
	{
		if (!IsKnownCompositeBlockId(blockId))
		{
			return null;
		}

		string prefix = GetCompositeBlockNodePrefix(blockId);
		if (IsCreatedCompositeBlockId(blockId))
		{
			return new[] { "A_" + prefix + "_1" };
		}

		if (IsSingleTransitionBlockDefinition(GetCompositeBlockDefinition(blockId)))
		{
			return new[] { "A_" + prefix + "_1" };
		}

		return new[] { "A_" + prefix + "_1", "A_" + prefix + "_2", "A_" + prefix + "_3" };
	}

	private bool IsCompositeBlockNodeId(string nodeId)
	{
		return GetCompositeBlockIdForNodeId(nodeId) != null;
	}

	private bool IsCompositeBlockNode(NodeRuntime node)
	{
		return node != null && IsCompositeBlockNodeId(node.id);
	}

	private bool IsHeldCompositeBlockNode(NodeRuntime node)
	{
		return !string.IsNullOrEmpty(heldCompositeBlockId)
			&& GetCompositeBlockIdForNodeId(node != null ? node.id : null) == heldCompositeBlockId;
	}

	private string GetCompositeBlockIdForNodeId(string nodeId)
	{
		if (string.IsNullOrEmpty(nodeId))
		{
			return null;
		}

		string createdBlockId = GetCreatedCompositeBlockIdForNodeId(nodeId);
		if (!string.IsNullOrEmpty(createdBlockId))
		{
			return createdBlockId;
		}

		List<string> blockIds = GetAllCompositeBlockIds();
		for (int i = 0; i < blockIds.Count; i++)
		{
			string blockId = blockIds[i];
			string[] nodeIds = GetCompositeBlockNodeIds(blockId);
			if (nodeIds == null)
			{
				continue;
			}

			for (int j = 0; j < nodeIds.Length; j++)
			{
				if (nodeId == nodeIds[j])
				{
					return blockId;
				}
			}
		}

		return null;
	}

	private bool IsCompositeBlockInternalArcId(string arcId)
	{
		if (string.IsNullOrEmpty(arcId))
		{
			return false;
		}

		List<string> blockIds = GetAllCompositeBlockIds();
		for (int blockIndex = 0; blockIndex < blockIds.Count; blockIndex++)
		{
			string[] arcIds = GetCompositeBlockArcIds(blockIds[blockIndex]);
			if (arcIds == null)
			{
				continue;
			}

			for (int i = 0; i < arcIds.Length; i++)
			{
				if (arcId == arcIds[i])
				{
					return true;
				}
			}
		}

		return false;
	}

	private bool IsCompositeBlockInternalArc(ArcRuntime arc)
	{
		return arc != null && IsCompositeBlockInternalArcId(arc.id);
	}

	private bool IsCompositeBlockFirstTransitionId(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		return nodeIds != null && nodeId == nodeIds[0];
	}

	private bool IsCompositeBlockLastPlaceId(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		return nodeIds != null && nodeIds.Length > 0 && nodeId == nodeIds[nodeIds.Length - 1];
	}

	private bool IsCompositeBlockInternalConnection(string fromId, string toId)
	{
		string blockId = GetCompositeBlockIdForNodeId(fromId);
		if (blockId == null || GetCompositeBlockIdForNodeId(toId) != blockId)
		{
			return false;
		}

		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		for (int i = 0; i < nodeIds.Length - 1; i++)
		{
			if (fromId == nodeIds[i] && toId == nodeIds[i + 1])
			{
				return true;
			}
		}

		return false;
	}

	private bool IsArcAllowedByCompositeBlockRules(string fromId, string toId)
	{
		string fromBlockId = GetCompositeBlockIdForNodeId(fromId);
		string toBlockId = GetCompositeBlockIdForNodeId(toId);
		bool fromComposite = fromBlockId != null;
		bool toComposite = toBlockId != null;
		if (!fromComposite && !toComposite)
		{
			return true;
		}

		if (IsCompositeBlockInternalConnection(fromId, toId))
		{
			return true;
		}

		if (fromComposite && toComposite)
		{
			return fromBlockId != toBlockId
				&& IsCompositeBlockLastPlaceId(fromId)
				&& IsCompositeBlockFirstTransitionId(toId);
		}

		if (toComposite)
		{
			return IsCompositeBlockFirstTransitionId(toId) && !fromComposite;
		}

		if (fromComposite)
		{
			return IsCompositeBlockLastPlaceId(fromId) && !toComposite;
		}

		return true;
	}

	private void EnsureCompositeBlockVisuals()
	{
		List<string> expectedBlockIds = GetAllCompositeBlockIds();
		for (int blockIndex = 0; blockIndex < expectedBlockIds.Count; blockIndex++)
		{
			string blockId = expectedBlockIds[blockIndex];
			string[] nodeIds = GetCompositeBlockNodeIds(blockId);
			bool hasAllNodes = nodeIds != null;
			if (nodeIds != null)
			{
				for (int i = 0; i < nodeIds.Length; i++)
				{
					if (!nodesById.ContainsKey(nodeIds[i]))
					{
						hasAllNodes = false;
						break;
					}
				}
			}

			if (!hasAllNodes)
			{
				RemoveCompositeBlockVisual(blockId);
				continue;
			}

			if (!compositeBlocksById.TryGetValue(blockId, out CompositeBlockRuntime block) || block == null || block.gameObject == null)
			{
				block = CreateCompositeBlockVisual(blockId);
				compositeBlocksById[blockId] = block;
			}

			UpdateCompositeBlockVisual(block);
		}

		List<string> staleBlockIds = new List<string>();
		foreach (KeyValuePair<string, CompositeBlockRuntime> pair in compositeBlocksById)
		{
			if (!expectedBlockIds.Contains(pair.Key))
			{
				staleBlockIds.Add(pair.Key);
			}
		}

		for (int i = 0; i < staleBlockIds.Count; i++)
		{
			RemoveCompositeBlockVisual(staleBlockIds[i]);
		}
	}

	private CompositeBlockRuntime CreateCompositeBlockVisual(string blockId)
	{
		GameObject blockObject = new GameObject(blockId);
		blockObject.transform.SetParent(petriNetRoot, false);

		GameObject fillObject = new GameObject("Fill");
		fillObject.transform.SetParent(blockObject.transform, false);
		SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
		fill.sprite = GetSquareSprite();
		fill.color = Color.white;
		fill.sortingOrder = 11;

		LineRenderer border = blockObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(border, 5, 0.075f, 14, new Color(0.18f, 0.18f, 0.2f, 0.9f), 6, 8);

		BoxCollider2D collider = blockObject.AddComponent<BoxCollider2D>();
		collider.isTrigger = true;

		GameObject baseShadowCasterObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		baseShadowCasterObject.name = "BaseShadowCaster";
		baseShadowCasterObject.transform.SetParent(blockObject.transform, false);
		Collider baseShadowCollider = baseShadowCasterObject.GetComponent<Collider>();
		if (baseShadowCollider != null)
		{
			Destroy(baseShadowCollider);
		}

		MeshRenderer baseShadowRenderer = baseShadowCasterObject.GetComponent<MeshRenderer>();
		if (baseShadowRenderer != null)
		{
			baseShadowRenderer.sharedMaterial = CreatePrimitiveVisualMaterial(Color.white);
			baseShadowRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
			baseShadowRenderer.receiveShadows = false;
		}

		CompositeBlockRuntime block = new CompositeBlockRuntime
		{
			id = blockId,
			gameObject = blockObject,
			fill = fill,
			border = border,
			collider = collider,
			baseShadowCaster = baseShadowCasterObject.transform,
		};

		compositeBlockByCollider[collider] = blockId;
		return block;
	}

	private void RemoveCompositeBlockVisual(string blockId)
	{
		if (!compositeBlocksById.TryGetValue(blockId, out CompositeBlockRuntime block))
		{
			return;
		}

		if (block.collider != null)
		{
			compositeBlockByCollider.Remove(block.collider);
		}

		if (block.gameObject != null)
		{
			Destroy(block.gameObject);
		}

		compositeBlocksById.Remove(blockId);
	}

	private void UpdateCompositeBlockVisual(CompositeBlockRuntime block)
	{
		if (block == null || block.fill == null || block.border == null || block.collider == null)
		{
			return;
		}

		RelayoutCompositeBlockNodes(block.id);
		if (!TryGetCompositeBlockBounds(block.id, out Rect bounds))
		{
			return;
		}

		float fillZ = GetCompositeBlockLayerZ(block.id, OverlayZ);
		float borderZ = GetCompositeBlockLayerZ(block.id, ArcZ);
		Vector3 center = new Vector3(bounds.center.x, bounds.center.y, fillZ);
		block.gameObject.transform.position = center;
		block.collider.offset = Vector2.zero;
		block.collider.size = new Vector2(bounds.width, bounds.height);
		block.fill.transform.position = new Vector3(bounds.center.x, bounds.center.y, fillZ);
		block.fill.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);
		if (block.baseShadowCaster != null)
		{
			block.baseShadowCaster.position = new Vector3(bounds.center.x, bounds.center.y, fillZ);
			block.baseShadowCaster.localScale = new Vector3(
				bounds.width,
				bounds.height,
				CompositeBlockBaseShadowCasterDepth);
		}

		Vector3 topLeft = new Vector3(bounds.xMin, bounds.yMax, borderZ);
		Vector3 topRight = new Vector3(bounds.xMax, bounds.yMax, borderZ);
		Vector3 bottomRight = new Vector3(bounds.xMax, bounds.yMin, borderZ);
		Vector3 bottomLeft = new Vector3(bounds.xMin, bounds.yMin, borderZ);
		block.border.SetPosition(0, topLeft);
		block.border.SetPosition(1, topRight);
		block.border.SetPosition(2, bottomRight);
		block.border.SetPosition(3, bottomLeft);
		block.border.SetPosition(4, topLeft);
	}

	private float GetCompositeBlockLayerZ(string blockId, float groundLayerZ)
	{
		return GetCompositeBlockNodeZ(blockId) + groundLayerZ - GroundZ;
	}

	private float GetCompositeBlockNodeZ(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return GroundZ;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) && node.transform != null)
			{
				return node.transform.position.z;
			}
		}

		return GroundZ;
	}

	private void RelayoutCompositeBlockNodes(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null || !TryGetCompositeBlockLayoutCenter(blockId, out Vector2 center))
		{
			return;
		}

		PoolBlockDefinition definition = GetCompositeBlockDefinition(blockId);
		float[] nodeOffsets = GetCompositeBlockNodeXOffsets(blockId, definition);
		if (nodeOffsets.Length < nodeIds.Length)
		{
			return;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.transform == null)
			{
				continue;
			}

			Vector3 position = node.transform.position;
			node.transform.position = new Vector3(center.x + nodeOffsets[i], center.y, position.z);
		}
	}

	private bool TryGetCompositeBlockLayoutCenter(string blockId, out Vector2 center)
	{
		center = Vector2.zero;
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		PoolBlockDefinition definition = GetCompositeBlockDefinition(blockId);
		float[] nodeOffsets = GetCompositeBlockNodeXOffsets(blockId, definition);
		if (nodeOffsets.Length < nodeIds.Length)
		{
			return false;
		}

		Vector2 sum = Vector2.zero;
		int count = 0;
		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.transform == null)
			{
				return false;
			}

			Vector3 position = node.transform.position;
			sum += new Vector2(position.x - nodeOffsets[i], position.y);
			count++;
		}

		if (count <= 0)
		{
			return false;
		}

		center = sum / count;
		return true;
	}

	private bool TryGetCompositeBlockBounds(string blockId, out Rect bounds)
	{
		bounds = new Rect();
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		bool initialized = false;
		float xMin = 0f;
		float xMax = 0f;
		float yMin = 0f;
		float yMax = 0f;
		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.transform == null)
			{
				return false;
			}

			Rect nodeBounds = GetNodePlacementRect(node, node.transform.position);
			if (!initialized)
			{
				xMin = nodeBounds.xMin;
				xMax = nodeBounds.xMax;
				yMin = nodeBounds.yMin;
				yMax = nodeBounds.yMax;
				initialized = true;
			}
			else
			{
				xMin = Mathf.Min(xMin, nodeBounds.xMin);
				xMax = Mathf.Max(xMax, nodeBounds.xMax);
				yMin = Mathf.Min(yMin, nodeBounds.yMin);
				yMax = Mathf.Max(yMax, nodeBounds.yMax);
			}
		}

		bounds = Rect.MinMaxRect(xMin - CompositeBlockPaddingX, yMin - CompositeBlockPaddingY, xMax + CompositeBlockPaddingX, yMax + CompositeBlockPaddingY);
		return true;
	}

	private Rect GetNodePlacementRect(NodeRuntime node, Vector3 position)
	{
		if (node != null && node.type == NodeType.Place)
		{
			GetPlacePlacementCircle(node, position, out Vector2 center, out float radius);
			return Rect.MinMaxRect(center.x - radius, center.y - radius, center.x + radius, center.y + radius);
		}

		return GetTransitionPlacementBounds(node, position);
	}

	private Vector2 GetCompositeBlockCenter(string blockId)
	{
		if (TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return bounds.center;
		}

		return Vector2.zero;
	}

	private bool MoveCompositeBlockInternal(string blockId, Vector2 desiredCenter, bool checkBlocked = true)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return false;
		}

		Vector2 delta = desiredCenter - bounds.center;
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.transform == null)
			{
				return false;
			}
		}

		if (checkBlocked && IsCompositeBlockPositionBlocked(blockId, desiredCenter))
		{
			return false;
		}

		if (delta.sqrMagnitude <= 0.000001f)
		{
			EnsureCompositeBlockVisuals();
			UpdateAllArcVisuals();
			return true;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			NodeRuntime node = nodesById[nodeIds[i]];
			Vector3 current = node.transform.position;
			node.transform.position = new Vector3(current.x + delta.x, current.y + delta.y, current.z);
		}

		EnsureCompositeBlockVisuals();
		UpdateAllArcVisuals();
		return true;
	}

	private Vector2 ClampCompositeBlockCenterToActorArea(string blockId, Vector2 desiredCenter, ulong actorClientId)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return ClampPositionToActorArea(desiredCenter, actorClientId, 0f);
		}

		float boundaryMargin = bounds.height * 0.5f;
		if (!singlePlayerMode && TryGetPlayerBoundCompositeBlockTopSide(blockId, out bool topSide))
		{
			return ClampPositionToSharedSide(desiredCenter, topSide, boundaryMargin);
		}

		return ClampPositionToActorArea(desiredCenter, actorClientId, boundaryMargin);
	}

	private bool TryClaimSharedCompositeBlock(string blockId, ulong actorClientId)
	{
		if (!IsCompositeBlockAvailableInSharedPool(blockId))
		{
			return false;
		}

		SetCompositeBlockSharedPoolState(blockId, actorClientId, false, true);
		RefreshPetriNetVisuals();
		return true;
	}

	private bool TryReturnSharedCompositeBlockToPool(string blockId, ulong actorClientId)
	{
		int poolIndex = GetCompositeBlockIndex(blockId);
		if (poolIndex < 0)
		{
			return false;
		}

		if (GetCompositeBlockOwner(blockId) != actorClientId)
		{
			return false;
		}

		Vector2 poolCenter = GetSharedPoolBlockSlotPositionByIndex(poolIndex);
		if (!IsCompositeBlockFullyInPoolZone(blockId, poolCenter))
		{
			return false;
		}

		if (IsCompositeBlockOverlappingOtherNodes(blockId, poolCenter))
		{
			return false;
		}

		if (HasExternalArcsForCompositeBlock(blockId))
		{
			return false;
		}

		if (!MoveCompositeBlockInternal(blockId, poolCenter, false))
		{
			return false;
		}

		SetCompositeBlockNodeHeight(blockId, GroundZ);
		SetCompositeBlockSharedPoolState(blockId, UnassignedOwnerClientId, true, true);
		RefreshPetriNetVisuals();
		return true;
	}

	private bool IsCompositeBlockPositionBlocked(string blockId, Vector2 desiredCenter)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return true;
		}

		Vector2 delta = desiredCenter - bounds.center;
		Rect desiredBounds = new Rect(bounds.x + delta.x, bounds.y + delta.y, bounds.width, bounds.height);
		if (enableSharedTransitionPool && DoTransitionBoundsOverlap(desiredBounds, GetSharedTransitionPoolRect()))
		{
			return true;
		}

		return IsCompositeBlockOverlappingOtherNodes(blockId, desiredCenter);
	}

	private bool IsCompositeBlockOverlappingOtherNodes(string blockId, Vector2 desiredCenter)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return true;
		}

		Vector2 delta = desiredCenter - bounds.center;
		Rect desiredBounds = new Rect(bounds.x + delta.x, bounds.y + delta.y, bounds.width, bounds.height);
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.transform == null || !node.transform.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (GetCompositeBlockIdForNodeId(node.id) == blockId)
			{
				continue;
			}

			if (node.type == NodeType.Place)
			{
				GetPlacePlacementCircle(node, node.transform.position, out Vector2 placeCenter, out float placeRadius);
				if (DoCircleRectOverlap(placeCenter, placeRadius, desiredBounds))
				{
					return true;
				}

				continue;
			}

			if (DoTransitionBoundsOverlap(desiredBounds, GetTransitionPlacementBounds(node, node.transform.position)))
			{
				return true;
			}
		}

		return false;
	}

	private bool IsCompositeBlockFullyInPoolZone(string blockId, Vector2 desiredCenter)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return false;
		}

		Vector2 delta = desiredCenter - bounds.center;
		Rect desiredBounds = new Rect(bounds.x + delta.x, bounds.y + delta.y, bounds.width, bounds.height);
		Rect poolRect = GetSharedTransitionPoolRect();
		return desiredBounds.xMin >= poolRect.xMin
			&& desiredBounds.xMax <= poolRect.xMax
			&& desiredBounds.yMin >= poolRect.yMin
			&& desiredBounds.yMax <= poolRect.yMax;
	}

	private bool IsCompositeBlockAvailableInSharedPool(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node))
			{
				return false;
			}

			if (node.type == NodeType.Transition && (!node.isSharedPoolTransition || !node.isSharedPoolAvailable))
			{
				return false;
			}
		}

		return true;
	}

	private bool IsCompositeBlockInSharedPool(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) && node.type == NodeType.Transition && node.isSharedPoolTransition)
			{
				return true;
			}
		}

		return false;
	}

	private ulong GetCompositeBlockOwner(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return UnassignedOwnerClientId;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (nodesById.TryGetValue(nodeIds[i], out NodeRuntime node))
			{
				return node.ownerClientId;
			}
		}

		return UnassignedOwnerClientId;
	}

	private bool CanActorPickupCompositeBlock(string blockId, ulong actorClientId)
	{
		if (IsPlayerBoundCompositeBlock(blockId))
		{
			return GetCompositeBlockOwner(blockId) == actorClientId;
		}

		if (IsCompositeBlockAvailableInSharedPool(blockId))
		{
			return true;
		}

		return GetCompositeBlockOwner(blockId) == actorClientId;
	}

	private void SetCompositeBlockSharedPoolState(string blockId, ulong ownerClientId, bool isAvailable, bool isInSharedPool)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return;
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node))
			{
				continue;
			}

			node.ownerClientId = ownerClientId;
			if (node.type == NodeType.Transition)
			{
				node.isSharedPoolTransition = isInSharedPool;
				node.isSharedPoolAvailable = isInSharedPool && isAvailable;
			}
		}
	}

	private bool HasExternalArcsForCompositeBlock(string blockId)
	{
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc == null || IsCompositeBlockInternalArc(arc))
			{
				continue;
			}

			if (GetCompositeBlockIdForNodeId(arc.fromId) == blockId || GetCompositeBlockIdForNodeId(arc.toId) == blockId)
			{
				return true;
			}
		}

		return false;
	}

	private bool CanDeleteCreatedCompositeBlock(string blockId, ulong actorClientId)
	{
		if (!IsCreatedCompositeBlockId(blockId) || GetCompositeBlockOwner(blockId) != actorClientId)
		{
			return false;
		}

		string outputPlaceId = GetCreatedCompositeBlockOutputPlaceId(blockId);
		if (string.IsNullOrEmpty(outputPlaceId) || !nodesById.TryGetValue(outputPlaceId, out NodeRuntime outputPlace))
		{
			return false;
		}

		return outputPlace.type == NodeType.Place && outputPlace.tokens <= 0;
	}

	private string GetCreatedCompositeBlockOutputPlaceId(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		return nodeIds != null && nodeIds.Length >= 2 ? nodeIds[1] : null;
	}

	private bool RemoveCompositeBlockInternal(string blockId)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null)
		{
			return false;
		}

		List<string> arcIdsToRemove = new List<string>();
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc == null)
			{
				continue;
			}

			if (GetCompositeBlockIdForNodeId(arc.fromId) == blockId || GetCompositeBlockIdForNodeId(arc.toId) == blockId)
			{
				arcIdsToRemove.Add(arc.id);
			}
		}

		for (int i = 0; i < arcIdsToRemove.Count; i++)
		{
			RemoveArcInternal(arcIdsToRemove[i], false);
		}

		for (int i = 0; i < nodeIds.Length; i++)
		{
			string nodeId = nodeIds[i];
			if (!nodesById.TryGetValue(nodeId, out NodeRuntime node))
			{
				continue;
			}

			if (connectStartNodeId == nodeId)
			{
				connectStartNodeId = null;
			}

			if (craneConnectStartNodeId == nodeId)
			{
				CancelCraneConnectPreview();
			}

			if (heldTransitionId == nodeId)
			{
				heldTransitionId = null;
			}

			if (heldPlaceId == nodeId)
			{
				heldPlaceId = null;
			}

			nodeByCollider.Remove(node.collider);
			nodesById.Remove(nodeId);
			if (node.transform != null)
			{
				Destroy(node.transform.gameObject);
			}
		}

		if (heldCompositeBlockId == blockId)
		{
			heldCompositeBlockId = null;
			heldCompositeBlockOffset = Vector2.zero;
		}

		if (draggedCompositeBlockId == blockId)
		{
			draggedCompositeBlockId = null;
		}

		if (pointerDownCompositeBlockId == blockId)
		{
			pointerDownCompositeBlockId = null;
		}

		RemoveCompositeBlockVisual(blockId);
		RefreshPetriNetVisuals();
		return true;
	}

	private void SetCompositeBlockActive(string blockId, bool active)
	{
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds != null)
		{
			for (int i = 0; i < nodeIds.Length; i++)
			{
				if (nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) && node.transform != null)
				{
					node.transform.gameObject.SetActive(active);
				}
			}
		}

		string[] arcIds = GetCompositeBlockArcIds(blockId);
		if (arcIds != null)
		{
			for (int i = 0; i < arcIds.Length; i++)
			{
				if (arcsById.TryGetValue(arcIds[i], out ArcRuntime arc) && arc.gameObject != null)
				{
					arc.gameObject.SetActive(active);
				}
			}
		}

		if (compositeBlocksById.TryGetValue(blockId, out CompositeBlockRuntime block) && block.gameObject != null)
		{
			block.gameObject.SetActive(active);
		}
	}

	private bool CanUseNodeAsExternalConnectionEndpoint(NodeRuntime node)
	{
		if (node == null)
		{
			return false;
		}

		if (!IsCompositeBlockNode(node))
		{
			return true;
		}

		return IsCompositeBlockFirstTransitionId(node.id) || IsCompositeBlockLastPlaceId(node.id);
	}

	private string GetIngredientPlaceIdForTransition(string transitionId)
	{
		if (!IsIngredientTransitionId(transitionId))
		{
			return null;
		}

		return "P_" + transitionId.Substring(2);
	}

	private string GetIngredientTransitionIdForPlace(string placeId)
	{
		if (!IsIngredientPlaceId(placeId))
		{
			return null;
		}

		return "T_" + placeId.Substring(2);
	}

	private bool IsArcAllowedByIngredientRules(string fromId, string toId)
	{
		if (!IsArcAllowedByPlayerExchangeRules(fromId, toId))
		{
			return false;
		}

		if (!IsArcAllowedByCompositeBlockRules(fromId, toId))
		{
			return false;
		}

		if (IsDeliveryTransitionId(fromId))
		{
			return false;
		}

		if (IsSharedPoolTrashTransitionId(fromId))
		{
			return false;
		}

		if (IsIngredientTransitionId(toId))
		{
			return false;
		}

		if (IsIngredientTransitionId(fromId))
		{
			return toId == GetIngredientPlaceIdForTransition(fromId);
		}

		if (IsIngredientPlaceId(toId))
		{
			return fromId == GetIngredientTransitionIdForPlace(toId);
		}

		return true;
	}

	private bool IsInsideSharedPoolHorizontal(float x)
	{
		return IsInsideSharedPoolHorizontal(x, 0f);
	}

	private bool IsInsideSharedPoolHorizontal(float x, float insideMargin)
	{
		float halfWidth = Mathf.Max(0f, GetSharedPoolHalfWidth() - insideMargin);
		return x >= -halfWidth && x <= halfWidth;
	}

	private Vector2 ClampPositionToSharedSide(Vector2 desired, bool topSide, float outsideBoundaryMargin)
	{
		if (!enableSharedTransitionPool)
		{
			return desired;
		}

		if (IsInsideSharedPoolZone(desired))
		{
			return desired;
		}

		bool insidePoolHorizontal = IsInsideSharedPoolHorizontal(desired.x);
		if (topSide)
		{
			float minY = insidePoolHorizontal ? sharedPoolY - sharedPoolHalfHeight : sharedPoolY + outsideBoundaryMargin;
			desired.y = Mathf.Max(desired.y, minY);
		}
		else
		{
			float maxY = insidePoolHorizontal ? sharedPoolY + sharedPoolHalfHeight : sharedPoolY - outsideBoundaryMargin;
			desired.y = Mathf.Min(desired.y, maxY);
		}

		return desired;
	}

	private Vector2 ClampPositionToActorArea(Vector2 desired, ulong actorClientId, float outsideBoundaryMargin)
	{
		if (!enableSharedTransitionPool || singlePlayerMode)
		{
			return desired;
		}

		return ClampPositionToSharedSide(desired, IsActorTopSide(actorClientId), outsideBoundaryMargin);
	}

	private Vector2 ClampMovableNodePositionToActorArea(NodeRuntime node, Vector2 desired, ulong actorClientId)
	{
		if (IsDeliveryTransition(node))
		{
			return ClampDeliveryTransitionPositionToOwnSide(node, desired);
		}

		return ClampPositionToActorArea(desired, actorClientId, 0f);
	}

	private Vector2 ClampDeliveryTransitionPositionToOwnSide(NodeRuntime transition, Vector2 desired)
	{
		if (!enableSharedTransitionPool || singlePlayerMode || !IsDeliveryTransition(transition))
		{
			return desired;
		}

		Rect bounds = GetTransitionPlacementBounds(transition, new Vector3(desired.x, desired.y, GroundZ));
		float halfHeight = Mathf.Max(NodeVisualFootprint * 0.5f, bounds.height * 0.5f);
		desired.y = Mathf.Min(desired.y, sharedPoolY - halfHeight);
		return desired;
	}

	private float GetAvatarBoundaryShadowHalfWidth()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId) && TryGetCompositeBlockBounds(heldCompositeBlockId, out Rect blockBounds))
		{
			return blockBounds.width * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			return NodeVisualFootprint * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			if (nodesById.TryGetValue(heldTransitionId, out NodeRuntime transition))
			{
				Rect bounds = GetTransitionPlacementBounds(transition, transition.transform.position);
				return bounds.width * 0.5f;
			}

			return NodeVisualFootprint * 0.5f;
		}

		float shadowScale = Mathf.Lerp(0.92f, 0.62f, Mathf.InverseLerp(avatarCraneLoweredHeight, avatarCraneRestHeight, avatarCraneCurrentHeight));
		return shadowScale * 0.5f;
	}

	private float GetAvatarBoundaryShadowHalfHeight()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId) && TryGetCompositeBlockBounds(heldCompositeBlockId, out Rect blockBounds))
		{
			return blockBounds.height * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			return NodeVisualFootprint * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			if (nodesById.TryGetValue(heldTransitionId, out NodeRuntime transition))
			{
				Rect bounds = GetTransitionPlacementBounds(transition, transition.transform.position);
				return bounds.height * 0.5f;
			}

			return NodeVisualFootprint * 0.5f;
		}

		float shadowScale = Mathf.Lerp(0.92f, 0.62f, Mathf.InverseLerp(avatarCraneLoweredHeight, avatarCraneRestHeight, avatarCraneCurrentHeight));
		return shadowScale * 0.52f * 0.5f;
	}

	private Vector3 ClampAvatarPositionToAllowedArea(Vector3 desired, ulong actorClientId)
	{
		if (enableSharedTransitionPool && !singlePlayerMode)
		{
			Vector2 current = new Vector2(avatarPosition.x, avatarPosition.y);
			Vector2 target = new Vector2(desired.x, desired.y);
			float shadowHalfWidth = GetAvatarBoundaryShadowHalfWidth();
			float shadowHalfHeight = GetAvatarBoundaryShadowHalfHeight();

			if (IsInsideSharedPoolZone(current))
			{
				bool topSide = IsActorTopSide(actorClientId);
				float poolHalfWidth = Mathf.Max(0f, GetSharedPoolHalfWidth() - shadowHalfWidth);
				float poolBottom = sharedPoolY - sharedPoolHalfHeight;
				float poolTop = sharedPoolY + sharedPoolHalfHeight;

				if (topSide)
				{
					target.y = Mathf.Max(target.y, poolBottom + shadowHalfHeight);
				}
				else
				{
					target.y = Mathf.Min(target.y, poolTop - shadowHalfHeight);
				}

				bool shadowOverlapsPoolVertically = target.y - shadowHalfHeight <= poolTop
					&& target.y + shadowHalfHeight >= poolBottom;
				bool targetOnOpponentSide = topSide
					? target.y < sharedPoolY + shadowHalfHeight
					: target.y > sharedPoolY - shadowHalfHeight;
				bool targetOutsidePoolSide = target.x < -poolHalfWidth || target.x > poolHalfWidth;
				if (shadowOverlapsPoolVertically && targetOnOpponentSide && targetOutsidePoolSide)
				{
					target.x = Mathf.Clamp(target.x, -poolHalfWidth, poolHalfWidth);
				}

				desired.x = target.x;
				desired.y = target.y;
			}
		}

		Vector2 clamped = ClampPositionToActorArea(new Vector2(desired.x, desired.y), actorClientId, GetAvatarBoundaryShadowHalfHeight());
		if (!string.IsNullOrEmpty(heldTransitionId)
			&& nodesById.TryGetValue(heldTransitionId, out NodeRuntime heldTransition)
			&& IsDeliveryTransition(heldTransition))
		{
			clamped = ClampDeliveryTransitionPositionToOwnSide(heldTransition, clamped);
		}

		return new Vector3(clamped.x, clamped.y, desired.z);
	}

	private Vector2 GetSharedPoolSlotPosition(string transitionId)
	{
		if (IsSharedPoolTrashTransitionId(transitionId))
		{
			return GetSharedPoolTrashTransitionPosition();
		}

		return new Vector2(0f, sharedPoolY);
	}

	private bool TryReturnSharedTransitionToPool(NodeRuntime node, ulong actorClientId, Vector2 desiredPosition)
	{
		if (node == null || node.type != NodeType.Transition)
		{
			return false;
		}

		// Allow returning transitions that were placed outside the pool back to the pool
		if (node.isSharedPoolTransition && node.isSharedPoolAvailable)
		{
			return false;
		}

		if (node.ownerClientId != actorClientId)
		{
			return false;
		}

		if (!IsInsideSharedPoolZone(desiredPosition))
		{
			return false;
		}

		if (!IsSharedPoolTrashTransitionId(node.id))
		{
			return false;
		}

		Vector2 poolPosition = GetSharedPoolSlotPosition(node.id);
		if (IsPositionBlockedByNode(new Vector3(poolPosition.x, poolPosition.y, 0f), node.id))
		{
			return false;
		}

		if (!IsTransitionFullyInPoolZone(node, poolPosition))
		{
			return false;
		}

		List<string> arcIdsToRemove = new List<string>();
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.fromId == node.id || arc.toId == node.id)
			{
				arcIdsToRemove.Add(arc.id);
			}
		}

		for (int i = 0; i < arcIdsToRemove.Count; i++)
		{
			RemoveArcInternal(arcIdsToRemove[i]);
		}

		node.transform.position = new Vector3(poolPosition.x, poolPosition.y, 0f);

		node.ownerClientId = UnassignedOwnerClientId;
		node.isSharedPoolTransition = true;
		node.isSharedPoolAvailable = true;

		return true;
	}

	private bool IsInsideSharedPoolZone(Vector2 worldPosition)
	{
		Rect poolRect = GetSharedTransitionPoolRect();

		return worldPosition.x >= poolRect.xMin && worldPosition.x <= poolRect.xMax
			&& worldPosition.y >= poolRect.yMin && worldPosition.y <= poolRect.yMax;
	}

	private Rect GetSharedTransitionPoolRect()
	{
		float halfWidth = GetSharedPoolHalfWidth();
		float halfHeight = sharedPoolHalfHeight;
		return Rect.MinMaxRect(-halfWidth, sharedPoolY - halfHeight, halfWidth, sharedPoolY + halfHeight);
	}

	private bool IsTransitionFullyInPoolZone(NodeRuntime transition, Vector2 transitionPosition)
	{
		Rect transitionBounds = GetTransitionPlacementBounds(transition, new Vector3(transitionPosition.x, transitionPosition.y, 0f));
		return IsTransitionBoundsFullyInPoolZone(transitionBounds);
	}

	private bool IsTransitionFullyInPoolZone(Vector2 transitionPosition)
	{
		Rect transitionBounds = GetTransitionPlacementBounds(null, new Vector3(transitionPosition.x, transitionPosition.y, 0f));
		return IsTransitionBoundsFullyInPoolZone(transitionBounds);
	}

	private bool IsTransitionBoundsFullyInPoolZone(Rect transitionBounds)
	{
		float halfWidth = GetSharedPoolHalfWidth();
		float halfHeight = sharedPoolHalfHeight;

		float poolLeft = -halfWidth;
		float poolRight = halfWidth;
		float poolBottom = sharedPoolY - halfHeight;
		float poolTop = sharedPoolY + halfHeight;

		return transitionBounds.xMin >= poolLeft
			&& transitionBounds.xMax <= poolRight
			&& transitionBounds.yMin >= poolBottom
			&& transitionBounds.yMax <= poolTop;
	}

	private void UpdateVisibilityForLocalPlayer()
	{
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			if (pair.Value.transform != null)
			{
				pair.Value.transform.gameObject.SetActive(true);
			}
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			if (pair.Value.gameObject != null)
			{
				pair.Value.gameObject.SetActive(true);
			}
		}
	}

	private Vector2 ClampPositionToPlayerZone(Vector2 desired, ulong actorClientId)
	{
		if (!enableNetworkAuthoritativeSync || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return desired;
		}

		return ClampPositionToActorArea(desired, actorClientId, 0f);
	}

	private Vector2 GetPlayerClaimedTransitionPosition(ulong actorClientId, string transitionId)
	{
		int ownedClaimedCount = 0;
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node.type != NodeType.Transition || !node.isSharedPoolTransition || node.isSharedPoolAvailable)
			{
				continue;
			}

			if (node.ownerClientId == actorClientId)
			{
				if (node.id == transitionId)
				{
					continue;
				}

				ownedClaimedCount++;
			}
		}

		float laneSpacing = GetSharedPoolTrashSlotWidth() + sharedPoolItemGap;
		float x = -0.5f * Mathf.Max(0, ownedClaimedCount - 1) * laneSpacing;
		float laneY = IsActorTopSide(actorClientId)
			? sharedPoolY + sharedPoolHalfHeight + 0.75f
			: sharedPoolY - sharedPoolHalfHeight - 0.75f;
		return new Vector2(x, laneY);
	}

	private float GetTransitionLabelCharacterSize(string label)
	{
		return TransitionLabelCharacterSize;
	}

	private float GetTransitionVisualWidthForDisplayName(string displayName)
	{
		return GetTransitionVisualWidthForFormattedLabel(FormatTransitionLabel(LocalizeVisibleText(displayName)));
	}

	private float GetTransitionVisualWidthForFormattedLabel(string formattedLabel)
	{
		if (string.IsNullOrEmpty(formattedLabel))
		{
			return NodeVisualFootprint;
		}

		int longestLineLength = Mathf.Max(1, GetLongestTransitionLabelLineLength(formattedLabel));
		float estimatedTextWidth = longestLineLength * TransitionLabelEstimatedCharacterWidth;
		float minimumWidth = formattedLabel.Contains("\n") ? TransitionMultilineMinimumVisualWidth : NodeVisualFootprint;
		return Mathf.Max(minimumWidth, estimatedTextWidth + TransitionLabelHorizontalPadding);
	}

	private void UpdateTransitionVisualDimensions(NodeRuntime node, string formattedLabel)
	{
		if (node == null)
		{
			return;
		}

		float width = GetTransitionVisualWidthForFormattedLabel(formattedLabel);
		if (IsIngredientTransitionId(node.id))
		{
			bool topSide = node.id.StartsWith("T_Top_Zutat_");
			width = GetWidestIngredientTransitionWidth(topSide);
		}

		Vector2 size = new Vector2(width, NodeVisualFootprint);

		if (node.renderer != null)
		{
			node.renderer.drawMode = SpriteDrawMode.Sliced;
			node.renderer.size = size;
		}

		if (node.collider is BoxCollider2D boxCollider)
		{
			boxCollider.size = size;
			boxCollider.offset = Vector2.zero;
		}

		if (node.visual3D != null)
		{
			node.visual3D.transform.localScale = new Vector3(width, NodeVisualFootprint, NodeVisualHeight);
		}
	}

	private string GetNodeDisplayName(NodeRuntime node)
	{
		if (node != null)
		{
			string displayName = node.displayName != null ? node.displayName.Trim() : "";
			if (!string.IsNullOrEmpty(displayName))
			{
				return displayName;
			}
		}

		return HumanizeId(node != null ? node.id : null);
	}

	private string FormatTransitionLabel(string label)
	{
		label = NormalizeTransitionLabelWhitespace(InsertKnownCompoundTransitionBreaks(InsertTransitionLabelBreakSpaces(label)));
		string[] words = label.Split(' ');
		if (words.Length < 2)
		{
			return label;
		}

		return WrapTransitionLabelToTwoLines(words);
	}

	private string WrapTransitionLabelToTwoLines(string[] words)
	{
		int bestSplitIndex = 1;
		int bestScore = int.MaxValue;
		for (int i = 1; i < words.Length; i++)
		{
			string left = JoinTransitionLabelWords(words, 0, i);
			string right = JoinTransitionLabelWords(words, i, words.Length - i);
			int longest = Mathf.Max(left.Length, right.Length);
			int score = Mathf.Abs(left.Length - right.Length) + longest;
			if (score < bestScore)
			{
				bestScore = score;
				bestSplitIndex = i;
			}
		}

		return JoinTransitionLabelWords(words, 0, bestSplitIndex) + "\n" + JoinTransitionLabelWords(words, bestSplitIndex, words.Length - bestSplitIndex);
	}

	private string JoinTransitionLabelWords(string[] words, int startIndex, int count)
	{
		StringBuilder result = new StringBuilder();
		for (int i = 0; i < count; i++)
		{
			if (i > 0)
			{
				result.Append(' ');
			}

			result.Append(words[startIndex + i]);
		}

		return result.ToString();
	}

	private string InsertKnownCompoundTransitionBreaks(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return "";
		}

		return ReplaceKnownCompoundTransitionBreak(label, "Suppengemüse", "Suppen-", "gemüse");
	}

	private string ReplaceKnownCompoundTransitionBreak(string label, string compound, string firstPart, string secondPart)
	{
		int index = 0;
		while (index < label.Length)
		{
			int matchIndex = label.IndexOf(compound, index, StringComparison.OrdinalIgnoreCase);
			if (matchIndex < 0)
			{
				return label;
			}

			string match = label.Substring(matchIndex, compound.Length);
			string replacement = BuildKnownCompoundTransitionBreakReplacement(match, firstPart, secondPart);
			label = label.Substring(0, matchIndex) + replacement + label.Substring(matchIndex + compound.Length);
			index = matchIndex + replacement.Length;
		}

		return label;
	}

	private string BuildKnownCompoundTransitionBreakReplacement(string match, string firstPart, string secondPart)
	{
		if (!string.IsNullOrEmpty(match) && char.IsLower(match[0]))
		{
			return firstPart.ToLowerInvariant() + " " + secondPart.ToLowerInvariant();
		}

		return firstPart + " " + secondPart;
	}

	private string InsertTransitionLabelBreakSpaces(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return "";
		}

		string result = label;
		for (int i = result.Length - 1; i > 0; i--)
		{
			char current = result[i];
			char previous = result[i - 1];
			if (current == ' ' || previous == ' ')
			{
				continue;
			}

			if (char.IsUpper(current) && (char.IsLower(previous) || char.IsDigit(previous)))
			{
				result = result.Insert(i, " ");
			}
		}

		return result;
	}

	private string NormalizeTransitionLabelWhitespace(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return "";
		}

		string trimmed = label.Trim();
		while (trimmed.Contains("  "))
		{
			trimmed = trimmed.Replace("  ", " ");
		}

		return trimmed;
	}

	private int GetLongestTransitionLabelLineLength(string label)
	{
		int longest = 0;
		int current = 0;
		for (int i = 0; i < label.Length; i++)
		{
			if (label[i] == '\n')
			{
				longest = Mathf.Max(longest, current);
				current = 0;
				continue;
			}

			current++;
		}

		return Mathf.Max(longest, current);
	}

	private void EnsureTypedTokenList(NodeRuntime placeNode)
	{
		if (placeNode == null || placeNode.type != NodeType.Place)
		{
			return;
		}

		if (placeNode.typedTokens == null)
		{
			placeNode.typedTokens = new List<TokenRuntime>();
		}

		while (placeNode.typedTokens.Count < placeNode.tokens)
		{
			placeNode.typedTokens.Add(CreateUntypedToken());
		}

		while (placeNode.typedTokens.Count > placeNode.tokens)
		{
			placeNode.typedTokens.RemoveAt(placeNode.typedTokens.Count - 1);
		}
	}

	private void SetUntypedTokenCount(NodeRuntime placeNode, int count)
	{
		if (placeNode == null || placeNode.type != NodeType.Place)
		{
			return;
		}

		placeNode.typedTokens = new List<TokenRuntime>();
		for (int i = 0; i < count; i++)
		{
			placeNode.typedTokens.Add(CreateUntypedToken());
		}

		placeNode.tokens = placeNode.typedTokens.Count;
	}

	private void AddTokenToPlace(NodeRuntime placeNode, TokenRuntime token)
	{
		if (placeNode == null || placeNode.type != NodeType.Place)
		{
			return;
		}

		EnsureTypedTokenList(placeNode);
		if (!CanAddTokensToPlace(placeNode, 1))
		{
			return;
		}

		placeNode.typedTokens.Add(CloneToken(token));
		placeNode.tokens = placeNode.typedTokens.Count;
	}

	private TokenRuntime TakeTokenFromPlace(NodeRuntime placeNode)
	{
		if (placeNode == null || placeNode.type != NodeType.Place)
		{
			return CreateUntypedToken();
		}

		EnsureTypedTokenList(placeNode);
		if (placeNode.typedTokens.Count <= 0)
		{
			placeNode.tokens = 0;
			return CreateUntypedToken();
		}

		TokenRuntime token = placeNode.typedTokens[0];
		placeNode.typedTokens.RemoveAt(0);
		placeNode.tokens = placeNode.typedTokens.Count;
		return token;
	}

	private TokenRuntime CreateUntypedToken()
	{
		return new TokenRuntime();
	}

	private TokenRuntime CreateIngredientToken(string ingredientName)
	{
		TokenRuntime token = new TokenRuntime();
		AddUniqueTokenValue(token.ingredients, ingredientName);
		NormalizeTokenIngredients(token);
		token.description = ingredientName != null ? ingredientName.Trim() : "";
		return token;
	}

	private TokenRuntime CloneToken(TokenRuntime source)
	{
		TokenRuntime clone = new TokenRuntime();
		if (source == null)
		{
			return clone;
		}

		clone.description = source.description ?? "";
		CopyTokenValues(source.ingredients, clone.ingredients);
		CopyTokenValues(source.states, clone.states);
		NormalizeTokenIngredients(clone);
		return clone;
	}

	private TokenRuntime CombineTokens(List<TokenRuntime> tokensToCombine)
	{
		TokenRuntime combined = new TokenRuntime();
		if (tokensToCombine == null)
		{
			return combined;
		}

		for (int i = 0; i < tokensToCombine.Count; i++)
		{
			TokenRuntime token = tokensToCombine[i];
			if (token == null)
			{
				continue;
			}

			CopyTokenValues(token.ingredients, combined.ingredients);
			CopyTokenValues(token.states, combined.states);
		}

		NormalizeTokenIngredients(combined);
		combined.description = JoinTokenDescriptions(tokensToCombine);
		return combined;
	}

	private void CopyTokenValues(List<string> from, List<string> to)
	{
		if (from == null || to == null)
		{
			return;
		}

		for (int i = 0; i < from.Count; i++)
		{
			AddUniqueTokenValue(to, from[i]);
		}
	}

	private void AddUniqueTokenValue(List<string> values, string value)
	{
		if (values == null)
		{
			return;
		}

		string trimmedValue = value != null ? value.Trim() : "";
		if (string.IsNullOrEmpty(trimmedValue))
		{
			return;
		}

		for (int i = 0; i < values.Count; i++)
		{
			if (string.Equals(values[i], trimmedValue, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}

		values.Add(trimmedValue);
	}

	private void NormalizeTokenIngredients(TokenRuntime token)
	{
		if (token == null || token.ingredients == null)
		{
			return;
		}

		token.ingredients.Sort(StringComparer.OrdinalIgnoreCase);
	}

	private string GetIngredientNameForTransition(string transitionId)
	{
		if (!IsIngredientTransitionId(transitionId))
		{
			return "";
		}

		bool topSide = transitionId.StartsWith("T_Top_Zutat_");
		return GetIngredientDisplayName(topSide, ExtractTrailingNumber(transitionId) - 1);
	}

	private string GetProcessingStateForTransition(string transitionId)
	{
		string blockId = GetCompositeBlockIdForNodeId(transitionId);
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		PoolBlockDefinition definition = GetCompositeBlockDefinition(blockId);
		if (definition == null || nodeIds == null)
		{
			return "";
		}

		if (IsSingleTransitionBlockDefinition(definition))
		{
			return transitionId == nodeIds[0] ? GetPoolBlockResultState(definition) : "";
		}

		return nodeIds.Length > 2 && transitionId == nodeIds[2] ? GetPoolBlockResultState(definition) : "";
	}

	private TokenRuntime CreateOutputTokenForTransition(string transitionId, List<TokenRuntime> consumedTokens)
	{
		TokenRuntime outputToken = IsIngredientTransitionId(transitionId)
			? CreateIngredientToken(GetIngredientNameForTransition(transitionId))
			: CombineTokens(consumedTokens);

		string processingState = GetProcessingStateForTransition(transitionId);
		string baseDescription = GetTokenDescription(outputToken);
		AddUniqueTokenValue(outputToken.states, processingState);
		if (!string.IsNullOrWhiteSpace(processingState))
		{
			if (ShouldWrapTokenBaseDescription(baseDescription, consumedTokens))
			{
				baseDescription = "(" + baseDescription + ")";
			}

			outputToken.description = baseDescription + " " + processingState.Trim();
		}

		return outputToken;
	}

	private bool ShouldWrapTokenBaseDescription(string baseDescription, List<TokenRuntime> consumedTokens)
	{
		string trimmedDescription = baseDescription != null ? baseDescription.Trim() : "";
		if (trimmedDescription.Length <= 0 || (trimmedDescription.StartsWith("(") && trimmedDescription.EndsWith(")")))
		{
			return false;
		}

		return GetNonEmptyTokenDescriptionCount(consumedTokens) > 1 || trimmedDescription.Contains(",");
	}

	private string GetTokenDescription(TokenRuntime token)
	{
		if (token == null)
		{
			return "unbekannt";
		}

		string description = token.description != null ? token.description.Trim() : "";
		if (!string.IsNullOrEmpty(description))
		{
			return description;
		}

		return GetTokenBaseDescription(token);
	}

	private string GetTokenBaseDescription(TokenRuntime token)
	{
		if (token == null)
		{
			return "unbekannt";
		}

		string ingredients = JoinTokenValues(token.ingredients, true);
		string states = JoinTokenValues(token.states);
		if (!string.IsNullOrEmpty(ingredients) && !string.IsNullOrEmpty(states))
		{
			return ingredients + " " + states;
		}

		if (!string.IsNullOrEmpty(ingredients))
		{
			return ingredients;
		}

		if (!string.IsNullOrEmpty(states))
		{
			return states;
		}

		return "unbekannt";
	}

	private string JoinTokenDescriptions(List<TokenRuntime> tokensToCombine)
	{
		if (tokensToCombine == null || tokensToCombine.Count <= 0)
		{
			return "";
		}

		List<string> descriptions = new List<string>();
		for (int i = 0; i < tokensToCombine.Count; i++)
		{
			TokenRuntime token = tokensToCombine[i];
			if (token == null)
			{
				continue;
			}

			string description = GetTokenDescription(token);
			if (string.IsNullOrEmpty(description) || description == "unbekannt")
			{
				continue;
			}

			AddUniqueTokenValue(descriptions, description);
		}

		descriptions.Sort(StringComparer.OrdinalIgnoreCase);

		StringBuilder result = new StringBuilder();
		for (int i = 0; i < descriptions.Count; i++)
		{
			if (result.Length > 0)
			{
				result.Append(", ");
			}

			result.Append(descriptions[i]);
		}

		return result.ToString();
	}

	private int GetNonEmptyTokenDescriptionCount(List<TokenRuntime> tokensToCombine)
	{
		if (tokensToCombine == null)
		{
			return 0;
		}

		int count = 0;
		for (int i = 0; i < tokensToCombine.Count; i++)
		{
			string description = tokensToCombine[i] != null ? GetTokenDescription(tokensToCombine[i]) : "";
			if (!string.IsNullOrEmpty(description) && description != "unbekannt")
			{
				count++;
			}
		}

		return count;
	}

	private string GetPlaceDebugLabel(NodeRuntime placeNode)
	{
		if (placeNode == null || placeNode.type != NodeType.Place)
		{
			return "";
		}

		EnsureTypedTokenList(placeNode);
		if (placeNode.typedTokens.Count <= 0)
		{
			return "";
		}

		int visibleCount = Mathf.Min(placeNode.typedTokens.Count, 3);
		StringBuilder label = new StringBuilder();
		for (int i = 0; i < visibleCount; i++)
		{
			if (i > 0)
			{
				label.Append('\n');
			}

			label.Append(GetLocalizedTokenDescription(placeNode.typedTokens[i]));
		}

		int hiddenCount = placeNode.typedTokens.Count - visibleCount;
		if (hiddenCount > 0)
		{
			label.Append("\n+");
			label.Append(hiddenCount);
		}

		return label.ToString();
	}

	private string JoinTokenValues(List<string> values, bool sortValues = false)
	{
		if (values == null || values.Count <= 0)
		{
			return "";
		}

		List<string> normalizedValues = new List<string>();
		for (int i = 0; i < values.Count; i++)
		{
			AddUniqueTokenValue(normalizedValues, values[i]);
		}

		if (sortValues)
		{
			normalizedValues.Sort(StringComparer.OrdinalIgnoreCase);
		}

		StringBuilder result = new StringBuilder();
		for (int i = 0; i < normalizedValues.Count; i++)
		{
			if (result.Length > 0)
			{
				result.Append(", ");
			}

			result.Append(normalizedValues[i]);
		}

		return result.ToString();
	}

	private string SanitizeTokenObjectName(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "unbekannt";
		}

		StringBuilder result = new StringBuilder();
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			result.Append(char.IsLetterOrDigit(c) ? c : '_');
		}

		return result.ToString();
	}

	private void RefreshTokenVisuals(NodeRuntime placeNode)
	{
		if (placeNode.tokenRoot == null)
		{
			return;
		}

		EnsureTypedTokenList(placeNode);
		for (int i = placeNode.tokenRoot.childCount - 1; i >= 0; i--)
		{
			Destroy(placeNode.tokenRoot.GetChild(i).gameObject);
		}

		int displayCount = Mathf.Min(placeNode.tokens, 12);
		for (int i = 0; i < displayCount; i++)
		{
			string tokenDescription = i < placeNode.typedTokens.Count ? GetTokenDescription(placeNode.typedTokens[i]) : "unbekannt";
			GameObject tokenObject = new GameObject("Token_" + (i + 1) + "_" + SanitizeTokenObjectName(tokenDescription));
			tokenObject.transform.SetParent(placeNode.tokenRoot, false);
			tokenObject.transform.localPosition = GetTokenLocalPosition(i, displayCount);
			CreatePrimitiveVisual3D(
				tokenObject.transform,
				"TokenSphere3D",
				PrimitiveType.Sphere,
				tokenColor,
				Vector3.zero,
				new Vector3(0.18f, 0.18f, 0.12f),
				Quaternion.identity);
		}
	}

	private void EnsureTimedPlaceProcessingVisual(NodeRuntime placeNode)
	{
		if (placeNode == null || placeNode.type != NodeType.Place || placeNode.processingDuration <= 0f || placeNode.processingBarRoot != null || placeNode.transform == null)
		{
			return;
		}

		GameObject root = new GameObject("ProcessingBar");
		root.transform.SetParent(placeNode.transform, false);
		root.transform.localPosition = new Vector3(0f, -0.72f, TokenLayerZ);

		GameObject backgroundObject = new GameObject("Background");
		backgroundObject.transform.SetParent(root.transform, false);
		backgroundObject.transform.localScale = new Vector3(0.78f, 0.08f, 1f);
		SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
		background.sprite = GetSquareSprite();
		background.color = new Color(0.04f, 0.05f, 0.06f, 0.38f);
		background.sortingOrder = 41;

		GameObject fillObject = new GameObject("Fill");
		fillObject.transform.SetParent(root.transform, false);
		fillObject.transform.localScale = new Vector3(0.78f, 0.08f, 1f);
		SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
		fill.sprite = GetSquareSprite();
		fill.color = new Color(0.05f, 0.65f, 0.9f, 0.9f);
		fill.sortingOrder = 42;

		placeNode.processingBarRoot = root;
		placeNode.processingBarFill = fill;
		root.SetActive(false);
	}

	private void UpdateTimedPlaceProcessingVisuals()
	{
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null)
			{
				continue;
			}

			if (node.type == NodeType.Place)
			{
				UpdateTimedPlaceProcessingVisual(node);
			}
			else if (node.renderer != null)
			{
				SetNodeVisualColor(node, IsTransitionEnabled(node.id) ? transitionEnabledColor : transitionDisabledColor);
			}
		}
	}

	private void UpdateTimedPlaceProcessingVisual(NodeRuntime placeNode)
	{
		if (placeNode == null || placeNode.processingDuration <= 0f)
		{
			return;
		}

		EnsureTimedPlaceProcessingVisual(placeNode);
		if (placeNode.processingBarRoot == null || placeNode.processingBarFill == null)
		{
			return;
		}

		float remaining = GetTimedPlaceProcessingRemaining(placeNode);
		bool active = placeNode.tokens > 0 && remaining > 0.001f;
		UpdateTimedPlaceActivityVisual(placeNode, active);
		placeNode.processingBarRoot.SetActive(active);
		if (!active)
		{
			return;
		}

		float progress = Mathf.Clamp01(remaining / Mathf.Max(0.001f, placeNode.processingDuration));
		float fullWidth = 0.78f;
		placeNode.processingBarFill.transform.localScale = new Vector3(fullWidth * progress, 0.08f, 1f);
		placeNode.processingBarFill.transform.localPosition = new Vector3(-fullWidth * (1f - progress) * 0.5f, 0f, 0f);
	}

	private float GetTimedPlaceProcessingRemaining(NodeRuntime placeNode)
	{
		if (placeNode == null || placeNode.processingDuration <= 0f || placeNode.tokens <= 0)
		{
			return 0f;
		}

		return Mathf.Max(0f, placeNode.processingReadyTime - Time.time);
	}

	private bool IsTimedPlaceProcessing(NodeRuntime placeNode)
	{
		return GetTimedPlaceProcessingRemaining(placeNode) > 0.001f;
	}

	private void HandlePlaceTokensChanged(NodeRuntime placeNode, int previousTokens)
	{
		if (placeNode == null || placeNode.processingDuration <= 0f)
		{
			return;
		}

		if (placeNode.tokens <= 0)
		{
			placeNode.processingReadyTime = 0f;
			return;
		}

		if (previousTokens > placeNode.tokens)
		{
			placeNode.processingReadyTime = Time.time + placeNode.processingDuration;
			return;
		}

		if (previousTokens <= 0 || placeNode.processingReadyTime <= 0f)
		{
			placeNode.processingReadyTime = Time.time + placeNode.processingDuration;
		}
	}

	private Vector3 GetTokenLocalPosition(int index, int totalCount)
	{
		if (totalCount <= 1)
		{
			return Vector3.zero;
		}

		float radius = totalCount <= 6 ? 0.27f : 0.34f;
		float angle = (360f / totalCount) * index;
		float rad = angle * Mathf.Deg2Rad;
		return new Vector3(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, -0.02f);
	}

	private void UpdateAllArcVisuals()
	{
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			UpdateArcVisual(pair.Value);
		}
	}

	private void UpdateArcVisual(ArcRuntime arc)
	{
		if (!nodesById.TryGetValue(arc.fromId, out NodeRuntime fromNode) || !nodesById.TryGetValue(arc.toId, out NodeRuntime toNode))
		{
			return;
		}

		Vector3 from = fromNode.transform.position;
		Vector3 to = toNode.transform.position;
		Vector3 dir = (to - from).normalized;
		if (dir.sqrMagnitude < 0.0001f)
		{
			return;
		}

		float fromOffset = GetNodeOffsetAlongDirection(fromNode, dir);
		float toOffset = GetNodeOffsetAlongDirection(toNode, -dir);

		Vector3 start = from + dir * fromOffset;
		Vector3 end = to - dir * toOffset;
		arc.kind = GetEffectiveArcKind(arc.fromId, arc.toId, arc.kind);

		if (arc.kind == ArcKind.Inhibitor)
		{
			SetLineWithInhibitorCircle(arc.body, arc.arrow, arc.inhibitorCircle, start, end, dir);
			HideArcLine(arc.resetArrow);
		}
		else if (arc.kind == ArcKind.Reset)
		{
			SetLineWithResetArrow(arc.body, arc.arrow, arc.resetArrow, start, end, dir);
			HideArcLine(arc.inhibitorCircle);
		}
		else
		{
			SetLineWithArrow(arc.body, arc.arrow, start, end, dir);
			HideArcLine(arc.resetArrow);
			HideArcLine(arc.inhibitorCircle);
		}

		arc.collider.points = new[] { new Vector2(start.x, start.y), new Vector2(end.x, end.y) };
		UpdateArcWeightLabel(arc, start, end, dir);
	}

	private void UpdateArcWeightLabel(ArcRuntime arc, Vector3 start, Vector3 end, Vector3 dir)
	{
		if (arc == null || arc.weightLabel == null)
		{
			return;
		}

		if (arc.weight <= 1 || arc.kind == ArcKind.Inhibitor)
		{
			arc.weightLabel.gameObject.SetActive(false);
			return;
		}

		Vector3 mid = (start + end) * 0.5f;
		Vector3 normal = new Vector3(dir.y, -dir.x, 0f).normalized;
		Vector3 labelPosition = mid + normal * 0.22f + new Vector3(0f, 0f, NodeLabelLayerZ);
		Rect blockBoundsForLabel = new Rect();
		bool constrainToBlock = false;
		string blockId = GetCompositeBlockIdForNodeId(arc.fromId);
		if (!string.IsNullOrEmpty(blockId) && blockId == GetCompositeBlockIdForNodeId(arc.toId) && TryGetCompositeBlockBounds(blockId, out Rect blockBounds))
		{
			const float labelInset = 0.18f;
			float distanceToEdge = GetDistanceFromPointToRectEdge(mid, normal, blockBounds) - labelInset;
			if (distanceToEdge > 0.01f)
			{
				labelPosition = mid + normal * (distanceToEdge * 0.5f) + new Vector3(0f, 0f, NodeLabelLayerZ);
			}

			blockBoundsForLabel = Rect.MinMaxRect(blockBounds.xMin + labelInset, blockBounds.yMin + labelInset, blockBounds.xMax - labelInset, blockBounds.yMax - labelInset);
			labelPosition.x = Mathf.Clamp(labelPosition.x, blockBoundsForLabel.xMin, blockBoundsForLabel.xMax);
			labelPosition.y = Mathf.Clamp(labelPosition.y, blockBoundsForLabel.yMin, blockBoundsForLabel.yMax);
			constrainToBlock = true;
		}

		arc.weightLabel.gameObject.SetActive(true);
		arc.weightLabel.text = arc.weight.ToString();
		arc.weightLabel.characterSize = ArcWeightLabelCharacterSize;
		PositionArcWeightLabel(arc.weightLabel, labelPosition, constrainToBlock, blockBoundsForLabel);
	}

	private void PositionArcWeightLabel(TextMesh label, Vector3 targetPosition, bool constrainToBlock, Rect blockBounds)
	{
		if (label == null)
		{
			return;
		}

		label.transform.position = targetPosition;
		MeshRenderer renderer = label.GetComponent<MeshRenderer>();
		if (renderer == null)
		{
			return;
		}

		Bounds visibleBounds = renderer.bounds;
		Vector3 visualCenterCorrection = targetPosition - visibleBounds.center;
		label.transform.position += visualCenterCorrection;

		if (!constrainToBlock)
		{
			return;
		}

		visibleBounds = renderer.bounds;
		Vector3 blockCorrection = Vector3.zero;
		if (visibleBounds.min.x < blockBounds.xMin)
		{
			blockCorrection.x += blockBounds.xMin - visibleBounds.min.x;
		}
		else if (visibleBounds.max.x > blockBounds.xMax)
		{
			blockCorrection.x -= visibleBounds.max.x - blockBounds.xMax;
		}

		if (visibleBounds.min.y < blockBounds.yMin)
		{
			blockCorrection.y += blockBounds.yMin - visibleBounds.min.y;
		}
		else if (visibleBounds.max.y > blockBounds.yMax)
		{
			blockCorrection.y -= visibleBounds.max.y - blockBounds.yMax;
		}

		label.transform.position += blockCorrection;
	}

	private float GetDistanceFromPointToRectEdge(Vector3 point, Vector3 direction, Rect rect)
	{
		float distance = float.PositiveInfinity;
		if (Mathf.Abs(direction.x) > 0.0001f)
		{
			float edgeX = direction.x > 0f ? rect.xMax : rect.xMin;
			distance = Mathf.Min(distance, (edgeX - point.x) / direction.x);
		}

		if (Mathf.Abs(direction.y) > 0.0001f)
		{
			float edgeY = direction.y > 0f ? rect.yMax : rect.yMin;
			distance = Mathf.Min(distance, (edgeY - point.y) / direction.y);
		}

		return float.IsInfinity(distance) ? 0f : Mathf.Max(0f, distance);
	}

	private void SetLineWithArrow(LineRenderer body, LineRenderer arrow, Vector3 start, Vector3 end, Vector3 dir)
	{
		Vector3 zOffset = new Vector3(0f, 0f, ArcZ);
		if (body != null)
		{
			body.SetPosition(0, start + zOffset);
			body.SetPosition(1, end + zOffset);
		}

		SetArrowHead(arrow, end, dir);
	}

	private void SetLineWithResetArrow(LineRenderer body, LineRenderer arrow, LineRenderer resetArrow, Vector3 start, Vector3 end, Vector3 dir)
	{
		SetLineWithArrow(body, arrow, start, end, dir);
		SetArrowHead(resetArrow, end - dir * (arrowHeadLength * 0.72f), dir);
	}

	private void SetArrowHead(LineRenderer arrow, Vector3 end, Vector3 dir)
	{
		if (arrow == null)
		{
			return;
		}

		arrow.gameObject.SetActive(true);
		arrow.positionCount = 3;
		Vector3 zOffset = new Vector3(0f, 0f, ArcZ);
		Vector3 leftDir = Quaternion.Euler(0f, 0f, 180f - arrowHeadAngle) * dir;
		Vector3 rightDir = Quaternion.Euler(0f, 0f, 180f + arrowHeadAngle) * dir;
		arrow.SetPosition(0, end + leftDir * arrowHeadLength + zOffset);
		arrow.SetPosition(1, end + zOffset);
		arrow.SetPosition(2, end + rightDir * arrowHeadLength + zOffset);
	}

	private void HideArcLine(LineRenderer line)
	{
		if (line != null)
		{
			line.gameObject.SetActive(false);
		}
	}

	private void SetLineWithInhibitorCircle(LineRenderer body, LineRenderer arrow, LineRenderer inhibitorCircle, Vector3 start, Vector3 end, Vector3 dir)
	{
		Vector3 zOffset = new Vector3(0f, 0f, ArcZ);
		float transitionClearance = arcWidth * 0.5f;
		Vector3 circleCenter = end - dir * (InhibitorCircleRadius + transitionClearance);
		Vector3 lineEnd = circleCenter - dir * InhibitorCircleRadius;
		if (body != null)
		{
			body.SetPosition(0, start + zOffset);
			body.SetPosition(1, lineEnd + zOffset);
		}

		if (arrow != null)
		{
			arrow.gameObject.SetActive(false);
		}

		if (inhibitorCircle == null)
		{
			return;
		}

		inhibitorCircle.gameObject.SetActive(true);
		Vector3 tangent = new Vector3(-dir.y, dir.x, 0f).normalized;
		for (int i = 0; i < inhibitorCircle.positionCount; i++)
		{
			float angle = (Mathf.PI * 2f * i) / inhibitorCircle.positionCount;
			Vector3 offset = Mathf.Cos(angle) * tangent * InhibitorCircleRadius
				+ Mathf.Sin(angle) * dir * InhibitorCircleRadius;
			inhibitorCircle.SetPosition(i, circleCenter + offset + zOffset);
		}
	}

	private float GetNodeOffsetAlongDirection(NodeRuntime node, Vector3 direction)
	{
		if (node.type == NodeType.Place)
		{
			GetPlacePlacementCircle(node, node.transform.position, out _, out float placeRadius);
			return placeRadius * 0.96f;
		}

		Rect bounds = GetTransitionPlacementBounds(node, node.transform.position);
		Vector2 ext = new Vector2(bounds.width * 0.5f, bounds.height * 0.5f);
		float dx = Mathf.Abs(direction.x);
		float dy = Mathf.Abs(direction.y);
		float xDistance = dx > 0.0001f ? ext.x / dx : float.PositiveInfinity;
		float yDistance = dy > 0.0001f ? ext.y / dy : float.PositiveInfinity;
		float edgeDistance = Mathf.Min(xDistance, yDistance);
		if (float.IsInfinity(edgeDistance))
		{
			return Mathf.Max(ext.x, ext.y);
		}

		return edgeDistance;
	}

	private TextMesh CreateNodeLabel(Transform nodeTransform, Vector3 localOffset, float characterSize)
	{
		GameObject labelObject = new GameObject("Label");
		labelObject.transform.SetParent(nodeTransform, false);
		labelObject.transform.localPosition = new Vector3(localOffset.x, localOffset.y, localOffset.z + NodeLabelLayerZ);

		TextMesh label = labelObject.AddComponent<TextMesh>();
		label.text = "";
		label.characterSize = characterSize;
		label.fontSize = 64;
		label.anchor = TextAnchor.MiddleCenter;
		label.alignment = TextAlignment.Center;
		label.color = new Color(0.13f, 0.16f, 0.2f);

		MeshRenderer labelRenderer = labelObject.GetComponent<MeshRenderer>();
		if (labelRenderer != null)
		{
			labelRenderer.sortingOrder = 50;
		}

		return label;
	}

	private string HumanizeId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return "Node";
		}

		if (IsDeliveryTransitionId(id))
		{
			return "Ausliefern";
		}

		if (IsSharedPoolTrashTransitionId(id))
		{
			return GetSharedPoolTrashTransitionDisplayName();
		}

		string compositeName = GetCompositeBlockDisplayNameForNodeId(id);
		if (!string.IsNullOrEmpty(compositeName))
		{
			return compositeName;
		}

		string ingredientName = GetIngredientDisplayNameForNodeId(id);
		if (!string.IsNullOrEmpty(ingredientName))
		{
			return ingredientName;
		}

		if (id.Contains("_Out"))
		{
			return "Out";
		}

		if (id.Contains("_In"))
		{
			return "In";
		}

		string result = id;
		if (id.Length > 2 && id[1] == '_' && (id[0] == 'P' || id[0] == 'T'))
		{
			result = id.Substring(2);
		}

		return result.Replace("_", " ");
	}

	private Material GetArcMaterial()
	{
		if (arcMaterial != null)
		{
			return arcMaterial;
		}

		if (runtimeArcMaterial == null)
		{
			runtimeArcMaterial = new Material(Shader.Find("Sprites/Default"));
		}

		return runtimeArcMaterial;
	}

	private Sprite GetCircleSprite()
	{
		if (circleSprite != null)
		{
			return circleSprite;
		}

		const int size = 64;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.filterMode = FilterMode.Bilinear;
		texture.wrapMode = TextureWrapMode.Clamp;

		Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
		float radius = size * 0.48f;
		Color[] pixels = new Color[size * size];

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
				pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
			}
		}

		texture.SetPixels(pixels);
		texture.Apply();
		circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
		return circleSprite;
	}

	private Sprite GetSquareSprite()
	{
		if (squareSprite != null)
		{
			return squareSprite;
		}

		const int size = 2;
		Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
		texture.filterMode = FilterMode.Point;
		texture.wrapMode = TextureWrapMode.Clamp;
		Color[] pixels = { Color.white, Color.white, Color.white, Color.white };
		texture.SetPixels(pixels);
		texture.Apply();
		squareSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
		return squareSprite;
	}
}
