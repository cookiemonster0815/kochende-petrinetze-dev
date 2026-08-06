using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class GameManager
{
	private const ulong NoGameplayMenuOwnerClientId = ulong.MaxValue;
	private const string SinglePlayerHiddenLevelId = "l1.3";

	private int selectedLevelIndex;
	private bool levelSelectionConfirmed;
	private bool gameplayMenuOpen;
	private ulong gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
	private bool levelEnded;
	private float levelResultAnimationStartedAt = -1f;
	private bool levelSelectionAvatarStateSent;
	private Vector2 levelResultScrollPosition;
	private Vector2 levelPauseScrollPosition;
	private Transform levelSelectionRoot;
	private Transform levelSelectionPlatform;
	private LineRenderer levelSelectionBoundaryLine;
	private Collider2D levelConfirmButtonCollider;
	private Collider2D levelLanguageButtonCollider;
	private TextMesh levelSelectionTitleText;
	private TextMesh levelInfoText;
	private readonly Dictionary<Collider2D, int> levelButtonByCollider = new Dictionary<Collider2D, int>();
	private Rect levelSelectionMovementBounds = Rect.MinMaxRect(-4.5f, -3.5f, 4.5f, 3.5f);
	private const int LevelSelectionGridColumns = 4;
	private const float LevelSelectionButtonSize = 0.82f;
	private const float LevelSelectionButtonGap = 0.24f;
	private const float LevelSelectionGridStartX = -3.55f;
	private const float LevelSelectionGridStartY = 1.45f;
	private const float LevelSelectionPlatformWidth = 8.6f;
	private const float LevelSelectionPlatformHeight = 6.6f;
	private const float LevelSelectionPlatformDepth = 0.04f;
	private const float LevelSelectionContentPadding = 0.42f;
	private const float LevelSelectionAvatarBoundaryPadding = 0.45f;
	private const float LevelSelectionButtonHitPadding = 0.06f;
	private const float LevelSelectionCameraViewPadding = 0.35f;
	private const float LevelSelectionMinimumCameraSize = 4.8f;
	private const float LevelSelectionNumberTextSize = 0.14f;
	private const float LevelSelectionConfirmTextSize = 0.055f;
	private const float LevelSelectionInfoTextSize = 0.052f;
	private const float LevelSelectionInfoMinimumTextSize = 0.038f;
	private const float LevelSelectionInfoTextWidthPadding = 0.35f;
	private const float LevelSelectionInfoTextHeightPadding = 0.45f;
	private const float LevelSelectionInfoTextBoundsPaddingX = 0.7f;
	private const float LevelSelectionInfoTextBoundsPaddingY = 0.42f;
	private const float LevelSelectionTitleY = 2.62f;
	private const float LevelSelectionTitleTextSize = 0.13f;
	private const float LevelSelectionTitleFrameGap = 0.7f;
	private const float LevelSelectionInfoX = 0.15f;
	private const float LevelSelectionInfoY = 1.55f;
	private const float LevelSelectionEstimatedCharacterWidth = 1.08f;
	private const float LevelSelectionEstimatedLineHeight = 1.9f;
	private const string LevelSelectionButtonVisualName = "ButtonBlock3D";

	private void OnGUI()
	{
		if (!showLevelSelection)
		{
			return;
		}

		if (IsGameplayMenuOpen())
		{
			DrawGameplayMenu();
		}
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

		EnsureLevelSelectionVisuals();
		UpdateLevelSelectionVisuals();
		bool refreshVisualsAfterStartPosition = !avatarStartPositionApplied;
		EnsureLevelSelectionAvatarStartPosition();
		if (refreshVisualsAfterStartPosition)
		{
			UpdateLevelSelectionVisuals();
		}
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
			avatarPosition = ClampLevelSelectionAvatarPosition(avatarPosition);
			EnsureLevelSelectionRemoteAvatarStartPositions(false);
			return;
		}

		ResetLocalAvatarToLevelSelectionStartPosition();
		EnsureLevelSelectionRemoteAvatarStartPositions(true);
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
		Rect movementBounds = levelSelectionMovementBounds.width > 0.001f && levelSelectionMovementBounds.height > 0.001f
			? levelSelectionMovementBounds
			: Rect.MinMaxRect(-4.5f, -3.5f, 4.5f, 3.5f);
		float x = movementBounds.xMin + Mathf.Max(1.05f, avatarCollisionRadius + 0.75f);
		float topY = movementBounds.yMax - Mathf.Max(0.75f, avatarCollisionRadius + 0.45f);
		float bottomY = movementBounds.yMin + Mathf.Max(0.75f, avatarCollisionRadius + 0.45f);
		Vector3 startPosition = IsActorTopSide(actorClientId)
			? new Vector3(x, topY, 0f)
			: new Vector3(x, bottomY, 0f);
		return ClampLevelSelectionAvatarPosition(startPosition);
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
		remoteCraneConnectStates.Remove(clientId);
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
		levelLanguageButtonCollider = null;
		levelSelectionTitleText = null;

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

		GameObject titleObject = new GameObject("LevelSelectionTitle");
		titleObject.transform.SetParent(levelSelectionRoot, false);
		titleObject.transform.position = new Vector3(0f, LevelSelectionTitleY, NodeLabelLayerZ);
		levelSelectionTitleText = titleObject.AddComponent<TextMesh>();
		levelSelectionTitleText.text = GameText("Levelübersicht", "Level Overview");
		levelSelectionTitleText.characterSize = LevelSelectionTitleTextSize;
		levelSelectionTitleText.fontSize = 96;
		levelSelectionTitleText.anchor = TextAnchor.MiddleCenter;
		levelSelectionTitleText.alignment = TextAlignment.Center;
		levelSelectionTitleText.color = Color.black;
		SetTextSortingOrder(levelSelectionTitleText, 74);

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
			GameText("Bestätigen", "Confirm"),
			new Color(0.78f, 0.92f, 1f),
			30,
			LevelSelectionConfirmTextSize);

		levelLanguageButtonCollider = CreateLevelSelectionButton(
			"LanguageLevelButton",
			GetLevelSelectionLanguageButtonPosition(levels.Count),
			new Vector2(1.72f, 0.58f),
			GetLanguageToggleButtonText(),
			new Color(0.9f, 0.88f, 1f),
			30,
			LevelSelectionConfirmTextSize);

		GameObject infoObject = new GameObject("LevelInfo");
		infoObject.transform.SetParent(levelSelectionRoot, false);
		infoObject.transform.position = new Vector3(LevelSelectionInfoX, LevelSelectionInfoY, NodeLabelLayerZ);
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

	private Vector2 GetLevelSelectionLanguageButtonPosition(int levelCount)
	{
		Vector2 confirmPosition = GetLevelSelectionConfirmButtonPosition(levelCount);
		return new Vector2(confirmPosition.x, confirmPosition.y - 1.28f);
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
		Vector2 avatarPointer = GetLevelSelectionAvatarPointerPosition();
		TryGetLevelSelectionControlWithinCraneRange(
			avatarPointer,
			out _,
			out int hoveredLevelIndex,
			out bool confirmHovered,
			out bool languageHovered);
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
			SetLevelSelectionButtonColor(
				levelConfirmButtonCollider.transform,
				confirmHovered ? new Color(0.56f, 0.84f, 1f) : new Color(0.78f, 0.92f, 1f));

			TextMesh confirmText = levelConfirmButtonCollider.transform.Find("Label")?.GetComponent<TextMesh>();
			if (confirmText != null)
			{
				confirmText.text = GameText("Bestätigen", "Confirm");
				Vector3 confirmScale = levelConfirmButtonCollider.transform.localScale;
				FitLevelSelectionButtonText(confirmText, new Vector2(Mathf.Abs(confirmScale.x), Mathf.Abs(confirmScale.y)));
			}
		}

		if (levelLanguageButtonCollider != null)
		{
			SetLevelSelectionButtonColor(
				levelLanguageButtonCollider.transform,
				languageHovered ? new Color(0.78f, 0.72f, 1f) : new Color(0.9f, 0.88f, 1f));

			TextMesh languageText = levelLanguageButtonCollider.transform.Find("Label")?.GetComponent<TextMesh>();
			if (languageText != null)
			{
				languageText.text = GetLanguageToggleButtonText();
				Vector3 languageScale = levelLanguageButtonCollider.transform.localScale;
				FitLevelSelectionButtonText(languageText, new Vector2(Mathf.Abs(languageScale.x), Mathf.Abs(languageScale.y)));
			}
		}

		if (levelSelectionTitleText != null)
		{
			levelSelectionTitleText.text = GameText("Levelübersicht", "Level Overview");
		}

		UpdateLevelSelectionBoundsAndFloor();
		if (levelInfoText != null && levels.Count > 0)
		{
			UpdateLevelSelectionInfoText(levels[selectedLevelIndex]);
		}
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
		levelSelectionMovementBounds = paddedRect;

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

		PositionLevelSelectionTitleAboveFrame();
		SetLevelSelectionBoundaryLine(levelSelectionMovementBounds);
	}

	private Rect CalculateLevelSelectionContentRect()
	{
		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		Rect contentRect = GetLevelSelectionItemRect(
			GetLevelSelectionConfirmButtonPosition(levels.Count),
			new Vector2(2.1f, 0.66f));
		for (int i = 0; i < levels.Count; i++)
		{
			contentRect = EncapsulateLevelSelectionRect(contentRect, GetLevelSelectionItemRect(GetLevelSelectionButtonPosition(i), new Vector2(LevelSelectionButtonSize, LevelSelectionButtonSize)));
		}

		contentRect = EncapsulateLevelSelectionRect(
			contentRect,
			GetLevelSelectionItemRect(GetLevelSelectionLanguageButtonPosition(levels.Count), new Vector2(1.72f, 0.58f)));
		contentRect = EncapsulateLevelSelectionRect(contentRect, GetLargestLevelSelectionInfoTextRect(levels));

		return contentRect;
	}

	private void PositionLevelSelectionTitleAboveFrame()
	{
		if (levelSelectionTitleText == null)
		{
			return;
		}

		float titleHeight = GetLevelSelectionTitleSize().y;
		float titleY = levelSelectionMovementBounds.yMax + LevelSelectionTitleFrameGap + titleHeight * 0.5f;
		levelSelectionTitleText.transform.position = new Vector3(
			levelSelectionMovementBounds.center.x,
			titleY,
			NodeLabelLayerZ);
	}

	private Rect GetLevelSelectionTitleRect()
	{
		Vector2 size = GetLevelSelectionTitleSize();
		Vector3 position = levelSelectionTitleText != null
			? levelSelectionTitleText.transform.position
			: new Vector3(0f, LevelSelectionTitleY, NodeLabelLayerZ);
		return Rect.MinMaxRect(
			position.x - size.x * 0.5f,
			position.y - size.y * 0.5f,
			position.x + size.x * 0.5f,
			position.y + size.y * 0.5f);
	}

	private Vector2 GetLevelSelectionTitleSize()
	{
		Vector2 germanSize = MeasureLevelSelectionTitleTextSize("Levelübersicht");
		Vector2 englishSize = MeasureLevelSelectionTitleTextSize("Level Overview");
		return new Vector2(
			Mathf.Max(germanSize.x, englishSize.x),
			Mathf.Max(germanSize.y, englishSize.y));
	}

	private Vector2 MeasureLevelSelectionTitleTextSize(string text)
	{
		Vector2 estimatedSize = new Vector2(
			EstimateLevelSelectionTextWidth(text, LevelSelectionTitleTextSize),
			EstimateLevelSelectionTextHeight(text, LevelSelectionTitleTextSize));
		if (levelSelectionTitleText == null || string.IsNullOrEmpty(text))
		{
			return estimatedSize;
		}

		MeshRenderer renderer = levelSelectionTitleText.GetComponent<MeshRenderer>();
		if (renderer == null)
		{
			return estimatedSize;
		}

		string previousText = levelSelectionTitleText.text;
		float previousCharacterSize = levelSelectionTitleText.characterSize;
		TextAnchor previousAnchor = levelSelectionTitleText.anchor;
		TextAlignment previousAlignment = levelSelectionTitleText.alignment;
		try
		{
			levelSelectionTitleText.characterSize = LevelSelectionTitleTextSize;
			levelSelectionTitleText.anchor = TextAnchor.MiddleCenter;
			levelSelectionTitleText.alignment = TextAlignment.Center;
			levelSelectionTitleText.text = text;
			Bounds bounds = renderer.bounds;
			if (bounds.size.x > 0.001f && bounds.size.y > 0.001f)
			{
				estimatedSize.x = Mathf.Max(estimatedSize.x, bounds.size.x);
				estimatedSize.y = Mathf.Max(estimatedSize.y, bounds.size.y);
			}
		}
		finally
		{
			levelSelectionTitleText.text = previousText;
			levelSelectionTitleText.characterSize = previousCharacterSize;
			levelSelectionTitleText.anchor = previousAnchor;
			levelSelectionTitleText.alignment = previousAlignment;
		}

		return estimatedSize;
	}

	private Rect GetLargestLevelSelectionInfoTextRect(List<PetriNetLevelDefinition> levels)
	{
		float maxWidth = 0f;
		float maxHeight = 0f;
		if (levels != null)
		{
			for (int i = 0; i < levels.Count; i++)
			{
				EncapsulateLevelSelectionInfoTextSize(levels[i], i, PetriNetGameLanguage.German, ref maxWidth, ref maxHeight);
				EncapsulateLevelSelectionInfoTextSize(levels[i], i, PetriNetGameLanguage.English, ref maxWidth, ref maxHeight);
			}
		}

		if (maxWidth <= 0.001f || maxHeight <= 0.001f)
		{
			string fallback = GameText(PetriNetGameLanguage.German, "Wähle ein Level aus.", "Choose a level.");
			Vector2 fallbackSize = MeasureLevelSelectionInfoTextSize(fallback, LevelSelectionInfoTextSize);
			maxWidth = fallbackSize.x;
			maxHeight = fallbackSize.y;
		}

		return Rect.MinMaxRect(
			LevelSelectionInfoX - LevelSelectionInfoTextBoundsPaddingX,
			LevelSelectionInfoY - maxHeight - LevelSelectionInfoTextBoundsPaddingY,
			LevelSelectionInfoX + maxWidth + LevelSelectionInfoTextBoundsPaddingX,
			LevelSelectionInfoY + LevelSelectionInfoTextBoundsPaddingY);
	}

	private void EncapsulateLevelSelectionInfoTextSize(
		PetriNetLevelDefinition level,
		int levelIndex,
		PetriNetGameLanguage language,
		ref float maxWidth,
		ref float maxHeight)
	{
		string text = GetLevelInfoText(level, levelIndex, language);
		Vector2 textSize = MeasureLevelSelectionInfoTextSize(text, LevelSelectionInfoTextSize);
		maxWidth = Mathf.Max(maxWidth, textSize.x);
		maxHeight = Mathf.Max(maxHeight, textSize.y);
	}

	private Vector2 MeasureLevelSelectionInfoTextSize(string text, float characterSize)
	{
		Vector2 estimatedSize = new Vector2(
			EstimateLevelSelectionTextWidth(text, characterSize),
			EstimateLevelSelectionTextHeight(text, characterSize));
		if (levelInfoText == null || string.IsNullOrEmpty(text))
		{
			return estimatedSize;
		}

		MeshRenderer renderer = levelInfoText.GetComponent<MeshRenderer>();
		if (renderer == null)
		{
			return estimatedSize;
		}

		string previousText = levelInfoText.text;
		float previousCharacterSize = levelInfoText.characterSize;
		TextAnchor previousAnchor = levelInfoText.anchor;
		TextAlignment previousAlignment = levelInfoText.alignment;
		try
		{
			levelInfoText.characterSize = characterSize;
			levelInfoText.anchor = TextAnchor.UpperLeft;
			levelInfoText.alignment = TextAlignment.Left;
			levelInfoText.text = text;
			Bounds bounds = renderer.bounds;
			if (bounds.size.x > 0.001f && bounds.size.y > 0.001f)
			{
				estimatedSize.x = Mathf.Max(estimatedSize.x, bounds.size.x);
				estimatedSize.y = Mathf.Max(estimatedSize.y, bounds.size.y);
			}
		}
		finally
		{
			levelInfoText.text = previousText;
			levelInfoText.characterSize = previousCharacterSize;
			levelInfoText.anchor = previousAnchor;
			levelInfoText.alignment = previousAlignment;
		}

		return estimatedSize;
	}

	private float EstimateLevelSelectionTextWidth(string text, float characterSize)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0f;
		}

		string[] lines = text.Split('\n');
		int longestLineLength = 0;
		for (int i = 0; i < lines.Length; i++)
		{
			longestLineLength = Mathf.Max(longestLineLength, lines[i].Length);
		}

		return longestLineLength * characterSize * LevelSelectionEstimatedCharacterWidth;
	}

	private float EstimateLevelSelectionTextHeight(string text, float characterSize)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0f;
		}

		return text.Split('\n').Length * characterSize * LevelSelectionEstimatedLineHeight;
	}

	private Rect GetLevelSelectionItemRect(Vector2 center, Vector2 size)
	{
		Vector2 halfSize = size * 0.5f;
		return Rect.MinMaxRect(center.x - halfSize.x, center.y - halfSize.y, center.x + halfSize.x, center.y + halfSize.y);
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
		return GetLevelInfoText(level, selectedLevelIndex, gameLanguage);
	}

	private string GetLevelInfoText(PetriNetLevelDefinition level, int levelIndex, PetriNetGameLanguage language)
	{
		if (level == null)
		{
			return "";
		}

		string levelName = GetLevelSelectionDisplayNameWithoutNumber(level, levelIndex, language);
		string selectedText = GameText(language, "Ausgewählt: Level ", "Selected: Level ")
			+ (levelIndex + 1).ToString();
		if (!string.IsNullOrEmpty(levelName))
		{
			selectedText += ": " + levelName;
		}

		return GameText(
			language,
			"Wähle ein Level aus und bestätige es mit Leertaste.",
			"Choose a level and confirm it with Space.")
			+ "\n"
			+ selectedText;
	}

	private string GetLevelSelectionDisplayNameWithoutNumber(
		PetriNetLevelDefinition level,
		int levelIndex,
		PetriNetGameLanguage language)
	{
		string displayName = GetLocalizedLevelDisplayName(level, language).Trim();
		string numberedPrefix = "Level " + (levelIndex + 1).ToString() + ":";
		if (displayName.StartsWith(numberedPrefix))
		{
			return displayName.Substring(numberedPrefix.Length).Trim();
		}

		int colonIndex = displayName.IndexOf(':');
		if (colonIndex >= 0 && colonIndex + 1 < displayName.Length)
		{
			return displayName.Substring(colonIndex + 1).Trim();
		}

		return displayName;
	}

	private void UpdateLevelSelectionInfoText(PetriNetLevelDefinition level)
	{
		if (levelInfoText == null)
		{
			return;
		}

		float maxWidth = GetLevelSelectionInfoTextMaxWidth();
		float maxHeight = GetLevelSelectionInfoTextMaxHeight();
		float characterSize = LevelSelectionInfoTextSize;
		string rawText = GetLevelInfoText(level);
		levelInfoText.characterSize = characterSize;
		levelInfoText.text = WrapLevelSelectionInfoText(rawText, maxWidth, characterSize);

		while (characterSize > LevelSelectionInfoMinimumTextSize
			&& !DoesLevelSelectionInfoTextFit(levelInfoText, maxWidth, maxHeight))
		{
			characterSize -= 0.003f;
			levelInfoText.characterSize = characterSize;
			levelInfoText.text = WrapLevelSelectionInfoText(rawText, maxWidth, characterSize);
		}
	}

	private float GetLevelSelectionInfoTextMaxWidth()
	{
		float textLeft = levelInfoText != null ? levelInfoText.transform.position.x : 0f;
		float rightEdge = levelSelectionMovementBounds.width > 0.001f
			? levelSelectionMovementBounds.xMax
			: LevelSelectionPlatformWidth * 0.5f;
		return Mathf.Max(1f, rightEdge - textLeft - LevelSelectionInfoTextWidthPadding);
	}

	private float GetLevelSelectionInfoTextMaxHeight()
	{
		float textTop = levelInfoText != null ? levelInfoText.transform.position.y : LevelSelectionGridStartY;
		float bottomEdge = levelSelectionMovementBounds.height > 0.001f
			? levelSelectionMovementBounds.yMin
			: -LevelSelectionPlatformHeight * 0.5f;
		return Mathf.Max(0.5f, textTop - bottomEdge - LevelSelectionInfoTextHeightPadding);
	}

	private bool DoesLevelSelectionInfoTextFit(TextMesh text, float maxWidth, float maxHeight)
	{
		if (text == null)
		{
			return true;
		}

		string[] lines = text.text.Split('\n');
		int longestLineLength = 0;
		for (int i = 0; i < lines.Length; i++)
		{
			longestLineLength = Mathf.Max(longestLineLength, lines[i].Length);
		}

		float estimatedWidth = longestLineLength * text.characterSize * LevelSelectionEstimatedCharacterWidth;
		float estimatedHeight = lines.Length * text.characterSize * LevelSelectionEstimatedLineHeight;
		return estimatedWidth <= maxWidth && estimatedHeight <= maxHeight;
	}

	private string WrapLevelSelectionInfoText(string text, float maxWidth, float characterSize)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "";
		}

		int maxCharactersPerLine = Mathf.Max(8, Mathf.FloorToInt(maxWidth / Mathf.Max(0.001f, characterSize * LevelSelectionEstimatedCharacterWidth)));
		string[] sourceLines = text.Split('\n');
		StringBuilder result = new StringBuilder();
		for (int i = 0; i < sourceLines.Length; i++)
		{
			AppendWrappedLevelSelectionInfoLine(sourceLines[i], maxCharactersPerLine, result);
			if (i + 1 < sourceLines.Length)
			{
				result.Append('\n');
			}
		}

		return result.ToString();
	}

	private void AppendWrappedLevelSelectionInfoLine(string line, int maxCharactersPerLine, StringBuilder result)
	{
		if (string.IsNullOrEmpty(line))
		{
			return;
		}

		string[] words = line.Split(' ');
		int currentLineLength = 0;
		for (int i = 0; i < words.Length; i++)
		{
			string word = words[i];
			if (string.IsNullOrEmpty(word))
			{
				continue;
			}

			if (word.Length > maxCharactersPerLine)
			{
				if (currentLineLength > 0)
				{
					result.Append('\n');
					currentLineLength = 0;
				}

				int wordOffset = 0;
				while (wordOffset < word.Length)
				{
					int chunkLength = Mathf.Min(maxCharactersPerLine, word.Length - wordOffset);
					result.Append(word.Substring(wordOffset, chunkLength));
					wordOffset += chunkLength;
					currentLineLength = chunkLength;
					if (wordOffset < word.Length)
					{
						result.Append('\n');
						currentLineLength = 0;
					}
				}

				continue;
			}

			int separatorLength = currentLineLength > 0 ? 1 : 0;
			if (currentLineLength > 0 && currentLineLength + separatorLength + word.Length > maxCharactersPerLine)
			{
				result.Append('\n');
				currentLineLength = 0;
				separatorLength = 0;
			}

			if (currentLineLength > 0)
			{
				result.Append(' ');
			}

			result.Append(word);
			currentLineLength += separatorLength + word.Length;
		}
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

		Vector3 previousAvatarPosition = avatarPosition;
		if (moveDirection.sqrMagnitude > 0.1f)
		{
			moveDirection = moveDirection.normalized;
			bool sprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
			float currentSpeed = sprinting ? avatarSpeed * avatarSprintMultiplier : avatarSpeed;
			avatarPosition = ClampLevelSelectionAvatarPosition(avatarPosition + moveDirection * currentSpeed * Time.deltaTime);
			avatarRotation = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
			if (IsTutorialMovementInputPressed(keyboard) && Vector3.Distance(previousAvatarPosition, avatarPosition) > 0.01f)
			{
				CompleteLevelSelectionTutorialMovementStep();
			}
		}

		if (keyboard.spaceKey.wasPressedThisFrame)
		{
			StartCraneDipAnimation();
			ActivateLevelSelectionAtCrane(GetLevelSelectionAvatarPointerPosition(), true);
		}

		if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			ActivateLevelSelectionAtPoint(
				GetLevelSelectionWorldPointFromScreen(Mouse.current.position.ReadValue()),
				true);
		}

		UpdateLevelSelectionVisuals();
		SendLevelSelectionAvatarUpdateIfNeeded();
		UpdateAvatarVisuals();
		UpdateLevelSelectionHoverVisual();
	}

	private Vector3 ClampLevelSelectionAvatarPosition(Vector3 desired)
	{
		float avatarBoundaryPadding = Mathf.Max(LevelSelectionAvatarBoundaryPadding, avatarCollisionRadius);
		Rect movementBounds = levelSelectionMovementBounds.width > 0.001f && levelSelectionMovementBounds.height > 0.001f
			? levelSelectionMovementBounds
			: Rect.MinMaxRect(-4.5f, -3.5f, 4.5f, 3.5f);
		Vector2 craneProjectionOffset = GetLevelSelectionCraneProjectionOffset();
		float hookLowerProjectionOffset = GetLevelSelectionHookLowerProjectionOffset();
		Rect centerBounds = Rect.MinMaxRect(
			movementBounds.xMin + avatarBoundaryPadding - craneProjectionOffset.x,
			movementBounds.yMin - hookLowerProjectionOffset,
			movementBounds.xMax - avatarBoundaryPadding - craneProjectionOffset.x,
			movementBounds.yMax - avatarBoundaryPadding - craneProjectionOffset.y);
		if (centerBounds.width <= 0.001f || centerBounds.height <= 0.001f)
		{
			centerBounds = movementBounds;
		}

		return new Vector3(
			Mathf.Clamp(desired.x, centerBounds.xMin, centerBounds.xMax),
			Mathf.Clamp(desired.y, centerBounds.yMin, centerBounds.yMax),
			0f);
	}

	private Vector2 GetLevelSelectionCraneProjectionOffset()
	{
		float heightAboveBoundary = Mathf.Max(0f, avatarCraneRestHeight + ArcZ);
		return new Vector2(0f, GameplayCameraTiltPercent * heightAboveBoundary);
	}

	private float GetLevelSelectionHookLowerProjectionOffset()
	{
		Transform hook = localAvatarCable != null
			? localAvatarCable.transform.Find("ChainHook")
			: null;
		if (hook != null && hook.gameObject.activeInHierarchy && TryGetRendererBounds(hook, out Bounds hookBounds))
		{
			float projectedLowerEdge = hookBounds.min.y
				+ GameplayCameraTiltPercent * (ArcZ - hookBounds.max.z);
			return projectedLowerEdge - avatarPosition.y;
		}

		float restingHookZ = -avatarCraneRestHeight
			+ GetCraneHookHangDistance()
			+ GetCraneHookVisualDrop();
		const float estimatedHookProjectedHalfHeight = 0.18f;
		return GameplayCameraTiltPercent * (ArcZ - restingHookZ)
			- estimatedHookProjectedHalfHeight;
	}

	private void UpdateLevelSelectionCameraFollow()
	{
		if (mainCamera == null)
		{
			return;
		}

		Rect viewBounds = GetLevelSelectionCameraViewBounds();
		mainCamera.orthographicSize = GetLevelSelectionStaticCameraSize(viewBounds);
		isMiddlePanning = false;
		manualCameraPanActive = false;
		ResetCameraFollowVelocity();
		SetCameraGroundCenter(mainCamera, viewBounds.center);
	}

	private float GetLevelSelectionStaticCameraSize(Rect viewBounds)
	{
		float aspect = mainCamera != null ? Mathf.Max(0.01f, mainCamera.aspect) : 1f;
		float widthSize = viewBounds.width / (2f * aspect);
		float forwardZ = new Vector3(0f, GameplayCameraTiltPercent, 1f).normalized.z;
		float heightSize = viewBounds.height * forwardZ * 0.5f;
		return Mathf.Max(minZoom, LevelSelectionMinimumCameraSize, widthSize, heightSize);
	}

	private Rect GetLevelSelectionCameraViewBounds()
	{
		Rect viewBounds = EncapsulateLevelSelectionRect(levelSelectionMovementBounds, GetLevelSelectionTitleRect());
		return Rect.MinMaxRect(
			viewBounds.xMin - LevelSelectionCameraViewPadding,
			viewBounds.yMin - LevelSelectionCameraViewPadding,
			viewBounds.xMax + LevelSelectionCameraViewPadding,
			viewBounds.yMax + LevelSelectionCameraViewPadding);
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
			return;
		}

		if (IsLevelLanguageButtonAtPoint(worldPoint))
		{
			ToggleGameLanguage();
		}
	}

	private void ActivateLevelSelectionAtCrane(Vector2 cranePoint, bool broadcastSelection)
	{
		if (!TryGetLevelSelectionControlWithinCraneRange(
			cranePoint,
			out _,
			out int levelIndex,
			out bool confirmHovered,
			out bool languageHovered))
		{
			return;
		}

		if (levelIndex >= 0)
		{
			SelectLevelIndex(levelIndex, broadcastSelection);
			return;
		}

		if (confirmHovered)
		{
			RequestConfirmLevelSelection();
			return;
		}

		if (languageHovered)
		{
			ToggleGameLanguage();
		}
	}

	private void UpdateLevelSelectionHoverVisual()
	{
		if (levelSelectionRoot == null || gameplayInitialized)
		{
			HideCraneHoverNodeVisual();
			return;
		}

		Vector2 avatarPointer = GetLevelSelectionAvatarPointerPosition();
		if (TryGetLevelSelectionControlWithinCraneRange(
			avatarPointer,
			out Collider2D hoveredCollider,
			out _,
			out _,
			out _))
		{
			ShowLevelSelectionButtonHoverVisual(hoveredCollider);
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
		foreach (KeyValuePair<Collider2D, int> pair in levelButtonByCollider)
		{
			if (IsLevelSelectionButtonAtPoint(pair.Key, worldPoint))
			{
				levelCollider = pair.Key;
				levelIndex = pair.Value;
				return true;
			}
		}

		return false;
	}

	private bool IsLevelConfirmButtonAtPoint(Vector2 worldPoint)
	{
		return IsLevelSelectionButtonAtPoint(levelConfirmButtonCollider, worldPoint);
	}

	private bool IsLevelLanguageButtonAtPoint(Vector2 worldPoint)
	{
		return IsLevelSelectionButtonAtPoint(levelLanguageButtonCollider, worldPoint);
	}

	private bool IsLevelConfirmButtonWithinCraneRange(Vector2 cranePoint)
	{
		return IsLevelSelectionButtonWithinCraneRange(levelConfirmButtonCollider, cranePoint);
	}

	private bool IsLevelLanguageButtonWithinCraneRange(Vector2 cranePoint)
	{
		return IsLevelSelectionButtonWithinCraneRange(levelLanguageButtonCollider, cranePoint);
	}

	private bool TryGetLevelSelectionControlWithinCraneRange(
		Vector2 cranePoint,
		out Collider2D hoveredCollider,
		out int hoveredLevelIndex,
		out bool confirmHovered,
		out bool languageHovered)
	{
		hoveredCollider = null;
		hoveredLevelIndex = -1;
		confirmHovered = false;
		languageHovered = false;
		float closestDistance = float.PositiveInfinity;

		TryUseLevelSelectionControlHover(
			levelConfirmButtonCollider,
			cranePoint,
			-1,
			true,
			false,
			ref closestDistance,
			ref hoveredCollider,
			ref hoveredLevelIndex,
			ref confirmHovered,
			ref languageHovered);
		TryUseLevelSelectionControlHover(
			levelLanguageButtonCollider,
			cranePoint,
			-1,
			false,
			true,
			ref closestDistance,
			ref hoveredCollider,
			ref hoveredLevelIndex,
			ref confirmHovered,
			ref languageHovered);

		foreach (KeyValuePair<Collider2D, int> pair in levelButtonByCollider)
		{
			TryUseLevelSelectionControlHover(
				pair.Key,
				cranePoint,
				pair.Value,
				false,
				false,
				ref closestDistance,
				ref hoveredCollider,
				ref hoveredLevelIndex,
				ref confirmHovered,
				ref languageHovered);
		}

		return hoveredCollider != null;
	}

	private void TryUseLevelSelectionControlHover(
		Collider2D collider,
		Vector2 cranePoint,
		int levelIndex,
		bool isConfirmButton,
		bool isLanguageButton,
		ref float closestDistance,
		ref Collider2D hoveredCollider,
		ref int hoveredLevelIndex,
		ref bool confirmHovered,
		ref bool languageHovered)
	{
		if (!TryGetLevelSelectionButtonCraneDistance(collider, cranePoint, out float distance))
		{
			return;
		}

		if (distance >= closestDistance)
		{
			return;
		}

		closestDistance = distance;
		hoveredCollider = collider;
		hoveredLevelIndex = levelIndex;
		confirmHovered = isConfirmButton;
		languageHovered = isLanguageButton;
	}

	private bool TryGetLevelSelectionButtonCraneDistance(Collider2D buttonCollider, Vector2 cranePoint, out float distance)
	{
		distance = float.PositiveInfinity;
		if (!IsLevelSelectionButtonWithinCraneRange(buttonCollider, cranePoint))
		{
			return false;
		}

		Bounds bounds = buttonCollider.bounds;
		float dx = Mathf.Max(bounds.min.x - cranePoint.x, 0f, cranePoint.x - bounds.max.x);
		float dy = Mathf.Max(bounds.min.y - cranePoint.y, 0f, cranePoint.y - bounds.max.y);
		distance = dx * dx + dy * dy;
		return true;
	}

	private bool TryGetLevelButtonColliderWithinCraneRange(
		Vector2 cranePoint,
		out Collider2D levelCollider,
		out int levelIndex)
	{
		if (TryGetLevelButtonColliderAtPoint(cranePoint, out levelCollider, out levelIndex))
		{
			return true;
		}

		levelCollider = null;
		levelIndex = -1;
		float closestDistance = float.PositiveInfinity;
		foreach (KeyValuePair<Collider2D, int> pair in levelButtonByCollider)
		{
			if (!IsLevelSelectionButtonWithinCraneRange(pair.Key, cranePoint))
			{
				continue;
			}

			Bounds bounds = pair.Key.bounds;
			float dx = Mathf.Max(bounds.min.x - cranePoint.x, 0f, cranePoint.x - bounds.max.x);
			float dy = Mathf.Max(bounds.min.y - cranePoint.y, 0f, cranePoint.y - bounds.max.y);
			float distance = dx * dx + dy * dy;
			if (distance >= closestDistance)
			{
				continue;
			}

			closestDistance = distance;
			levelCollider = pair.Key;
			levelIndex = pair.Value;
		}

		return levelCollider != null;
	}

	private bool IsLevelSelectionButtonWithinCraneRange(Collider2D buttonCollider, Vector2 cranePoint)
	{
		if (buttonCollider == null)
		{
			return false;
		}

		Bounds bounds = buttonCollider.bounds;
		Rect buttonBounds = Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
		return ExpandRect(buttonBounds, avatarCollisionRadius + 0.08f).Contains(cranePoint);
	}

	private bool IsLevelSelectionButtonAtPoint(Collider2D buttonCollider, Vector2 worldPoint)
	{
		if (buttonCollider == null)
		{
			return false;
		}

		if (buttonCollider is BoxCollider2D boxCollider)
		{
			Vector3 localPoint = boxCollider.transform.InverseTransformPoint(worldPoint);
			Vector3 scale = boxCollider.transform.lossyScale;
			Vector2 localPadding = new Vector2(
				LevelSelectionButtonHitPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
				LevelSelectionButtonHitPadding / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
			Vector2 halfSize = boxCollider.size * 0.5f + localPadding;
			Vector2 offset = boxCollider.offset;
			return Mathf.Abs(localPoint.x - offset.x) <= halfSize.x
				&& Mathf.Abs(localPoint.y - offset.y) <= halfSize.y;
		}

		return buttonCollider.OverlapPoint(worldPoint);
	}

	private Vector2 GetLevelSelectionAvatarPointerPosition()
	{
		if (mainCamera == null)
		{
			return new Vector2(avatarPosition.x, avatarPosition.y);
		}

		Vector3 screenPoint = mainCamera.WorldToScreenPoint(GetCraneVisualPosition());
		return GetLevelSelectionWorldPointFromScreen(new Vector2(screenPoint.x, screenPoint.y));
	}

	private Vector2 GetLevelSelectionWorldPointFromScreen(Vector2 screenPoint)
	{
		if (mainCamera == null)
		{
			return Vector2.zero;
		}

		Ray ray = mainCamera.ScreenPointToRay(new Vector3(screenPoint.x, screenPoint.y, 0f));
		Plane buttonTopPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, NodeVisualTopZ));
		if (buttonTopPlane.Raycast(ray, out float distance))
		{
			Vector3 worldPoint = ray.GetPoint(distance);
			return new Vector2(worldPoint.x, worldPoint.y);
		}

		return new Vector2(avatarPosition.x, avatarPosition.y);
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
		levelSelectionMovementBounds = Rect.MinMaxRect(-4.5f, -3.5f, 4.5f, 3.5f);
		levelConfirmButtonCollider = null;
		levelLanguageButtonCollider = null;
		levelSelectionTitleText = null;
		levelInfoText = null;
	}

	private void DrawGameplayMenu()
	{
		float uiScale = GetGameplayMenuUiScale();
		if (levelEnded)
		{
			DrawLevelResultScreen();
			return;
		}

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
			GUI.Box(panel, levelEnded ? GameText("Level beendet", "Level ended") : GameText("Menü", "Menu"), menuBoxStyle);
			DrawLanguageToggleGuiButton(
				new Rect(panel.xMax - 162f * uiScale, panel.y + 14f * uiScale, 138f * uiScale, 42f * uiScale),
				uiScale);
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
				GUI.Label(new Rect(x, y, contentWidth, 50f * uiScale), GameText("Auswertung", "Results"), resultTitleStyle);
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
				if (GUI.Button(new Rect(x, y, contentWidth, 56f * uiScale), GameText("Zur Levelübersicht", "Return to level overview"), menuButtonStyle))
				{
					RequestReturnToLevelSelection();
				}

			GUI.enabled = previousGuiEnabled;
			return;
		}

			string pauseLabel = canControlMenu
				? GameText("Spiel pausiert.", "Game paused.")
				: GetGameplayPauseOwnerLabel() + GameText(" hat pausiert.", " paused.");
		GUIStyle pauseLabelStyle = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			fontSize = Mathf.RoundToInt(46f * uiScale),
			fontStyle = FontStyle.Bold
		};
		float buttonHeight = 68f * uiScale;
		float buttonGap = 18f * uiScale;
		float buttonAreaHeight = buttonHeight * 2f + buttonGap;
		Rect scrollViewRect = new Rect(x, y, contentWidth, Mathf.Max(120f * uiScale, panel.y + panel.height - y - buttonAreaHeight - 30f * uiScale));
		float scrollContentWidth = scrollViewRect.width - 18f * uiScale;
		float pauseLabelHeight = Mathf.Max(76f * uiScale, pauseLabelStyle.CalcHeight(new GUIContent(pauseLabel), scrollContentWidth));
		float controlsY = pauseLabelHeight + 20f * uiScale;
		float controlsHeight = GetPauseControlsInfoHeight(scrollContentWidth, uiScale);
		Rect scrollContentRect = new Rect(0f, 0f, scrollContentWidth, Mathf.Max(scrollViewRect.height, controlsY + controlsHeight + 16f * uiScale));
		levelPauseScrollPosition = GUI.BeginScrollView(scrollViewRect, levelPauseScrollPosition, scrollContentRect);
		GUI.Label(new Rect(0f, 0f, scrollContentRect.width, pauseLabelHeight), pauseLabel, pauseLabelStyle);
		DrawPauseControlsInfo(new Rect(0f, controlsY, scrollContentRect.width, controlsHeight), uiScale);
		GUI.EndScrollView();

		GUI.enabled = previousGuiEnabled && canControlMenu;
		y = panel.y + panel.height - buttonAreaHeight - 20f * uiScale;
		if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), GameText("Weiter", "Continue"), menuButtonStyle))
		{
			RequestResumeGameplay();
		}

		y += buttonHeight + buttonGap;
		if (GUI.Button(new Rect(x, y, contentWidth, buttonHeight), GameText("Zur Levelübersicht", "Return to level overview"), menuButtonStyle))
		{
			RequestReturnToLevelSelection();
		}

		GUI.enabled = previousGuiEnabled;
	}

	private void DrawLevelResultScreen()
	{
		if (levelResultAnimationStartedAt < 0f)
		{
			levelResultAnimationStartedAt = Time.unscaledTime;
		}

		float elapsed = Mathf.Max(0f, Time.unscaledTime - levelResultAnimationStartedAt);
		float uiScale = Mathf.Clamp(
			Mathf.Min(Screen.width / 1600f, Screen.height / 900f),
			0.72f,
			1.6f);
		Rect fullScreen = new Rect(0f, 0f, Screen.width, Screen.height);
		Color previousColor = GUI.color;
		Color previousBackgroundColor = GUI.backgroundColor;
		bool previousEnabled = GUI.enabled;

		GUI.color = new Color(0.075f, 0.105f, 0.15f, 1f);
		GUI.DrawTexture(fullScreen, Texture2D.whiteTexture);
		GUI.color = Color.white;

		float panelWidth = Mathf.Min(920f * uiScale, Screen.width - 48f * uiScale);
		float panelHeight = Mathf.Min(690f * uiScale, Screen.height - 48f * uiScale);
		Rect panel = new Rect(
			(Screen.width - panelWidth) * 0.5f,
			(Screen.height - panelHeight) * 0.5f,
			panelWidth,
			panelHeight);
		GUI.color = new Color(0.95f, 0.97f, 1f, 1f);
			GUI.DrawTexture(panel, Texture2D.whiteTexture);
			GUI.color = Color.white;
			DrawLanguageToggleGuiButton(
				new Rect(panel.xMax - 162f * uiScale, panel.y + 18f * uiScale, 138f * uiScale, 44f * uiScale),
				uiScale);

			float titleProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.55f));
		GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			fontSize = Mathf.RoundToInt(58f * uiScale),
			fontStyle = FontStyle.Bold
		};
		SetStaticGuiTextColor(titleStyle, new Color(0.08f, 0.12f, 0.18f));
		float titleY = Mathf.Lerp(panel.y - 36f * uiScale, panel.y + 42f * uiScale, titleProgress);
			GUI.Label(new Rect(panel.x, titleY, panel.width, 76f * uiScale), GameText("Level abgeschlossen!", "Level complete!"), titleStyle);

		int earnedStars = GetLevelResultStarCount();
		float starRowY = panel.y + 145f * uiScale;
		float starSpacing = 190f * uiScale;
		float starCenterX = panel.center.x;
		for (int i = 0; i < 3; i++)
		{
			float starProgress = GetLevelResultPopProgress(elapsed - 0.38f - i * 0.22f);
			float pulse = i < earnedStars && starProgress >= 1f
				? 1f + Mathf.Sin((elapsed - i * 0.2f) * 3.5f) * 0.025f
				: 1f;
			float starSize = 132f * uiScale * starProgress * pulse;
			GUIStyle starStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = Mathf.Max(1, Mathf.RoundToInt(starSize)),
				fontStyle = FontStyle.Bold
			};
			SetStaticGuiTextColor(
				starStyle,
				i < earnedStars
					? new Color(1f, 0.66f, 0.04f)
					: new Color(0.55f, 0.59f, 0.66f));
			float centerX = starCenterX + (i - 1) * starSpacing;
			GUI.Label(
				new Rect(centerX - 90f * uiScale, starRowY, 180f * uiScale, 170f * uiScale),
				i < earnedStars ? "★" : "☆",
				starStyle);
		}

		int totalScore = GetLevelOrderScore();
		int maximumScore = GetLevelOrderCount() * 3;
		float scoreProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 0.75f) / 0.9f));
		int displayedScore = Mathf.RoundToInt(totalScore * scoreProgress);
		GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			fontSize = Mathf.RoundToInt(42f * uiScale),
			fontStyle = FontStyle.Bold
		};
		SetStaticGuiTextColor(scoreStyle, new Color(0.12f, 0.17f, 0.24f));
		GUI.Label(
			new Rect(panel.x, panel.y + 345f * uiScale, panel.width, 60f * uiScale),
				displayedScore + " / " + maximumScore + " " + GameText("Punkte", "points"),
				scoreStyle);

		GUIStyle detailStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			fontSize = Mathf.RoundToInt(25f * uiScale)
		};
		SetStaticGuiTextColor(detailStyle, new Color(0.3f, 0.35f, 0.42f));
		GUI.Label(
			new Rect(panel.x, panel.y + 410f * uiScale, panel.width, 42f * uiScale),
				GetLevelOrderCount() + " " + GameText("Bestellungen abgeschlossen", "orders completed"),
				detailStyle);

		float buttonProgress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 1.15f) / 0.45f));
		float buttonWidth = Mathf.Min(360f * uiScale, (panel.width - 90f * uiScale) * 0.5f);
		float buttonHeight = 76f * uiScale;
		float buttonGap = 28f * uiScale;
		float buttonY = Mathf.Lerp(
			panel.y + panel.height,
			panel.y + panel.height - buttonHeight - 54f * uiScale,
			buttonProgress);
		float buttonsStartX = panel.center.x - buttonWidth - buttonGap * 0.5f;
		GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
		{
			fontSize = Mathf.RoundToInt(28f * uiScale),
			fontStyle = FontStyle.Bold
		};

		bool hasNextLevel = HasNextLevel();
		GUI.enabled = previousEnabled && hasNextLevel && buttonProgress >= 0.99f;
		GUI.backgroundColor = new Color(0.42f, 0.78f, 1f);
		if (GUI.Button(
			new Rect(buttonsStartX, buttonY, buttonWidth, buttonHeight),
				hasNextLevel ? GameText("Nächstes Level", "Next level") : GameText("Letztes Level", "Last level"),
				buttonStyle))
		{
			RequestNextLevel();
		}

		GUI.enabled = previousEnabled && buttonProgress >= 0.99f;
		GUI.backgroundColor = new Color(0.82f, 0.86f, 0.91f);
		if (GUI.Button(
			new Rect(buttonsStartX + buttonWidth + buttonGap, buttonY, buttonWidth, buttonHeight),
				GameText("Zur Levelübersicht", "Return to level overview"),
				buttonStyle))
		{
			RequestReturnToLevelSelection();
		}

		GUI.enabled = previousEnabled;
		GUI.backgroundColor = previousBackgroundColor;
		GUI.color = previousColor;
	}

	private float GetLevelResultPopProgress(float elapsed)
	{
		float progress = Mathf.Clamp01(elapsed / 0.5f);
		float shifted = progress - 1f;
		return 1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted;
	}

	private void SetStaticGuiTextColor(GUIStyle style, Color color)
	{
		if (style == null)
		{
			return;
		}

		style.normal.textColor = color;
		style.hover.textColor = color;
		style.active.textColor = color;
		style.focused.textColor = color;
		style.onNormal.textColor = color;
		style.onHover.textColor = color;
		style.onActive.textColor = color;
		style.onFocused.textColor = color;
	}

	private void DrawLanguageToggleGuiButton(Rect rect, float uiScale)
	{
		bool previousEnabled = GUI.enabled;
		GUI.enabled = true;
		GUIStyle languageButtonStyle = new GUIStyle(GUI.skin.button)
		{
			fontSize = Mathf.RoundToInt(22f * uiScale),
			fontStyle = FontStyle.Bold
		};
		if (GUI.Button(rect, GetLanguageToggleButtonText(), languageButtonStyle))
		{
			ToggleGameLanguage();
		}

		GUI.enabled = previousEnabled;
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

		string controlsText = GetPauseControlsText();
		GUI.Label(new Rect(rect.x + 20f * uiScale, rect.y + 12f * uiScale, rect.width - 40f * uiScale, 52f * uiScale), GameText("Tasten", "Controls"), titleStyle);
		GUI.Label(new Rect(rect.x + 20f * uiScale, rect.y + 76f * uiScale, rect.width - 40f * uiScale, rect.height - 88f * uiScale), controlsText, infoStyle);
	}

	private float GetPauseControlsInfoHeight(float width, float uiScale)
	{
		GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			fontSize = Mathf.RoundToInt(36f * uiScale)
		};
		float contentWidth = Mathf.Max(1f, width - 40f * uiScale);
		float textHeight = infoStyle.CalcHeight(new GUIContent(GetPauseControlsText()), contentWidth);
		return Mathf.Max(520f * uiScale, 76f * uiScale + textHeight + 36f * uiScale);
	}

	private string GetPauseControlsText()
	{
		return GameText(
			"WASD / Pfeile: bewegen\n"
				+ "Shift: schneller fliegen\n"
				+ "Leertaste: Haken senken, aufnehmen/absetzen, Pfeil setzen\n"
				+ "E: Lager-Block erstellen oder platzieren\n"
				+ "Q: Verbindung starten oder Richtung umdrehen\n"
				+ "R: löschen oder gehaltenen Pfeil abbrechen\n"
				+ "C: Rezeptdetails ein-/ausblenden\n"
				+ "F: Transition auslösen\n"
				+ "Esc: Pause öffnen oder fortsetzen",
			"WASD / arrows: move\n"
				+ "Shift: fly faster\n"
				+ "Space: lower hook, pick up/set down, place arrow\n"
				+ "E: create or place storage block\n"
				+ "Q: start connection or reverse direction\n"
				+ "R: delete or cancel held arrow\n"
				+ "C: show/hide recipe details\n"
				+ "F: fire transition\n"
				+ "Esc: open or close pause");
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
		return singlePlayerMode
			|| (showLevelSelection && !gameplayInitialized && IsGameplayConnectionReady());
	}

	public bool ShouldShowLevelSelectionLobbyOverlay()
	{
		return showLevelSelection && !gameplayInitialized && IsGameplayConnectionReady();
	}

	private void RequestReturnToLevelSelection()
	{
		ExecuteOrSendCommand(new CommandData { action = "ReturnToLevelSelection" });
	}

	private void RequestNextLevel()
	{
		ExecuteOrSendCommand(new CommandData { action = "NextLevel" });
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

	private void RequestExitToDesktop()
	{
		LobbyRelayManager lobbyManager = FindAnyObjectByType<LobbyRelayManager>();
		if (lobbyManager != null)
		{
			lobbyManager.RequestExitToDesktop();
			return;
		}

		ExitApplicationNow();
	}

	private bool ShouldShowExitToDesktopButton()
	{
#if UNITY_WEBGL
		return false;
#else
		return true;
#endif
	}

	private void ExitApplicationNow()
	{
#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
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
			return GameText("Ein Spieler", "A player");
		}

		if (gameplayMenuOwnerClientId == GetLocalActorClientId())
		{
			return GameText("Du", "You");
		}

		return IsActorTopSide(gameplayMenuOwnerClientId) ? GameText("Spieler 1", "Player 1") : GameText("Spieler 2", "Player 2");
	}

	private bool IsGameplayCommandBlockedByPause(string action, ulong actorClientId)
	{
		if (string.IsNullOrEmpty(action) || !gameplayMenuOpen)
		{
			return false;
		}

		if (levelEnded && (action == "NextLevel" || action == "ReturnToLevelSelection"))
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
		levelResultAnimationStartedAt = Time.unscaledTime;
		CancelGameplayMenuTransientInput();
	}

	private void ApplyLevelEndSnapshotState(bool snapshotLevelEnded)
	{
		bool wasLevelEnded = levelEnded;
		levelEnded = snapshotLevelEnded;
		if (!levelEnded)
		{
			levelResultAnimationStartedAt = -1f;
			return;
		}

		gameplayMenuOpen = true;
		if (!wasLevelEnded)
		{
			levelResultScrollPosition = Vector2.zero;
			levelResultAnimationStartedAt = Time.unscaledTime;
		}

		CancelGameplayMenuTransientInput();
	}

	private void ReturnToLevelSelectionFromHost()
	{
		if (!showLevelSelection
			|| !gameplayInitialized
			|| (!gameplayMenuOpen && !levelEnded))
		{
			return;
		}

		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
		levelResultAnimationStartedAt = -1f;
		ApplyLevelSelectionState(new LevelSelectionState
		{
			showSelection = true,
			selectedLevelIndex = selectedLevelIndex
		});
		BroadcastLevelSelectionStateToClients();
	}

	private bool HasNextLevel()
	{
		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		return levels.Count > 0 && selectedLevelIndex + 1 < levels.Count;
	}

	private void StartNextLevelFromHost()
	{
		if (!levelEnded || !HasNextLevel())
		{
			return;
		}

		int nextLevelIndex = selectedLevelIndex + 1;
		ApplyLevelSelectionState(new LevelSelectionState
		{
			showSelection = true,
			selectedLevelIndex = nextLevelIndex
		});
		BroadcastLevelSelectionStateToClients();
		ConfirmLevelSelection(nextLevelIndex);
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
		CompleteLevelSelectionTutorialMovementForSession();
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
		CompleteLevelSelectionTutorialMovementForSession();
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
		OnLevelDefinitionApplied(level);
	}

	private List<PetriNetLevelDefinition> GetLevelDefinitions()
	{
		List<PetriNetLevelDefinition> levels = PetriNetLevelCatalog.Levels ?? new List<PetriNetLevelDefinition>();
		if (!singlePlayerMode)
		{
			return levels;
		}

		List<PetriNetLevelDefinition> filteredLevels = new List<PetriNetLevelDefinition>();
		for (int i = 0; i < levels.Count; i++)
		{
			PetriNetLevelDefinition level = levels[i];
			if (level != null && level.id == SinglePlayerHiddenLevelId)
			{
				continue;
			}

			filteredLevels.Add(level);
		}

		return filteredLevels;
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
		return GetLevelBlockOverviewText(blocks, owner, gameLanguage);
	}

	private string GetLevelBlockOverviewText(List<PetriNetLevelBlockDefinition> blocks, PetriNetLevelBlockOwner owner, PetriNetGameLanguage language)
	{
		if (blocks == null || blocks.Count <= 0)
		{
			return GameText(language, "keine\n", "none\n");
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
			text.Append(LocalizeVisibleText(block.firstTransitionName, language));
			if (!block.singleTransition)
			{
				text.Append(" -> ");
				text.Append(LocalizeVisibleText(block.secondTransitionName, language));
			}
			text.Append(" / ");
			text.Append(block.processingSeconds.ToString("0.#"));
			text.Append("s / ");
			text.Append(LocalizeVisibleText(GetFallbackText(block.resultState, "kein Zustand"), language));
			if (block.outputTokenCount > 1)
			{
				text.Append(GameText(language, " / Ausgabe x", " / output x"));
				text.Append(block.outputTokenCount);
			}

			text.Append('\n');
		}

		return text.Length <= 0 ? GameText(language, "keine\n", "none\n") : text.ToString();
	}

	private string GetLevelInhibitorOverviewText(List<PetriNetLevelInhibitorArcDefinition> inhibitors)
	{
		return GetLevelInhibitorOverviewText(inhibitors, gameLanguage);
	}

	private string GetLevelInhibitorOverviewText(List<PetriNetLevelInhibitorArcDefinition> inhibitors, PetriNetGameLanguage language)
	{
		if (inhibitors == null || inhibitors.Count <= 0)
		{
			return GameText(language, "keine\n", "none\n");
		}

		StringBuilder text = new StringBuilder();
		for (int i = 0; i < inhibitors.Count; i++)
		{
			PetriNetLevelInhibitorArcDefinition inhibitor = inhibitors[i];
			if (inhibitor == null)
			{
				continue;
			}

			string sourceBlock = LocalizeVisibleText(GetFallbackText(inhibitor.sourceBlockFirstTransitionName, "Block"), language);
			string sourcePlace = GetLevelBlockPlaceOverviewText(inhibitor.sourcePlace, language);
			string targetTransition = LocalizeVisibleText(GetFallbackText(inhibitor.targetTransitionName, "Transition"), language);
			text.Append("- ");
			text.Append(sourceBlock);
			text.Append(" / ");
			text.Append(sourcePlace);
			text.Append(" --o ");
			text.Append(targetTransition);
			text.Append(GameText(language, "\n  sperrt, wenn dort Token liegen\n", "\n  blocks while tokens are there\n"));
		}

		return text.Length <= 0 ? GameText(language, "keine\n", "none\n") : text.ToString();
	}

	private string GetLevelBlockPlaceOverviewText(PetriNetLevelBlockPlace place)
	{
		return GetLevelBlockPlaceOverviewText(place, gameLanguage);
	}

	private string GetLevelBlockPlaceOverviewText(PetriNetLevelBlockPlace place, PetriNetGameLanguage language)
	{
		switch (place)
		{
			case PetriNetLevelBlockPlace.ausgabe:
				return GameText(language, "Ausgabe-Stelle", "output place");
			case PetriNetLevelBlockPlace.zwischenstelle:
			default:
				return GameText(language, "Zwischenstelle", "intermediate place");
		}
	}

	private string JoinLevelList(List<string> values)
	{
		return JoinLevelList(values, gameLanguage);
	}

	private string JoinLevelList(List<string> values, PetriNetGameLanguage language)
	{
		if (values == null || values.Count <= 0)
		{
			return GameText(language, "keine", "none");
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

			result.Append(LocalizeVisibleText(value, language));
		}

		return result.Length <= 0 ? GameText(language, "keine", "none") : result.ToString();
	}

	private string GetLevelOrderOverviewText(PetriNetLevelDefinition level, PetriNetGameLanguage language)
	{
		if (level == null || level.orders == null || level.orders.Count <= 0)
		{
			return GameText(language, "keine", "none");
		}

		StringBuilder text = new StringBuilder();
		for (int i = 0; i < level.orders.Count; i++)
		{
			PetriNetLevelOrderDefinition order = level.orders[i];
			if (order == null)
			{
				continue;
			}

			if (text.Length > 0)
			{
				text.Append('\n');
			}

			text.Append("- ");
			if (order.amount > 1)
			{
				text.Append(order.amount);
				text.Append("x ");
			}

			text.Append(LocalizeVisibleText(order.dishText, language));
			text.Append(GameText(language, "\n  Gefordert: ", "\n  Required: "));
			text.Append(LocalizeVisibleText(GetOrderRequiredTokenText(order), language));
			string recipeText = LocalizeVisibleText(GetOrderRecipeText(order), language);
			if (!string.IsNullOrEmpty(recipeText))
			{
				text.Append(GameText(language, "\n  Rezept: ", "\n  Recipe: "));
				text.Append(recipeText);
			}
		}

		return text.Length <= 0 ? GameText(language, "keine", "none") : text.ToString();
	}

	private string GetFallbackText(string value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
	}
}
