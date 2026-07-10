using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public partial class GameManager
{
	private void HandleModeHotkeys()
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard == null)
		{
			return;
		}

		if (keyboard.digit1Key.wasPressedThisFrame)
		{
			currentMode = EditMode.Select;
		}
		else if (keyboard.digit2Key.wasPressedThisFrame)
		{
			currentMode = EditMode.CreatePlace;
		}
		else if (keyboard.digit4Key.wasPressedThisFrame)
		{
			currentMode = EditMode.Connect;
		}
		else if (keyboard.digit5Key.wasPressedThisFrame)
		{
			currentMode = EditMode.Delete;
		}
		else if (keyboard.digit6Key.wasPressedThisFrame)
		{
			currentMode = EditMode.TokenAdd;
		}
		else if (keyboard.digit7Key.wasPressedThisFrame)
		{
			currentMode = EditMode.TokenRemove;
		}

		if (keyboard.escapeKey.wasPressedThisFrame)
		{
			connectStartNodeId = null;
			CancelCraneConnectPreview();
			draggedNodeId = null;
		}
	}

	private void HandleCameraControls()
	{
		if (IsGameplayMenuOpen() || (showLevelSelection && !gameplayInitialized && IsGameplayConnectionReady()))
		{
			return;
		}

		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}

		if (mainCamera == null)
		{
			return;
		}

		float scroll = 0f;
		if (Mouse.current != null)
		{
			scroll = Mouse.current.scroll.ReadValue().y;
		}

		if (Mathf.Abs(scroll) > 0.001f)
		{
			float zoomDelta = scroll * zoomSpeed * 0.05f;
			mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize - zoomDelta, minZoom, maxZoom);
		}

		if (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
		{
			isMiddlePanning = true;
			panReferenceWorld = GetMouseWorldPosition();
		}

		if (Mouse.current != null && isMiddlePanning && Mouse.current.middleButton.isPressed)
		{
			Vector3 currentWorld = GetMouseWorldPosition();
			Vector3 delta = panReferenceWorld - currentWorld;
			mainCamera.transform.position += delta;
		}

		if (Mouse.current != null && Mouse.current.middleButton.wasReleasedThisFrame)
		{
			isMiddlePanning = false;
		}

	}

	private float cameraVelocityX = 0f;
	private float cameraVelocityY = 0f;

	private void UpdateCameraFollowAvatar()
	{
		if (enableSharedTransitionPool)
		{
			float requiredSize = GetSharedScreenCameraSize();
			if (mainCamera.orthographicSize < requiredSize)
			{
				mainCamera.orthographicSize = requiredSize;
			}

			Vector3 sharedCamPos = mainCamera.transform.position;
			float sharedScreenHeight = mainCamera.orthographicSize * 2f;
			float sharedScreenWidth = sharedScreenHeight * mainCamera.aspect;
			float sharedRestMarginX = sharedScreenWidth * cameraRestAreaMargin;
			float sharedRestMarginY = sharedScreenHeight * cameraRestAreaMargin;
			float sharedNewX = sharedCamPos.x;
			float sharedNewY = sharedCamPos.y;

			if (avatarPosition.x > sharedCamPos.x + sharedRestMarginX)
			{
				sharedNewX = avatarPosition.x - sharedRestMarginX;
			}
			else if (avatarPosition.x < sharedCamPos.x - sharedRestMarginX)
			{
				sharedNewX = avatarPosition.x + sharedRestMarginX;
			}

			if (avatarPosition.y > sharedCamPos.y + sharedRestMarginY)
			{
				sharedNewY = avatarPosition.y - sharedRestMarginY;
			}
			else if (avatarPosition.y < sharedCamPos.y - sharedRestMarginY)
			{
				sharedNewY = avatarPosition.y + sharedRestMarginY;
			}

			mainCamera.transform.position = new Vector3(sharedNewX, sharedNewY, sharedCamPos.z);
			cameraVelocityX = 0f;
			cameraVelocityY = 0f;
			return;
		}

		// Rest area: only start moving camera once avatar leaves the inner margin
		float screenHeight = mainCamera.orthographicSize * 2f;
		float screenWidth = screenHeight * mainCamera.aspect;
		float restMarginX = screenWidth * cameraRestAreaMargin;
		float restMarginY = screenHeight * cameraRestAreaMargin;

		Vector3 camPos = mainCamera.transform.position;
		float deltaX = Mathf.Abs(avatarPosition.x - camPos.x);
		float deltaY = Mathf.Abs(avatarPosition.y - camPos.y);

		// Handle each axis independently to prevent diagonal drift
		float newX = camPos.x;
		float newY = camPos.y;

		if (deltaX > restMarginX)
		{
			newX = Mathf.SmoothDamp(camPos.x, avatarPosition.x, ref cameraVelocityX, 0.18f, Mathf.Infinity, Time.deltaTime);
		}
		else
		{
			cameraVelocityX = Mathf.Lerp(cameraVelocityX, 0f, Time.deltaTime * 10f);
		}

		if (deltaY > restMarginY)
		{
			newY = Mathf.SmoothDamp(camPos.y, avatarPosition.y, ref cameraVelocityY, 0.18f, Mathf.Infinity, Time.deltaTime);
		}
		else
		{
			cameraVelocityY = Mathf.Lerp(cameraVelocityY, 0f, Time.deltaTime * 10f);
		}

		mainCamera.transform.position = new Vector3(newX, newY, camPos.z);
	}

	private void HandleAvatarInput()
	{
		Keyboard keyboard = Keyboard.current;
		if (keyboard == null || mainCamera == null)
		{
			return;
		}

		Vector3 moveDirection = Vector3.zero;
		if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) { moveDirection.y += 1f; }
		if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) { moveDirection.y -= 1f; }
		if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) { moveDirection.x -= 1f; }
		if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) { moveDirection.x += 1f; }

		if (keyboard.eKey.wasPressedThisFrame)
		{
			HandleCreatePlaceAction();
			UpdateAvatarVisuals();
			return;
		}

		if (keyboard.rKey.wasPressedThisFrame)
		{
			HandlePlaceDeleteAction();
			UpdateAvatarVisuals();
			return;
		}

		if (keyboard.qKey.wasPressedThisFrame)
		{
			HandleCraneConnectAction();
			UpdateAvatarVisuals();
			return;
		}

		if (keyboard.fKey.wasPressedThisFrame)
		{
			HandleCraneFireAction();
			UpdateAvatarVisuals();
			return;
		}

		// Update avatar position
		if (moveDirection.sqrMagnitude > 0.1f)
		{
			moveDirection = moveDirection.normalized;
			bool sprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
			float currentSpeed = sprinting ? avatarSpeed * avatarSprintMultiplier : avatarSpeed;
			Vector3 newPosition = avatarPosition + moveDirection * currentSpeed * Time.deltaTime;
			avatarPosition = ClampAvatarPositionToAllowedArea(newPosition, GetLocalActorClientId());

			// Update rotation to face movement direction
			float targetRotation = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
			avatarRotation = targetRotation;

			if (!string.IsNullOrEmpty(heldTransitionId))
			{
				UpdateHeldTransitionVisual();
			}

			if (!string.IsNullOrEmpty(heldPlaceId))
			{
				UpdateHeldPlaceVisual();
			}

			if (!string.IsNullOrEmpty(heldCompositeBlockId))
			{
				UpdateHeldCompositeBlockVisual();
			}
		}

		// Pickup/Drop with spacebar
		if (keyboard.spaceKey.wasPressedThisFrame)
		{
			StartCraneDipAnimation();
			HandleAvatarInteraction();
		}

		string currentHeldTransitionId = heldTransitionId ?? "";
		float movedDistance = Vector3.Distance(lastAvatarPosition, avatarPosition);
		bool heldTransitionChanged = lastAvatarNetworkSyncHeldId != currentHeldTransitionId;
		bool rotationChanged = Mathf.Abs(Mathf.DeltaAngle(lastAvatarNetworkSyncRotation, avatarRotation)) > 2f;
		bool shouldSendAvatarUpdate = heldTransitionChanged
			|| movedDistance > 0.65f
			|| ((movedDistance > 0.05f || rotationChanged) && Time.unscaledTime >= nextAvatarNetworkSyncTime);
		if (shouldSendAvatarUpdate)
		{
			nextAvatarNetworkSyncTime = Time.unscaledTime + avatarNetworkSyncInterval;
			lastAvatarPosition = avatarPosition;
			lastAvatarNetworkSyncRotation = avatarRotation;
			lastAvatarNetworkSyncHeldId = currentHeldTransitionId;
			SendAvatarUpdate(avatarPosition, avatarRotation, heldTransitionId);
		}

		// Update avatar visual every frame
		UpdateAvatarVisuals();
	}

	private void HandleAvatarInteraction()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			DropHeldCompositeBlock();
			return;
		}

		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			DropHeldPlace();
			return;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			TryDropTransition();
			return;
		}

		if (!string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			HandleCraneConnectAction();
			return;
		}

		if (TryPickupCompositeBlockAtCraneTarget())
		{
			return;
		}

		if (TryPickupPlaceAtCraneTarget())
		{
			return;
		}

		TryPickupTransition();
		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return;
		}

		TryPickupArcWithCrane();
	}

	private void TryPickupTransition()
	{
		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			return;
		}

		// Find closest available transition in pool nearby
		// Pickup range = touching distance (sum of radii) + small buffer
		float pickupRange = avatarCollisionRadius + transitionCollisionRadius + 0.2f;
		float closestDistance = float.MaxValue;
		NodeRuntime closestTransition = null;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node.type != NodeType.Transition)
			{
				continue;
			}

			if (IsIngredientTransition(node) || IsDeliveryTransition(node) || IsCompositeBlockNode(node))
			{
				continue;
			}

			bool isAvailablePoolTransition = node.isSharedPoolTransition && node.isSharedPoolAvailable;
			bool isOwnedPlacedTransition = !node.isSharedPoolTransition && node.ownerClientId == GetLocalActorClientId();
			if (!isAvailablePoolTransition && !isOwnedPlacedTransition)
			{
				continue;
			}

			float distance = Vector3.Distance(avatarPosition, node.transform.position);
			if (distance < pickupRange && distance < closestDistance)
			{
				closestDistance = distance;
				closestTransition = node;
			}
		}

		if (closestTransition != null)
		{
			heldTransitionId = closestTransition.id;
			closestTransition.transform.position = avatarPosition;
			if (closestTransition.isSharedPoolTransition)
			{
				RequestClaimTransition(closestTransition.id);
				closestTransition.isSharedPoolAvailable = false;
			}
			StartCraneDipAnimation();
			UpdateHeldTransitionVisual();
			RefreshPetriNetVisuals();
		}
	}

	private void TryDropTransition()
	{
		if (!nodesById.TryGetValue(heldTransitionId, out NodeRuntime transition))
		{
			heldTransitionId = null;
			return;
		}

		Vector3 dropPosition = avatarPosition;

		// Check if drop position is in pool zone
		Vector2 dropPos2D = new Vector2(dropPosition.x, dropPosition.y);
		if (IsInsideSharedPoolZone(dropPos2D))
		{
			if (!IsTransitionFullyInPoolZone(dropPosition))
			{
				return;
			}

			if (IsPositionBlockedByNode(dropPosition, heldTransitionId))
			{
				return;
			}

			string transitionId = heldTransitionId;
			heldTransitionId = null;

			if (IsHostOrOffline())
			{
				// HOST: Let the authoritative command path update ownership/availability
				// so it can validate against the current owner and broadcast the snapshot.
				RequestReturnTransitionToPool(transitionId, dropPosition);
			}
			else
			{
				// CLIENT: Hide the transition temporarily until snapshot confirms
				transition.transform.gameObject.SetActive(false);
				RequestReturnTransitionToPool(transitionId, dropPosition);
				// Position and flags will be updated when snapshot arrives
				// RefreshPetriNetVisuals will be called in ApplySnapshot and make it visible again
			}
			StartCraneDipAnimation();
		}
		else
		{
			// Check if another node is already at this position
			if (IsPositionBlockedByNode(dropPosition, heldTransitionId))
			{
				// Cannot place here - position is occupied
				return;
			}

			// Place transition outside pool
			transition.transform.position = dropPosition;
			transition.ownerClientId = GetLocalActorClientId();
			transition.isSharedPoolAvailable = false;
			transition.isSharedPoolTransition = false; // No longer a pool transition once placed
			RequestPlaceTransition(heldTransitionId, dropPosition);
			heldTransitionId = null;
			StartCraneDipAnimation();
			RefreshPetriNetVisuals();
		}
	}

	private void HandleCreatePlaceAction()
	{
		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			DropHeldPlace();
			return;
		}

		if (!string.IsNullOrEmpty(heldTransitionId) || !string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return;
		}

		CreateAndHoldPlaceAtCraneTarget();
	}

	private void HandleCraneConnectAction()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return;
		}

		if (!TryGetNodeAtCraneTarget(out NodeRuntime targetNode))
		{
			if (!string.IsNullOrEmpty(craneConnectStartNodeId))
			{
				ToggleCraneConnectDirection();
				return;
			}

			if (TryGetArcAtCraneTarget(out ArcRuntime arc))
			{
				TryReverseArcWithCrane(arc);
			}

			return;
		}

		if (!CanUseNodeAsExternalConnectionEndpoint(targetNode))
		{
			if (!string.IsNullOrEmpty(craneConnectStartNodeId))
			{
				ToggleCraneConnectDirection();
			}

			return;
		}

		if (string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			craneConnectStartNodeId = targetNode.id;
			craneConnectReversed = false;
			UpdateCraneConnectPreviewVisual();
			return;
		}

		if (!nodesById.TryGetValue(craneConnectStartNodeId, out NodeRuntime startNode) || !CanActorEditNode(startNode, GetLocalActorClientId()))
		{
			CancelCraneConnectPreview();
			return;
		}

		if (startNode.id == targetNode.id)
		{
			ToggleCraneConnectDirection();
			return;
		}

		string fromId = craneConnectReversed ? targetNode.id : startNode.id;
		string toId = craneConnectReversed ? startNode.id : targetNode.id;
		if (!CanCreatePendingCraneArc(fromId, toId))
		{
			ToggleCraneConnectDirection();
			return;
		}

		RequestCreateArc(fromId, toId);
		CancelCraneConnectPreview();
	}

	private void TryReverseArcWithCrane(ArcRuntime arc)
	{
		if (!CanActorReverseArc(arc, GetLocalActorClientId()))
		{
			return;
		}

		RequestReverseArc(arc.id);
	}

	private bool TryPickupArcWithCrane()
	{
		if (!TryGetArcAtCraneTarget(out ArcRuntime arc))
		{
			return false;
		}

		if (!TryGetArcPickupAnchor(arc, out string fixedNodeId, out bool reversed))
		{
			return false;
		}

		string arcId = arc.id;
		craneConnectStartNodeId = fixedNodeId;
		craneConnectReversed = reversed;
		RequestDeleteArc(arcId);
		if (!IsHostOrOffline() && arc.gameObject != null)
		{
			arc.gameObject.SetActive(false);
		}

		HideCraneHoverSelectionVisual();
		UpdateCraneConnectPreviewVisual();
		RefreshPetriNetVisuals();
		return true;
	}

	private bool TryGetArcPickupAnchor(ArcRuntime arc, out string fixedNodeId, out bool reversed)
	{
		fixedNodeId = null;
		reversed = false;
		if (arc == null || !TryGetArcSegment(arc, out Vector3 start, out Vector3 end))
		{
			return false;
		}

		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		float distanceToStart = Vector2.Distance(craneTarget, new Vector2(start.x, start.y));
		float distanceToEnd = Vector2.Distance(craneTarget, new Vector2(end.x, end.y));
		bool pickupFromSide = distanceToStart <= distanceToEnd;
		fixedNodeId = pickupFromSide ? arc.toId : arc.fromId;
		reversed = pickupFromSide;

		return nodesById.TryGetValue(fixedNodeId, out NodeRuntime fixedNode)
			&& CanUseNodeAsExternalConnectionEndpoint(fixedNode)
			&& CanActorEditNode(fixedNode, GetLocalActorClientId());
	}

	private void ToggleCraneConnectDirection()
	{
		if (string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			return;
		}

		craneConnectReversed = !craneConnectReversed;
		UpdateCraneConnectPreviewVisual();
	}

	private void CancelCraneConnectPreview()
	{
		craneConnectStartNodeId = null;
		craneConnectReversed = false;
		HideCraneConnectPreviewVisual();
	}

	private bool CanCreatePendingCraneArc(string fromId, string toId)
	{
		if (!CanActorCreateArc(fromId, toId, GetLocalActorClientId()))
		{
			return false;
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.fromId == fromId && arc.toId == toId)
			{
				return false;
			}
		}

		return true;
	}

	private void HandleCraneFireAction()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return;
		}

		if (!TryGetTransitionAtCraneTarget(out NodeRuntime transition))
		{
			return;
		}

		RequestFireTransition(transition.id);
	}

	private bool TryPickupCompositeBlockAtCraneTarget()
	{
		if (TryGetCompositeBlockAtCraneTarget(out CompositeBlockRuntime block))
		{
			if (!CanActorPickupCompositeBlock(block.id, GetLocalActorClientId()))
			{
				return false;
			}

			Vector2 blockCenter = GetCompositeBlockCenter(block.id);
			heldCompositeBlockId = block.id;
			heldCompositeBlockOffset = blockCenter - new Vector2(avatarPosition.x, avatarPosition.y);
			if (IsCompositeBlockAvailableInSharedPool(block.id))
			{
				SetCompositeBlockSharedPoolState(block.id, GetLocalActorClientId(), false, true);
				RequestClaimCompositeBlock(block.id);
			}
			else if (!IsCompositeBlockInSharedPool(block.id))
			{
				SetCompositeBlockSharedPoolState(block.id, GetLocalActorClientId(), false, false);
			}

			StartCraneDipAnimation();
			UpdateHeldCompositeBlockVisual();
			RefreshPetriNetVisuals();
			return true;
		}

		return false;
	}

	private bool TryPickupPlaceAtCraneTarget()
	{
		if (TryGetPlaceAtCraneTarget(out NodeRuntime place))
		{
			heldPlaceId = place.id;
			StartCraneDipAnimation();
			UpdateHeldPlaceVisual();
			RefreshPetriNetVisuals();
			return true;
		}

		return false;
	}

	private void DropHeldCompositeBlock()
	{
		if (string.IsNullOrEmpty(heldCompositeBlockId) || !compositeBlocksById.ContainsKey(heldCompositeBlockId))
		{
			heldCompositeBlockId = null;
			heldCompositeBlockOffset = Vector2.zero;
			return;
		}

		string blockId = heldCompositeBlockId;
		Vector2 groundCenter = GetHeldCompositeBlockGroundCenter();
		if (IsInsideSharedPoolZone(groundCenter))
		{
			bool returned = TryReturnSharedCompositeBlockToPool(blockId, GetLocalActorClientId());
			if (!returned)
			{
				return;
			}

			if (IsHostOrOffline())
			{
				BroadcastSnapshotToClients();
			}
			else
			{
				RequestReturnCompositeBlock(blockId);
			}

			SetCompositeBlockSorting(blockId, false);
			heldCompositeBlockId = null;
			heldCompositeBlockOffset = Vector2.zero;
			StartCraneDipAnimation();
			RefreshPetriNetVisuals();
			return;
		}

		Vector2 desiredCenter = ClampCompositeBlockCenterToActorArea(blockId, groundCenter, GetLocalActorClientId());
		if (!MoveCompositeBlockInternal(blockId, desiredCenter))
		{
			return;
		}

		SetCompositeBlockSharedPoolState(blockId, GetLocalActorClientId(), false, false);
		RequestMoveCompositeBlock(blockId, new Vector3(desiredCenter.x, desiredCenter.y, 0f));
		SetCompositeBlockSorting(blockId, false);
		heldCompositeBlockId = null;
		heldCompositeBlockOffset = Vector2.zero;
		StartCraneDipAnimation();
		RefreshPetriNetVisuals();
	}

	private void HandlePlaceDeleteAction()
	{
		if (!string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			CancelCraneConnectPreview();
			return;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return;
		}

		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return;
		}

		if (!string.IsNullOrEmpty(heldPlaceId) && nodesById.TryGetValue(heldPlaceId, out NodeRuntime heldPlace))
		{
			TryDeletePlaceWithCrane(heldPlace);
			return;
		}

		if (TryGetPlaceAtCraneTarget(out NodeRuntime place))
		{
			TryDeletePlaceWithCrane(place);
			return;
		}

		if (TryGetArcAtCraneTarget(out ArcRuntime arc))
		{
			TryDeleteArcWithCrane(arc);
		}
	}

	private void CreateAndHoldPlaceAtCraneTarget()
	{
		if (pendingCreatedPlacePickup)
		{
			return;
		}

		if (IsPlaceOverSharedTransitionPool(avatarPosition, null))
		{
			return;
		}

		pendingCreatedPlacePickup = true;
		pendingCreatedPlacePickupPosition = avatarPosition;
		pendingCreatedPlaceExistingIds.Clear();
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			if (pair.Value.type == NodeType.Place)
			{
				pendingCreatedPlaceExistingIds.Add(pair.Key);
			}
		}

		RequestCreateHeldPlace(avatarPosition);
		TryAttachPendingCreatedPlace();
	}

	private void DropHeldPlace()
	{
		if (!nodesById.TryGetValue(heldPlaceId, out NodeRuntime place))
		{
			heldPlaceId = null;
			return;
		}

		Vector3 dropPosition = avatarPosition;
		if (IsPlacePlacementBlocked(dropPosition, heldPlaceId))
		{
			return;
		}

		place.transform.position = dropPosition;
		RequestMoveNode(heldPlaceId, dropPosition);
		heldPlaceId = null;
		StartCraneDipAnimation();
		RefreshPetriNetVisuals();
	}

	private void TryDeletePlaceWithCrane(NodeRuntime place)
	{
		if (place == null || place.type != NodeType.Place || !CanDeleteNode(place))
		{
			return;
		}

		if (!CanActorEditNode(place, GetLocalActorClientId()))
		{
			return;
		}

		string placeId = place.id;
		if (heldPlaceId == placeId)
		{
			heldPlaceId = null;
		}

		RequestDeleteNode(placeId);
		if (!IsHostOrOffline() && place.transform != null)
		{
			place.transform.gameObject.SetActive(false);
		}

		RefreshPetriNetVisuals();
	}

	private void TryDeleteArcWithCrane(ArcRuntime arc)
	{
		if (!CanActorSelectArc(arc))
		{
			return;
		}

		string arcId = arc.id;
		RequestDeleteArc(arcId);
		if (!IsHostOrOffline() && arc.gameObject != null)
		{
			arc.gameObject.SetActive(false);
		}

		HideCraneHoverSelectionVisual();
		RefreshPetriNetVisuals();
	}

	private bool TryGetPlaceAtCraneTarget(out NodeRuntime closestPlace)
	{
		closestPlace = null;
		float closestDistance = float.MaxValue;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node.type != NodeType.Place || node.transform == null || !node.transform.gameObject.activeInHierarchy || !CanActorMoveNode(node, GetLocalActorClientId()))
			{
				continue;
			}

			float pickupRange = avatarCollisionRadius + GetPlaceInteractionRadius(node) + 0.08f;
			float distance = Vector2.Distance(avatarPosition, node.transform.position);
			if (distance <= pickupRange && distance < closestDistance)
			{
				closestDistance = distance;
				closestPlace = node;
			}
		}

		return closestPlace != null;
	}

	private bool TryGetNodeAtCraneTarget(out NodeRuntime closestNode)
	{
		closestNode = null;
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		float closestDistance = float.MaxValue;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.transform == null || !node.transform.gameObject.activeInHierarchy || !CanActorEditNode(node, GetLocalActorClientId()))
			{
				continue;
			}

			float distance;
			bool withinRange;
			if (node.type == NodeType.Place)
			{
				distance = Vector2.Distance(craneTarget, node.transform.position);
				withinRange = distance <= avatarCollisionRadius + GetPlaceInteractionRadius(node) + 0.08f;
			}
			else
			{
				Rect expandedBounds = ExpandRect(GetTransitionPlacementBounds(node, node.transform.position), avatarCollisionRadius + 0.08f);
				distance = GetPointRectDistance(craneTarget, expandedBounds);
				withinRange = distance <= 0f;
			}

			if (withinRange && distance < closestDistance)
			{
				closestDistance = distance;
				closestNode = node;
			}
		}

		return closestNode != null;
	}

	private bool TryGetHoverSelectableNodeAtCraneTarget(out NodeRuntime closestNode)
	{
		closestNode = null;
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		float closestDistance = float.MaxValue;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (!CanActorHoverSelectNode(node))
			{
				continue;
			}

			float distance;
			bool withinRange;
			if (node.type == NodeType.Place)
			{
				distance = Vector2.Distance(craneTarget, node.transform.position);
				withinRange = distance <= avatarCollisionRadius + GetPlaceInteractionRadius(node) + 0.08f;
			}
			else
			{
				Rect expandedBounds = ExpandRect(GetTransitionPlacementBounds(node, node.transform.position), avatarCollisionRadius + 0.08f);
				distance = GetPointRectDistance(craneTarget, expandedBounds);
				withinRange = distance <= 0f;
			}

			if (withinRange && distance < closestDistance)
			{
				closestDistance = distance;
				closestNode = node;
			}
		}

		return closestNode != null;
	}

	private bool TryGetCompositeBlockAtCraneTarget(out CompositeBlockRuntime closestBlock)
	{
		closestBlock = null;
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		float closestDistance = float.MaxValue;

		foreach (KeyValuePair<string, CompositeBlockRuntime> pair in compositeBlocksById)
		{
			CompositeBlockRuntime block = pair.Value;
			if (block == null || block.gameObject == null || !block.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (!CanActorPickupCompositeBlock(block.id, GetLocalActorClientId()))
			{
				continue;
			}

			if (!TryGetCompositeBlockBounds(block.id, out Rect bounds))
			{
				continue;
			}

			if (!DoesCraneShadowTouchRect(bounds))
			{
				continue;
			}

			float distance = GetPointRectDistance(craneTarget, bounds);
			if (distance < closestDistance)
			{
				closestDistance = distance;
				closestBlock = block;
			}
		}

		return closestBlock != null;
	}

	private bool DoesCraneShadowTouchRect(Rect rect)
	{
		Vector2 shadowCenter = new Vector2(avatarPosition.x, avatarPosition.y);
		float halfWidth = Mathf.Max(0.001f, GetAvatarBoundaryShadowHalfWidth());
		float halfHeight = Mathf.Max(0.001f, GetAvatarBoundaryShadowHalfHeight());
		float closestX = Mathf.Clamp(shadowCenter.x, rect.xMin, rect.xMax);
		float closestY = Mathf.Clamp(shadowCenter.y, rect.yMin, rect.yMax);
		float normalizedX = (shadowCenter.x - closestX) / halfWidth;
		float normalizedY = (shadowCenter.y - closestY) / halfHeight;
		return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
	}

	private bool CanActorHoverSelectNode(NodeRuntime node)
	{
		if (node == null || node.transform == null || !node.transform.gameObject.activeInHierarchy)
		{
			return false;
		}

		if (IsCompositeBlockNode(node))
		{
			return false;
		}

		if (node.type == NodeType.Transition && IsSharedTransitionAvailable(node))
		{
			return true;
		}

		return CanActorEditNode(node, GetLocalActorClientId());
	}

	private bool TryGetTransitionAtCraneTarget(out NodeRuntime closestTransition)
	{
		closestTransition = null;
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		float closestDistance = float.MaxValue;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.type != NodeType.Transition || node.transform == null || !node.transform.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (node.id == heldTransitionId || !CanActorEditNode(node, GetLocalActorClientId()))
			{
				continue;
			}

			Rect expandedBounds = ExpandRect(GetTransitionPlacementBounds(node, node.transform.position), avatarCollisionRadius + 0.08f);
			float distance = GetPointRectDistance(craneTarget, expandedBounds);
			if (distance <= 0f && distance < closestDistance)
			{
				closestDistance = distance;
				closestTransition = node;
			}
		}

		return closestTransition != null;
	}

	private bool TryGetArcAtCraneTarget(out ArcRuntime closestArc)
	{
		closestArc = null;
		Vector2 craneTarget = new Vector2(avatarPosition.x, avatarPosition.y);
		float closestDistance = float.MaxValue;
		float selectionDistance = Mathf.Max(0.2f, avatarCollisionRadius * 0.55f);

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (!CanActorSelectArc(arc))
			{
				continue;
			}

			if (!TryGetArcSegment(arc, out Vector3 start, out Vector3 end))
			{
				continue;
			}

			float distance = GetPointSegmentDistance(craneTarget, new Vector2(start.x, start.y), new Vector2(end.x, end.y));
			if (distance <= selectionDistance && distance < closestDistance)
			{
				closestDistance = distance;
				closestArc = arc;
			}
		}

		return closestArc != null;
	}

	private bool CanActorSelectArc(ArcRuntime arc)
	{
		return arc != null
			&& arc.gameObject != null
			&& arc.gameObject.activeInHierarchy
			&& arc.ownerClientId == GetLocalActorClientId()
			&& !IsIngredientSourceArc(arc)
			&& !IsCompositeBlockInternalArc(arc)
			&& !IsPlayerExchangeArc(arc);
	}

	private bool TryGetArcSegment(ArcRuntime arc, out Vector3 start, out Vector3 end)
	{
		start = Vector3.zero;
		end = Vector3.zero;
		if (arc == null)
		{
			return false;
		}

		if (arc.body != null && arc.body.positionCount >= 2)
		{
			start = arc.body.GetPosition(0);
			end = arc.body.GetPosition(1);
			start.z = 0f;
			end.z = 0f;
			return (end - start).sqrMagnitude > 0.0001f;
		}

		if (arc.collider != null && arc.collider.points != null && arc.collider.points.Length >= 2)
		{
			Vector2[] points = arc.collider.points;
			start = arc.collider.transform.TransformPoint(points[0]);
			end = arc.collider.transform.TransformPoint(points[1]);
			start.z = 0f;
			end.z = 0f;
			return (end - start).sqrMagnitude > 0.0001f;
		}

		return false;
	}

	private float GetPointSegmentDistance(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
	{
		Vector2 segment = segmentEnd - segmentStart;
		float lengthSquared = segment.sqrMagnitude;
		if (lengthSquared <= 0.000001f)
		{
			return Vector2.Distance(point, segmentStart);
		}

		float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
		Vector2 closest = segmentStart + segment * t;
		return Vector2.Distance(point, closest);
	}

	private void TryAttachPendingCreatedPlace()
	{
		if (!pendingCreatedPlacePickup)
		{
			return;
		}

		NodeRuntime bestPlace = null;
		float bestDistance = float.MaxValue;
		int bestTrailingNumber = -1;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node.type != NodeType.Place || node.transform == null || !node.transform.gameObject.activeInHierarchy || !CanActorEditNode(node, GetLocalActorClientId()) || IsProtectedInputPlace(node) || pendingCreatedPlaceExistingIds.Contains(node.id))
			{
				continue;
			}

			float distance = Vector2.Distance(pendingCreatedPlacePickupPosition, node.transform.position);
			int trailingNumber = ExtractTrailingNumber(node.id);
			if (distance < bestDistance - 0.001f || (Mathf.Abs(distance - bestDistance) <= 0.001f && trailingNumber > bestTrailingNumber))
			{
				bestPlace = node;
				bestDistance = distance;
				bestTrailingNumber = trailingNumber;
			}
		}

		if (bestPlace == null || bestDistance > avatarCollisionRadius + GetPlaceInteractionRadius(bestPlace) + 0.08f)
		{
			return;
		}

		heldPlaceId = bestPlace.id;
		pendingCreatedPlacePickup = false;
		pendingCreatedPlaceExistingIds.Clear();
		UpdateHeldPlaceVisual();
		RefreshPetriNetVisuals();
	}

	private float GetPlaceInteractionRadius(NodeRuntime node)
	{
		if (node == null)
		{
			return 0.6f;
		}

		if (node.collider is CircleCollider2D circleCollider)
		{
			Vector3 scale = node.transform != null ? node.transform.lossyScale : Vector3.one;
			float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
			return circleCollider.radius * maxScale;
		}

		if (node.collider != null)
		{
			Bounds bounds = node.collider.bounds;
			return Mathf.Max(bounds.extents.x, bounds.extents.y);
		}

		return 0.6f;
	}

	private bool IsPositionBlockedByNode(Vector3 targetPosition, string ignoredNodeId)
	{
		NodeRuntime movingNode = null;
		nodesById.TryGetValue(ignoredNodeId, out movingNode);

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.transform == null || !node.transform.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (node.id == ignoredNodeId)
			{
				continue;
			}

			if (DoNodePlacementBoundsOverlap(movingNode, targetPosition, node, node.transform.position))
			{
				return true; // Position is blocked
			}
		}

		return false; // Position is free
	}

	private bool IsNodeMovePositionBlocked(NodeRuntime node, Vector3 targetPosition)
	{
		if (node != null && node.type == NodeType.Place && IsPlaceOverSharedTransitionPool(targetPosition, node.id))
		{
			return true;
		}

		return IsPositionBlockedByNode(targetPosition, node != null ? node.id : null);
	}

	private bool IsPlacePlacementBlocked(Vector3 targetPosition, string ignoredPlaceId)
	{
		return IsPlaceOverSharedTransitionPool(targetPosition, ignoredPlaceId)
			|| IsPositionBlockedByNode(targetPosition, ignoredPlaceId);
	}

	private bool IsPlaceOverSharedTransitionPool(Vector3 targetPosition, string placeId)
	{
		if (!enableSharedTransitionPool)
		{
			return false;
		}

		NodeRuntime place = null;
		if (!string.IsNullOrEmpty(placeId))
		{
			nodesById.TryGetValue(placeId, out place);
		}

		Vector2 center;
		float radius;
		if (place != null)
		{
			GetPlacePlacementCircle(place, targetPosition, out center, out radius);
		}
		else
		{
			center = new Vector2(targetPosition.x, targetPosition.y);
			radius = 0.6f;
		}

		return DoCircleRectOverlap(center, radius, GetSharedTransitionPoolRect());
	}

	private bool IsNewPlacePositionBlocked(Vector3 targetPosition)
	{
		if (IsPlaceOverSharedTransitionPool(targetPosition, null))
		{
			return true;
		}

		Vector2 placeCenter = new Vector2(targetPosition.x, targetPosition.y);
		float placeRadius = 0.6f;

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.transform == null || !node.transform.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (node.type == NodeType.Place)
			{
				GetPlacePlacementCircle(node, node.transform.position, out Vector2 existingCenter, out float existingRadius);
				float combinedRadius = placeRadius + existingRadius;
				if ((placeCenter - existingCenter).sqrMagnitude < combinedRadius * combinedRadius)
				{
					return true;
				}
			}
			else if (DoCircleRectOverlap(placeCenter, placeRadius, GetTransitionPlacementBounds(node, node.transform.position)))
			{
				return true;
			}
		}

		return false;
	}

	private Rect ExpandRect(Rect rect, float amount)
	{
		return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
	}

	private float GetPointRectDistance(Vector2 point, Rect rect)
	{
		float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
		float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
		return Mathf.Sqrt(dx * dx + dy * dy);
	}

	private bool DoNodePlacementBoundsOverlap(NodeRuntime movingNode, Vector3 movingPosition, NodeRuntime existingNode, Vector3 existingPosition)
	{
		if (movingNode == null || existingNode == null)
		{
			return false;
		}

		if (movingNode.type == NodeType.Place && existingNode.type == NodeType.Place)
		{
			GetPlacePlacementCircle(movingNode, movingPosition, out Vector2 movingCenter, out float movingRadius);
			GetPlacePlacementCircle(existingNode, existingPosition, out Vector2 existingCenter, out float existingRadius);
			float combinedRadius = movingRadius + existingRadius;
			return (movingCenter - existingCenter).sqrMagnitude < combinedRadius * combinedRadius;
		}

		if (movingNode.type == NodeType.Transition && existingNode.type == NodeType.Transition)
		{
			return DoTransitionBoundsOverlap(
				GetTransitionPlacementBounds(movingNode, movingPosition),
				GetTransitionPlacementBounds(existingNode, existingPosition));
		}

		if (movingNode.type == NodeType.Place)
		{
			GetPlacePlacementCircle(movingNode, movingPosition, out Vector2 placeCenter, out float placeRadius);
			return DoCircleRectOverlap(placeCenter, placeRadius, GetTransitionPlacementBounds(existingNode, existingPosition));
		}

		GetPlacePlacementCircle(existingNode, existingPosition, out Vector2 existingPlaceCenter, out float existingPlaceRadius);
		return DoCircleRectOverlap(existingPlaceCenter, existingPlaceRadius, GetTransitionPlacementBounds(movingNode, movingPosition));
	}

	private void GetPlacePlacementCircle(NodeRuntime node, Vector3 centerPosition, out Vector2 center, out float radius)
	{
		center = new Vector2(centerPosition.x, centerPosition.y);
		radius = 0.6f;

		if (node == null)
		{
			return;
		}

		if (node.collider is CircleCollider2D circleCollider)
		{
			Vector3 scale = node.transform != null ? node.transform.lossyScale : Vector3.one;
			center += new Vector2(circleCollider.offset.x * scale.x, circleCollider.offset.y * scale.y);
			radius = circleCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
			return;
		}

		if (node.collider != null)
		{
			Bounds bounds = node.collider.bounds;
			radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
		}
	}

	private Rect GetTransitionPlacementBounds(NodeRuntime node, Vector3 centerPosition)
	{
		Vector2 halfExtents = new Vector2(transitionCollisionRadius, transitionCollisionRadius);
		Vector2 offset = Vector2.zero;

		if (node != null && node.collider is BoxCollider2D boxCollider)
		{
			Vector3 scale = boxCollider.transform.lossyScale;
			halfExtents = new Vector2(
				Mathf.Abs(boxCollider.size.x * scale.x) * 0.5f,
				Mathf.Abs(boxCollider.size.y * scale.y) * 0.5f);
			offset = new Vector2(boxCollider.offset.x * scale.x, boxCollider.offset.y * scale.y);
		}

		Vector2 center = new Vector2(centerPosition.x + offset.x, centerPosition.y + offset.y);
		return new Rect(center.x - halfExtents.x, center.y - halfExtents.y, halfExtents.x * 2f, halfExtents.y * 2f);
	}

	private bool DoTransitionBoundsOverlap(Rect a, Rect b)
	{
		return a.xMin < b.xMax && a.xMax > b.xMin
			&& a.yMin < b.yMax && a.yMax > b.yMin;
	}

	private bool DoCircleRectOverlap(Vector2 circleCenter, float circleRadius, Rect rect)
	{
		float closestX = Mathf.Clamp(circleCenter.x, rect.xMin, rect.xMax);
		float closestY = Mathf.Clamp(circleCenter.y, rect.yMin, rect.yMax);
		Vector2 closestPoint = new Vector2(closestX, closestY);
		return (circleCenter - closestPoint).sqrMagnitude < circleRadius * circleRadius;
	}

	private bool IsTransitionFullyInPoolZone(Vector3 transitionPosition)
	{
		// Check if the entire transition (with its collision radius) is inside the pool zone
		// We need to check all four corners/edges of the transition's bounding box

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

	private void RequestClaimTransition(string transitionId)
	{
		ExecuteOrSendCommand(new CommandData { action = "ClaimSharedTransition", id = transitionId, x = avatarPosition.x, y = avatarPosition.y });
	}

	private void RequestPlaceTransition(string transitionId, Vector3 position)
	{
		ExecuteOrSendCommand(new CommandData { action = "MoveNode", id = transitionId, x = position.x, y = position.y });
	}

	private void RequestReturnTransitionToPool(string transitionId, Vector3 position)
	{
		ExecuteOrSendCommand(new CommandData { action = "ReturnSharedTransition", id = transitionId, x = position.x, y = position.y });
	}

	private void BeginSelectPress(Vector3 worldPosition)
	{
		pointerDownNodeId = null;
		pointerDownCompositeBlockId = null;
		pointerDragActive = false;
		draggedNodeId = null;
		draggedCompositeBlockId = null;

		if (TryGetNodeAtPoint(worldPosition, out NodeRuntime node))
		{
			if (IsCompositeBlockNode(node))
			{
				pointerDownCompositeBlockId = GetCompositeBlockIdForNodeId(node.id);
				pointerDownWorld = worldPosition;
				return;
			}

			pointerDownNodeId = node.id;
			pointerDownWorld = worldPosition;
			return;
		}

		if (TryGetCompositeBlockAtPoint(worldPosition, out CompositeBlockRuntime block))
		{
			pointerDownCompositeBlockId = block.id;
			pointerDownWorld = worldPosition;
			return;
		}
	}

	private void HandleSelectHold(Vector3 worldPosition)
	{
		if (!string.IsNullOrEmpty(pointerDownCompositeBlockId))
		{
			HandleCompositeBlockSelectHold(worldPosition);
			return;
		}

		if (string.IsNullOrEmpty(pointerDownNodeId))
		{
			return;
		}

		if (!pointerDragActive)
		{
			if (Vector2.Distance(new Vector2(pointerDownWorld.x, pointerDownWorld.y), new Vector2(worldPosition.x, worldPosition.y)) < sharedPoolDragThreshold)
			{
				return;
			}

			if (!nodesById.TryGetValue(pointerDownNodeId, out NodeRuntime pressedNode))
			{
				pointerDownNodeId = null;
				return;
			}

			if (pressedNode.type == NodeType.Transition && pressedNode.isSharedPoolTransition && pressedNode.isSharedPoolAvailable)
			{
				RequestClaimSharedTransition(pressedNode.id, worldPosition);
				pendingClaimedTransitionId = pressedNode.id;
				RefreshPetriNetVisuals();
			}

			if (!nodesById.TryGetValue(pointerDownNodeId, out NodeRuntime dragNode) || !CanStartDraggingNode(dragNode))
			{
				return;
			}

			pointerDragActive = true;
			draggedNodeId = dragNode.id;
			dragOffset = dragNode.transform.position - new Vector3(worldPosition.x, worldPosition.y, 0f);
			nextDragNetworkSyncTime = 0f;
			lastDragNetworkSyncPosition = dragNode.transform.position;
		}

		if (pointerDragActive && draggedNodeId != null && nodesById.TryGetValue(draggedNodeId, out NodeRuntime node))
		{
			Vector2 clampedDragPosition = ClampPositionToActorArea(new Vector2(worldPosition.x + dragOffset.x, worldPosition.y + dragOffset.y), GetLocalActorClientId(), 0f);
			Vector3 desiredPosition = new Vector3(clampedDragPosition.x, clampedDragPosition.y, 0f);
			if (IsNodeMovePositionBlocked(node, desiredPosition))
			{
				return;
			}

			node.transform.position = desiredPosition;
			UpdateAllArcVisuals();

			Vector3 currentPosition = node.transform.position;
			if (Time.unscaledTime >= nextDragNetworkSyncTime || Vector3.Distance(lastDragNetworkSyncPosition, currentPosition) > 0.45f)
			{
				nextDragNetworkSyncTime = Time.unscaledTime + 0.2f;
				lastDragNetworkSyncPosition = currentPosition;
				RequestMoveNode(node.id, currentPosition);
			}
		}
	}

	private void HandleCompositeBlockSelectHold(Vector3 worldPosition)
	{
		if (!pointerDragActive)
		{
			if (Vector2.Distance(new Vector2(pointerDownWorld.x, pointerDownWorld.y), new Vector2(worldPosition.x, worldPosition.y)) < sharedPoolDragThreshold)
			{
				return;
			}

			if (!compositeBlocksById.ContainsKey(pointerDownCompositeBlockId))
			{
				pointerDownCompositeBlockId = null;
				return;
			}

			if (!CanActorPickupCompositeBlock(pointerDownCompositeBlockId, GetLocalActorClientId()))
			{
				pointerDownCompositeBlockId = null;
				return;
			}

			if (IsCompositeBlockAvailableInSharedPool(pointerDownCompositeBlockId))
			{
				SetCompositeBlockSharedPoolState(pointerDownCompositeBlockId, GetLocalActorClientId(), false, true);
				RequestClaimCompositeBlock(pointerDownCompositeBlockId);
			}

			pointerDragActive = true;
			draggedCompositeBlockId = pointerDownCompositeBlockId;
			Vector2 blockCenter = GetCompositeBlockCenter(draggedCompositeBlockId);
			dragOffset = new Vector3(blockCenter.x - worldPosition.x, blockCenter.y - worldPosition.y, 0f);
			nextDragNetworkSyncTime = 0f;
			lastDragNetworkSyncPosition = new Vector3(blockCenter.x, blockCenter.y, 0f);
		}

		if (!pointerDragActive || string.IsNullOrEmpty(draggedCompositeBlockId))
		{
			return;
		}

		Vector2 desiredCenter = new Vector2(worldPosition.x + dragOffset.x, worldPosition.y + dragOffset.y);
		desiredCenter = ClampCompositeBlockCenterToActorArea(draggedCompositeBlockId, desiredCenter, GetLocalActorClientId());
		if (!MoveCompositeBlockInternal(draggedCompositeBlockId, desiredCenter))
		{
			return;
		}

		if (!IsInsideSharedPoolZone(desiredCenter))
		{
			SetCompositeBlockSharedPoolState(draggedCompositeBlockId, GetLocalActorClientId(), false, false);
		}

		Vector2 currentCenter = GetCompositeBlockCenter(draggedCompositeBlockId);
		Vector3 currentPosition = new Vector3(currentCenter.x, currentCenter.y, 0f);
		if (Time.unscaledTime >= nextDragNetworkSyncTime || Vector3.Distance(lastDragNetworkSyncPosition, currentPosition) > 0.45f)
		{
			nextDragNetworkSyncTime = Time.unscaledTime + 0.2f;
			lastDragNetworkSyncPosition = currentPosition;
			RequestMoveCompositeBlock(draggedCompositeBlockId, currentPosition);
		}
	}

	private void HandleSelectRelease(Vector3 worldPosition)
	{
		if (pointerDragActive && !string.IsNullOrEmpty(draggedCompositeBlockId))
		{
			Vector2 center = GetCompositeBlockCenter(draggedCompositeBlockId);
			RequestMoveCompositeBlock(draggedCompositeBlockId, new Vector3(center.x, center.y, 0f));
		}
		else if (pointerDragActive && draggedNodeId != null && nodesById.TryGetValue(draggedNodeId, out NodeRuntime draggedNode))
		{
			RequestMoveNode(draggedNode.id, draggedNode.transform.position);
		}
		else if (!pointerDragActive && !string.IsNullOrEmpty(pointerDownNodeId) && nodesById.TryGetValue(pointerDownNodeId, out NodeRuntime clickedNode))
		{
			HandleSelectClick(clickedNode, worldPosition);
		}

		pointerDownNodeId = null;
		pointerDownCompositeBlockId = null;
		pointerDragActive = false;
		draggedNodeId = null;
		draggedCompositeBlockId = null;
		nextDragNetworkSyncTime = 0f;
		// Don't clear pendingClaimedTransitionId here - let ApplySnapshot() do it when host confirms
	}

	private void HandleSelectClick(NodeRuntime node, Vector3 worldPosition)
	{
		if (node.type != NodeType.Transition)
		{
			return;
		}

		if (node.isSharedPoolTransition)
		{
			return;
		}

		RequestFireTransition(node.id);
	}

	private bool CanStartDraggingNode(NodeRuntime node)
	{
		if (node == null)
		{
			return false;
		}

		if (!CanActorMoveNode(node, GetLocalActorClientId()))
		{
			return false;
		}

		if (node.type != NodeType.Transition)
		{
			return true;
		}

		if (!node.isSharedPoolTransition)
		{
			return true;
		}

		if (!node.isSharedPoolAvailable)
		{
			return node.ownerClientId == GetLocalActorClientId();
		}

		return node.id == pendingClaimedTransitionId;
	}

	private void OnPrimaryPressed(Vector3 worldPosition)
	{
		switch (currentMode)
		{
			case EditMode.Select:
				break;
			case EditMode.CreatePlace:
				if (!IsNewPlacePositionBlocked(worldPosition))
				{
					RequestCreatePlace(worldPosition);
				}
				break;
			case EditMode.CreateTransition:
				break;
			case EditMode.Connect:
				HandleConnectModeClick(worldPosition);
				break;
			case EditMode.Delete:
				HandleDeleteModeClick(worldPosition);
				break;
			case EditMode.TokenAdd:
				HandleTokenModeClick(worldPosition, 1);
				break;
			case EditMode.TokenRemove:
				HandleTokenModeClick(worldPosition, -1);
				break;
		}
	}

	private void HandleConnectModeClick(Vector3 worldPosition)
	{
		if (!TryGetNodeAtPoint(worldPosition, out NodeRuntime node))
		{
			return;
		}

		if (!CanUseNodeAsExternalConnectionEndpoint(node))
		{
			return;
		}

		if (connectStartNodeId == null)
		{
			connectStartNodeId = node.id;
			Debug.Log("Connect start: " + node.id);
			return;
		}

		if (connectStartNodeId == node.id)
		{
			connectStartNodeId = null;
			return;
		}

		RequestCreateArc(connectStartNodeId, node.id);
		connectStartNodeId = null;
	}

	private void HandleDeleteModeClick(Vector3 worldPosition)
	{
		if (TryGetNodeAtPoint(worldPosition, out NodeRuntime node))
		{
			if (!CanDeleteNode(node))
			{
				return;
			}

			RequestDeleteNode(node.id);
			return;
		}

		if (TryGetArcAtPoint(worldPosition, out ArcRuntime arc))
		{
			if (IsIngredientSourceArc(arc) || IsCompositeBlockInternalArc(arc) || IsPlayerExchangeArc(arc))
			{
				return;
			}

			RequestDeleteArc(arc.id);
		}
	}

	private void HandleTokenModeClick(Vector3 worldPosition, int delta)
	{
		if (!TryGetNodeAtPoint(worldPosition, out NodeRuntime node))
		{
			return;
		}

		if (node.type != NodeType.Place)
		{
			return;
		}

		RequestChangeTokens(node.id, delta);
	}

	private bool TryGetNodeAtPoint(Vector3 worldPosition, out NodeRuntime node)
	{
		node = null;
		Collider2D hit = Physics2D.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
		if (hit == null)
		{
			return false;
		}

		if (!nodeByCollider.TryGetValue(hit, out string nodeId))
		{
			return false;
		}

		return nodesById.TryGetValue(nodeId, out node);
	}

	private bool TryGetArcAtPoint(Vector3 worldPosition, out ArcRuntime arc)
	{
		arc = null;
		Collider2D hit = Physics2D.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
		if (hit == null)
		{
			return false;
		}

		if (!arcByCollider.TryGetValue(hit, out string arcId))
		{
			return false;
		}

		return arcsById.TryGetValue(arcId, out arc);
	}

	private bool TryGetCompositeBlockAtPoint(Vector3 worldPosition, out CompositeBlockRuntime block)
	{
		block = null;
		Collider2D hit = Physics2D.OverlapPoint(new Vector2(worldPosition.x, worldPosition.y));
		if (hit == null)
		{
			return false;
		}

		if (!compositeBlockByCollider.TryGetValue(hit, out string blockId))
		{
			return false;
		}

		return compositeBlocksById.TryGetValue(blockId, out block);
	}

	private Vector3 GetMouseWorldPosition()
	{
		Vector2 mouse = Mouse.current.position.ReadValue();
		float depth = -mainCamera.transform.position.z;
		Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, depth));
		world.z = 0f;
		return world;
	}
}
