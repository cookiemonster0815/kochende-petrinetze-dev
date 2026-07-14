using System.Collections.Generic;
using System.Text;
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
	private Transform levelSelectionRoot;
	private Collider2D levelConfirmButtonCollider;
	private TextMesh levelInfoText;
	private readonly Dictionary<Collider2D, int> levelButtonByCollider = new Dictionary<Collider2D, int>();
	private const int LevelSelectionGridColumns = 4;
	private const float LevelSelectionButtonSize = 0.82f;
	private const float LevelSelectionButtonGap = 0.24f;
	private const float LevelSelectionGridStartX = -3.55f;
	private const float LevelSelectionGridStartY = 2.32f;
	private const float LevelSelectionPlatformWidth = 9.6f;
	private const float LevelSelectionPlatformHeight = 7.25f;
	private const float LevelSelectionPlatformDepth = 0.04f;
	private const float LevelSelectionNumberTextSize = 0.14f;
	private const float LevelSelectionConfirmTextSize = 0.055f;
	private const float LevelSelectionTitleTextSize = 0.15f;
	private const float LevelSelectionInfoTextSize = 0.052f;
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
		ConfigureLevelSelectionCamera();
		EnsureLevelSelectionAvatarStartPosition();
		EnsureLevelSelectionVisuals();
		UpdateLevelSelectionVisuals();
		UpdateAvatarVisuals();
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
			return;
		}

		avatarPosition = IsActorTopSide(GetLocalActorClientId())
			? new Vector3(-3.8f, 2.85f, 0f)
			: new Vector3(-3.8f, -2.85f, 0f);
		avatarRotation = 0f;
		avatarStartPositionApplied = true;
		lastAvatarPosition = avatarPosition;
		lastAvatarNetworkSyncRotation = avatarRotation;
		lastAvatarNetworkSyncHeldId = "";
		lastAvatarNetworkSyncCraneHeight = avatarCraneCurrentHeight;
		levelSelectionAvatarStateSent = false;
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
		Collider backgroundCollider = background.GetComponent<Collider>();
		if (backgroundCollider != null)
		{
			Destroy(backgroundCollider);
		}

		MeshRenderer backgroundRenderer = background.GetComponent<MeshRenderer>();
		if (backgroundRenderer != null)
		{
			backgroundRenderer.material = CreatePrimitiveVisualMaterial(Color.white);
			ConfigureMeshRendererFor3D(backgroundRenderer, false, true);
		}

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

		GameObject titleObject = new GameObject("LevelTitle");
		titleObject.transform.SetParent(levelSelectionRoot, false);
		titleObject.transform.position = new Vector3(0f, 3.45f, NodeLabelLayerZ);
		TextMesh title = titleObject.AddComponent<TextMesh>();
		title.text = "Levelauswahl";
		title.characterSize = LevelSelectionTitleTextSize;
		title.fontSize = 96;
		title.anchor = TextAnchor.MiddleCenter;
		title.alignment = TextAlignment.Center;
		title.color = Color.black;
		SetTextSortingOrder(title, 72);

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
			ActivateLevelSelectionAtPoint(new Vector2(avatarPosition.x, avatarPosition.y), IsHostOrOffline());
		}

		if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
		{
			Vector3 mouseWorld = GetMouseWorldPosition();
			ActivateLevelSelectionAtPoint(new Vector2(mouseWorld.x, mouseWorld.y), IsHostOrOffline());
		}

		UpdateLevelSelectionVisuals();
		SendLevelSelectionAvatarUpdateIfNeeded();
		UpdateAvatarVisuals();
	}

	private Vector3 ClampLevelSelectionAvatarPosition(Vector3 desired)
	{
		return new Vector3(Mathf.Clamp(desired.x, -4.45f, 4.45f), Mathf.Clamp(desired.y, -3.25f, 3.25f), 0f);
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
	}

	private bool TryGetLevelButtonAtPoint(Vector2 worldPoint, out int levelIndex)
	{
		levelIndex = -1;
		Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
		for (int i = 0; i < hits.Length; i++)
		{
			if (hits[i] != null && levelButtonByCollider.TryGetValue(hits[i], out levelIndex))
			{
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
		bool shouldSendAvatarUpdate = !levelSelectionAvatarStateSent
			|| heldObjectChanged
			|| movedDistance > 0.65f
			|| ((movedDistance > 0.05f || rotationChanged) && Time.unscaledTime >= nextAvatarNetworkSyncTime);
		if (!shouldSendAvatarUpdate)
		{
			return;
		}

		nextAvatarNetworkSyncTime = Time.unscaledTime + avatarNetworkSyncInterval;
		lastAvatarPosition = avatarPosition;
		lastAvatarNetworkSyncRotation = avatarRotation;
		lastAvatarNetworkSyncHeldId = currentHeldNetworkKey;
		lastAvatarNetworkSyncCraneHeight = avatarCraneCurrentHeight;
		levelSelectionAvatarStateSent = true;
		SendAvatarUpdate(avatarPosition, avatarRotation, heldTransitionId, heldObjectChanged);
	}

	private void DestroyLevelSelectionScreen()
	{
		if (levelSelectionRoot != null)
		{
			Destroy(levelSelectionRoot.gameObject);
			levelSelectionRoot = null;
		}

		levelButtonByCollider.Clear();
		levelConfirmButtonCollider = null;
		levelInfoText = null;
	}

	private void DrawGameplayMenu()
	{
		float uiScale = GetGameplayMenuUiScale();
		float panelWidth = (levelEnded ? 560f : 620f) * uiScale;
		float panelHeight = (levelEnded ? 520f : 560f) * uiScale;
		Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
		GUI.Box(panel, levelEnded ? "Level beendet" : "Menü");
		float x = panel.x + 24f * uiScale;
		float y = panel.y + 42f * uiScale;
		float contentWidth = panel.width - 48f * uiScale;
		bool canControlMenu = CanControlGameplayMenu();
		bool previousGuiEnabled = GUI.enabled;

		if (levelEnded)
		{
			GUI.Label(new Rect(x, y, contentWidth, 24f * uiScale), "Auswertung");
			y += 32f * uiScale;

			Rect scrollRect = new Rect(x, y, contentWidth, panel.height - 134f * uiScale);
			int orderCount = GetLevelOrderCount();
			Rect contentRect = new Rect(0f, 0f, scrollRect.width - 18f * uiScale, Mathf.Max(scrollRect.height, (90f + orderCount * 96f) * uiScale));
			GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				fontSize = Mathf.RoundToInt(13f * uiScale)
			};

			levelResultScrollPosition = GUI.BeginScrollView(scrollRect, levelResultScrollPosition, contentRect);
			GUI.Label(contentRect, GetLevelOrderResultSummaryText(), resultStyle);
			GUI.EndScrollView();

			y = panel.y + panel.height - 58f * uiScale;
			GUI.enabled = previousGuiEnabled && canControlMenu;
			if (GUI.Button(new Rect(x, y, contentWidth, 34f * uiScale), "Zur Levelübersicht"))
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
			fontSize = Mathf.RoundToInt(22f * uiScale)
		};
		GUI.Label(new Rect(x, y, contentWidth, 32f * uiScale), pauseLabel, pauseLabelStyle);
		y += 44f * uiScale;

		DrawPauseControlsInfo(new Rect(x, y, contentWidth, 288f * uiScale), uiScale);
		y += 308f * uiScale;

		GUI.enabled = previousGuiEnabled && canControlMenu;
		if (GUI.Button(new Rect(x, y, contentWidth, 40f * uiScale), "Weiter"))
		{
			RequestResumeGameplay();
		}

		y += 52f * uiScale;
		if (GUI.Button(new Rect(x, y, contentWidth, 40f * uiScale), "Level beenden"))
		{
			RequestEndLevel();
		}

		y += 52f * uiScale;
		if (GUI.Button(new Rect(x, y, contentWidth, 40f * uiScale), "Zur Levelübersicht"))
		{
			RequestReturnToLevelSelection();
		}

		GUI.enabled = previousGuiEnabled;
	}

	private float GetGameplayMenuUiScale()
	{
		return Mathf.Clamp(Screen.height / 900f, 1f, 1.8f);
	}

	private void DrawPauseControlsInfo(Rect rect, float uiScale)
	{
		GUI.Box(rect, "");
		GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Mathf.RoundToInt(22f * uiScale),
			fontStyle = FontStyle.Bold
		};
		GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
		{
			wordWrap = true,
			fontSize = Mathf.RoundToInt(22f * uiScale)
		};

		string controlsText =
			"WASD / Pfeile: bewegen\n"
			+ "Shift: schneller fliegen\n"
			+ "Leertaste: Haken senken, aufnehmen/absetzen, Pfeil setzen\n"
			+ "E: neue Stelle erstellen oder platzieren\n"
			+ "Q: Verbindung starten oder Richtung umdrehen\n"
			+ "R: löschen oder gehaltenen Pfeil abbrechen\n"
			+ "F: Transition auslösen\n"
			+ "Esc: Pause öffnen oder fortsetzen";
		GUI.Label(new Rect(rect.x + 18f * uiScale, rect.y + 8f * uiScale, rect.width - 36f * uiScale, 28f * uiScale), "Tasten", titleStyle);
		GUI.Label(new Rect(rect.x + 18f * uiScale, rect.y + 40f * uiScale, rect.width - 36f * uiScale, rect.height - 50f * uiScale), controlsText, infoStyle);
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

	private bool PauseGameplayFromHost(ulong actorClientId)
	{
		if (levelEnded || gameplayMenuOpen)
		{
			return false;
		}

		gameplayMenuOpen = true;
		gameplayMenuOwnerClientId = actorClientId;
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
		pendingCreatedPlacePickup = false;
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
				block.resultState));
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
			text.Append(" -> ");
			text.Append(block.secondTransitionName);
			text.Append(" / ");
			text.Append(block.processingSeconds.ToString("0.#"));
			text.Append("s / ");
			text.Append(GetFallbackText(block.resultState, "kein Zustand"));
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
