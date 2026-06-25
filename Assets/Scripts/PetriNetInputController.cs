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
			draggedNodeId = null;
		}
	}

	private void HandleCameraControls()
	{
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

		// Follow avatar with rest area
		if (gameplayInitialized)
		{
			UpdateCameraFollowAvatar();
		}
	}

	private float cameraVelocityX = 0f;
	private float cameraVelocityY = 0f;

	private void UpdateCameraFollowAvatar()
	{
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

		if (!string.IsNullOrEmpty(temporarilyIgnoredCollisionNodeId) && Time.unscaledTime > temporarilyIgnoredCollisionUntilTime)
		{
			temporarilyIgnoredCollisionNodeId = null;
		}

		// Update avatar position
		if (moveDirection.sqrMagnitude > 0.1f)
		{
			moveDirection = moveDirection.normalized;
			Vector3 newPosition = avatarPosition + moveDirection * avatarSpeed * Time.deltaTime;
			avatarPosition = GetAvatarCollisionSafePosition(avatarPosition, newPosition);

			// Update rotation to face movement direction
			float targetRotation = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
			avatarRotation = targetRotation;

			// Move held transition in front of avatar (already positioned at pickup above)
			if (!string.IsNullOrEmpty(heldTransitionId) && nodesById.TryGetValue(heldTransitionId, out NodeRuntime heldNode))
			{
				float rad = avatarRotation * Mathf.Deg2Rad;
				float holdOffset = avatarCollisionRadius + transitionCollisionRadius + 0.05f;
				Vector3 front = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * holdOffset;
				heldNode.transform.position = avatarPosition + front;
				UpdateAllArcVisuals();
			}
		}

		// Pickup/Drop with spacebar
		if (keyboard.spaceKey.wasPressedThisFrame)
		{
			HandleAvatarInteraction();
		}

		// Send position update to host periodically
		// (disabled - remote avatar rendering is off, no need to flood network)
		// if (Time.unscaledTime >= nextAvatarNetworkSyncTime || Vector3.Distance(lastAvatarPosition, avatarPosition) > 0.1f)
		// {
		// 	nextAvatarNetworkSyncTime = Time.unscaledTime + 0.08f;
		// 	lastAvatarPosition = avatarPosition;
		// 	lastAvatarRotation = avatarRotation;
		// 	SendAvatarUpdate(avatarPosition, avatarRotation, heldTransitionId);
		// }

		// Update avatar visual every frame
		UpdateAvatarVisuals();
	}

	private void HandleAvatarInteraction()
	{
		if (string.IsNullOrEmpty(heldTransitionId))
		{
			// Try to pick up a transition
			TryPickupTransition();
		}
		else
		{
			// Drop the held transition
			TryDropTransition();
		}
	}

	private void TryPickupTransition()
	{
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
			if (closestTransition.id == temporarilyIgnoredCollisionNodeId)
			{
				temporarilyIgnoredCollisionNodeId = null;
			}

			heldTransitionId = closestTransition.id;
			// Position held transition immediately in front (don't wait for next HandleAvatarInput frame)
			float rad = avatarRotation * Mathf.Deg2Rad;
			float pickupOffset = avatarCollisionRadius + transitionCollisionRadius + 0.05f;
			Vector3 front = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * pickupOffset;
			closestTransition.transform.position = avatarPosition + front;
			if (closestTransition.isSharedPoolTransition)
			{
				RequestClaimTransition(closestTransition.id);
				closestTransition.isSharedPoolAvailable = false;
			}
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

		// Calculate drop position in front of avatar
		float rad = avatarRotation * Mathf.Deg2Rad;
		float dropOffset = avatarCollisionRadius + transitionCollisionRadius + 0.05f;
		Vector3 dropPosition = avatarPosition + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * dropOffset;

		// Check if drop position is in pool zone
		Vector2 dropPos2D = new Vector2(dropPosition.x, dropPosition.y);
		if (IsInsideSharedPoolZone(dropPos2D))
		{
			if (IsPositionBlockedByTransition(dropPosition, heldTransitionId))
			{
				return;
			}

			string transitionId = heldTransitionId;
			heldTransitionId = null;

			if (transition.id == temporarilyIgnoredCollisionNodeId)
			{
				temporarilyIgnoredCollisionNodeId = null;
			}

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
		}
		else
		{
			// Check if another transition is already at this position
			if (IsPositionBlockedByTransition(dropPosition, heldTransitionId))
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
			temporarilyIgnoredCollisionNodeId = transition.id;
			temporarilyIgnoredCollisionUntilTime = Time.unscaledTime + postDropCollisionIgnoreDuration;
			heldTransitionId = null;
			RefreshPetriNetVisuals();
		}
	}

	private bool IsPositionBlockedByTransition(Vector3 targetPosition, string ignoredTransitionId)
	{
		NodeRuntime movingNode = null;
		nodesById.TryGetValue(ignoredTransitionId, out movingNode);
		Rect targetBounds = GetTransitionPlacementBounds(movingNode, targetPosition);

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;

			// Skip non-transitions
			if (node.type != NodeType.Transition)
			{
				continue;
			}

			// Skip the transition we're trying to place
			if (node.id == ignoredTransitionId)
			{
				continue;
			}

			Rect nodeBounds = GetTransitionPlacementBounds(node, node.transform.position);
			if (DoTransitionBoundsOverlap(targetBounds, nodeBounds))
			{
				return true; // Position is blocked
			}
		}

		return false; // Position is free
	}

	private Vector3 GetAvatarCollisionSafePosition(Vector3 currentPosition, Vector3 desiredPosition)
	{
		Vector3 cutPosition = CutAvatarMovementAtTransitionBounds(currentPosition, desiredPosition);
		return ResolveAvatarTransitionOverlaps(cutPosition);
	}

	private Vector3 CutAvatarMovementAtTransitionBounds(Vector3 currentPosition, Vector3 desiredPosition)
	{
		Vector2 start = new Vector2(currentPosition.x, currentPosition.y);
		Vector2 end = new Vector2(desiredPosition.x, desiredPosition.y);
		Vector2 delta = end - start;
		if (delta.sqrMagnitude <= 0.000001f)
		{
			return desiredPosition;
		}

		float earliestHit = 1f;
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (!ShouldBlockAvatarWithTransition(node))
			{
				continue;
			}

			Rect expandedBounds = ExpandRect(GetTransitionPlacementBounds(node, node.transform.position), avatarCollisionRadius);
			if (IsPointInsideOrOnRect(start, expandedBounds))
			{
				continue;
			}

			if (TryGetSegmentRectEntry(start, end, expandedBounds, out float hitT))
			{
				earliestHit = Mathf.Min(earliestHit, hitT);
			}
		}

		if (earliestHit >= 1f)
		{
			return desiredPosition;
		}

		const float contactSkin = 0.002f;
		float skinT = contactSkin / delta.magnitude;
		Vector2 safePosition = start + delta * Mathf.Max(0f, earliestHit - skinT);
		return new Vector3(safePosition.x, safePosition.y, desiredPosition.z);
	}

	private Vector3 ResolveAvatarTransitionOverlaps(Vector3 position)
	{
		Vector3 resolved = position;
		for (int iteration = 0; iteration < 4; iteration++)
		{
			bool changed = false;
			foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
			{
				NodeRuntime node = pair.Value;
				if (!ShouldBlockAvatarWithTransition(node))
				{
					continue;
				}

				Rect bounds = GetTransitionPlacementBounds(node, node.transform.position);
				if (TryGetAvatarPushOut(new Vector2(resolved.x, resolved.y), bounds, out Vector2 pushOut))
				{
					resolved.x += pushOut.x;
					resolved.y += pushOut.y;
					changed = true;
				}
			}

			if (!changed)
			{
				break;
			}
		}

		return resolved;
	}

	private bool ShouldBlockAvatarWithTransition(NodeRuntime node)
	{
		if (node == null || node.type != NodeType.Transition || node.transform == null)
		{
			return false;
		}

		if (!node.transform.gameObject.activeInHierarchy)
		{
			return false;
		}

		return node.id != heldTransitionId;
	}

	private bool TryGetAvatarPushOut(Vector2 avatarCenter, Rect transitionBounds, out Vector2 pushOut)
	{
		pushOut = Vector2.zero;
		float closestX = Mathf.Clamp(avatarCenter.x, transitionBounds.xMin, transitionBounds.xMax);
		float closestY = Mathf.Clamp(avatarCenter.y, transitionBounds.yMin, transitionBounds.yMax);
		Vector2 closestPoint = new Vector2(closestX, closestY);
		Vector2 awayFromTransition = avatarCenter - closestPoint;
		float sqrDistance = awayFromTransition.sqrMagnitude;
		float sqrRadius = avatarCollisionRadius * avatarCollisionRadius;

		if (sqrDistance > sqrRadius)
		{
			return false;
		}

		if (sqrDistance > 0.000001f)
		{
			float distance = Mathf.Sqrt(sqrDistance);
			pushOut = awayFromTransition / distance * (avatarCollisionRadius - distance);
			return pushOut.sqrMagnitude > 0.000001f;
		}

		float leftDistance = avatarCenter.x - transitionBounds.xMin;
		float rightDistance = transitionBounds.xMax - avatarCenter.x;
		float bottomDistance = avatarCenter.y - transitionBounds.yMin;
		float topDistance = transitionBounds.yMax - avatarCenter.y;
		float minHorizontal = Mathf.Min(leftDistance, rightDistance);
		float minVertical = Mathf.Min(bottomDistance, topDistance);

		if (minHorizontal <= minVertical)
		{
			pushOut = leftDistance <= rightDistance
				? new Vector2(-(leftDistance + avatarCollisionRadius), 0f)
				: new Vector2(rightDistance + avatarCollisionRadius, 0f);
		}
		else
		{
			pushOut = bottomDistance <= topDistance
				? new Vector2(0f, -(bottomDistance + avatarCollisionRadius))
				: new Vector2(0f, topDistance + avatarCollisionRadius);
		}

		return true;
	}

	private Rect ExpandRect(Rect rect, float amount)
	{
		return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
	}

	private bool IsPointInsideOrOnRect(Vector2 point, Rect rect)
	{
		return point.x >= rect.xMin && point.x <= rect.xMax
			&& point.y >= rect.yMin && point.y <= rect.yMax;
	}

	private bool TryGetSegmentRectEntry(Vector2 start, Vector2 end, Rect rect, out float entryT)
	{
		entryT = 0f;
		Vector2 delta = end - start;
		float tMin = 0f;
		float tMax = 1f;

		if (!UpdateSegmentSlab(start.x, delta.x, rect.xMin, rect.xMax, ref tMin, ref tMax))
		{
			return false;
		}

		if (!UpdateSegmentSlab(start.y, delta.y, rect.yMin, rect.yMax, ref tMin, ref tMax))
		{
			return false;
		}

		if (tMax < 0f || tMin > 1f)
		{
			return false;
		}

		entryT = Mathf.Clamp01(tMin);
		return true;
	}

	private bool UpdateSegmentSlab(float start, float delta, float min, float max, ref float tMin, ref float tMax)
	{
		if (Mathf.Abs(delta) < 0.000001f)
		{
			return start >= min && start <= max;
		}

		float invDelta = 1f / delta;
		float t1 = (min - start) * invDelta;
		float t2 = (max - start) * invDelta;
		if (t1 > t2)
		{
			float swap = t1;
			t1 = t2;
			t2 = swap;
		}

		tMin = Mathf.Max(tMin, t1);
		tMax = Mathf.Min(tMax, t2);
		return tMin <= tMax;
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

	private bool IsTransitionFullyInPoolZone(Vector3 transitionPosition)
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

	private void RequestClaimTransition(string transitionId)
	{
		// Send the position the transition should be at (in front of avatar), not just avatar position
		float rad = avatarRotation * Mathf.Deg2Rad;
		float pickupOffset = avatarCollisionRadius + transitionCollisionRadius + 0.05f;
		Vector3 pickupPosition = avatarPosition + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * pickupOffset;
		ExecuteOrSendCommand(new CommandData { action = "ClaimSharedTransition", id = transitionId, x = pickupPosition.x, y = pickupPosition.y });
	}

	private void RequestPlaceTransition(string transitionId, Vector3 position)
	{
		ExecuteOrSendCommand(new CommandData { action = "MoveNode", id = transitionId, x = position.x, y = position.y });
	}

	private void RequestReturnTransitionToPool(string transitionId, Vector3 position)
	{
		ExecuteOrSendCommand(new CommandData { action = "ReturnSharedTransition", id = transitionId, x = position.x, y = position.y });
	}

	private void SendAvatarUpdate(Vector3 position, float rotation, string heldId)
	{
		ExecuteOrSendCommand(new CommandData { action = "UpdateAvatar", x = position.x, y = position.y, rotation = rotation, id = heldId });
	}

	private void BeginSelectPress(Vector3 worldPosition)
	{
		pointerDownNodeId = null;
		pointerDragActive = false;
		draggedNodeId = null;

		if (!TryGetNodeAtPoint(worldPosition, out NodeRuntime node))
		{
			return;
		}

		pointerDownNodeId = node.id;
		pointerDownWorld = worldPosition;
	}

	private void HandleSelectHold(Vector3 worldPosition)
	{
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
			node.transform.position = new Vector3(worldPosition.x + dragOffset.x, worldPosition.y + dragOffset.y, 0f);
			UpdateAllArcVisuals();

			Vector3 currentPosition = node.transform.position;
			if (Time.unscaledTime >= nextDragNetworkSyncTime || Vector3.Distance(lastDragNetworkSyncPosition, currentPosition) > 0.18f)
			{
				nextDragNetworkSyncTime = Time.unscaledTime + 0.06f;
				lastDragNetworkSyncPosition = currentPosition;
				RequestMoveNode(node.id, currentPosition);
			}
		}
	}

	private void HandleSelectRelease(Vector3 worldPosition)
	{
		if (pointerDragActive && draggedNodeId != null && nodesById.TryGetValue(draggedNodeId, out NodeRuntime draggedNode))
		{
			RequestMoveNode(draggedNode.id, draggedNode.transform.position);
		}
		else if (!pointerDragActive && !string.IsNullOrEmpty(pointerDownNodeId) && nodesById.TryGetValue(pointerDownNodeId, out NodeRuntime clickedNode))
		{
			HandleSelectClick(clickedNode, worldPosition);
		}

		pointerDownNodeId = null;
		pointerDragActive = false;
		draggedNodeId = null;
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

		if (!CanActorEditNode(node, GetLocalActorClientId()))
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
				if (!TryGetNodeAtPoint(worldPosition, out _))
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
			RequestDeleteNode(node.id);
			return;
		}

		if (TryGetArcAtPoint(worldPosition, out ArcRuntime arc))
		{
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

	private Vector3 GetMouseWorldPosition()
	{
		Vector2 mouse = Mouse.current.position.ReadValue();
		float depth = -mainCamera.transform.position.z;
		Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, depth));
		world.z = 0f;
		return world;
	}
}
