using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class GameManager
{
	private const ulong NoGameplayMenuOwnerClientId = ulong.MaxValue;

	private int selectedLevelIndex;
	private bool levelSelectionConfirmed;
	private bool gameplayMenuOpen;
	private ulong gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
	private bool levelEnded;
	private bool levelSelectionAvatarStateSent;
	private Vector2 levelResultScrollPosition;
	private Vector2 levelPauseScrollPosition;
	private Transform levelSelectionRoot;
	private Transform levelSelectionPlatform;
	private LineRenderer levelSelectionBoundaryLine;
	private Collider2D levelConfirmButtonCollider;
	private TextMesh levelInfoText;
	private readonly Dictionary<Collider2D, int> levelButtonByCollider = new Dictionary<Collider2D, int>();
	private Rect levelSelectionMovementBounds = Rect.MinMaxRect(-4.45f, -3.25f, 4.45f, 3.25f);
	private const int LevelSelectionGridColumns = 4;
	private const float LevelSelectionButtonSize = 0.82f;
	private const float LevelSelectionButtonGap = 0.24f;
	private const float LevelSelectionGridStartX = -3.55f;
	private const float LevelSelectionGridStartY = 2.32f;
	private const float LevelSelectionPlatformWidth = 9.6f;
	private const float LevelSelectionPlatformHeight = 7.25f;
	private const float LevelSelectionPlatformDepth = 0.04f;
	private const float LevelSelectionContentPadding = 1.35f;
	private const float LevelSelectionAvatarBoundaryPadding = 0.45f;
	private const float LevelSelectionCameraViewPadding = 1.35f;
	private const float LevelSelectionMinimumHalfWidth = 4.8f;
	private const float LevelSelectionMinimumHalfHeight = 3.6f;
	private const float LevelSelectionNumberTextSize = 0.14f;
	private const float LevelSelectionConfirmTextSize = 0.055f;
	private const float LevelSelectionInfoTextSize = 0.052f;
	private const string LevelSelectionButtonVisualName = "ButtonBlock3D";

	private void OnGUI()
	{
		if (!showLevelSelection)
		{
			return;
		}

		if (!gameplayInitialized && IsGameplayConnectionReady())
		{
			DrawLevelSelectionHeaderOverlay();
		}

		if (IsGameplayMenuOpen())
		{
			DrawGameplayMenu();
		}
	}

	private void DrawLevelSelectionHeaderOverlay()
	{
		float uiScale = Mathf.Clamp(Screen.height / 900f, 0.9f, 1.55f);
		GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.UpperCenter,
			fontSize = Mathf.RoundToInt(34f * uiScale),
			fontStyle = FontStyle.Bold,
			wordWrap = false
		};
		titleStyle.normal.textColor = Color.black;
		GUI.Label(new Rect(0f, 12f * uiScale, Screen.width, 52f * uiScale), "Levelübersicht", titleStyle);
	}

	private void EnsureLevelSelectionScreen()
	{
		if (!showLevelSelection || gameplayInitialized)
		{
			return;
		}

		EnsureGraphRootExists();
		if (levelSelectionRoot == null || mainCamera == null)
		{
			ConfigureLevelSelectionCamera();
		}

		EnsureLevelSelectionAvatarStartPosition();
		EnsureLevelSelectionVisuals();
		UpdateLevelSelectionVisuals();
		UpdateAvatarVisuals();
		UpdateLevelSelectionHoverVisual();
	}

	private void ConfigureLevelSelectionCamera()
	{
		if (mainCamera == null)
		{
			mainCamera = Camera.main;
		}

		if (mainCamera == null)
		{
			return;
		}

		ConfigureCamera(mainCamera);
		ConfigureSceneLight();
		EnsureGroundPlane();
	}

	private void EnsureLevelSelectionAvatarStartPosition()
	{
		if (avatarStartPositionApplied)
		{
			EnsureLevelSelectionRemoteAvatarStartPositions(false);
			return;
		}

		ResetLocalAvatarToLevelSelectionStartPosition();
		EnsureLevelSelectionRemoteAvatarStartPositions(false);
	}

	private void ResetLocalAvatarToLevelSelectionStartPosition()
	{
		avatarPosition = GetDefaultLevelSelectionAvatarStartPosition(GetLocalActorClientId());
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
		levelSelectionAvatarStateSent = false;
	}

	private Vector3 GetDefaultLevelSelectionAvatarStartPosition(ulong actorClientId)
	{
		return IsActorTopSide(actorClientId)
			? new Vector3(-3.8f, 2.85f, 0f)
			: new Vector3(-3.8f, -2.85f, 0f);
	}

	private void EnsureLevelSelectionRemoteAvatarStartPositions(bool overwriteExisting)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return;
		}

		if (!Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			SeedRemoteAvatarLevelSelectionStartPosition(NetworkManager.ServerClientId, overwriteExisting);
		}

		foreach (ulong clientId in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds)
		{
			SeedRemoteAvatarLevelSelectionStartPosition(clientId, overwriteExisting);
		}
	}

	private void SeedRemoteAvatarLevelSelectionStartPosition(ulong clientId, bool overwriteExisting)
	{
		if (clientId == GetLocalActorClientId())
		{
			return;
		}

		if (!overwriteExisting && remoteAvatarPositions.ContainsKey(clientId))
		{
			return;
		}

		remoteAvatarPositions[clientId] = GetDefaultLevelSelectionAvatarStartPosition(clientId);
		remoteAvatarRotations[clientId] = 0f;
		remoteAvatarInventories[clientId] = new RemoteHeldObjectState { kind = HeldObjectKind.None, id = "", offset = Vector2.zero };
		remoteAvatarCraneHeights[clientId] = avatarCraneRestHeight;
	}

	private void EnsureLevelSelectionVisuals()
	{
		if (levelSelectionRoot != null)
		{
			return;
		}

		levelSelectionRoot = new GameObject("LevelSelectionScreen").transform;
		levelSelectionRoot.SetParent(petriNetRoot, false);
		levelButtonByCollider.Clear();
		levelConfirmButtonCollider = null;

		GameObject background = GameObject.CreatePrimitive(PrimitiveType.Cube);
		background.name = "LevelSelectionPlatform";
		background.transform.SetParent(levelSelectionRoot, false);
		background.transform.position = new Vector3(0f, 0f, -LevelSelectionPlatformDepth * 0.5f);
		background.transform.localScale = new Vector3(LevelSelectionPlatformWidth, LevelSelectionPlatformHeight, LevelSelectionPlatformDepth);
		levelSelectionPlatform = background.transform;
		Collider backgroundCollider = background.GetComponent<Collider>();
		if (backgroundCollider != null)
		{
			Destroy(backgroundCollider);
		}

		MeshRenderer backgroundRenderer = background.GetComponent<MeshRenderer>();
		if (backgroundRenderer != null)
		{
			backgroundRenderer.material = CreatePrimitiveVisualMaterial(GetLevelSelectionFloorColor());
			ConfigureMeshRendererFor3D(backgroundRenderer, false, true);
		}

		GameObject boundaryObject = new GameObject("LevelSelectionMovementBoundary");
		boundaryObject.transform.SetParent(levelSelectionRoot, false);
		levelSelectionBoundaryLine = boundaryObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(levelSelectionBoundaryLine, 4, 0.075f, 80, new Color(0.08f, 0.12f, 0.16f, 0.9f), 6, 8, true);

		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		for (int i = 0; i < levels.Count; i++)
		{
			Vector2 position = GetLevelSelectionButtonPosition(i);
			Collider2D collider = CreateLevelSelectionButton(
				"LevelButton_" + (i + 1),
				position,
				new Vector2(LevelSelectionButtonSize, LevelSelectionButtonSize),
				(i + 1).ToString(),
				new Color(1f, 0.9f, 0.72f),
				30,
				LevelSelectionNumberTextSize);
			if (collider != null)
			{
				levelButtonByCollider[collider] = i;
			}
		}

		levelConfirmButtonCollider = CreateLevelSelectionButton(
			"ConfirmLevelButton",
			GetLevelSelectionConfirmButtonPosition(levels.Count),
			new Vector2(2.1f, 0.66f),
			"Bestätigen",
			new Color(0.78f, 0.92f, 1f),
			30,
			LevelSelectionConfirmTextSize);

		GameObject infoObject = new GameObject("LevelInfo");
		infoObject.transform.SetParent(levelSelectionRoot, false);
		infoObject.transform.position = new Vector3(0.2f, 2.35f, NodeLabelLayerZ);
		levelInfoText = infoObject.AddComponent<TextMesh>();
		levelInfoText.characterSize = LevelSelectionInfoTextSize;
		levelInfoText.fontSize = 64;
		levelInfoText.anchor = TextAnchor.UpperLeft;
		levelInfoText.alignment = TextAlignment.Left;
		levelInfoText.color = Color.black;
		SetTextSortingOrder(levelInfoText, 72);
	}

	private Vector2 GetLevelSelectionButtonPosition(int index)
	{
		int safeIndex = Mathf.Max(0, index);
		int column = safeIndex % LevelSelectionGridColumns;
		int row = safeIndex / LevelSelectionGridColumns;
		float step = LevelSelectionButtonSize + LevelSelectionButtonGap;
		return new Vector2(
			LevelSelectionGridStartX + column * step,
			LevelSelectionGridStartY - row * step);
	}

	private Vector2 GetLevelSelectionConfirmButtonPosition(int levelCount)
	{
		int rows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, levelCount) / (float)LevelSelectionGridColumns));
		float gridWidth = LevelSelectionGridColumns * LevelSelectionButtonSize + (LevelSelectionGridColumns - 1) * LevelSelectionButtonGap;
		float x = LevelSelectionGridStartX + gridWidth * 0.5f - LevelSelectionButtonSize * 0.5f;
		float y = LevelSelectionGridStartY - rows * (LevelSelectionButtonSize + LevelSelectionButtonGap) - 0.26f;
		return new Vector2(x, y);
	}

	private Collider2D CreateLevelSelectionButton(string name, Vector2 position, Vector2 size, string label, Color color, int sortingOrder, float labelCharacterSize)
	{
		GameObject button = new GameObject(name);
		button.transform.SetParent(levelSelectionRoot, false);
		button.transform.position = new Vector3(position.x, position.y, 0f);
		button.transform.localScale = new Vector3(size.x, size.y, 1f);

		SpriteRenderer renderer = button.AddComponent<SpriteRenderer>();
		renderer.sprite = GetSquareSprite();
		renderer.color = new Color(color.r, color.g, color.b, 0f);
		renderer.sortingOrder = sortingOrder;
		MakeSpriteRendererInvisible(renderer);

		MeshRenderer visualRenderer = CreatePrimitiveVisual3D(
			button.transform,
			LevelSelectionButtonVisualName,
			PrimitiveType.Cube,
			color,
			new Vector3(0f, 0f, NodeVisualCenterZ),
			new Vector3(1f, 1f, NodeVisualHeight),
			Quaternion.identity);
		ConfigureMeshRendererFor3D(visualRenderer, true, true);

		BoxCollider2D collider = button.AddComponent<BoxCollider2D>();

		GameObject labelObject = new GameObject("Label");
		labelObject.transform.SetParent(button.transform, false);
		labelObject.transform.localPosition = new Vector3(0f, 0f, NodeLabelLayerZ);
		labelObject.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
		TextMesh text = labelObject.AddComponent<TextMesh>();
		text.text = label;
		text.characterSize = labelCharacterSize;
		text.fontSize = 64;
		text.anchor = TextAnchor.MiddleCenter;
		text.alignment = TextAlignment.Center;
		text.color = Color.black;
		SetTextSortingOrder(text, sortingOrder + 2);
		FitLevelSelectionButtonText(text, size);

		return collider;
	}

	private void FitLevelSelectionButtonText(TextMesh text, Vector2 buttonSize)
	{
		if (text == null)
		{
			return;
		}

		MeshRenderer renderer = text.GetComponent<MeshRenderer>();
		if (renderer == null)
		{
			return;
		}

		float allowedWidth = buttonSize.x * 0.66f;
		float allowedHeight = buttonSize.y * 0.5f;
		for (int i = 0; i < 6; i++)
		{
			Vector3 labelSize = renderer.bounds.size;
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

			text.characterSize = Mathf.Max(0.026f, text.characterSize * scale * 0.94f);
		}
	}

	private void SetTextSortingOrder(TextMesh text, int sortingOrder)
	{
		if (text == null)
		{
			return;
		}

		MeshRenderer renderer = text.GetComponent<MeshRenderer>();
		if (renderer != null)
		{
			renderer.sortingOrder = sortingOrder;
		}
	}

	private void UpdateLevelSelectionVisuals()
	{
		if (levelSelectionRoot == null)
		{
			return;
		}

		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		selectedLevelIndex = levels.Count > 0 ? Mathf.Clamp(selectedLevelIndex, 0, levels.Count - 1) : 0;
		TryGetLevelButtonAtPoint(new Vector2(avatarPosition.x, avatarPosition.y), out int hoveredLevelIndex);
		foreach (KeyValuePair<Collider2D, int> pair in levelButtonByCollider)
		{
			if (pair.Key == null)
			{
				continue;
			}

			Color buttonColor = new Color(1f, 0.9f, 0.72f);
			if (pair.Value == hoveredLevelIndex)
			{
				buttonColor = new Color(1f, 0.96f, 0.82f);
			}

			if (pair.Value == selectedLevelIndex)
			{
				buttonColor = new Color(0.97f, 0.53f, 0.12f);
			}

			SetLevelSelectionButtonColor(pair.Key.transform, buttonColor);
		}

		if (levelConfirmButtonCollider != null)
		{
			bool confirmHovered = IsLevelConfirmButtonAtPoint(new Vector2(avatarPosition.x, avatarPosition.y));
			SetLevelSelectionButtonColor(
				levelConfirmButtonCollider.transform,
				confirmHovered ? new Color(0.56f, 0.84f, 1f) : new Color(0.78f, 0.92f, 1f));

			TextMesh confirmText = levelConfirmButtonCollider.transform.Find("Label")?.GetComponent<TextMesh>();
			if (confirmText != null)
			{
				confirmText.text = "Bestätigen";
				Vector3 confirmScale = levelConfirmButtonCollider.transform.localScale;
				FitLevelSelectionButtonText(confirmText, new Vector2(Mathf.Abs(confirmScale.x), Mathf.Abs(confirmScale.y)));
			}
		}

		if (levelInfoText != null && levels.Count > 0)
		{
			levelInfoText.text = GetLevelInfoText(levels[selectedLevelIndex]);
		}

		UpdateLevelSelectionBoundsAndFloor();
	}

	private Color GetLevelSelectionFloorColor()
	{
		return new Color(0.9f, 0.93f, 0.91f);
	}

	private void UpdateLevelSelectionBoundsAndFloor()
	{
		if (levelSelectionRoot == null)
		{
			return;
		}

		Rect contentRect = CalculateLevelSelectionContentRect();
		Rect paddedRect = Rect.MinMaxRect(
			contentRect.xMin - LevelSelectionContentPadding,
			contentRect.yMin - LevelSelectionContentPadding,
			contentRect.xMax + LevelSelectionContentPadding,
			contentRect.yMax + LevelSelectionContentPadding);
		float halfWidth = Mathf.Max(LevelSelectionMinimumHalfWidth, paddedRect.width * 0.5f);
		float halfHeight = Mathf.Max(LevelSelectionMinimumHalfHeight, paddedRect.height * 0.5f);
		Vector2 center = paddedRect.center;
		levelSelectionMovementBounds = Rect.MinMaxRect(center.x - halfWidth, center.y - halfHeight, center.x + halfWidth, center.y + halfHeight);

		if (levelSelectionPlatform != null)
		{
			levelSelectionPlatform.position = new Vector3(levelSelectionMovementBounds.center.x, levelSelectionMovementBounds.center.y, -LevelSelectionPlatformDepth * 0.5f);
			levelSelectionPlatform.localScale = new Vector3(levelSelectionMovementBounds.width, levelSelectionMovementBounds.height, LevelSelectionPlatformDepth);
			MeshRenderer platformRenderer = levelSelectionPlatform.GetComponent<MeshRenderer>();
			if (platformRenderer != null)
			{
				SetPrimitiveVisualColor(platformRenderer, GetLevelSelectionFloorColor());
			}
		}

		SetLevelSelectionBoundaryLine(levelSelectionMovementBounds);
	}

	private Rect CalculateLevelSelectionContentRect()
	{
		Rect contentRect = Rect.MinMaxRect(-LevelSelectionPlatformWidth * 0.5f, -LevelSelectionPlatformHeight * 0.5f, LevelSelectionPlatformWidth * 0.5f, LevelSelectionPlatformHeight * 0.5f);
		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		for (int i = 0; i < levels.Count; i++)
		{
			contentRect = EncapsulateLevelSelectionRect(contentRect, GetLevelSelectionItemRect(GetLevelSelectionButtonPosition(i), new Vector2(LevelSelectionButtonSize, LevelSelectionButtonSize)));
		}

		contentRect = EncapsulateLevelSelectionRect(
			contentRect,
			GetLevelSelectionItemRect(GetLevelSelectionConfirmButtonPosition(levels.Count), new Vector2(2.1f, 0.66f)));

		if (TryGetLevelSelectionTextRect(levelInfoText, out Rect infoRect))
		{
			contentRect = EncapsulateLevelSelectionRect(contentRect, infoRect);
		}

		return contentRect;
	}

	private Rect GetLevelSelectionItemRect(Vector2 center, Vector2 size)
	{
		Vector2 halfSize = size * 0.5f;
		return Rect.MinMaxRect(center.x - halfSize.x, center.y - halfSize.y, center.x + halfSize.x, center.y + halfSize.y);
	}

	private bool TryGetLevelSelectionTextRect(TextMesh text, out Rect rect)
	{
		rect = new Rect();
		if (text == null || string.IsNullOrEmpty(text.text))
		{
			return false;
		}

		MeshRenderer renderer = text.GetComponent<MeshRenderer>();
		if (renderer != null)
		{
			Bounds bounds = renderer.bounds;
			if (bounds.size.x > 0.001f && bounds.size.y > 0.001f)
			{
				rect = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
				return true;
			}
		}

		string[] lines = text.text.Split('\n');
		int maxLineLength = 0;
		for (int i = 0; i < lines.Length; i++)
		{
			maxLineLength = Mathf.Max(maxLineLength, lines[i].Length);
		}

		float width = Mathf.Max(0.5f, maxLineLength * text.characterSize * 0.52f);
		float height = Mathf.Max(0.5f, lines.Length * text.characterSize * 1.35f);
		Vector3 position = text.transform.position;
		rect = Rect.MinMaxRect(position.x, position.y - height, position.x + width, position.y);
		return true;
	}

	private Rect EncapsulateLevelSelectionRect(Rect current, Rect addition)
	{
		return Rect.MinMaxRect(
			Mathf.Min(current.xMin, addition.xMin),
			Mathf.Min(current.yMin, addition.yMin),
			Mathf.Max(current.xMax, addition.xMax),
			Mathf.Max(current.yMax, addition.yMax));
	}

	private void SetLevelSelectionBoundaryLine(Rect bounds)
	{
		if (levelSelectionBoundaryLine == null)
		{
			return;
		}

		levelSelectionBoundaryLine.SetPosition(0, new Vector3(bounds.xMin, bounds.yMax, ArcZ));
		levelSelectionBoundaryLine.SetPosition(1, new Vector3(bounds.xMax, bounds.yMax, ArcZ));
		levelSelectionBoundaryLine.SetPosition(2, new Vector3(bounds.xMax, bounds.yMin, ArcZ));
		levelSelectionBoundaryLine.SetPosition(3, new Vector3(bounds.xMin, bounds.yMin, ArcZ));
	}

	private void SetLevelSelectionButtonColor(Transform buttonTransform, Color color)
	{
		if (buttonTransform == null)
		{
			return;
		}

		Transform visual = buttonTransform.Find(LevelSelectionButtonVisualName);
		MeshRenderer meshRenderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
		if (meshRenderer != null)
		{
			SetPrimitiveVisualColor(meshRenderer, color);
		}
	}

	private string GetLevelInfoText(PetriNetLevelDefinition level)
	{
		if (level == null)
		{
			return "";
		}

		StringBuilder text = new StringBuilder();
		text.Append(GetFallbackText(level.displayName, "Level"));
		text.Append("\n\nInhibitor-Arcs:\n");
		text.Append(GetLevelInhibitorOverviewText(level.inhibitorArcs));
		text.Append("\nGeteilte Blöcke:\n");
		text.Append(GetLevelBlockOverviewText(level.blocks, PetriNetLevelBlockOwner.geteilt));
		text.Append("\n\nSpieler1-Blöcke:\n");
		text.Append(GetLevelBlockOverviewText(level.blocks, PetriNetLevelBlockOwner.spieler1));
		text.Append("\n\nSpieler2-Blöcke:\n");
		text.Append(GetLevelBlockOverviewText(level.blocks, PetriNetLevelBlockOwner.spieler2));
		text.Append("\nOben:\n");
		text.Append(JoinLevelList(level.topIngredients));
		text.Append("\n\nUnten:\n");
		text.Append(JoinLevelList(level.bottomIngredients));
		text.Append("\n\nGerichte:\n");
		text.Append(GetLevelOrderOverviewText(level));
		text.Append("\n\nExtras:\n");
		text.Append(JoinLevelList(level.extras));
		return text.ToString();
	}

	private void HandleLevelSelectionInput()
	{
		if (!showLevelSelection || gameplayInitialized || !IsGameplayConnectionReady())
		{
			return;
		}

		Keyboard keyboard = Keyboard.current;
		if (keyboard == null)
		{
			SendLevelSelectionAvatarUpdateIfNeeded();
			UpdateAvatarVisuals();
			UpdateLevelSelectionHoverVisual();
			return;
		}

		Vector3 moveDirection = Vector3.zero;
		if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) { moveDirection.y += 1f; }
		if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) { moveDirection.y -= 1f; }
		if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) { moveDirection.x -= 1f; }
		if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) { moveDirection.x += 1f; }

		if (moveDirection.sqrMagnitude > 0.1f)
		{
			moveDirection = moveDirection.normalized;
			bool sprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
			float currentSpeed = sprinting ? avatarSpeed * avatarSprintMultiplier : avatarSpeed;
			avatarPosition = ClampLevelSelectionAvatarPosition(avatarPosition + moveDirection * currentSpeed * Time.deltaTime);
			avatarRotation = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
		}

		if (keyboard.spaceKey.wasPressedThisFrame)
		{
			StartCraneDipAnimation();
			ActivateLevelSelectionAtPoint(new Vector2(avatarPosition.x, avatarPosition.y), true);
		}

		if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			Vector3 mouseWorld = GetMouseWorldPosition();
			ActivateLevelSelectionAtPoint(new Vector2(mouseWorld.x, mouseWorld.y), true);
		}

		UpdateLevelSelectionVisuals();
		SendLevelSelectionAvatarUpdateIfNeeded();
		UpdateAvatarVisuals();
		UpdateLevelSelectionHoverVisual();
	}

	private Vector3 ClampLevelSelectionAvatarPosition(Vector3 desired)
	{
		if (levelSelectionMovementBounds.width <= 0.001f || levelSelectionMovementBounds.height <= 0.001f)
		{
			return new Vector3(Mathf.Clamp(desired.x, -4.45f, 4.45f), Mathf.Clamp(desired.y, -3.25f, 3.25f), 0f);
		}

		float avatarBoundaryPadding = Mathf.Max(LevelSelectionAvatarBoundaryPadding, avatarCollisionRadius);
		Rect centerBounds = Rect.MinMaxRect(
			levelSelectionMovementBounds.xMin + avatarBoundaryPadding,
			levelSelectionMovementBounds.yMin + avatarBoundaryPadding,
			levelSelectionMovementBounds.xMax - avatarBoundaryPadding,
			levelSelectionMovementBounds.yMax - avatarBoundaryPadding);
		if (centerBounds.width <= 0.001f || centerBounds.height <= 0.001f)
		{
			centerBounds = levelSelectionMovementBounds;
		}

		return new Vector3(
			Mathf.Clamp(desired.x, centerBounds.xMin, centerBounds.xMax),
			Mathf.Clamp(desired.y, centerBounds.yMin, centerBounds.yMax),
			0f);
	}

	private void UpdateLevelSelectionCameraFollow()
	{
		if (mainCamera == null)
		{
			return;
		}

		float requiredSize = GetSharedScreenCameraSize();
		if (mainCamera.orthographicSize < requiredSize)
		{
			mainCamera.orthographicSize = requiredSize;
		}

		isMiddlePanning = false;
		Vector2 cameraCenter = GetCameraGroundCenter();
		Vector2 clampedCameraCenter = ClampLevelSelectionCameraCenter(cameraCenter);
		if ((clampedCameraCenter - cameraCenter).sqrMagnitude > 0.0001f)
		{
			cameraVelocityX = 0f;
			cameraVelocityY = 0f;
			SetCameraGroundCenter(mainCamera, clampedCameraCenter);
			cameraCenter = clampedCameraCenter;
		}

		float screenHeight = mainCamera.orthographicSize * 2f;
		float screenWidth = screenHeight * mainCamera.aspect;
		float restMarginX = screenWidth * cameraRestAreaMargin;
		float restMarginY = screenHeight * cameraRestAreaMargin;
		Vector2 target = cameraCenter;
		bool shouldMoveX = false;
		bool shouldMoveY = false;

		if (avatarPosition.x > cameraCenter.x + restMarginX)
		{
			target.x = avatarPosition.x - restMarginX;
			shouldMoveX = true;
		}
		else if (avatarPosition.x < cameraCenter.x - restMarginX)
		{
			target.x = avatarPosition.x + restMarginX;
			shouldMoveX = true;
		}

		if (avatarPosition.y > cameraCenter.y + restMarginY)
		{
			target.y = avatarPosition.y - restMarginY;
			shouldMoveY = true;
		}
		else if (avatarPosition.y < cameraCenter.y - restMarginY)
		{
			target.y = avatarPosition.y + restMarginY;
			shouldMoveY = true;
		}

		target = ClampLevelSelectionCameraCenter(target);
		Vector2 newCenter = cameraCenter;
		if (shouldMoveX && Mathf.Abs(target.x - cameraCenter.x) > 0.001f)
		{
			newCenter.x = SmoothCameraAxis(cameraCenter.x, target.x, ref cameraVelocityX, 0.16f);
		}
		else
		{
			cameraVelocityX = 0f;
		}

		if (shouldMoveY && Mathf.Abs(target.y - cameraCenter.y) > 0.001f)
		{
			newCenter.y = SmoothCameraAxis(cameraCenter.y, target.y, ref cameraVelocityY, 0.16f);
		}
		else
		{
			cameraVelocityY = 0f;
		}

		Vector2 boundedCenter = ClampLevelSelectionCameraCenter(newCenter);
		if (Mathf.Abs(boundedCenter.x - newCenter.x) > 0.0001f)
		{
			cameraVelocityX = 0f;
		}

		if (Mathf.Abs(boundedCenter.y - newCenter.y) > 0.0001f)
		{
			cameraVelocityY = 0f;
		}

		SetCameraGroundCenter(mainCamera, boundedCenter);
	}

	private Vector2 ClampLevelSelectionCameraCenter(Vector2 desiredCenter)
	{
		if (levelSelectionMovementBounds.width <= 0.001f || levelSelectionMovementBounds.height <= 0.001f || mainCamera == null)
		{
			return desiredCenter;
		}

		Rect centerBounds = GetLevelSelectionCameraCenterBounds();
		return new Vector2(
			Mathf.Clamp(desiredCenter.x, centerBounds.xMin, centerBounds.xMax),
			Mathf.Clamp(desiredCenter.y, centerBounds.yMin, centerBounds.yMax));
	}

	private Rect GetLevelSelectionCameraCenterBounds()
	{
		Vector2 cameraCenter = GetCameraGroundCenter();
		Rect viewBounds = GetCameraGroundViewBounds();
		Rect cameraViewBounds = GetLevelSelectionCameraViewBounds();
		float leftOffset = cameraCenter.x - viewBounds.xMin;
		float rightOffset = viewBounds.xMax - cameraCenter.x;
		float bottomOffset = cameraCenter.y - viewBounds.yMin;
		float topOffset = viewBounds.yMax - cameraCenter.y;
		float minX = cameraViewBounds.xMin + leftOffset;
		float maxX = cameraViewBounds.xMax - rightOffset;
		float minY = cameraViewBounds.yMin + bottomOffset;
		float maxY = cameraViewBounds.yMax - topOffset;

		if (minX > maxX)
		{
			minX = cameraViewBounds.center.x;
			maxX = minX;
		}

		if (minY > maxY)
		{
			minY = cameraViewBounds.center.y;
			maxY = minY;
		}

		return Rect.MinMaxRect(minX, minY, maxX, maxY);
	}

	private Rect GetLevelSelectionCameraViewBounds()
	{
		return Rect.MinMaxRect(
			levelSelectionMovementBounds.xMin - LevelSelectionCameraViewPadding,
			levelSelectionMovementBounds.yMin - LevelSelectionCameraViewPadding,
			levelSelectionMovementBounds.xMax + LevelSelectionCameraViewPadding,
			levelSelectionMovementBounds.yMax + LevelSelectionCameraViewPadding);
	}

	private Rect GetCameraGroundViewBounds()
	{
		Vector3 bottomLeft = GetCameraGroundViewportPoint(new Vector2(0f, 0f));
		Vector3 topLeft = GetCameraGroundViewportPoint(new Vector2(0f, 1f));
		Vector3 topRight = GetCameraGroundViewportPoint(new Vector2(1f, 1f));
		Vector3 bottomRight = GetCameraGroundViewportPoint(new Vector2(1f, 0f));
		float minX = Mathf.Min(bottomLeft.x, topLeft.x, topRight.x, bottomRight.x);
		float maxX = Mathf.Max(bottomLeft.x, topLeft.x, topRight.x, bottomRight.x);
		float minY = Mathf.Min(bottomLeft.y, topLeft.y, topRight.y, bottomRight.y);
		float maxY = Mathf.Max(bottomLeft.y, topLeft.y, topRight.y, bottomRight.y);
		return Rect.MinMaxRect(minX, minY, maxX, maxY);
	}

	private void ActivateLevelSelectionAtPoint(Vector2 worldPoint, bool broadcastSelection)
	{
		if (TryGetLevelButtonAtPoint(worldPoint, out int levelIndex))
		{
			SelectLevelIndex(levelIndex, broadcastSelection);
			return;
		}

		if (IsLevelConfirmButtonAtPoint(worldPoint))
		{
			RequestConfirmLevelSelection();
		}
	}

	private void UpdateLevelSelectionHoverVisual()
	{
		if (levelSelectionRoot == null || gameplayInitialized)
		{
			HideCraneHoverNodeVisual();
			return;
		}

		if (TryGetLevelButtonColliderAtPoint(new Vector2(avatarPosition.x, avatarPosition.y), out Collider2D levelCollider, out _))
		{
			ShowLevelSelectionButtonHoverVisual(levelCollider);
			return;
		}

		if (IsLevelConfirmButtonAtPoint(new Vector2(avatarPosition.x, avatarPosition.y)))
		{
			ShowLevelSelectionButtonHoverVisual(levelConfirmButtonCollider);
			return;
		}

		HideCraneHoverNodeVisual();
	}

	private void ShowLevelSelectionButtonHoverVisual(Collider2D levelCollider)
	{
		if (levelCollider == null)
		{
			HideCraneHoverNodeVisual();
			return;
		}

		EnsureCraneHoverNodeVisual();
		localCraneHoverNodeShadow.SetActive(true);
		Bounds bounds = levelCollider.bounds;
		SetCraneHoverRectOutline(new Rect(
			bounds.min.x - 0.08f,
			bounds.min.y - 0.08f,
			bounds.size.x + 0.16f,
			bounds.size.y + 0.16f));
	}

	private void SelectLevelIndex(int levelIndex, bool broadcast)
	{
		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		if (levels.Count <= 0)
		{
			return;
		}

		selectedLevelIndex = Mathf.Clamp(levelIndex, 0, levels.Count - 1);
		UpdateLevelSelectionVisuals();
		if (broadcast && IsHostOrOffline())
		{
			BroadcastLevelSelectionStateToClients();
		}
		else if (broadcast)
		{
			RequestSelectLevelSelection(selectedLevelIndex);
		}
	}

	private bool TryGetLevelButtonAtPoint(Vector2 worldPoint, out int levelIndex)
	{
		return TryGetLevelButtonColliderAtPoint(worldPoint, out _, out levelIndex);
	}

	private bool TryGetLevelButtonColliderAtPoint(Vector2 worldPoint, out Collider2D levelCollider, out int levelIndex)
	{
		levelCollider = null;
		levelIndex = -1;
		Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i] != null && levelButtonByCollider.TryGetValue(hits[i], out levelIndex))
			{
				levelCollider = hits[i];
				return true;
			}
		}

		return false;
	}

	private bool IsLevelConfirmButtonAtPoint(Vector2 worldPoint)
	{
		if (levelConfirmButtonCollider == null)
		{
			return false;
		}

		Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i] == levelConfirmButtonCollider)
			{
				return true;
			}
		}

		return false;
	}

	private void SendLevelSelectionAvatarUpdateIfNeeded()
	{
		string currentHeldNetworkKey = GetCurrentHeldNetworkKey();
		float movedDistance = Vector3.Distance(lastAvatarPosition, avatarPosition);
		bool heldObjectChanged = lastAvatarNetworkSyncHeldId != currentHeldNetworkKey;
		bool rotationChanged = Mathf.Abs(Mathf.DeltaAngle(lastAvatarNetworkSyncRotation, avatarRotation)) > 2f;
		bool reliableHeartbeatDue = Time.unscaledTime >= nextReliableAvatarNetworkSyncTime;
		bool shouldSendAvatarUpdate = !levelSelectionAvatarStateSent
			|| heldObjectChanged
			|| movedDistance > 0.65f
			|| reliableHeartbeatDue
			|| ((movedDistance > 0.05f || rotationChanged) && Time.unscaledTime >= nextAvatarNetworkSyncTime);
		if (!shouldSendAvatarUpdate)
		{
			return;
		}

		bool reliable = !levelSelectionAvatarStateSent || heldObjectChanged || reliableHeartbeatDue;
		nextAvatarNetworkSyncTime = Time.unscaledTime + avatarNetworkSyncInterval;
		if (reliable)
		{
			nextReliableAvatarNetworkSyncTime = Time.unscaledTime + reliableAvatarNetworkSyncInterval;
		}

		lastAvatarPosition = avatarPosition;
		lastAvatarNetworkSyncRotation = avatarRotation;
		lastAvatarNetworkSyncHeldId = currentHeldNetworkKey;
		lastAvatarNetworkSyncCraneHeight = avatarCraneCurrentHeight;
		levelSelectionAvatarStateSent = true;
		SendAvatarUpdate(avatarPosition, avatarRotation, heldTransitionId, reliable);
	}

	private void DestroyLevelSelectionScreen()
	{
		if (levelSelectionRoot != null)
		{
			levelSelectionRoot.gameObject.SetActive(false);
			Destroy(levelSelectionRoot.gameObject);
			levelSelectionRoot = null;
		}

		levelButtonByCollider.Clear();
		levelSelectionPlatform = null;
		levelSelectionBoundaryLine = null;
		levelSelectionMovementBounds = Rect.MinMaxRect(-4.45f, -3.25f, 4.45f, 3.25f);
		levelConfirmButtonCollider = null;
		levelInfoText = null;
	}

	private void DrawGameplayMenu()
	{
		float uiScale = GetGameplayMenuUiScale();
		float maxPanelWidth = Mathf.Max(320f, Screen.width - 36f * uiScale);
		float maxPanelHeight = Mathf.Max(320f, Screen.height - 36f * uiScale);
		float panelWidth = Mathf.Min((levelEnded ? 680f : 780f) * uiScale, maxPanelWidth);
		float panelHeight = Mathf.Min((levelEnded ? 620f : 760f) * uiScale, maxPanelHeight);
		Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
		GUIStyle menuBoxStyle = new GUIStyle(GUI.skin.box)
		{
			alignment = TextAnchor.UpperCenter,
			fontSize = Mathf.RoundToInt(40f * uiScale),
			fontStyle = FontStyle.Bold
		};
		GUI.Box(panel, levelEnded ? "Level beendet" : "Menü", menuBoxStyle);
		float x = panel.x + 24f * uiScale;
		float y = panel.y + 62f * uiScale;
		float contentWidth = panel.width - 48f * uiScale;
		bool canControlMenu = CanControlGameplayMenu();
		bool previousGuiEnabled = GUI.enabled;
		GUIStyle menuButtonStyle = new GUIStyle(GUI.skin.button)
		{
			fontSize = Mathf.RoundToInt(34f * uiScale),
			fontStyle = FontStyle.Bold
		};

		if (levelEnded)
		{
			GUIStyle resultTitleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = Mathf.RoundToInt(38f * uiScale),
				fontStyle = FontStyle.Bold
			};
			GUI.Label(new Rect(x, y, contentWidth, 50f * uiScale), "Auswertung", resultTitleStyle);
			y += 62f * uiScale;

			Rect scrollRect = new Rect(x, y, contentWidth, panel.height - 160f * uiScale);
			int orderCount = GetLevelOrderCount();
			Rect contentRect = new Rect(0f, 0f, scrollRect.width - 18f * uiScale, Mathf.Max(scrollRect.height, (130f + orderCount * 136f) * uiScale));
			GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				fontSize = Mathf.RoundToInt(28f * uiScale)
			};

			levelResultScrollPosition = GUI.BeginScrollView(scrollRect, levelResultScrollPosition, contentRect);
			GUI.Label(contentRect, GetLevelOrderResultSummaryText(), resultStyle);
			GUI.EndScrollView();

			y = panel.y + panel.height - 68f * uiScale;
			GUI.enabled = previousGuiEnabled && canControlMenu;
			if (GUI.Button(new Rect(x, y, contentWidth, 56f * uiScale), "Zur Levelübersicht", menuButtonStyle))
			{
				RequestReturnToLevelSelection();
			}

			GUI.enabled = previousGuiEnabled;
			return;
		}

		string pauseLabel = canControlMenu
			? "Spiel pausiert."
			: GetGameplayPauseOwnerLabel() + " hat pausiert.";
		GUIStyle pauseLabelStyle = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			fontSize = Mathf.RoundToInt(46f * uiScale),
			fontStyle = FontStyle.Bold
		};
		float buttonHeight = 68f * uiScale;
		float buttonGap = 18f * uiScale;
		float buttonAreaHeight = buttonHeight * 3f + buttonGap * 2f;
		Rect scrollViewRect = new Rect(x, y, contentWidth, Mathf.Max(120f * uiScale, panel.y + panel.height - y - buttonAreaHeight - 30f * uiScale));
		Rect scrollContentRect = new Rect(0f, 0f, scrollViewRect.width - 18f * uiScale, 650f * uiScale);
		levelPauseScrollPosition = GUI.BeginScrollView(scrollViewRect, levelPauseScrollPosition, scrollContentRect);
		GUI.Label(new Rect(0f, 0f, scrollContentRect.width, 76f * uiScale), pauseLabel, pauseLabelStyle);
		DrawPauseControlsInfo(new Rect(0f, 96f * uiScale, scrollContentRect.width, 520f * uiScale), uiScale);
		GUI.EndScrollView();

		GUI.enabled = previousGuiEnabled && canControlMenu;
		y = panel.y + panel.height - buttonAreaHeight - 20f * uiScale;
		if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Weiter", menuButtonStyle))
		{
			RequestResumeGameplay();
		}

		y += buttonHeight + buttonGap;
		if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Level beenden", menuButtonStyle))
		{
			RequestEndLevel();
		}

		y += buttonHeight + buttonGap;
		if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), "Zur Levelübersicht", menuButtonStyle))
		{
			RequestReturnToLevelSelection();
		}

		GUI.enabled = previousGuiEnabled;
	}

	private float GetGameplayMenuUiScale()
	{
		return Mathf.Clamp(Screen.height / 720f, 1.15f, 2.2f);
	}

	private void DrawPauseControlsInfo(Rect rect, float uiScale)
	{
		GUI.Box(rect, "");
		GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Mathf.RoundToInt(40f * uiScale),
			fontStyle = FontStyle.Bold
		};
		GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			fontSize = Mathf.RoundToInt(36f * uiScale)
		};

		string controlsText =
			"WASD / Pfeile: bewegen\n"
			+ "Shift: schneller fliegen\n"
			+ "Leertaste: Haken senken, aufnehmen/absetzen, Pfeil setzen\n"
			+ "E: Lager-Block erstellen oder platzieren\n"
			+ "Q: Verbindung starten oder Richtung umdrehen\n"
			+ "R: löschen oder gehaltenen Pfeil abbrechen\n"
			+ "F: Transition auslösen\n"
			+ "Esc: Pause öffnen oder fortsetzen";
		GUI.Label(new Rect(rect.x + 20f * uiScale, rect.y + 12f * uiScale, rect.width - 40f * uiScale, 52f * uiScale), "Tasten", titleStyle);
		GUI.Label(new Rect(rect.x + 20f * uiScale, rect.y + 76f * uiScale, rect.width - 40f * uiScale, rect.height - 88f * uiScale), controlsText, infoStyle);
	}

	private void HandleGameplayMenuHotkey()
	{
		if (!showLevelSelection || !gameplayInitialized)
		{
			return;
		}

		Keyboard keyboard = Keyboard.current;
		if (keyboard == null)
		{
			return;
		}

		if (keyboard.escapeKey.wasPressedThisFrame)
		{
			if (levelEnded)
			{
				gameplayMenuOpen = true;
				return;
			}

			if (gameplayMenuOpen)
			{
				if (CanControlGameplayMenu())
				{
					RequestResumeGameplay();
				}

				return;
			}

			RequestPauseGameplay();
		}
	}

	private bool IsGameplayMenuOpen()
	{
		return showLevelSelection && gameplayInitialized && (gameplayMenuOpen || levelEnded);
	}

	public bool ShouldSuppressLobbyOverlay()
	{
		return showLevelSelection && !gameplayInitialized && IsGameplayConnectionReady();
	}

	public bool ShouldShowLevelSelectionLobbyOverlay()
	{
		return showLevelSelection && !gameplayInitialized && IsGameplayConnectionReady();
	}

	private void RequestReturnToLevelSelection()
	{
		ExecuteOrSendCommand(new CommandData { action = "ReturnToLevelSelection" });
	}

	private void RequestPauseGameplay()
	{
		ExecuteOrSendCommand(new CommandData { action = "PauseGameplay" });
	}

	private void RequestResumeGameplay()
	{
		ExecuteOrSendCommand(new CommandData { action = "ResumeGameplay" });
	}

	private void RequestEndLevel()
	{
		ExecuteOrSendCommand(new CommandData { action = "EndLevel" });
	}

	private void RequestConfirmLevelSelection()
	{
		ExecuteOrSendCommand(new CommandData { action = "ConfirmLevelSelection", amount = selectedLevelIndex });
	}

	private void RequestSelectLevelSelection(int levelIndex)
	{
		ExecuteOrSendCommand(new CommandData { action = "SelectLevelSelection", amount = levelIndex });
	}

	private bool PauseGameplayFromHost(ulong actorClientId)
	{
		if (levelEnded || gameplayMenuOpen)
		{
			return false;
		}

		gameplayMenuOpen = true;
		gameplayMenuOwnerClientId = actorClientId;
		levelPauseScrollPosition = Vector2.zero;
		PauseLevelOrderTimeline();
		CancelGameplayMenuTransientInput();
		return true;
	}

	private bool ResumeGameplayFromHost(ulong actorClientId)
	{
		if (levelEnded || !gameplayMenuOpen || !CanActorControlGameplayMenu(actorClientId))
		{
			return false;
		}

		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		ResumeLevelOrderTimeline();
		CancelGameplayMenuTransientInput();
		return true;
	}

	private bool CanActorControlGameplayMenu(ulong actorClientId)
	{
		return gameplayMenuOwnerClientId == NoGameplayMenuOwnerClientId
			|| gameplayMenuOwnerClientId == actorClientId;
	}

	private bool CanControlGameplayMenu()
	{
		return CanActorControlGameplayMenu(GetLocalActorClientId());
	}

	private string GetGameplayPauseOwnerLabel()
	{
		if (gameplayMenuOwnerClientId == NoGameplayMenuOwnerClientId)
		{
			return "Ein Spieler";
		}

		if (gameplayMenuOwnerClientId == GetLocalActorClientId())
		{
			return "Du";
		}

		return IsActorTopSide(gameplayMenuOwnerClientId) ? "Spieler 1" : "Spieler 2";
	}

	private bool IsGameplayCommandBlockedByPause(string action, ulong actorClientId)
	{
		if (string.IsNullOrEmpty(action) || !gameplayMenuOpen)
		{
			return false;
		}

		if (action == "ResumeGameplay" || action == "EndLevel" || action == "ReturnToLevelSelection")
		{
			return !CanActorControlGameplayMenu(actorClientId);
		}

		return action != "PauseGameplay";
	}

	private long GetSerializableGameplayMenuOwnerClientId()
	{
		return gameplayMenuOwnerClientId == NoGameplayMenuOwnerClientId
			? -1L
			: (long)gameplayMenuOwnerClientId;
	}

	private void ApplyGameplayMenuSnapshotState(bool snapshotGameplayMenuOpen, long snapshotOwnerClientId)
	{
		bool wasGameplayMenuOpen = gameplayMenuOpen;
		gameplayMenuOpen = snapshotGameplayMenuOpen;
		gameplayMenuOwnerClientId = snapshotOwnerClientId >= 0
			? (ulong)snapshotOwnerClientId
			: NoGameplayMenuOwnerClientId;

		if (gameplayMenuOpen && !wasGameplayMenuOpen)
		{
			levelPauseScrollPosition = Vector2.zero;
			PauseLevelOrderTimeline();
		}
		else if (!gameplayMenuOpen && wasGameplayMenuOpen)
		{
			ResumeLevelOrderTimeline();
		}

		if (gameplayMenuOpen)
		{
			CancelGameplayMenuTransientInput();
		}
	}

	private void CancelGameplayMenuTransientInput()
	{
		connectStartNodeId = null;
		craneConnectStartNodeId = null;
		CancelCraneConnectPreview();
		draggedNodeId = null;
		draggedCompositeBlockId = null;
		pointerDownNodeId = null;
		pointerDownCompositeBlockId = null;
		pointerDragActive = false;
		isMiddlePanning = false;
	}

	private void EndLevelFromHost(ulong actorClientId)
	{
		levelEnded = true;
		gameplayMenuOpen = true;
		PauseLevelOrderTimeline();
		if (gameplayMenuOwnerClientId == NoGameplayMenuOwnerClientId)
		{
			gameplayMenuOwnerClientId = actorClientId;
		}

		levelResultScrollPosition = Vector2.zero;
		CancelGameplayMenuTransientInput();
	}

	private void ApplyLevelEndSnapshotState(bool snapshotLevelEnded)
	{
		bool wasLevelEnded = levelEnded;
		levelEnded = snapshotLevelEnded;
		if (!levelEnded)
		{
			return;
		}

		gameplayMenuOpen = true;
		if (!wasLevelEnded)
		{
			levelResultScrollPosition = Vector2.zero;
		}

		CancelGameplayMenuTransientInput();
	}

	private void ReturnToLevelSelectionFromHost()
	{
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
		ApplyLevelSelectionState(new LevelSelectionState
		{
			showSelection = true,
			selectedLevelIndex = selectedLevelIndex
		});
		BroadcastLevelSelectionStateToClients();
	}

	private void ApplyLevelSelectionState(LevelSelectionState state)
	{
		if (state == null || !state.showSelection)
		{
			return;
		}

		selectedLevelIndex = Mathf.Max(0, state.selectedLevelIndex);
		if (!gameplayInitialized && nodesById.Count == 0 && arcsById.Count == 0)
		{
			levelSelectionConfirmed = false;
			gameplayMenuOpen = false;
			gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
			levelEnded = false;
			UpdateLevelSelectionVisuals();
			return;
		}

		levelSelectionConfirmed = false;
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
		gameplayInitialized = false;
		collaborativeLayoutApplied = false;
		avatarStartPositionApplied = false;
		StopLevelOrderTimeline();
		pendingClaimedTransitionId = null;
		draggedCompositeBlockId = null;
		pendingCreatedBlockPickup = false;
		pendingCreatedBlockExistingIds.Clear();
		pointerDownNodeId = null;
		pointerDownCompositeBlockId = null;
		pointerDragActive = false;
		ClearGraph();
		DestroyAvatarVisuals();
		EnsureGraphRootExists();
		if (mainCamera != null)
		{
			ConfigureCamera(mainCamera);
		}
	}

	private void ApplySnapshotLevelDefinition(int snapshotLevelIndex)
	{
		if (!showLevelSelection)
		{
			return;
		}

		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		if (levels.Count <= 0)
		{
			return;
		}

		selectedLevelIndex = Mathf.Clamp(snapshotLevelIndex, 0, levels.Count - 1);
		ApplyLevelDefinition(levels[selectedLevelIndex]);
		levelSelectionConfirmed = true;
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
	}

	private void ConfirmLevelSelection()
	{
		ConfirmLevelSelection(selectedLevelIndex);
	}

	private void ConfirmLevelSelection(int levelIndex)
	{
		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		if (levels.Count <= 0)
		{
			return;
		}

		selectedLevelIndex = Mathf.Clamp(levelIndex, 0, levels.Count - 1);
		ApplyLevelDefinition(levels[selectedLevelIndex]);
		levelSelectionConfirmed = true;
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
	}

	private void ApplyLevelDefinition(PetriNetLevelDefinition level)
	{
		if (level == null)
		{
			return;
		}

		sharedPoolBlocks = CopyBlockDefinitionList(level.blocks, PetriNetLevelBlockOwner.geteilt);
		topPlayerBlocks = CopyBlockDefinitionList(level.blocks, PetriNetLevelBlockOwner.spieler1);
		bottomPlayerBlocks = CopyBlockDefinitionList(level.blocks, PetriNetLevelBlockOwner.spieler2);
		levelInhibitorArcs = CopyInhibitorArcDefinitionList(level.inhibitorArcs);
		topIngredientNames = CopyStringList(level.topIngredients);
		bottomIngredientNames = CopyStringList(level.bottomIngredients);
		SetLevelOrders(level.orders);
	}

	private List<PetriNetLevelDefinition> GetLevelDefinitions()
	{
		return PetriNetLevelCatalog.Levels ?? new List<PetriNetLevelDefinition>();
	}

	private List<string> CopyStringList(List<string> source)
	{
		List<string> copy = new List<string>();
		if (source == null)
		{
			return copy;
		}

		for (int i = 0; i < source.Count; i++)
		{
			if (!string.IsNullOrWhiteSpace(source[i]))
			{
				copy.Add(source[i].Trim());
			}
		}

		return copy;
	}

	private List<PoolBlockDefinition> CopyBlockDefinitionList(List<PetriNetLevelBlockDefinition> source, PetriNetLevelBlockOwner owner)
	{
		List<PoolBlockDefinition> copy = new List<PoolBlockDefinition>();
		if (source == null)
		{
			return copy;
		}

		for (int i = 0; i < source.Count; i++)
		{
			PetriNetLevelBlockDefinition block = source[i];
			if (block == null)
			{
				continue;
			}

			if (block.owner != owner)
			{
				continue;
			}

			copy.Add(new PoolBlockDefinition(
				block.firstTransitionName,
				block.secondTransitionName,
				Mathf.Max(0f, block.processingSeconds),
				block.resultState,
				Mathf.Max(1, block.outputTokenCount),
				block.singleTransition));
		}

		return copy;
	}

	private List<PetriNetLevelInhibitorArcDefinition> CopyInhibitorArcDefinitionList(List<PetriNetLevelInhibitorArcDefinition> source)
	{
		List<PetriNetLevelInhibitorArcDefinition> copy = new List<PetriNetLevelInhibitorArcDefinition>();
		if (source == null)
		{
			return copy;
		}

		for (int i = 0; i < source.Count; i++)
		{
			PetriNetLevelInhibitorArcDefinition inhibitor = source[i];
			if (inhibitor == null)
			{
				continue;
			}

			copy.Add(new PetriNetLevelInhibitorArcDefinition(
				inhibitor.sourceBlockFirstTransitionName,
				inhibitor.sourcePlace,
				inhibitor.targetTransitionName));
		}

		return copy;
	}

	private string GetLevelBlockOverviewText(List<PetriNetLevelBlockDefinition> blocks, PetriNetLevelBlockOwner owner)
	{
		if (blocks == null || blocks.Count <= 0)
		{
			return "keine\n";
		}

		StringBuilder text = new StringBuilder();
		for (int i = 0; i < blocks.Count; i++)
		{
			PetriNetLevelBlockDefinition block = blocks[i];
			if (block == null)
			{
				continue;
			}

			if (block.owner != owner)
			{
				continue;
			}

			text.Append("- ");
			text.Append(block.firstTransitionName);
			if (!block.singleTransition)
			{
				text.Append(" -> ");
				text.Append(block.secondTransitionName);
			}
			text.Append(" / ");
			text.Append(block.processingSeconds.ToString("0.#"));
			text.Append("s / ");
			text.Append(GetFallbackText(block.resultState, "kein Zustand"));
			if (block.outputTokenCount > 1)
			{
				text.Append(" / Ausgabe x");
				text.Append(block.outputTokenCount);
			}

			text.Append('\n');
		}

		return text.Length <= 0 ? "keine\n" : text.ToString();
	}

	private string GetLevelInhibitorOverviewText(List<PetriNetLevelInhibitorArcDefinition> inhibitors)
	{
		if (inhibitors == null || inhibitors.Count <= 0)
		{
			return "keine\n";
		}

		StringBuilder text = new StringBuilder();
		for (int i = 0; i < inhibitors.Count; i++)
		{
			PetriNetLevelInhibitorArcDefinition inhibitor = inhibitors[i];
			if (inhibitor == null)
			{
				continue;
			}

			string sourceBlock = GetFallbackText(inhibitor.sourceBlockFirstTransitionName, "Block");
			string sourcePlace = GetLevelBlockPlaceOverviewText(inhibitor.sourcePlace);
			string targetTransition = GetFallbackText(inhibitor.targetTransitionName, "Transition");
			text.Append("- ");
			text.Append(sourceBlock);
			text.Append(" / ");
			text.Append(sourcePlace);
			text.Append(" --o ");
			text.Append(targetTransition);
			text.Append("\n  sperrt, wenn dort Token liegen\n");
		}

		return text.Length <= 0 ? "keine\n" : text.ToString();
	}

	private string GetLevelBlockPlaceOverviewText(PetriNetLevelBlockPlace place)
	{
		switch (place)
		{
			case PetriNetLevelBlockPlace.ausgabe:
				return "Ausgabe-Stelle";
			case PetriNetLevelBlockPlace.zwischenstelle:
			default:
				return "Zwischenstelle";
		}
	}

	private string JoinLevelList(List<string> values)
	{
		if (values == null || values.Count <= 0)
		{
			return "keine";
		}

		StringBuilder result = new StringBuilder();
		for (int i = 0; i < values.Count; i++)
		{
			string value = values[i] != null ? values[i].Trim() : "";
			if (string.IsNullOrEmpty(value))
			{
				continue;
			}

			if (result.Length > 0)
			{
				result.Append(", ");
			}

			result.Append(value);
		}

		return result.Length <= 0 ? "keine" : result.ToString();
	}

	private string GetFallbackText(string value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
	}
}
