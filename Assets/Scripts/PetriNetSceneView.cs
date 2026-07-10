using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class GameManager
{
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

		if (createDefaultLightIfMissing && FindAnyObjectByType<Light>() == null)
		{
			GameObject lightObject = new GameObject("Directional Light");
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.transform.rotation = Quaternion.Euler(45f, -20f, 0f);
			light.intensity = 0.9f;
		}

		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}
	}

	private void ConfigureCamera(Camera camera)
	{
		camera.transform.position = new Vector3(0f, 0f, -10f);
		camera.transform.rotation = Quaternion.identity;
		camera.orthographic = true;
		camera.orthographicSize = GetSharedScreenCameraSize();
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = new Color(0.95f, 0.96f, 0.98f);
	}

	private float GetSharedScreenCameraSize()
	{
		if (!enableSharedTransitionPool)
		{
			return 3.6f;
		}

		return Mathf.Max(4.8f, playerZoneYSpacing + sharedPoolHalfHeight + 1.4f);
	}

	private void EnsureGraphRootExists()
	{
		if (petriNetRoot != null)
		{
			return;
		}

		Transform existing = transform.Find(petriNetRootName);
		if (existing != null)
		{
			petriNetRoot = existing;
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
			ulong bottomOwner = GetFirstOtherConnectedClientId(topOwner);
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

		CreatePlaceNode("P_Top_In", new Vector2(-horizontalOffset, topY), 0, false, topOwnerClientId, false, false);
		CreateTransitionNode("T_Top_Out", new Vector2(horizontalOffset, topY), false, topOwnerClientId, false, false);

		CreatePlaceNode("P_Bottom_In", new Vector2(horizontalOffset, bottomY), 0, false, bottomOwnerClientId, false, false);
		CreateTransitionNode("T_Bottom_Out", new Vector2(-horizontalOffset, bottomY), false, bottomOwnerClientId, false, false);

		CreateArcInternal("A_Top_1", "T_Top_Out", "P_Bottom_In", 1, false, topOwnerClientId);
		CreateArcInternal("A_Bottom_1", "T_Bottom_Out", "P_Top_In", 1, false, bottomOwnerClientId);

		CreateIngredientSourceNodes(true, topOwnerClientId);
		CreateIngredientSourceNodes(false, bottomOwnerClientId);
		CreateTransitionNode("T_Bottom_Ausliefern", GetDeliveryTransitionPosition(), false, bottomOwnerClientId, false, false);

		placeCounter = 1;
		transitionCounter = 1;
		arcCounter = 1;
		collaborativeLayoutApplied = true;

		// Initialize avatars
		avatarPosition = GetDefaultAvatarStartPosition(GetLocalActorClientId());
		avatarStartPositionApplied = true;
		avatarRotation = 0f;
		heldTransitionId = null;
		heldPlaceId = null;
		heldCompositeBlockId = null;
		heldCompositeBlockOffset = Vector2.zero;
		pendingCreatedPlacePickup = false;
		lastAvatarPosition = avatarPosition;
		lastAvatarNetworkSyncRotation = avatarRotation;
		lastAvatarNetworkSyncHeldId = "";
		SeedRemoteAvatarStartPosition(topOwnerClientId);
		SeedRemoteAvatarStartPosition(bottomOwnerClientId);

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

		CreateTransitionNode(nodeIds[0], center + new Vector2(-2.25f, 0f), false, ownerClientId, isSharedPoolBlock, isSharedPoolAvailable);
		CreatePlaceNode(nodeIds[1], center + new Vector2(-0.75f, 0f), 0, false, ownerClientId, false, false);
		CreateTransitionNode(nodeIds[2], center + new Vector2(0.75f, 0f), false, ownerClientId, isSharedPoolBlock, isSharedPoolAvailable);
		CreatePlaceNode(nodeIds[3], center + new Vector2(2.25f, 0f), 0, false, ownerClientId, false, false);

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

		CreateArcInternal(arcIds[0], nodeIds[0], nodeIds[1], 1, false, ownerClientId);
		CreateArcInternal(arcIds[1], nodeIds[1], nodeIds[2], 1, false, ownerClientId);
		CreateArcInternal(arcIds[2], nodeIds[2], nodeIds[3], 1, false, ownerClientId);
		EnsureCompositeBlockVisuals();
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
		remoteAvatarInventories[clientId] = "";
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

		avatarPosition = GetDefaultAvatarStartPosition(GetLocalActorClientId());
		avatarRotation = 0f;
		avatarStartPositionApplied = true;
		lastAvatarPosition = avatarPosition;
		lastAvatarNetworkSyncRotation = avatarRotation;
		lastAvatarNetworkSyncHeldId = heldTransitionId ?? "";
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
		avatarStartPositionApplied = false;
		heldTransitionId = null;
		heldPlaceId = null;
		heldCompositeBlockId = null;
		heldCompositeBlockOffset = Vector2.zero;
		pendingCreatedPlaceExistingIds.Clear();
		DestroyCraneConnectPreviewVisual();
		DestroyCraneHoverSelectionVisual();
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
		remoteAvatarPositions.Clear();
		remoteAvatarRotations.Clear();
		remoteAvatarInventories.Clear();
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
		nodeObject.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

		SpriteRenderer renderer = nodeObject.AddComponent<SpriteRenderer>();
		renderer.sprite = GetCircleSprite();
		renderer.sortingOrder = 30;
		renderer.color = placeColor;
		if (placeMaterial != null)
		{
			renderer.sharedMaterial = placeMaterial;
		}

		CircleCollider2D collider = nodeObject.AddComponent<CircleCollider2D>();

		Transform tokenRoot = new GameObject("Tokens").transform;
		tokenRoot.SetParent(nodeObject.transform, false);
		tokenRoot.localPosition = new Vector3(0f, 0f, -0.02f);

		TextMesh label = CreateNodeLabel(nodeObject.transform, new Vector3(0f, -1.1f, 0f), 0.08f);

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
			collider = collider,
			label = label,
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
		nodeObject.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

		SpriteRenderer renderer = nodeObject.AddComponent<SpriteRenderer>();
		renderer.sprite = GetSquareSprite();
		renderer.sortingOrder = 30;
		renderer.color = transitionEnabledColor;
		if (transitionMaterial != null)
		{
			renderer.sharedMaterial = transitionMaterial;
		}

		BoxCollider2D collider = nodeObject.AddComponent<BoxCollider2D>();
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

	private bool CreateArcInternal(string arcId, string fromId, string toId, int weight, bool refreshVisuals, ulong ownerClientId)
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

		if (fromNode.type == toNode.type)
		{
			Debug.LogWarning("Arc rejected: only Place->Transition or Transition->Place is allowed.");
			return false;
		}

		if (!IsArcAllowedByIngredientRules(fromId, toId))
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

		LineRenderer body = arcObject.AddComponent<LineRenderer>();
		body.positionCount = 2;
		body.useWorldSpace = true;
		body.sortingOrder = 24;
		body.startWidth = arcWidth;
		body.endWidth = arcWidth;
		body.numCapVertices = 4;
		body.material = GetArcMaterial();
		body.startColor = new Color(0.18f, 0.2f, 0.25f);
		body.endColor = new Color(0.18f, 0.2f, 0.25f);

		GameObject arrowObject = new GameObject("Arrow");
		arrowObject.transform.SetParent(arcObject.transform, false);
		LineRenderer arrow = arrowObject.AddComponent<LineRenderer>();
		arrow.positionCount = 3;
		arrow.useWorldSpace = true;
		arrow.sortingOrder = 25;
		arrow.startWidth = arcWidth;
		arrow.endWidth = arcWidth;
		arrow.numCapVertices = 4;
		arrow.material = GetArcMaterial();
		arrow.startColor = new Color(0.18f, 0.2f, 0.25f);
		arrow.endColor = new Color(0.18f, 0.2f, 0.25f);

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
			gameObject = arcObject,
			body = body,
			arrow = arrow,
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

	private bool RemoveArcInternal(string arcId)
	{
		if (!arcsById.TryGetValue(arcId, out ArcRuntime arc))
		{
			return false;
		}

		arcByCollider.Remove(arc.collider);
		arcsById.Remove(arcId);
		Destroy(arc.gameObject);
		RefreshPetriNetVisuals();
		return true;
	}

	private void RefreshPetriNetVisuals()
	{
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node.type == NodeType.Place)
			{
				node.label.text = GetPlaceDebugLabel(node);
				node.label.color = Color.black;
				node.label.characterSize = 0.04f;
				node.label.lineSpacing = 0.78f;
				RefreshTokenVisuals(node);
				SetPlaceSorting(node, node.id == heldPlaceId || IsHeldCompositeBlockNode(node));
			}
			else
			{
				string transitionLabel = FormatTransitionLabel(GetNodeDisplayName(node));
				node.label.text = transitionLabel;
				node.label.color = Color.black;
				node.label.characterSize = GetTransitionLabelCharacterSize(transitionLabel);
				node.label.lineSpacing = transitionLabel.Contains("\n") ? 0.78f : 1f;
				FitTransitionLabelInsideNode(node);
				SetTransitionSorting(node, node.id == heldTransitionId || IsHeldCompositeBlockNode(node));
				node.renderer.color = IsTransitionEnabled(node.id) ? transitionEnabledColor : transitionDisabledColor;
			}
		}

		EnsureCompositeBlockVisuals();
		UpdateAllArcVisuals();
		UpdateTimedPlaceProcessingVisuals();
		UpdateVisibilityForLocalPlayer();
	}

	private GameObject localAvatarVisual;
	private GameObject localAvatarArrow;
	private GameObject localAvatarShadow;
	private GameObject localAvatarCable;
	private GameObject localHeldNodeShadow;
	private GameObject localCraneHoverNodeShadow;
	private GameObject localCraneHoverArcHighlight;
	private LineRenderer localCraneHoverArcBody;
	private LineRenderer localCraneHoverArcArrow;
	private GameObject localCraneConnectPreview;
	private LineRenderer localCraneConnectPreviewBody;
	private LineRenderer localCraneConnectPreviewArrow;
	private Dictionary<ulong, GameObject> remoteAvatarVisuals = new Dictionary<ulong, GameObject>();

	private Color GetAvatarColor(ulong clientId)
	{
		// Player 1 (ClientId 0) = Grün, Player 2 (ClientId 1) = Rot
		if (clientId == 0)
			return new Color(0.2f, 0.8f, 0.2f, 0.9f); // Grün
		else
			return new Color(0.8f, 0.2f, 0.2f, 0.9f); // Rot
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

		if (localAvatarShadow == null)
		{
			localAvatarShadow = new GameObject("LocalAvatarShadow");
			localAvatarShadow.transform.SetParent(petriNetRoot);
			SpriteRenderer shadowRenderer = localAvatarShadow.AddComponent<SpriteRenderer>();
			shadowRenderer.sprite = GetCircleSprite();
			shadowRenderer.color = new Color(0.02f, 0.03f, 0.04f, 0.28f);
			shadowRenderer.sortingOrder = 49;
			localAvatarShadow.transform.localScale = new Vector3(0.72f, 0.38f, 1f);
		}

		if (localHeldNodeShadow == null)
		{
			localHeldNodeShadow = new GameObject("LocalHeldNodeShadow");
			localHeldNodeShadow.transform.SetParent(petriNetRoot);
			SpriteRenderer shadowRenderer = localHeldNodeShadow.AddComponent<SpriteRenderer>();
			shadowRenderer.sprite = GetSquareSprite();
			shadowRenderer.color = new Color(0.02f, 0.03f, 0.04f, 0.24f);
			shadowRenderer.sortingOrder = 49;
			localHeldNodeShadow.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
			localHeldNodeShadow.SetActive(false);
		}

		if (localAvatarCable == null)
		{
			localAvatarCable = new GameObject("LocalAvatarCable");
			localAvatarCable.transform.SetParent(petriNetRoot);
			LineRenderer cable = localAvatarCable.AddComponent<LineRenderer>();
			cable.positionCount = 2;
			cable.useWorldSpace = true;
			cable.sortingOrder = 52;
			cable.startWidth = 0.035f;
			cable.endWidth = 0.035f;
			cable.material = GetArcMaterial();
			cable.startColor = new Color(0.08f, 0.1f, 0.13f, 0.55f);
			cable.endColor = cable.startColor;
		}

		if (localAvatarVisual == null)
		{
			localAvatarVisual = new GameObject("LocalAvatar");
			localAvatarVisual.transform.SetParent(petriNetRoot);
			SpriteRenderer spriteRenderer = localAvatarVisual.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = GetCircleSprite();
			spriteRenderer.color = GetAvatarColor(GetLocalActorClientId());
			spriteRenderer.sortingOrder = 60;
			localAvatarVisual.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

			// Trigger only: the crane flies over graph nodes and uses input logic for interactions.
			CircleCollider2D collider = localAvatarVisual.AddComponent<CircleCollider2D>();
			collider.radius = 0.4f;
			collider.isTrigger = true;
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

		if (localAvatarShadow != null)
		{
			float shadowScale = Mathf.Lerp(0.92f, 0.62f, Mathf.InverseLerp(avatarCraneLoweredHeight, avatarCraneRestHeight, avatarCraneCurrentHeight));
			localAvatarShadow.SetActive(!isHoldingTransition);
			localAvatarShadow.transform.position = new Vector3(avatarPosition.x, avatarPosition.y, -0.01f);
			localAvatarShadow.transform.localScale = new Vector3(shadowScale, shadowScale * 0.52f, 1f);
		}

		UpdateHeldTransitionVisual();
		UpdateHeldPlaceVisual();
		UpdateHeldCompositeBlockVisual();
		UpdateCraneHoverSelectionVisual(isHoldingNode);

		if (localHeldNodeShadow != null)
		{
			localHeldNodeShadow.SetActive(isHoldingNode);
			SpriteRenderer shadowRenderer = localHeldNodeShadow.GetComponent<SpriteRenderer>();
			if (shadowRenderer != null)
			{
				shadowRenderer.sprite = isHoldingPlace ? GetCircleSprite() : GetSquareSprite();
			}

			if (isHoldingCompositeBlock && TryGetCompositeBlockBounds(heldCompositeBlockId, out Rect blockBounds))
			{
				Vector2 shadowCenter = GetHeldCompositeBlockGroundCenter();
				localHeldNodeShadow.transform.position = new Vector3(shadowCenter.x, shadowCenter.y, -0.015f);
				localHeldNodeShadow.transform.localScale = new Vector3(blockBounds.width, blockBounds.height, 1f);
			}
			else
			{
				localHeldNodeShadow.transform.position = new Vector3(avatarPosition.x, avatarPosition.y, -0.015f);
				localHeldNodeShadow.transform.localScale = isHoldingPlace
					? new Vector3(1.05f, 0.58f, 1f)
					: new Vector3(0.9f, 0.9f, 1f);
			}
		}

		// Update local avatar dot
		if (localAvatarVisual != null)
		{
			float bodyScale = Mathf.Lerp(0.72f, 0.86f, Mathf.InverseLerp(avatarCraneLoweredHeight, avatarCraneRestHeight, avatarCraneCurrentHeight));
			localAvatarVisual.transform.position = craneVisualPosition;
			localAvatarVisual.transform.localScale = new Vector3(bodyScale, bodyScale, 1f);
		}

		if (localAvatarCable != null)
		{
			LineRenderer cable = localAvatarCable.GetComponent<LineRenderer>();
			if (cable != null)
			{
				cable.SetPosition(0, new Vector3(avatarPosition.x, avatarPosition.y, -0.02f));
				cable.SetPosition(1, new Vector3(craneVisualPosition.x, craneVisualPosition.y, -0.02f));
			}
		}

		UpdateCraneConnectPreviewVisual();
		UpdateRemoteAvatarVisuals();
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

		bool isHoldingTransition = remoteAvatarInventories.TryGetValue(clientId, out string heldId) && !string.IsNullOrEmpty(heldId);
		SpriteRenderer shadow = EnsureRemoteAvatarSprite(root.transform, "Shadow", GetCircleSprite(), new Color(0.02f, 0.03f, 0.04f, 0.28f), 49);
		float shadowScale = Mathf.Lerp(0.92f, 0.62f, Mathf.InverseLerp(avatarCraneLoweredHeight, avatarCraneRestHeight, avatarCraneRestHeight));
		shadow.gameObject.SetActive(!isHoldingTransition);
		shadow.transform.localPosition = new Vector3(0f, 0f, -0.01f);
		shadow.transform.localScale = new Vector3(shadowScale, shadowScale * 0.52f, 1f);

		LineRenderer cable = EnsureRemoteAvatarCable(root.transform);
		Vector3 ground = root.transform.position;
		Vector3 crane = ground + new Vector3(0f, avatarCraneRestHeight, -0.05f);
		cable.SetPosition(0, new Vector3(ground.x, ground.y, -0.02f));
		cable.SetPosition(1, new Vector3(crane.x, crane.y, -0.02f));

		SpriteRenderer body = EnsureRemoteAvatarSprite(root.transform, "Body", GetCircleSprite(), GetAvatarColor(clientId), 60);
		body.transform.localPosition = new Vector3(0f, avatarCraneRestHeight, -0.05f);
		body.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
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

	private LineRenderer EnsureRemoteAvatarCable(Transform root)
	{
		Transform child = root.Find("Cable");
		if (child == null)
		{
			child = new GameObject("Cable").transform;
			child.SetParent(root, false);
		}

		LineRenderer cable = child.GetComponent<LineRenderer>();
		if (cable == null)
		{
			cable = child.gameObject.AddComponent<LineRenderer>();
		}

		cable.positionCount = 2;
		cable.useWorldSpace = true;
		cable.sortingOrder = 52;
		cable.startWidth = 0.035f;
		cable.endWidth = 0.035f;
		cable.material = GetArcMaterial();
		cable.startColor = new Color(0.08f, 0.1f, 0.13f, 0.55f);
		cable.endColor = cable.startColor;
		return cable;
	}

	private void StartCraneDipAnimation()
	{
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
		avatarCraneCurrentHeight = Mathf.Lerp(avatarCraneRestHeight, avatarCraneLoweredHeight, lowerAmount);
	}

	private Vector3 GetCraneVisualPosition()
	{
		return avatarPosition + new Vector3(0f, avatarCraneCurrentHeight, -0.05f);
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
		return GetHeldNodeVisualPosition();
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
		SetCompositeBlockSorting(heldCompositeBlockId, true);
	}

	private Vector2 GetHeldCompositeBlockGroundCenter()
	{
		return new Vector2(avatarPosition.x, avatarPosition.y) + heldCompositeBlockOffset;
	}

	private Vector2 GetHeldCompositeBlockVisualCenter()
	{
		Vector2 center = GetHeldCompositeBlockGroundCenter();
		float liftHeight = Mathf.Max(avatarCraneLoweredHeight, avatarCraneCurrentHeight - 0.2f);
		center.y += liftHeight;
		return center;
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
		if (localCraneHoverNodeShadow != null)
		{
			return;
		}

		localCraneHoverNodeShadow = new GameObject("LocalCraneHoverNodeShadow");
		localCraneHoverNodeShadow.transform.SetParent(petriNetRoot, false);
		SpriteRenderer shadowRenderer = localCraneHoverNodeShadow.AddComponent<SpriteRenderer>();
		shadowRenderer.sprite = GetSquareSprite();
		shadowRenderer.color = new Color(0.02f, 0.05f, 0.08f, 0.24f);
		shadowRenderer.sortingOrder = 29;
		localCraneHoverNodeShadow.SetActive(false);
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
		line.positionCount = positionCount;
		line.useWorldSpace = true;
		line.sortingOrder = 23;
		line.startWidth = arcWidth * 3.8f;
		line.endWidth = arcWidth * 3.8f;
		line.numCapVertices = 6;
		line.material = GetArcMaterial();
		Color shadowColor = new Color(0.02f, 0.05f, 0.08f, 0.24f);
		line.startColor = shadowColor;
		line.endColor = shadowColor;
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
		SpriteRenderer shadowRenderer = localCraneHoverNodeShadow.GetComponent<SpriteRenderer>();
		if (shadowRenderer != null)
		{
			shadowRenderer.sprite = node.type == NodeType.Place ? GetCircleSprite() : GetSquareSprite();
		}

		localCraneHoverNodeShadow.SetActive(true);
		Vector3 nodePosition = node.transform.position;
		localCraneHoverNodeShadow.transform.position = new Vector3(nodePosition.x, nodePosition.y, -0.025f);
		localCraneHoverNodeShadow.transform.localScale = node.type == NodeType.Place
			? new Vector3(1.46f, 1.46f, 1f)
			: new Vector3(1.08f, 1.08f, 1f);
	}

	private void ShowCraneHoverCompositeBlockVisual(string blockId)
	{
		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			HideCraneHoverNodeVisual();
			return;
		}

		EnsureCraneHoverNodeVisual();
		SpriteRenderer shadowRenderer = localCraneHoverNodeShadow.GetComponent<SpriteRenderer>();
		if (shadowRenderer != null)
		{
			shadowRenderer.sprite = GetSquareSprite();
		}

		localCraneHoverNodeShadow.SetActive(true);
		localCraneHoverNodeShadow.transform.position = new Vector3(bounds.center.x, bounds.center.y, -0.025f);
		localCraneHoverNodeShadow.transform.localScale = new Vector3(bounds.width + 0.12f, bounds.height + 0.12f, 1f);
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
			localCraneHoverArcBody.SetPosition(0, start + new Vector3(0f, 0f, 0.1f));
			localCraneHoverArcBody.SetPosition(1, end + new Vector3(0f, 0f, 0.1f));
			localCraneHoverArcArrow.SetPosition(0, end + new Vector3(0f, 0f, 0.1f));
			localCraneHoverArcArrow.SetPosition(1, end + new Vector3(0f, 0f, 0.1f));
			localCraneHoverArcArrow.SetPosition(2, end + new Vector3(0f, 0f, 0.1f));
		}
	}

	private bool TryGetArcHoverSegment(ArcRuntime arc, out Vector3 segmentStart, out Vector3 segmentEnd, out bool showArrowHead)
	{
		segmentStart = Vector3.zero;
		segmentEnd = Vector3.zero;
		showArrowHead = false;
		if (!TryGetArcSegment(arc, out Vector3 start, out Vector3 end))
		{
			return false;
		}

		Vector3 middle = (start + end) * 0.5f;
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
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
		}

		if (localCraneHoverArcHighlight != null)
		{
			Destroy(localCraneHoverArcHighlight);
			localCraneHoverArcHighlight = null;
			localCraneHoverArcBody = null;
			localCraneHoverArcArrow = null;
		}
	}

	private void ConfigureCraneConnectPreviewLine(LineRenderer line, int sortingOrder, int positionCount)
	{
		line.positionCount = positionCount;
		line.useWorldSpace = true;
		line.sortingOrder = sortingOrder;
		line.startWidth = arcWidth;
		line.endWidth = arcWidth;
		line.numCapVertices = 4;
		line.material = GetArcMaterial();
		Color previewColor = new Color(0.04f, 0.36f, 0.68f, 0.88f);
		line.startColor = previewColor;
		line.endColor = previewColor;
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

	private Vector3 GetHeldNodeVisualPosition()
	{
		float liftHeight = Mathf.Max(avatarCraneLoweredHeight, avatarCraneCurrentHeight - 0.2f);
		return avatarPosition + new Vector3(0f, liftHeight, -0.04f);
	}

	private void SetPlaceSorting(NodeRuntime node, bool lifted)
	{
		if (node == null)
		{
			return;
		}

		if (node.renderer != null)
		{
			node.renderer.sortingOrder = lifted ? 58 : 30;
		}

		if (node.label != null)
		{
			MeshRenderer labelRenderer = node.label.GetComponent<MeshRenderer>();
			if (labelRenderer != null)
			{
				labelRenderer.sortingOrder = lifted ? 59 : 50;
			}
		}

		if (node.tokenRoot != null)
		{
			for (int i = 0; i < node.tokenRoot.childCount; i++)
			{
				SpriteRenderer tokenRenderer = node.tokenRoot.GetChild(i).GetComponent<SpriteRenderer>();
				if (tokenRenderer != null)
				{
					tokenRenderer.sortingOrder = lifted ? 59 : 40;
				}
			}
		}
	}

	private void SetTransitionSorting(NodeRuntime node, bool lifted)
	{
		if (node == null)
		{
			return;
		}

		if (node.renderer != null)
		{
			node.renderer.sortingOrder = lifted ? 58 : 30;
		}

		if (node.label != null)
		{
			MeshRenderer labelRenderer = node.label.GetComponent<MeshRenderer>();
			if (labelRenderer != null)
			{
				labelRenderer.sortingOrder = lifted ? 59 : 50;
			}
		}
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
					arc.body.sortingOrder = lifted ? 54 : 24;
				}

				if (arc.arrow != null)
				{
					arc.arrow.sortingOrder = lifted ? 55 : 25;
				}
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
				arc.body.sortingOrder = lifted ? 54 : 24;
			}

			if (arc.arrow != null)
			{
				arc.arrow.sortingOrder = lifted ? 55 : 25;
			}
		}

		if (compositeBlocksById.TryGetValue(blockId, out CompositeBlockRuntime block))
		{
			if (block.fill != null)
			{
				block.fill.sortingOrder = lifted ? 53 : 11;
			}

			if (block.border != null)
			{
				block.border.sortingOrder = lifted ? 56 : 14;
			}
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
		CreateSharedBoundaryLine(10000f);
		CreatePoolZoneVisual("PoolAvailable", sharedPoolY, width, new Color(0.82f, 0.92f, 1f, 0.35f));
		CreateIngredientAreaVisual(true);
		CreateIngredientAreaVisual(false);

		for (int i = 0; i < GetPoolBlockCount(); i++)
		{
			Vector2 slot = GetSharedPoolBlockSlotPositionByIndex(i);
			GameObject slotObject = new GameObject("BlockSlot_" + (i + 1));
			slotObject.transform.SetParent(sharedPoolVisualRoot, false);
			slotObject.transform.position = new Vector3(slot.x, slot.y, 0.2f);
			slotObject.transform.localScale = new Vector3(GetCompositeBlockTemplateWidth(), GetCompositeBlockTemplateHeight(), 1f);

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
			slotObject.transform.position = new Vector3(slot.x, slot.y, 0.2f);
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
		line.positionCount = 2;
		line.useWorldSpace = true;
		line.sortingOrder = 13;
		line.startWidth = 0.08f;
		line.endWidth = 0.08f;
		line.material = GetArcMaterial();
		line.startColor = new Color(0.08f, 0.12f, 0.16f, 0.78f);
		line.endColor = line.startColor;
		line.SetPosition(0, new Vector3(fromX, sharedPoolY, 0.08f));
		line.SetPosition(1, new Vector3(toX, sharedPoolY, 0.08f));
	}

	private void CreatePoolZoneVisual(string name, float centerY, float width, Color fillColor)
	{
		GameObject backgroundObject = new GameObject(name + "Background");
		backgroundObject.transform.SetParent(sharedPoolVisualRoot, false);
		backgroundObject.transform.position = new Vector3(0f, centerY, 0.25f);
		backgroundObject.transform.localScale = new Vector3(width, sharedPoolHalfHeight * 2f, 1f);

		SpriteRenderer backgroundRenderer = backgroundObject.AddComponent<SpriteRenderer>();
		backgroundRenderer.sprite = GetSquareSprite();
		backgroundRenderer.color = fillColor;
		backgroundRenderer.sortingOrder = 5;

		GameObject borderObject = new GameObject(name + "Border");
		borderObject.transform.SetParent(sharedPoolVisualRoot, false);
		LineRenderer border = borderObject.AddComponent<LineRenderer>();
		border.positionCount = 5;
		border.loop = false;
		border.useWorldSpace = true;
		border.sortingOrder = 12;
		border.startWidth = 0.08f;
		border.endWidth = 0.08f;
		border.material = GetArcMaterial();
		border.startColor = new Color(0.07f, 0.34f, 0.56f, 0.95f);
		border.endColor = border.startColor;

		float halfWidth = width * 0.5f;
		float halfHeight = sharedPoolHalfHeight;
		Vector3 topLeft = new Vector3(-halfWidth, centerY + halfHeight, 0.15f);
		Vector3 topRight = new Vector3(halfWidth, centerY + halfHeight, 0.15f);
		Vector3 bottomRight = new Vector3(halfWidth, centerY - halfHeight, 0.15f);
		Vector3 bottomLeft = new Vector3(-halfWidth, centerY - halfHeight, 0.15f);
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
		border.positionCount = 5;
		border.loop = false;
		border.useWorldSpace = true;
		border.sortingOrder = 12;
		border.startWidth = 0.07f;
		border.endWidth = 0.07f;
		border.material = GetArcMaterial();
		border.startColor = new Color(0.24f, 0.18f, 0.08f, 0.9f);
		border.endColor = border.startColor;

		Vector3 topLeft = new Vector3(bounds.xMin, bounds.yMax, 0.12f);
		Vector3 topRight = new Vector3(bounds.xMax, bounds.yMax, 0.12f);
		Vector3 bottomRight = new Vector3(bounds.xMax, bounds.yMin, 0.12f);
		Vector3 bottomLeft = new Vector3(bounds.xMin, bounds.yMin, 0.12f);
		border.SetPosition(0, topLeft);
		border.SetPosition(1, topRight);
		border.SetPosition(2, bottomRight);
		border.SetPosition(3, bottomLeft);
		border.SetPosition(4, topLeft);

		GameObject labelObject = new GameObject("ZutatenLabel");
		labelObject.transform.SetParent(sharedPoolVisualRoot, false);
		float labelY = topSide ? bounds.yMin - 0.12f : bounds.yMax + 0.12f;
		labelObject.transform.position = new Vector3(bounds.xMin, labelY, 0.1f);
		TextMesh label = labelObject.AddComponent<TextMesh>();
		label.text = "Zutaten";
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
		float horizontalPadding = 0.58f;
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
		return transitionPosition + new Vector2(1.55f, 0f);
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

		return "Zutat " + (index + 1);
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

		float width = blockCount * GetCompositeBlockTemplateWidth();
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
		float x = -GetSharedPoolContentWidth() * 0.5f + GetCompositeBlockTemplateWidth() * 0.5f;
		x += safeIndex * (GetCompositeBlockTemplateWidth() + sharedPoolItemGap);
		return new Vector2(x, sharedPoolY);
	}

	private Vector2 GetSharedPoolTrashTransitionPosition()
	{
		float x = -GetSharedPoolContentWidth() * 0.5f;
		int blockCount = GetPoolBlockCount();
		if (blockCount > 0)
		{
			x += blockCount * GetCompositeBlockTemplateWidth();
			x += blockCount * sharedPoolItemGap;
		}

		x += GetSharedPoolTrashSlotWidth() * 0.5f;
		return new Vector2(x, sharedPoolY);
	}

	private float GetCompositeBlockTemplateWidth()
	{
		return 5.7f;
	}

	private float GetCompositeBlockTemplateHeight()
	{
		return 1.35f;
	}

	private float GetSharedPoolTrashSlotWidth()
	{
		return 1.1f;
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
		float x = GetSharedPoolHalfWidth() + 0.95f + GetCompositeBlockTemplateWidth() * 0.5f;
		x += safeIndex * (GetCompositeBlockTemplateWidth() + sharedPoolItemGap);
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

		return blockIds;
	}

	private bool IsKnownCompositeBlockId(string blockId)
	{
		return GetCompositeBlockIndex(blockId) >= 0
			|| GetPlayerCompositeBlockIndex(blockId, true) >= 0
			|| GetPlayerCompositeBlockIndex(blockId, false) >= 0;
	}

	private bool IsPlayerBoundCompositeBlock(string blockId)
	{
		return GetPlayerCompositeBlockIndex(blockId, true) >= 0
			|| GetPlayerCompositeBlockIndex(blockId, false) >= 0;
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

		if (nodeId == nodeIds[2])
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
		return new[] { "T_" + prefix + "_Start", "P_" + prefix + "_Buffer", "T_" + prefix + "_End", "P_" + prefix + "_Output" };
	}

	private bool IsCompositeBlockBufferPlaceId(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		return nodeIds != null && nodeId == nodeIds[1];
	}

	private float GetTimedPlaceProcessingDuration(string nodeId)
	{
		string blockId = GetCompositeBlockIdForNodeId(nodeId);
		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		if (nodeIds == null || nodeId != nodeIds[1])
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
		return nodeIds != null && nodeId == nodeIds[3];
	}

	private bool IsCompositeBlockInternalConnection(string fromId, string toId)
	{
		string blockId = GetCompositeBlockIdForNodeId(fromId);
		if (blockId == null || GetCompositeBlockIdForNodeId(toId) != blockId)
		{
			return false;
		}

		string[] nodeIds = GetCompositeBlockNodeIds(blockId);
		return nodeIds != null
			&& ((fromId == nodeIds[0] && toId == nodeIds[1])
				|| (fromId == nodeIds[1] && toId == nodeIds[2])
				|| (fromId == nodeIds[2] && toId == nodeIds[3]));
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
		border.positionCount = 5;
		border.loop = false;
		border.useWorldSpace = true;
		border.sortingOrder = 14;
		border.startWidth = 0.075f;
		border.endWidth = 0.075f;
		border.material = GetArcMaterial();
		border.startColor = new Color(0.18f, 0.18f, 0.2f, 0.9f);
		border.endColor = border.startColor;

		BoxCollider2D collider = blockObject.AddComponent<BoxCollider2D>();
		collider.isTrigger = true;

		CompositeBlockRuntime block = new CompositeBlockRuntime
		{
			id = blockId,
			gameObject = blockObject,
			fill = fill,
			border = border,
			collider = collider,
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

		if (!TryGetCompositeBlockBounds(block.id, out Rect bounds))
		{
			return;
		}

		Vector3 center = new Vector3(bounds.center.x, bounds.center.y, 0.05f);
		block.gameObject.transform.position = center;
		block.collider.offset = Vector2.zero;
		block.collider.size = new Vector2(bounds.width, bounds.height);
		block.fill.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0.1f);
		block.fill.transform.localScale = new Vector3(bounds.width, bounds.height, 1f);

		Vector3 topLeft = new Vector3(bounds.xMin, bounds.yMax, 0.13f);
		Vector3 topRight = new Vector3(bounds.xMax, bounds.yMax, 0.13f);
		Vector3 bottomRight = new Vector3(bounds.xMax, bounds.yMin, 0.13f);
		Vector3 bottomLeft = new Vector3(bounds.xMin, bounds.yMin, 0.13f);
		block.border.SetPosition(0, topLeft);
		block.border.SetPosition(1, topRight);
		block.border.SetPosition(2, bottomRight);
		block.border.SetPosition(3, bottomLeft);
		block.border.SetPosition(4, topLeft);
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
			if (!nodesById.TryGetValue(nodeIds[i], out NodeRuntime node) || node.renderer == null)
			{
				return false;
			}

			Bounds rendererBounds = node.renderer.bounds;
			if (!initialized)
			{
				xMin = rendererBounds.min.x;
				xMax = rendererBounds.max.x;
				yMin = rendererBounds.min.y;
				yMax = rendererBounds.max.y;
				initialized = true;
			}
			else
			{
				xMin = Mathf.Min(xMin, rendererBounds.min.x);
				xMax = Mathf.Max(xMax, rendererBounds.max.x);
				yMin = Mathf.Min(yMin, rendererBounds.min.y);
				yMax = Mathf.Max(yMax, rendererBounds.max.y);
			}
		}

		float paddingX = 0.06f;
		float paddingY = 0.06f;
		bounds = Rect.MinMaxRect(xMin - paddingX, yMin - paddingY, xMax + paddingX, yMax + paddingY);
		return true;
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

		RemoveExternalArcsForCompositeBlock(blockId);
		if (!MoveCompositeBlockInternal(blockId, poolCenter, false))
		{
			return false;
		}

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

	private void RemoveExternalArcsForCompositeBlock(string blockId)
	{
		List<string> arcIdsToRemove = new List<string>();
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc == null || IsCompositeBlockInternalArc(arc))
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
			RemoveArcInternal(arcIdsToRemove[i]);
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
		float halfWidth = GetSharedPoolHalfWidth();
		return x >= -halfWidth && x <= halfWidth;
	}

	private Vector2 ClampPositionToActorArea(Vector2 desired, ulong actorClientId, float outsideBoundaryMargin)
	{
		if (!enableSharedTransitionPool)
		{
			return desired;
		}

		if (IsInsideSharedPoolZone(desired))
		{
			return desired;
		}

		bool topSide = IsActorTopSide(actorClientId);
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

	private float GetAvatarBoundaryShadowHalfWidth()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId) && TryGetCompositeBlockBounds(heldCompositeBlockId, out Rect blockBounds))
		{
			return blockBounds.width * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			return 1.05f * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return 0.9f * 0.5f;
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
			return 0.58f * 0.5f;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return 0.9f * 0.5f;
		}

		float shadowScale = Mathf.Lerp(0.92f, 0.62f, Mathf.InverseLerp(avatarCraneLoweredHeight, avatarCraneRestHeight, avatarCraneCurrentHeight));
		return shadowScale * 0.52f * 0.5f;
	}

	private Vector3 ClampAvatarPositionToAllowedArea(Vector3 desired, ulong actorClientId)
	{
		if (enableSharedTransitionPool)
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

		if (!IsTransitionFullyInPoolZone(poolPosition))
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

	private bool IsTransitionFullyInPoolZone(Vector2 transitionPosition)
	{
		// Check if the entire transition (with its collision radius) is inside the pool zone
		// We need to check all four corners/edges of the transition's bounding box

		// Get pool zone boundaries
		float halfWidth = GetSharedPoolHalfWidth();
		float halfHeight = sharedPoolHalfHeight;

		float poolLeft = -halfWidth;
		float poolRight = halfWidth;
		float poolBottom = sharedPoolY - halfHeight;
		float poolTop = sharedPoolY + halfHeight;

		// Transition bounds (considering it's roughly square with transitionCollisionRadius)
		float transLeft = transitionPosition.x - transitionCollisionRadius;
		float transRight = transitionPosition.x + transitionCollisionRadius;
		float transBottom = transitionPosition.y - transitionCollisionRadius;
		float transTop = transitionPosition.y + transitionCollisionRadius;

		// Check if all edges are inside the pool
		return transLeft >= poolLeft && transRight <= poolRight && transBottom >= poolBottom && transTop <= poolTop;
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
		if (string.IsNullOrEmpty(label))
		{
			return 0.06f;
		}

		int longestLineLength = Mathf.Max(1, GetLongestTransitionLabelLineLength(label));
		int lineCount = Mathf.Max(1, CountTransitionLabelLines(label));
		float maxSize = lineCount > 1 ? 0.046f : 0.058f;
		float sizeByWidth = 0.52f / (longestLineLength * 0.78f);
		float sizeByHeight = 0.48f / (lineCount * 1.35f);
		return Mathf.Clamp(Mathf.Min(maxSize, sizeByWidth, sizeByHeight), 0.018f, maxSize);
	}

	private void FitTransitionLabelInsideNode(NodeRuntime node)
	{
		if (node == null || node.label == null || node.renderer == null)
		{
			return;
		}

		MeshRenderer labelRenderer = node.label.GetComponent<MeshRenderer>();
		if (labelRenderer == null)
		{
			return;
		}

		Vector3 transitionSize = node.renderer.bounds.size;
		float allowedWidth = transitionSize.x * 0.72f;
		float allowedHeight = transitionSize.y * 0.62f;
		for (int i = 0; i < 8; i++)
		{
			Vector3 labelSize = labelRenderer.bounds.size;
			if (labelSize.x <= 0.0001f || labelSize.y <= 0.0001f)
			{
				return;
			}

			float widthScale = allowedWidth / labelSize.x;
			float heightScale = allowedHeight / labelSize.y;
			float scale = Mathf.Min(widthScale, heightScale);
			if (scale >= 0.995f)
			{
				return;
			}

			node.label.characterSize = Mathf.Max(0.014f, node.label.characterSize * scale * 0.96f);
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
		label = NormalizeTransitionLabelWhitespace(InsertTransitionLabelBreakSpaces(label));
		if (string.IsNullOrEmpty(label) || label.Length <= 7)
		{
			return label;
		}

		if (!label.Contains(" ") && label.Length <= 10)
		{
			return label;
		}

		return WrapTransitionLabelToTwoLines(label);
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

	private string WrapTransitionLabelToTwoLines(string label)
	{
		string[] words = label.Split(' ');
		if (words.Length <= 1)
		{
			int splitIndex = Mathf.Clamp(Mathf.CeilToInt(label.Length * 0.5f), 1, label.Length - 1);
			return label.Substring(0, splitIndex) + "\n" + label.Substring(splitIndex);
		}

		int bestSplitIndex = 1;
		int bestScore = int.MaxValue;
		for (int i = 1; i < words.Length; i++)
		{
			string left = JoinTransitionLabelWords(words, 0, i);
			string right = JoinTransitionLabelWords(words, i, words.Length - i);
			int longest = Mathf.Max(left.Length, right.Length);
			int score = Mathf.Abs(left.Length - right.Length) + Mathf.Max(0, longest - 7) * 4;
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
		string result = "";
		for (int i = 0; i < count; i++)
		{
			if (i > 0)
			{
				result += " ";
			}

			result += words[startIndex + i];
		}

		return result;
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

	private int CountTransitionLabelLines(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return 1;
		}

		int count = 1;
		for (int i = 0; i < label.Length; i++)
		{
			if (label[i] == '\n')
			{
				count++;
			}
		}

		return count;
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
		if (definition == null || nodeIds == null || transitionId != nodeIds[2])
		{
			return "";
		}

		return GetPoolBlockResultState(definition);
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
			if (GetNonEmptyTokenDescriptionCount(consumedTokens) > 1)
			{
				baseDescription = "(" + baseDescription + ")";
			}

			outputToken.description = baseDescription + " " + processingState.Trim();
		}

		return outputToken;
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

		string ingredients = JoinTokenValues(token.ingredients);
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

		string result = "";
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

			if (!string.IsNullOrEmpty(result))
			{
				result += ", ";
			}

			result += description;
		}

		return result;
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
			return "leer";
		}

		int visibleCount = Mathf.Min(placeNode.typedTokens.Count, 3);
		string label = "";
		for (int i = 0; i < visibleCount; i++)
		{
			if (i > 0)
			{
				label += "\n";
			}

			label += GetTokenDescription(placeNode.typedTokens[i]);
		}

		int hiddenCount = placeNode.typedTokens.Count - visibleCount;
		if (hiddenCount > 0)
		{
			label += "\n+" + hiddenCount;
		}

		return label;
	}

	private string JoinTokenValues(List<string> values)
	{
		if (values == null || values.Count <= 0)
		{
			return "";
		}

		string result = "";
		for (int i = 0; i < values.Count; i++)
		{
			string value = values[i] != null ? values[i].Trim() : "";
			if (string.IsNullOrEmpty(value))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(result))
			{
				result += ", ";
			}

			result += value;
		}

		return result;
	}

	private string SanitizeTokenObjectName(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "unbekannt";
		}

		string result = "";
		for (int i = 0; i < value.Length; i++)
		{
			char c = value[i];
			result += char.IsLetterOrDigit(c) ? c : '_';
		}

		return result;
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
			tokenObject.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

			SpriteRenderer tokenRenderer = tokenObject.AddComponent<SpriteRenderer>();
			tokenRenderer.sprite = GetCircleSprite();
			tokenRenderer.color = tokenColor;
			tokenRenderer.sortingOrder = 40;
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
		root.transform.localPosition = new Vector3(0f, -0.72f, -0.04f);

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
				node.renderer.color = IsTransitionEnabled(node.id) ? transitionEnabledColor : transitionDisabledColor;
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

		SetLineWithArrow(arc.body, arc.arrow, start, end, dir);

		arc.collider.points = new[] { new Vector2(start.x, start.y), new Vector2(end.x, end.y) };
	}

	private void SetLineWithArrow(LineRenderer body, LineRenderer arrow, Vector3 start, Vector3 end, Vector3 dir)
	{
		Vector3 zOffset = new Vector3(0f, 0f, 0.1f);
		if (body != null)
		{
			body.SetPosition(0, start + zOffset);
			body.SetPosition(1, end + zOffset);
		}

		if (arrow == null)
		{
			return;
		}

		Vector3 leftDir = Quaternion.Euler(0f, 0f, 180f - arrowHeadAngle) * dir;
		Vector3 rightDir = Quaternion.Euler(0f, 0f, 180f + arrowHeadAngle) * dir;
		arrow.SetPosition(0, end + leftDir * arrowHeadLength + zOffset);
		arrow.SetPosition(1, end + zOffset);
		arrow.SetPosition(2, end + rightDir * arrowHeadLength + zOffset);
	}

	private float GetNodeOffsetAlongDirection(NodeRuntime node, Vector3 direction)
	{
		if (node.type == NodeType.Place)
		{
			return node.renderer.bounds.extents.x * 0.96f;
		}

		Vector3 ext = node.renderer.bounds.extents;
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
		labelObject.transform.localPosition = localOffset;

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
