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

			// Check for collisions with transitions before moving
			float collisionCheckRadius = avatarCollisionRadius;
			Collider2D[] colliders = Physics2D.OverlapCircleAll(newPosition, collisionCheckRadius);
			bool canMove = true;
			foreach (Collider2D col in colliders)
			{
				// Ignore arcs, avatars, held transitions and triggers
				if (col.isTrigger) { continue; }
				string objName = col.gameObject.name;
				if (objName.StartsWith("A_") || objName.StartsWith("LocalAvatar") || objName.StartsWith("RemoteAvatar") || objName == heldTransitionId)
				{
					continue;
				}

				if (!string.IsNullOrEmpty(temporarilyIgnoredCollisionNodeId)
					&& Time.unscaledTime <= temporarilyIgnoredCollisionUntilTime
					&& objName == temporarilyIgnoredCollisionNodeId)
				{
					continue;
				}

				canMove = false;
				break;
			}

			if (canMove)
			{
				avatarPosition = newPosition;
			}

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

		// Check if avatar is in pool zone
		Vector2 avatarPos2D = new Vector2(avatarPosition.x, avatarPosition.y);
		if (IsInsideSharedPoolZone(avatarPos2D))
		{
			// Return to pool
			RequestReturnTransitionToPool(heldTransitionId);
			transition.isSharedPoolAvailable = true;
			transition.ownerClientId = UnassignedOwnerClientId;
			if (transition.id == temporarilyIgnoredCollisionNodeId)
			{
				temporarilyIgnoredCollisionNodeId = null;
			}
			heldTransitionId = null;
			RefreshPetriNetVisuals();
		}
		else
		{
			// Place transition slightly in front of avatar so we don't clip into it
			float rad = avatarRotation * Mathf.Deg2Rad;
			float dropOffset = avatarCollisionRadius + transitionCollisionRadius + 0.05f;
			Vector3 dropPosition = avatarPosition + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * dropOffset;

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

	private void RequestReturnTransitionToPool(string transitionId)
	{
		ExecuteOrSendCommand(new CommandData { action = "ReturnSharedTransition", id = transitionId });
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
