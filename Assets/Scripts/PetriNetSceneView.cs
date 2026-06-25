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
		camera.orthographicSize = 3.6f;
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = new Color(0.95f, 0.96f, 0.98f);
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
			ulong leftOwner = GetLocalActorClientId();
			ulong rightOwner = leftOwner + 1;
			BuildCollaborativeTwoPlayerLayout(leftOwner, rightOwner);
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

	private void BuildCollaborativeTwoPlayerLayout(ulong leftOwnerClientId, ulong rightOwnerClientId)
	{
		ClearGraph();
		EnsureGraphRootExists();
		RebuildSharedPoolVisual();

		for (int i = 0; i < Mathf.Max(1, sharedPoolTransitionCount); i++)
		{
			Vector2 slot = GetSharedPoolSlotPositionByIndex(i);
			CreateTransitionNode("T_POOL_" + (i + 1), slot, false, UnassignedOwnerClientId, true, true);
		}

		CreatePlaceNode("P_Left_In", new Vector2(-playerZoneXOffset + 1.6f, -playerZoneYSpacing * 0.5f), 0, false, leftOwnerClientId, false, false);
		CreateTransitionNode("T_Left_Out", new Vector2(-playerZoneXOffset, 0f), false, leftOwnerClientId, false, false);

		CreatePlaceNode("P_Right_In", new Vector2(playerZoneXOffset - 1.6f, -playerZoneYSpacing * 0.5f), 0, false, rightOwnerClientId, false, false);
		CreateTransitionNode("T_Right_Out", new Vector2(playerZoneXOffset, 0f), false, rightOwnerClientId, false, false);

		CreateArcInternal("A_Left_1", "T_Left_Out", "P_Right_In", 1, false, leftOwnerClientId);
		CreateArcInternal("A_Right_1", "T_Right_Out", "P_Left_In", 1, false, rightOwnerClientId);

		placeCounter = 1;
		transitionCounter = 1;
		arcCounter = 1;
		collaborativeLayoutApplied = true;
		
		// Initialize avatars
		if (GetLocalActorClientId() == leftOwnerClientId)
		{
			avatarPosition = new Vector3(-playerZoneXOffset, playerZoneYSpacing * 0.5f, 0f);
		}
		else
		{
			avatarPosition = new Vector3(playerZoneXOffset, playerZoneYSpacing * 0.5f, 0f);
		}
		avatarRotation = 0f;
		heldTransitionId = null;
		
		RefreshPetriNetVisuals();
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
		if (sharedPoolVisualRoot != null)
		{
			Destroy(sharedPoolVisualRoot.gameObject);
			sharedPoolVisualRoot = null;
		}
		connectStartNodeId = null;
		draggedNodeId = null;
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
			tokens = Mathf.Max(0, initialTokens),
			ownerClientId = ownerClientId,
			isSharedPoolTransition = isSharedPoolTransition,
			isSharedPoolAvailable = isSharedPoolAvailable,
			transform = nodeObject.transform,
			renderer = renderer,
			collider = collider,
			label = label,
			tokenRoot = tokenRoot,
		};

		nodesById[id] = node;
		nodeByCollider[collider] = id;

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
				node.label.text = HumanizeId(node.id);
				node.label.characterSize = 0.08f;
				RefreshTokenVisuals(node);
			}
			else
			{
				string transitionLabel = HumanizeId(node.id);
				node.label.text = transitionLabel;
				node.label.characterSize = GetTransitionLabelCharacterSize(transitionLabel);
				node.renderer.color = IsTransitionEnabled(node.id) ? transitionEnabledColor : transitionDisabledColor;
			}
		}

		UpdateAllArcVisuals();
		UpdateVisibilityForLocalPlayer();
	}

	private GameObject localAvatarVisual;
	private GameObject localAvatarArrow;
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
		if (localAvatarVisual == null)
		{
			localAvatarVisual = new GameObject("LocalAvatar");
			localAvatarVisual.transform.SetParent(petriNetRoot);
			SpriteRenderer spriteRenderer = localAvatarVisual.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = circleSprite;
			spriteRenderer.color = GetAvatarColor(GetLocalActorClientId());
			spriteRenderer.sortingOrder = 50;
			localAvatarVisual.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

			// Add collider – trigger so Physics2D.OverlapCircle can still detect nodes around avatar
			CircleCollider2D collider = localAvatarVisual.AddComponent<CircleCollider2D>();
			collider.radius = 0.4f;
			collider.isTrigger = true;
		}

		if (localAvatarArrow == null)
		{
			localAvatarArrow = new GameObject("LocalAvatarArrow");
			localAvatarArrow.transform.SetParent(petriNetRoot);
			SpriteRenderer arrowRenderer = localAvatarArrow.AddComponent<SpriteRenderer>();
			arrowRenderer.sprite = squareSprite;
			arrowRenderer.color = GetAvatarColor(GetLocalActorClientId());
			arrowRenderer.sortingOrder = 51;
			// Thin, elongated rectangle pointing right by default
			localAvatarArrow.transform.localScale = new Vector3(0.55f, 0.15f, 1f);
		}
	}

	private void UpdateAvatarVisuals()
	{
		EnsureAvatarVisuals();

		// Update local avatar dot
		if (localAvatarVisual != null)
		{
			localAvatarVisual.transform.position = avatarPosition;
		}

		// Update direction arrow: offset from center toward facing direction
		if (localAvatarArrow != null)
		{
			float rad = avatarRotation * Mathf.Deg2Rad;
			Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
			// Place arrow tip-to-center: center of the rectangle sits 0.5 units ahead
			localAvatarArrow.transform.position = avatarPosition + dir * 0.5f;
			localAvatarArrow.transform.rotation = Quaternion.Euler(0f, 0f, avatarRotation);
		}

		// Remote avatars are intentionally hidden.
		if (remoteAvatarVisuals.Count > 0)
		{
			foreach (KeyValuePair<ulong, GameObject> pair in remoteAvatarVisuals)
			{
				if (pair.Value != null)
				{
					Destroy(pair.Value);
				}
			}

			remoteAvatarVisuals.Clear();
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

		int slotCount = Mathf.Max(1, sharedPoolTransitionCount);
		float width = (slotCount - 1) * sharedPoolSlotSpacing + 2.2f;
		CreatePoolZoneVisual("PoolAvailable", sharedPoolY, width, new Color(0.82f, 0.92f, 1f, 0.35f));

		for (int i = 0; i < slotCount; i++)
		{
			Vector2 slot = GetSharedPoolSlotPositionByIndex(i);
			GameObject slotObject = new GameObject("SlotAvailable_" + (i + 1));
			slotObject.transform.SetParent(sharedPoolVisualRoot, false);
			slotObject.transform.position = new Vector3(slot.x, slot.y, 0.2f);
			slotObject.transform.localScale = new Vector3(0.95f, 1.1f, 1f);

			SpriteRenderer slotRenderer = slotObject.AddComponent<SpriteRenderer>();
			slotRenderer.sprite = GetSquareSprite();
			slotRenderer.color = new Color(1f, 1f, 1f, 0.22f);
			slotRenderer.sortingOrder = 10;
		}

	}

	private void CreatePoolZoneVisual(string name, float centerY, float width, Color fillColor)
	{
		GameObject backgroundObject = new GameObject(name + "Background");
		backgroundObject.transform.SetParent(sharedPoolVisualRoot, false);
		backgroundObject.transform.position = new Vector3(0f, centerY, 0.25f);
		backgroundObject.transform.localScale = new Vector3(width, 2f, 1f);

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
		float halfHeight = 1f;
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

	private Vector2 GetSharedPoolSlotPositionByIndex(int index)
	{
		int slotCount = Mathf.Max(1, sharedPoolTransitionCount);
		int safeIndex = Mathf.Clamp(index, 0, slotCount - 1);
		float startX = -0.5f * (slotCount - 1) * sharedPoolSlotSpacing;
		return new Vector2(startX + safeIndex * sharedPoolSlotSpacing, sharedPoolY);
	}

	private Vector2 GetSharedPoolSlotPosition(string transitionId)
	{
		int numericIndex = Mathf.Max(1, ExtractTrailingNumber(transitionId));
		return GetSharedPoolSlotPositionByIndex(numericIndex - 1);
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

		// Only transitions from the pool can be returned (must have T_POOL_ prefix)
		if (!node.id.StartsWith("T_POOL_"))
		{
			return false;
		}

		if (IsPositionBlockedByTransition(new Vector3(desiredPosition.x, desiredPosition.y, 0f), node.id))
		{
			return false;
		}

		// Check if transition is fully inside the pool zone
		bool fullyInPool = IsTransitionFullyInPoolZone(desiredPosition);

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

		node.transform.position = new Vector3(desiredPosition.x, desiredPosition.y, 0f);

		if (fullyInPool)
		{
			// Fully in pool: make available for both players
			node.ownerClientId = UnassignedOwnerClientId;
			node.isSharedPoolTransition = true;
			node.isSharedPoolAvailable = true;
		}
		else
		{
			// Partially in pool: keep it owned by current player
			node.ownerClientId = actorClientId;
			node.isSharedPoolTransition = false;
			node.isSharedPoolAvailable = false;
		}

		// Position stays where it was dropped (already set by client)
		return true;
	}

	private bool IsInsideSharedPoolZone(Vector2 worldPosition)
	{
		int slotCount = Mathf.Max(1, sharedPoolTransitionCount);
		float width = (slotCount - 1) * sharedPoolSlotSpacing + 2.2f;
		float halfWidth = width * 0.5f;
		float halfHeight = 1f;

		return worldPosition.x >= -halfWidth && worldPosition.x <= halfWidth
			&& worldPosition.y >= sharedPoolY - halfHeight && worldPosition.y <= sharedPoolY + halfHeight;
	}

	private bool IsTransitionFullyInPoolZone(Vector2 transitionPosition)
	{
		// Check if the entire transition (with its collision radius) is inside the pool zone
		// We need to check all four corners/edges of the transition's bounding box

		// Get pool zone boundaries
		int slotCount = Mathf.Max(1, sharedPoolTransitionCount);
		float width = (slotCount - 1) * sharedPoolSlotSpacing + 2.2f;
		float halfWidth = width * 0.5f;
		float halfHeight = 1f;

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
		if (!enableNetworkAuthoritativeSync || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
			{
				pair.Value.transform.gameObject.SetActive(true);
			}

			foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
			{
				pair.Value.gameObject.SetActive(true);
			}

			return;
		}

		ulong localClientId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			bool visible = false;

			if (node.type == NodeType.Place)
			{
				// Places are always visible to their owner
				visible = node.ownerClientId == localClientId;
			}
			else if (node.isSharedPoolTransition)
			{
				// Pool transitions: visible if available (for all players) OR if I'm holding it
				visible = node.isSharedPoolAvailable || node.id == heldTransitionId;
			}
			else
			{
				// Regular transitions: visible if owned by me OR held by me (even if not owned yet)
				bool ownedByLocal = node.ownerClientId == localClientId;
				bool heldByLocal = node.id == heldTransitionId;

				visible = ownedByLocal || heldByLocal;
			}

			node.transform.gameObject.SetActive(visible);
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			bool fromVisible = nodesById.TryGetValue(arc.fromId, out NodeRuntime fromNode) && fromNode.transform.gameObject.activeSelf;
			bool toVisible = nodesById.TryGetValue(arc.toId, out NodeRuntime toNode) && toNode.transform.gameObject.activeSelf;
			bool visible = fromVisible && toVisible && arc.ownerClientId == localClientId;
			arc.gameObject.SetActive(visible);
		}
	}

	private Vector2 ClampPositionToPlayerZone(Vector2 desired, ulong actorClientId)
	{
		if (!enableNetworkAuthoritativeSync || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return desired;
		}

		ulong leftOwner = NetworkManager.ServerClientId;
		bool isLeftPlayer = actorClientId == leftOwner;
		float clampBorder = 0.8f;
		if (isLeftPlayer)
		{
			desired.x = Mathf.Min(desired.x, -clampBorder);
		}
		else
		{
			desired.x = Mathf.Max(desired.x, clampBorder);
		}

		return desired;
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

		float laneX = actorClientId == NetworkManager.ServerClientId ? -playerZoneXOffset : playerZoneXOffset;
		float y = sharedPoolY - 1.2f - Mathf.Max(0, ownedClaimedCount - 1) * 0.95f;
		return new Vector2(laneX, y);
	}

	private float GetTransitionLabelCharacterSize(string label)
	{
		if (string.IsNullOrEmpty(label))
		{
			return 0.06f;
		}

		if (label.Length <= 3)
		{
			return 0.075f;
		}

		if (label.Length <= 6)
		{
			return 0.06f;
		}

		return 0.05f;
	}

	private void RefreshTokenVisuals(NodeRuntime placeNode)
	{
		if (placeNode.tokenRoot == null)
		{
			return;
		}

		for (int i = placeNode.tokenRoot.childCount - 1; i >= 0; i--)
		{
			Destroy(placeNode.tokenRoot.GetChild(i).gameObject);
		}

		int displayCount = Mathf.Min(placeNode.tokens, 12);
		for (int i = 0; i < displayCount; i++)
		{
			GameObject tokenObject = new GameObject("Token_" + (i + 1));
			tokenObject.transform.SetParent(placeNode.tokenRoot, false);
			tokenObject.transform.localPosition = GetTokenLocalPosition(i, displayCount);
			tokenObject.transform.localScale = new Vector3(0.2f, 0.2f, 1f);

			SpriteRenderer tokenRenderer = tokenObject.AddComponent<SpriteRenderer>();
			tokenRenderer.sprite = GetCircleSprite();
			tokenRenderer.color = tokenColor;
			tokenRenderer.sortingOrder = 40;
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

		arc.body.SetPosition(0, start + new Vector3(0f, 0f, 0.1f));
		arc.body.SetPosition(1, end + new Vector3(0f, 0f, 0.1f));

		Vector3 leftDir = Quaternion.Euler(0f, 0f, 180f - arrowHeadAngle) * dir;
		Vector3 rightDir = Quaternion.Euler(0f, 0f, 180f + arrowHeadAngle) * dir;
		arc.arrow.SetPosition(0, end + leftDir * arrowHeadLength + new Vector3(0f, 0f, 0.1f));
		arc.arrow.SetPosition(1, end + new Vector3(0f, 0f, 0.1f));
		arc.arrow.SetPosition(2, end + rightDir * arrowHeadLength + new Vector3(0f, 0f, 0.1f));

		arc.collider.points = new[] { new Vector2(start.x, start.y), new Vector2(end.x, end.y) };
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
		float divisor = dx + dy;
		if (divisor < 0.0001f)
		{
			return ext.x;
		}

		return (ext.x * dx + ext.y * dy) / divisor;
	}

	private TextMesh CreateNodeLabel(Transform nodeTransform, Vector3 localOffset, float characterSize)
	{
		GameObject labelObject = new GameObject("Label");
		labelObject.transform.SetParent(nodeTransform, false);
		labelObject.transform.localPosition = localOffset;

		TextMesh label = labelObject.AddComponent<TextMesh>();
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
