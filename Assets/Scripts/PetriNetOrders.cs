using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class GameManager
{
	private List<PetriNetLevelOrderDefinition> levelOrderDefinitions = new List<PetriNetLevelOrderDefinition>();
	private float levelOrderStartTime = -1f;
	private float levelOrderPauseStartedTime = -1f;
	private RectTransform levelOrderDisplayRoot;
	private Font levelOrderUiFont;
	private readonly List<GameObject> levelOrderCardObjects = new List<GameObject>();
	private readonly List<Image> levelOrderCardBackgrounds = new List<Image>();
	private readonly List<Text> levelOrderCardTexts = new List<Text>();
	private readonly List<Text> levelOrderRecipeTexts = new List<Text>();
	private readonly List<Text> levelOrderHintTexts = new List<Text>();
	private readonly List<Image> levelOrderHintKeyBackgrounds = new List<Image>();
	private readonly List<Text> levelOrderHintKeyTexts = new List<Text>();
	private readonly List<Image> levelOrderTimeBarBackgrounds = new List<Image>();
	private readonly List<Image> levelOrderTimeBarFills = new List<Image>();
	private readonly HashSet<int> completedLevelOrderIndexes = new HashSet<int>();
	private readonly Dictionary<int, float> completedLevelOrderDeliveredAtSeconds = new Dictionary<int, float>();
	private readonly Dictionary<int, float> highlightedLevelOrderUntil = new Dictionary<int, float>();
	private bool showLevelOrderRecipeDetails;
	private string levelOrderDisplayLayoutKey = "";
	private float nextLevelOrderDynamicRefreshTime;
	private const float LevelOrderHighlightSeconds = 1.25f;
	// Rezeptkarten sind nur Anzeige; 5 Hz reicht aus und spart in allen Levels UI-Arbeit.
	private const float LevelOrderDynamicRefreshIntervalSeconds = 0.2f;
	private const string LevelOrderDisplayNoCardsLayoutKey = "no-cards";
	private const float LevelOrderCardMargin = 12f;
	private const float LevelOrderCardGap = 8f;
	private const float LevelOrderCardHeight = 80f;
	private const float LevelOrderCardMinWidth = 120f;
	private const float LevelOrderCardMaxWidth = 270f;
	private const float LevelOrderCardPadding = 6f;
	private const float LevelOrderHintFooterHeight = 24f;
	private const float LevelOrderHintKeySize = 18f;
	private const float LevelOrderHintGap = 4f;
	private const float LevelOrderTimeBarHeight = 8f;
	private const float LevelOrderTimeBarGap = 4f;
	private const float LevelOrderTimeBarDurationSeconds = 600f;
	private const int LevelOrderCanvasSortingOrder = 53;
	private const int LevelOrderTextFontSize = 24;
	private const int LevelOrderRecipeTextFontSize = 17;
	private const int LevelOrderMinTextFontSize = 15;
	private const int LevelOrderHintTextFontSize = 13;
	private const int LevelOrderHintKeyFontSize = 14;
	private const int LevelOrderTextWrapLength = 28;
	private const int LevelOrderRecipeTextWrapLength = 42;
	private const float LevelOrderThreePointSeconds = 90f;
	private const float LevelOrderTwoPointSeconds = 120f;
	private const float LevelOrderOnePointSeconds = 300f;
	private const float LastLevelOrderThreePointSeconds = 150f;
	private const float LastLevelOrderTwoPointSeconds = 240f;
	private const float LastLevelOrderOnePointSeconds = 420f;
	private static readonly Color LevelOrderDefaultCardColor = new Color(1f, 1f, 1f, 0.94f);

	private void SetLevelOrders(List<PetriNetLevelOrderDefinition> orders)
	{
		levelOrderDefinitions = CopyLevelOrders(orders);
		InvalidateLevelOrderDisplayLayout();
	}

	private List<PetriNetLevelOrderDefinition> CopyLevelOrders(List<PetriNetLevelOrderDefinition> source)
	{
		List<PetriNetLevelOrderDefinition> copy = new List<PetriNetLevelOrderDefinition>();
		if (source == null)
		{
			return copy;
		}

		for (int i = 0; i < source.Count; i++)
		{
			PetriNetLevelOrderDefinition order = source[i];
			if (order == null || string.IsNullOrWhiteSpace(order.dishText))
			{
				continue;
			}

			float appearsAt = Mathf.Max(0f, order.appearsAtSeconds);
			int amount = Mathf.Max(1, order.amount);
			for (int copyIndex = 0; copyIndex < amount; copyIndex++)
			{
				copy.Add(new PetriNetLevelOrderDefinition(order.dishText.Trim(), GetOrderRequiredTokenText(order), GetOrderRecipeText(order), appearsAt));
			}
		}

		return copy;
	}

	private void StartLevelOrderTimeline()
	{
		levelOrderStartTime = Time.time;
		levelOrderPauseStartedTime = -1f;
		completedLevelOrderIndexes.Clear();
		completedLevelOrderDeliveredAtSeconds.Clear();
		highlightedLevelOrderUntil.Clear();
		showLevelOrderRecipeDetails = false;
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
		levelResultAnimationStartedAt = -1f;
		levelResultScrollPosition = Vector2.zero;
		InvalidateLevelOrderDisplayLayout();
		ClearLevelOrderCards();
	}

	private void StopLevelOrderTimeline()
	{
		levelOrderStartTime = -1f;
		levelOrderPauseStartedTime = -1f;
		completedLevelOrderIndexes.Clear();
		completedLevelOrderDeliveredAtSeconds.Clear();
		highlightedLevelOrderUntil.Clear();
		showLevelOrderRecipeDetails = false;
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelResultAnimationStartedAt = -1f;
		InvalidateLevelOrderDisplayLayout();
		ClearLevelOrderDisplay();
	}

	private void InvalidateLevelOrderDisplayLayout()
	{
		levelOrderDisplayLayoutKey = "";
		nextLevelOrderDynamicRefreshTime = 0f;
	}

	private void UpdateLevelOrderDisplay()
	{
		if (!gameplayInitialized || levelOrderStartTime < 0f || levelOrderDefinitions == null || levelOrderDefinitions.Count <= 0)
		{
			ClearLevelOrderCards();
			return;
		}

		if (!string.IsNullOrEmpty(levelOrderDisplayLayoutKey) && Time.unscaledTime < nextLevelOrderDynamicRefreshTime)
		{
			return;
		}

		CleanupExpiredLevelOrderHighlights();
		float elapsed = GetLevelOrderElapsedTime();
		List<int> activeOrderIndexes = GetVisibleLevelOrderIndexes(elapsed);
		if (activeOrderIndexes.Count <= 0)
		{
			ClearLevelOrderCards();
			levelOrderDisplayLayoutKey = LevelOrderDisplayNoCardsLayoutKey;
			nextLevelOrderDynamicRefreshTime = Time.unscaledTime + LevelOrderDynamicRefreshIntervalSeconds;
			return;
		}

		EnsureLevelOrderDisplayRoot();
		float uiScale = GetGameplayMenuUiScale();
		string layoutKey = GetLevelOrderDisplayLayoutKey(activeOrderIndexes, uiScale);
		bool layoutChanged = layoutKey != levelOrderDisplayLayoutKey
			|| levelOrderCardObjects.Count != activeOrderIndexes.Count;

		if (!layoutChanged && Time.unscaledTime < nextLevelOrderDynamicRefreshTime)
		{
			return;
		}

		if (!layoutChanged)
		{
			UpdateLevelOrderDynamicCardState(activeOrderIndexes, elapsed);
			nextLevelOrderDynamicRefreshTime = Time.unscaledTime + LevelOrderDynamicRefreshIntervalSeconds;
			return;
		}

		TrimLevelOrderCards(activeOrderIndexes.Count);
		float x = LevelOrderCardMargin * uiScale;
		List<float> cardWidths = CalculateLevelOrderCardWidths(activeOrderIndexes, uiScale);
		for (int i = 0; i < activeOrderIndexes.Count; i++)
		{
			EnsureLevelOrderCard(i);
			int orderIndex = activeOrderIndexes[i];
			float cardWidth = i < cardWidths.Count ? cardWidths[i] : LevelOrderCardMinWidth * uiScale;
			UpdateLevelOrderCard(i, orderIndex, levelOrderDefinitions[orderIndex], elapsed, x, cardWidth, uiScale);
			x += cardWidth + LevelOrderCardGap * uiScale;
		}

		levelOrderDisplayLayoutKey = layoutKey;
		nextLevelOrderDynamicRefreshTime = Time.unscaledTime + LevelOrderDynamicRefreshIntervalSeconds;
	}

	private void HandleLevelOrderToggleHotkey()
	{
		if (!gameplayInitialized || levelOrderStartTime < 0f)
		{
			return;
		}

		Keyboard keyboard = Keyboard.current;
		if (keyboard == null || !keyboard.cKey.wasPressedThisFrame)
		{
			return;
		}

		showLevelOrderRecipeDetails = !showLevelOrderRecipeDetails;
		InvalidateLevelOrderDisplayLayout();
		UpdateLevelOrderDisplay();
		CompleteLevelTutorialOrdersAndDeliveryStep();
	}

	private List<int> GetVisibleLevelOrderIndexes(float elapsed)
	{
		List<int> activeOrderIndexes = new List<int>();
		for (int i = 0; i < levelOrderDefinitions.Count; i++)
		{
			PetriNetLevelOrderDefinition order = levelOrderDefinitions[i];
			if (order == null)
			{
				continue;
			}

			bool highlighted = highlightedLevelOrderUntil.ContainsKey(i);
			bool active = !completedLevelOrderIndexes.Contains(i)
				&& elapsed >= order.appearsAtSeconds;
			if (highlighted || active)
			{
				activeOrderIndexes.Add(i);
			}
		}

		return activeOrderIndexes;
	}

	private string GetLevelOrderDisplayLayoutKey(List<int> activeOrderIndexes, float uiScale)
	{
		StringBuilder builder = new StringBuilder();
		builder.Append(IsEnglishLanguage() ? "en" : "de").Append('|');
		builder.Append(showLevelOrderRecipeDetails ? '1' : '0').Append('|');
		builder.Append(Screen.width).Append('x').Append(Screen.height).Append('|');
		builder.Append(Mathf.RoundToInt(uiScale * 1000f)).Append('|');
		builder.Append(ShouldShowLevelOrderTimeBar() ? '1' : '0').Append('|');
		if (activeOrderIndexes != null)
		{
			for (int i = 0; i < activeOrderIndexes.Count; i++)
			{
				builder.Append(activeOrderIndexes[i]).Append(',');
			}
		}

		return builder.ToString();
	}

	private void UpdateLevelOrderDynamicCardState(List<int> activeOrderIndexes, float elapsed)
	{
		if (activeOrderIndexes == null)
		{
			return;
		}

		for (int i = 0; i < activeOrderIndexes.Count; i++)
		{
			int orderIndex = activeOrderIndexes[i];
			if (orderIndex < 0 || orderIndex >= levelOrderDefinitions.Count)
			{
				continue;
			}

			PetriNetLevelOrderDefinition order = levelOrderDefinitions[orderIndex];
			bool highlighted = highlightedLevelOrderUntil.ContainsKey(orderIndex);
			if (i >= 0 && i < levelOrderCardBackgrounds.Count && levelOrderCardBackgrounds[i] != null)
			{
				levelOrderCardBackgrounds[i].color = highlighted
					? GetLevelOrderHighlightedCardColor(orderIndex, order)
					: LevelOrderDefaultCardColor;
			}

			UpdateLevelOrderTimeBarFill(i, orderIndex, order, elapsed);
		}
	}

	private void EnsureLevelOrderDisplayRoot()
	{
		if (levelOrderDisplayRoot != null)
		{
			return;
		}

		GameObject canvasObject = new GameObject("LevelOrderDisplay", typeof(RectTransform));
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceCamera;
		canvas.worldCamera = mainCamera != null ? mainCamera : Camera.main;
		canvas.planeDistance = 1f;
		canvas.overrideSorting = true;
		canvas.sortingOrder = LevelOrderCanvasSortingOrder;

		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
		scaler.scaleFactor = 1f;

		levelOrderDisplayRoot = canvasObject.GetComponent<RectTransform>();
	}

	private void EnsureLevelOrderCard(int index)
	{
		while (levelOrderCardObjects.Count <= index)
		{
			GameObject card = new GameObject("OrderCard_" + (levelOrderCardObjects.Count + 1), typeof(RectTransform));
			card.transform.SetParent(levelOrderDisplayRoot, false);
			RectTransform cardRect = card.GetComponent<RectTransform>();
			cardRect.anchorMin = new Vector2(0f, 1f);
			cardRect.anchorMax = new Vector2(0f, 1f);
			cardRect.pivot = new Vector2(0f, 1f);

			Image background = card.AddComponent<Image>();
			background.sprite = GetSquareSprite();
			background.color = LevelOrderDefaultCardColor;
			background.raycastTarget = false;

			GameObject textObject = new GameObject("Label", typeof(RectTransform));
			textObject.transform.SetParent(card.transform, false);
			RectTransform textRect = textObject.GetComponent<RectTransform>();
			textRect.anchorMin = Vector2.zero;
			textRect.anchorMax = Vector2.one;
			textRect.offsetMin = new Vector2(LevelOrderCardPadding, LevelOrderCardPadding);
			textRect.offsetMax = new Vector2(-LevelOrderCardPadding, -LevelOrderCardPadding);

			Text text = textObject.AddComponent<Text>();
			text.font = GetLevelOrderUiFont();
			text.alignment = TextAnchor.MiddleCenter;
			text.color = Color.black;
			text.fontSize = LevelOrderTextFontSize;
			text.resizeTextForBestFit = false;
			text.resizeTextMinSize = LevelOrderMinTextFontSize;
			text.resizeTextMaxSize = LevelOrderTextFontSize;
			text.horizontalOverflow = HorizontalWrapMode.Overflow;
			text.verticalOverflow = VerticalWrapMode.Overflow;
			text.supportRichText = false;
			text.raycastTarget = false;

			GameObject recipeTextObject = new GameObject("Recipe", typeof(RectTransform));
			recipeTextObject.transform.SetParent(card.transform, false);
			Text recipeText = recipeTextObject.AddComponent<Text>();
			recipeText.font = GetLevelOrderUiFont();
			recipeText.alignment = TextAnchor.MiddleCenter;
			recipeText.color = Color.black;
			recipeText.fontSize = LevelOrderRecipeTextFontSize;
			recipeText.lineSpacing = 1f;
			recipeText.resizeTextForBestFit = false;
			recipeText.horizontalOverflow = HorizontalWrapMode.Overflow;
			recipeText.verticalOverflow = VerticalWrapMode.Overflow;
			recipeText.supportRichText = false;
			recipeText.raycastTarget = false;
			recipeTextObject.SetActive(false);

			GameObject hintObject = new GameObject("RecipeHint", typeof(RectTransform));
			hintObject.transform.SetParent(card.transform, false);
			Text hintText = hintObject.AddComponent<Text>();
			hintText.font = GetLevelOrderUiFont();
			hintText.alignment = TextAnchor.MiddleRight;
			hintText.color = new Color(0f, 0f, 0f, 0.72f);
			hintText.fontSize = LevelOrderHintTextFontSize;
			hintText.resizeTextForBestFit = false;
			hintText.resizeTextMinSize = 9;
			hintText.resizeTextMaxSize = LevelOrderHintTextFontSize;
			hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
			hintText.verticalOverflow = VerticalWrapMode.Truncate;
			hintText.raycastTarget = false;

			GameObject keyObject = new GameObject("RecipeHintKey", typeof(RectTransform));
			keyObject.transform.SetParent(card.transform, false);
			Image keyBackground = keyObject.AddComponent<Image>();
			keyBackground.sprite = GetSquareSprite();
			keyBackground.color = new Color(1f, 1f, 1f, 0.94f);
			keyBackground.raycastTarget = false;
			Outline keyOutline = keyObject.AddComponent<Outline>();
			keyOutline.effectColor = new Color(0f, 0f, 0f, 0.65f);
			keyOutline.effectDistance = new Vector2(1f, -1f);
			keyOutline.useGraphicAlpha = false;

			GameObject keyTextObject = new GameObject("Label", typeof(RectTransform));
			keyTextObject.transform.SetParent(keyObject.transform, false);
			RectTransform keyTextRect = keyTextObject.GetComponent<RectTransform>();
			keyTextRect.anchorMin = Vector2.zero;
			keyTextRect.anchorMax = Vector2.one;
			keyTextRect.offsetMin = Vector2.zero;
			keyTextRect.offsetMax = Vector2.zero;
			Text keyText = keyTextObject.AddComponent<Text>();
			keyText.font = GetLevelOrderUiFont();
			keyText.alignment = TextAnchor.MiddleCenter;
			keyText.color = Color.black;
			keyText.fontSize = LevelOrderHintKeyFontSize;
			keyText.fontStyle = FontStyle.Bold;
			keyText.resizeTextForBestFit = false;
			keyText.resizeTextMinSize = 9;
			keyText.resizeTextMaxSize = LevelOrderHintKeyFontSize;
			keyText.raycastTarget = false;

			GameObject timeBarObject = new GameObject("TimeBar", typeof(RectTransform));
			timeBarObject.transform.SetParent(card.transform, false);
			Image timeBarBackground = timeBarObject.AddComponent<Image>();
			timeBarBackground.sprite = GetSquareSprite();
			timeBarBackground.color = new Color(0.04f, 0.05f, 0.06f, 0.32f);
			timeBarBackground.raycastTarget = false;

			GameObject timeBarFillObject = new GameObject("Fill", typeof(RectTransform));
			timeBarFillObject.transform.SetParent(timeBarObject.transform, false);
			RectTransform timeBarFillRect = timeBarFillObject.GetComponent<RectTransform>();
			timeBarFillRect.anchorMin = Vector2.zero;
			timeBarFillRect.anchorMax = Vector2.one;
			timeBarFillRect.offsetMin = Vector2.zero;
			timeBarFillRect.offsetMax = Vector2.zero;
			Image timeBarFill = timeBarFillObject.AddComponent<Image>();
			timeBarFill.sprite = GetSquareSprite();
			timeBarFill.type = Image.Type.Filled;
			timeBarFill.fillMethod = Image.FillMethod.Horizontal;
			timeBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
			timeBarFill.fillAmount = 1f;
			timeBarFill.color = new Color(0.18f, 0.78f, 0.3f, 0.96f);
			timeBarFill.raycastTarget = false;

			levelOrderCardObjects.Add(card);
			levelOrderCardBackgrounds.Add(background);
			levelOrderCardTexts.Add(text);
			levelOrderRecipeTexts.Add(recipeText);
			levelOrderHintTexts.Add(hintText);
			levelOrderHintKeyBackgrounds.Add(keyBackground);
			levelOrderHintKeyTexts.Add(keyText);
			levelOrderTimeBarBackgrounds.Add(timeBarBackground);
			levelOrderTimeBarFills.Add(timeBarFill);
		}
	}

	private List<float> CalculateLevelOrderCardWidths(List<int> activeOrderIndexes, float uiScale)
	{
		List<float> widths = new List<float>();
		if (activeOrderIndexes == null || activeOrderIndexes.Count <= 0)
		{
			return widths;
		}

		float margin = LevelOrderCardMargin * uiScale;
		float gap = LevelOrderCardGap * uiScale;
		float availableWidth = Mathf.Max(LevelOrderCardMinWidth * uiScale, Screen.width - margin * 2f - gap * Mathf.Max(0, activeOrderIndexes.Count - 1));
		float totalWidth = 0f;
		List<float> minimumWidths = new List<float>();
		for (int i = 0; i < activeOrderIndexes.Count; i++)
		{
			int orderIndex = activeOrderIndexes[i];
			PetriNetLevelOrderDefinition order = orderIndex >= 0 && orderIndex < levelOrderDefinitions.Count
				? levelOrderDefinitions[orderIndex]
				: null;
			float minimumWidth = GetMinimumUnbrokenLevelOrderCardWidth(order, uiScale);
			float width = Mathf.Max(GetDesiredLevelOrderCardWidth(order, uiScale), minimumWidth);
			widths.Add(width);
			minimumWidths.Add(minimumWidth);
			totalWidth += width;
		}

		float totalWithGaps = totalWidth + gap * Mathf.Max(0, activeOrderIndexes.Count - 1);
		if (totalWithGaps <= availableWidth)
		{
			return widths;
		}

		float shrinkCapacity = 0f;
		for (int i = 0; i < widths.Count; i++)
		{
			shrinkCapacity += Mathf.Max(0f, widths[i] - minimumWidths[i]);
		}

		if (shrinkCapacity <= 0.001f)
		{
			return widths;
		}

		float overflow = totalWithGaps - availableWidth;
		for (int i = 0; i < widths.Count; i++)
		{
			float canShrink = Mathf.Max(0f, widths[i] - minimumWidths[i]);
			widths[i] = Mathf.Max(
				minimumWidths[i],
				widths[i] - overflow * (canShrink / shrinkCapacity));
		}

		return widths;
	}

	private float GetDesiredLevelOrderCardWidth(PetriNetLevelOrderDefinition order, float uiScale)
	{
		float fontSize = LevelOrderTextFontSize * uiScale;
		float padding = LevelOrderCardPadding * uiScale * 2f;
		string dishText = GetLocalizedOrderDishText(order);
		int dishLineLength = GetLongestWrappedLevelOrderLineLength(dishText, LevelOrderTextWrapLength);
		float textWidth = Mathf.Max(4f, dishLineLength) * fontSize * 0.48f + padding;
		string recipeText = GetLocalizedOrderRecipeText(order);
		int recipeLineLength = GetLongestWrappedLevelOrderLineLength(recipeText, LevelOrderRecipeTextWrapLength);
		float recipeWidth = Mathf.Max(7f, recipeLineLength) * LevelOrderRecipeTextFontSize * uiScale * 0.48f + padding;
		float hintWidth = GetLevelOrderHintTotalWidth(true, uiScale) + padding;
		return Mathf.Clamp(
			Mathf.Max(textWidth, recipeWidth, hintWidth),
			LevelOrderCardMinWidth * uiScale,
			LevelOrderCardMaxWidth * uiScale);
	}

	private float GetMinimumUnbrokenLevelOrderCardWidth(PetriNetLevelOrderDefinition order, float uiScale)
	{
		float padding = LevelOrderCardPadding * uiScale * 2f;
		int dishFontSize = Mathf.Max(1, Mathf.RoundToInt(LevelOrderTextFontSize * uiScale));
		int recipeFontSize = Mathf.Max(1, Mathf.RoundToInt(LevelOrderRecipeTextFontSize * uiScale));
		float dishTextWidth = GetMinimumReadableLevelOrderTextWidth(
			GetLocalizedOrderDishText(order),
			dishFontSize) + padding;
		float recipeTextWidth = Mathf.Max(
			GetLevelOrderTextUnitWidth(GameText("Rezept:", "Recipe:"), recipeFontSize),
			GetMinimumReadableLevelOrderTextWidth(GetLocalizedOrderRecipeText(order), recipeFontSize)) + padding;
		float hintWidth = GetLevelOrderHintTotalWidth(true, uiScale) + padding;
		return Mathf.Max(
			LevelOrderCardMinWidth * uiScale,
			dishTextWidth,
			recipeTextWidth,
			hintWidth);
	}

	private float GetMinimumReadableLevelOrderTextWidth(string text, int fontSize)
	{
		return Mathf.Max(
			GetWidestLevelOrderWordWidth(text, fontSize),
			GetWidestLevelOrderWordPairWidth(text, fontSize));
	}

	private float GetWidestLevelOrderWordWidth(string text, int fontSize)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0f;
		}

		float widest = 0f;
		string[] words = text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < words.Length; i++)
		{
			widest = Mathf.Max(widest, GetLevelOrderTextUnitWidth(words[i], fontSize));
		}

		return widest;
	}

	private float GetWidestLevelOrderWordPairWidth(string text, int fontSize)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return 0f;
		}

		float widest = 0f;
		string[] words = text.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i + 1 < words.Length; i++)
		{
			string pair = words[i] + " " + words[i + 1];
			widest = Mathf.Max(widest, GetLevelOrderTextUnitWidth(pair, fontSize));
		}

		return widest;
	}

	private float GetLevelOrderTextUnitWidth(string text, int fontSize)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0f;
		}

		return Mathf.Max(
			GetLevelOrderWordWidth(text, fontSize),
			text.Length * fontSize * 0.48f + 2f);
	}

	private float GetLevelOrderWordWidth(string word, int fontSize)
	{
		if (string.IsNullOrEmpty(word))
		{
			return 0f;
		}

		Font font = GetLevelOrderUiFont();
		if (font == null)
		{
			return word.Length * fontSize * 0.55f;
		}

		font.RequestCharactersInTexture(word, fontSize, FontStyle.Normal);
		float width = 0f;
		for (int i = 0; i < word.Length; i++)
		{
			if (font.GetCharacterInfo(word[i], out CharacterInfo character, fontSize, FontStyle.Normal))
			{
				width += character.advance;
			}
			else
			{
				width += fontSize * 0.55f;
			}
		}

		return width + 2f;
	}

	private float GetLevelOrderHintTotalWidth(bool expanded, float uiScale)
	{
			string hintLabel = expanded ? GameText("Einklappen:", "Collapse:") : GameText("Rezept:", "Recipe:");
			float hintWidth = Mathf.Max(
				(expanded ? 76f : 50f) * uiScale,
				GetLevelOrderWordWidth(hintLabel, Mathf.Max(1, Mathf.RoundToInt(LevelOrderHintTextFontSize * uiScale))) + 4f * uiScale);
			return hintWidth + LevelOrderHintGap * uiScale + LevelOrderHintKeySize * uiScale;
		}

	private void UpdateLevelOrderCard(int index, int orderIndex, PetriNetLevelOrderDefinition order, float elapsed, float x, float cardWidth, float uiScale)
	{
		bool showRecipe = showLevelOrderRecipeDetails && !string.IsNullOrWhiteSpace(GetOrderRecipeText(order));
		bool highlighted = highlightedLevelOrderUntil.ContainsKey(orderIndex);
		int textMaxSize = Mathf.RoundToInt(LevelOrderTextFontSize * uiScale);
		int textMinSize = Mathf.RoundToInt(LevelOrderMinTextFontSize * uiScale);
		int recipeTextSize = Mathf.RoundToInt(LevelOrderRecipeTextFontSize * uiScale);
		int dishWrapLength = GetLevelOrderWrapLengthForWidth(cardWidth, uiScale, LevelOrderTextFontSize);
		int recipeWrapLength = GetLevelOrderWrapLengthForWidth(cardWidth, uiScale, LevelOrderRecipeTextFontSize);
			string cardText = WrapLevelOrderText(GetLocalizedOrderDishText(order), dishWrapLength);
			string recipeCardText = showRecipe
				? GameText("Rezept:\n", "Recipe:\n") + WrapLevelOrderText(GetLocalizedOrderRecipeText(order), recipeWrapLength)
				: "";
		float cardHeight = GetLevelOrderCardHeight(
			order,
			showRecipe,
			dishWrapLength,
			recipeWrapLength,
			uiScale);

		GameObject card = levelOrderCardObjects[index];
		RectTransform cardRect = card.transform as RectTransform;
		if (cardRect != null)
		{
			cardRect.anchoredPosition = new Vector2(x, -LevelOrderCardMargin * uiScale);
			cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
		}

		if (levelOrderCardBackgrounds[index] != null)
		{
			levelOrderCardBackgrounds[index].color = highlighted
				? GetLevelOrderHighlightedCardColor(orderIndex, order)
				: LevelOrderDefaultCardColor;
		}

		Text text = levelOrderCardTexts[index];
		if (text != null)
		{
			RectTransform textRect = text.transform as RectTransform;
			if (textRect != null)
			{
				float padding = LevelOrderCardPadding * uiScale;
				float titleHeight = GetLevelOrderTitleHeight(cardText, uiScale);
				textRect.anchorMin = new Vector2(0f, 1f);
				textRect.anchorMax = new Vector2(1f, 1f);
				textRect.pivot = new Vector2(0.5f, 1f);
				textRect.anchoredPosition = new Vector2(0f, -padding);
				textRect.sizeDelta = new Vector2(-padding * 2f, titleHeight);
			}

			text.text = cardText;
			text.fontSize = textMaxSize;
			text.resizeTextMinSize = textMinSize;
			text.resizeTextMaxSize = textMaxSize;
		}

		Text recipeText = index >= 0 && index < levelOrderRecipeTexts.Count
			? levelOrderRecipeTexts[index]
			: null;
		if (recipeText != null)
		{
			recipeText.gameObject.SetActive(showRecipe);
			if (showRecipe)
			{
				float padding = LevelOrderCardPadding * uiScale;
				float titleHeight = GetLevelOrderTitleHeight(cardText, uiScale);
				float gap = LevelOrderTimeBarGap * uiScale;
				float timeBarSpace = ShouldShowLevelOrderTimeBar()
					? LevelOrderTimeBarHeight * uiScale + gap
					: 0f;
				float recipeHeight = GetLevelOrderRecipeHeight(recipeCardText, uiScale);
				RectTransform recipeRect = recipeText.transform as RectTransform;
				if (recipeRect != null)
				{
					recipeRect.anchorMin = new Vector2(0f, 1f);
					recipeRect.anchorMax = new Vector2(1f, 1f);
					recipeRect.pivot = new Vector2(0.5f, 1f);
					recipeRect.anchoredPosition = new Vector2(
						0f,
						-(padding + titleHeight + gap + timeBarSpace));
					recipeRect.sizeDelta = new Vector2(-padding * 2f, recipeHeight);
				}

				recipeText.text = recipeCardText;
				recipeText.fontSize = recipeTextSize;
				recipeText.lineSpacing = 1f;
			}
		}

		float barTop = LevelOrderCardPadding * uiScale
			+ GetLevelOrderTitleHeight(cardText, uiScale)
			+ LevelOrderTimeBarGap * uiScale;
		UpdateLevelOrderCardHint(index, cardWidth, uiScale);
		UpdateLevelOrderTimeBar(index, orderIndex, order, elapsed, barTop, uiScale);
	}

	private float GetLevelOrderCardHeight(
		PetriNetLevelOrderDefinition order,
		bool showRecipe,
		int dishWrapLength,
		int recipeWrapLength,
		float uiScale)
	{
		float minHeight = LevelOrderCardHeight * uiScale;
			string wrappedDish = WrapLevelOrderText(GetLocalizedOrderDishText(order), dishWrapLength);
			float textHeight = GetLevelOrderTitleHeight(wrappedDish, uiScale);
			if (showRecipe)
			{
				string wrappedRecipe = WrapLevelOrderText(GetLocalizedOrderRecipeText(order), recipeWrapLength);
				textHeight += GetLevelOrderRecipeHeight(GameText("Rezept:\n", "Recipe:\n") + wrappedRecipe, uiScale);
			}

		float padding = LevelOrderCardPadding * uiScale;
		float footerHeight = LevelOrderHintFooterHeight * uiScale;
		bool showTimeBar = ShouldShowLevelOrderTimeBar();
		float barHeight = showTimeBar ? LevelOrderTimeBarHeight * uiScale : 0f;
		float gapCount = (showRecipe ? 1f : 0f) + (showTimeBar ? 1f : 0f);
		float gaps = LevelOrderTimeBarGap * uiScale * gapCount;
		return Mathf.Max(minHeight, textHeight + footerHeight + barHeight + gaps + padding * 2f);
	}

	private float GetLevelOrderTitleHeight(string text, float uiScale)
	{
		return CountTextLines(text) * LevelOrderTextFontSize * uiScale * 1.18f;
	}

	private float GetLevelOrderRecipeHeight(string text, float uiScale)
	{
		return CountTextLines(text) * LevelOrderRecipeTextFontSize * uiScale;
	}

	private void UpdateLevelOrderTimeBar(
		int index,
		int orderIndex,
		PetriNetLevelOrderDefinition order,
		float elapsed,
		float barTop,
		float uiScale)
	{
		if (index < 0
			|| index >= levelOrderTimeBarBackgrounds.Count
			|| index >= levelOrderTimeBarFills.Count)
		{
			return;
		}

		Image background = levelOrderTimeBarBackgrounds[index];
		Image fill = levelOrderTimeBarFills[index];
		if (background == null || fill == null)
		{
			return;
		}

		bool showTimeBar = ShouldShowLevelOrderTimeBar();
		background.gameObject.SetActive(showTimeBar);
		fill.gameObject.SetActive(showTimeBar);
		if (!showTimeBar)
		{
			return;
		}

		RectTransform barRect = background.transform as RectTransform;
		if (barRect != null)
		{
			float padding = LevelOrderCardPadding * uiScale;
			float barHeight = LevelOrderTimeBarHeight * uiScale;
			barRect.anchorMin = new Vector2(0f, 1f);
			barRect.anchorMax = new Vector2(1f, 1f);
			barRect.pivot = new Vector2(0.5f, 1f);
			barRect.anchoredPosition = new Vector2(0f, -barTop);
			barRect.sizeDelta = new Vector2(-padding * 2f, barHeight);
		}

		UpdateLevelOrderTimeBarFill(index, orderIndex, order, elapsed);
	}

	private void UpdateLevelOrderTimeBarFill(
		int index,
		int orderIndex,
		PetriNetLevelOrderDefinition order,
		float elapsed)
	{
		if (index < 0
			|| index >= levelOrderTimeBarBackgrounds.Count
			|| index >= levelOrderTimeBarFills.Count)
		{
			return;
		}

		Image background = levelOrderTimeBarBackgrounds[index];
		Image fill = levelOrderTimeBarFills[index];
		if (background == null || fill == null)
		{
			return;
		}

		bool showTimeBar = ShouldShowLevelOrderTimeBar();
		background.gameObject.SetActive(showTimeBar);
		fill.gameObject.SetActive(showTimeBar);
		if (!showTimeBar)
		{
			return;
		}

		float deliveredAtSeconds = completedLevelOrderIndexes.Contains(orderIndex)
			&& completedLevelOrderDeliveredAtSeconds.TryGetValue(orderIndex, out float storedDeliveryTime)
				? storedDeliveryTime
				: elapsed;
		float appearedAtSeconds = order != null ? order.appearsAtSeconds : 0f;
		float orderAge = Mathf.Max(0f, deliveredAtSeconds - appearedAtSeconds);
		fill.fillAmount = 1f - Mathf.Clamp01(orderAge / LevelOrderTimeBarDurationSeconds);
		fill.color = GetLevelOrderTimeBarColor(orderAge);
	}

	private Color GetLevelOrderTimeBarColor(float orderAge)
	{
		if (orderAge <= GetLevelOrderThreePointSeconds())
		{
			return new Color(0.16f, 0.78f, 0.28f, 0.97f);
		}

		if (orderAge <= GetLevelOrderTwoPointSeconds())
		{
			return new Color(0.95f, 0.78f, 0.12f, 0.97f);
		}

		if (orderAge <= GetLevelOrderOnePointSeconds())
		{
			return new Color(1f, 0.48f, 0.08f, 0.97f);
		}

		return new Color(0.9f, 0.12f, 0.1f, 0.97f);
	}

	private bool ShouldShowLevelOrderTimeBar()
	{
		return !IsTutorialLevelActive();
	}

	private int GetLevelOrderWrapLengthForWidth(float cardWidth, float uiScale, float baseFontSize)
	{
		float contentWidth = Mathf.Max(1f, cardWidth - LevelOrderCardPadding * uiScale * 2f);
		float fontSize = Mathf.Max(1f, baseFontSize * uiScale);
		return Mathf.Clamp(Mathf.FloorToInt(contentWidth / (fontSize * 0.48f)), 8, LevelOrderRecipeTextWrapLength);
	}

	private int CountTextLines(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 1;
		}

		int lines = 1;
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n')
			{
				lines++;
			}
		}

		return lines;
	}

	private void UpdateLevelOrderCardHint(int index, float cardWidth, float uiScale)
	{
		if (index < 0 || index >= levelOrderHintTexts.Count || index >= levelOrderHintKeyBackgrounds.Count || index >= levelOrderHintKeyTexts.Count)
		{
			return;
		}

		Text hintText = levelOrderHintTexts[index];
		Image keyBackground = levelOrderHintKeyBackgrounds[index];
		Text keyText = levelOrderHintKeyTexts[index];
		if (hintText == null || keyBackground == null || keyText == null)
		{
			return;
		}

		float padding = LevelOrderCardPadding * uiScale;
		float keySize = LevelOrderHintKeySize * uiScale;
		float gap = LevelOrderHintGap * uiScale;
		float y = padding * 0.55f;
			string hintLabel = showLevelOrderRecipeDetails ? GameText("Einklappen:", "Collapse:") : GameText("Rezept:", "Recipe:");
			int hintFontSize = Mathf.RoundToInt(LevelOrderHintTextFontSize * uiScale);
			float hintWidth = Mathf.Max(
				(showLevelOrderRecipeDetails ? 76f : 50f) * uiScale,
				GetLevelOrderWordWidth(hintLabel, Mathf.Max(1, hintFontSize)) + 4f * uiScale);
		float totalWidth = hintWidth + gap + keySize;
		float startX = Mathf.Max(padding, (cardWidth - totalWidth) * 0.5f);

		RectTransform hintRect = hintText.transform as RectTransform;
		if (hintRect != null)
		{
			hintRect.anchorMin = new Vector2(0f, 0f);
			hintRect.anchorMax = new Vector2(0f, 0f);
			hintRect.pivot = new Vector2(0f, 0f);
			hintRect.anchoredPosition = new Vector2(startX, y);
			hintRect.sizeDelta = new Vector2(hintWidth, keySize);
		}

		RectTransform keyRect = keyBackground.transform as RectTransform;
		if (keyRect != null)
		{
			keyRect.anchorMin = new Vector2(0f, 0f);
			keyRect.anchorMax = new Vector2(0f, 0f);
			keyRect.pivot = new Vector2(0f, 0f);
			keyRect.anchoredPosition = new Vector2(startX + hintWidth + gap, y);
			keyRect.sizeDelta = new Vector2(keySize, keySize);
		}

			int keyFontSize = Mathf.RoundToInt(LevelOrderHintKeyFontSize * uiScale);
		hintText.text = hintLabel;
		hintText.fontSize = hintFontSize;
		hintText.resizeTextMaxSize = hintFontSize;
		hintText.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(9f * uiScale));
		keyText.text = "C";
		keyText.fontSize = keyFontSize;
		keyText.resizeTextMaxSize = keyFontSize;
		keyText.resizeTextMinSize = Mathf.Max(8, Mathf.RoundToInt(9f * uiScale));
		keyBackground.color = new Color(1f, 1f, 1f, 0.96f);
	}

	private void TrimLevelOrderCards(int count)
	{
		for (int i = levelOrderCardObjects.Count - 1; i >= count; i--)
		{
			if (levelOrderCardObjects[i] != null)
			{
				Destroy(levelOrderCardObjects[i]);
			}

			levelOrderCardObjects.RemoveAt(i);
			levelOrderCardBackgrounds.RemoveAt(i);
			levelOrderCardTexts.RemoveAt(i);
			levelOrderRecipeTexts.RemoveAt(i);
			levelOrderHintTexts.RemoveAt(i);
			levelOrderHintKeyBackgrounds.RemoveAt(i);
			levelOrderHintKeyTexts.RemoveAt(i);
			levelOrderTimeBarBackgrounds.RemoveAt(i);
			levelOrderTimeBarFills.RemoveAt(i);
		}
	}

	private void ClearLevelOrderCards()
	{
		TrimLevelOrderCards(0);
	}

	private void ClearLevelOrderDisplay()
	{
		ClearLevelOrderCards();
		if (levelOrderDisplayRoot != null)
		{
			Destroy(levelOrderDisplayRoot.gameObject);
			levelOrderDisplayRoot = null;
		}
	}

	private Font GetLevelOrderUiFont()
	{
		if (levelOrderUiFont != null)
		{
			return levelOrderUiFont;
		}

		levelOrderUiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (levelOrderUiFont == null)
		{
			levelOrderUiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}

		if (levelOrderUiFont == null)
		{
			levelOrderUiFont = Font.CreateDynamicFontFromOSFont("Arial", LevelOrderTextFontSize);
		}

		return levelOrderUiFont;
	}

	private void HandleDeliveredTokens(List<TokenRuntime> deliveredTokens)
	{
		if (levelOrderStartTime < 0f || deliveredTokens == null || deliveredTokens.Count <= 0)
		{
			return;
		}

		float elapsed = GetLevelOrderElapsedTime();
		List<string> deliveredCandidates = BuildDeliveredDishCandidates(deliveredTokens);
		for (int i = 0; i < deliveredCandidates.Count; i++)
		{
			int matchingOrderIndex = FindMatchingActiveOrderIndex(deliveredCandidates[i], elapsed);
			if (matchingOrderIndex >= 0)
			{
				MarkLevelOrderDelivered(matchingOrderIndex, elapsed);
				return;
			}
		}
	}

	private List<string> BuildDeliveredDishCandidates(List<TokenRuntime> deliveredTokens)
	{
		List<string> candidates = new List<string>();
		for (int i = 0; i < deliveredTokens.Count; i++)
		{
			AddDeliveredDishCandidate(candidates, GetTokenDescription(deliveredTokens[i]));
		}

		if (deliveredTokens.Count > 1)
		{
			AddDeliveredDishCandidate(candidates, JoinTokenDescriptions(deliveredTokens));
		}

		return candidates;
	}

	private void AddDeliveredDishCandidate(List<string> candidates, string candidate)
	{
		string normalized = NormalizeDishText(candidate);
		if (string.IsNullOrEmpty(normalized))
		{
			return;
		}

		for (int i = 0; i < candidates.Count; i++)
		{
			if (candidates[i] == normalized)
			{
				return;
			}
		}

		candidates.Add(normalized);
	}

	private int FindMatchingActiveOrderIndex(string normalizedDeliveredDish, float elapsed)
	{
		if (string.IsNullOrEmpty(normalizedDeliveredDish) || levelOrderDefinitions == null)
		{
			return -1;
		}

		List<int> visibleOrderIndexes = GetVisibleLevelOrderIndexes(elapsed);
		for (int visibleIndex = 0; visibleIndex < visibleOrderIndexes.Count; visibleIndex++)
		{
			int orderIndex = visibleOrderIndexes[visibleIndex];
			PetriNetLevelOrderDefinition order = levelOrderDefinitions[orderIndex];
			if (order == null || completedLevelOrderIndexes.Contains(orderIndex))
			{
				continue;
			}

			if (NormalizeDishText(GetOrderRequiredTokenText(order)) == normalizedDeliveredDish)
			{
				// Cards are displayed in this same order, so the first match is the leftmost one.
				return orderIndex;
			}
		}

		return -1;
	}

	private void MarkLevelOrderDelivered(int orderIndex)
	{
		float elapsed = GetLevelOrderElapsedTime();
		MarkLevelOrderDelivered(orderIndex, elapsed);
	}

	private void MarkLevelOrderDelivered(int orderIndex, float deliveredElapsed)
	{
		if (orderIndex < 0)
		{
			return;
		}

		completedLevelOrderIndexes.Add(orderIndex);
		completedLevelOrderDeliveredAtSeconds[orderIndex] = Mathf.Max(0f, deliveredElapsed);
		highlightedLevelOrderUntil[orderIndex] = Time.time + LevelOrderHighlightSeconds;
		TryFinishLevelAfterAllOrdersDelivered();
	}

	private void TryFinishLevelAfterAllOrdersDelivered()
	{
		if (!IsHostOrOffline()
			|| levelEnded
			|| levelOrderDefinitions == null
			|| levelOrderDefinitions.Count <= 0)
		{
			return;
		}

		for (int i = 0; i < levelOrderDefinitions.Count; i++)
		{
			if (!completedLevelOrderIndexes.Contains(i))
			{
				return;
			}
		}

		EndLevelFromHost(NoGameplayMenuOwnerClientId);
	}

	private Color GetLevelOrderHighlightedCardColor(int orderIndex, PetriNetLevelOrderDefinition order)
	{
		if (IsTutorialLevelActive())
		{
			Color tutorialHighlightedColor = Color.Lerp(GetLevelOrderTimeBarColor(0f), Color.white, 0.22f);
			tutorialHighlightedColor.a = 0.98f;
			return tutorialHighlightedColor;
		}

		float deliveredAtSeconds = completedLevelOrderDeliveredAtSeconds.TryGetValue(orderIndex, out float storedDeliveryTime)
			? storedDeliveryTime
			: GetLevelOrderElapsedTime();
		float appearedAtSeconds = order != null ? order.appearsAtSeconds : 0f;
		Color barColor = GetLevelOrderTimeBarColor(Mathf.Max(0f, deliveredAtSeconds - appearedAtSeconds));
		float remaining = highlightedLevelOrderUntil.TryGetValue(orderIndex, out float highlightedUntil)
			? Mathf.Max(0f, highlightedUntil - Time.time)
			: 0f;
		float fade = Mathf.Clamp01(remaining / 0.3f);
		float pulse = 0.78f + Mathf.Sin(Time.unscaledTime * 18f) * 0.22f;
		Color highlightedColor = Color.Lerp(barColor, Color.white, 0.22f);
		highlightedColor.a = 0.98f;
		return Color.Lerp(LevelOrderDefaultCardColor, highlightedColor, pulse * fade);
	}

	private void PauseLevelOrderTimeline()
	{
		if (levelOrderStartTime < 0f || levelOrderPauseStartedTime >= 0f)
		{
			return;
		}

		levelOrderPauseStartedTime = Time.time;
	}

	private void ResumeLevelOrderTimeline()
	{
		if (levelOrderStartTime < 0f || levelOrderPauseStartedTime < 0f)
		{
			return;
		}

		float pausedSeconds = Mathf.Max(0f, Time.time - levelOrderPauseStartedTime);
		levelOrderStartTime += pausedSeconds;
		ShiftLevelOrderHighlightTimers(pausedSeconds);
		levelOrderPauseStartedTime = -1f;
	}

	private float GetLevelOrderElapsedTime()
	{
		if (levelOrderStartTime < 0f)
		{
			return 0f;
		}

		float clockTime = levelOrderPauseStartedTime >= 0f ? levelOrderPauseStartedTime : Time.time;
		return Mathf.Max(0f, clockTime - levelOrderStartTime);
	}

	private void ShiftLevelOrderHighlightTimers(float seconds)
	{
		if (seconds <= 0f || highlightedLevelOrderUntil.Count <= 0)
		{
			return;
		}

		List<int> keys = new List<int>(highlightedLevelOrderUntil.Keys);
		for (int i = 0; i < keys.Count; i++)
		{
			highlightedLevelOrderUntil[keys[i]] += seconds;
		}
	}

	private List<int> GetCompletedLevelOrderIndexes()
	{
		return new List<int>(completedLevelOrderIndexes);
	}

	private List<float> GetCompletedLevelOrderDeliveryTimes()
	{
		List<float> deliveryTimes = new List<float>();
		int orderCount = levelOrderDefinitions != null ? levelOrderDefinitions.Count : 0;
		for (int i = 0; i < orderCount; i++)
		{
			deliveryTimes.Add(completedLevelOrderDeliveredAtSeconds.TryGetValue(i, out float deliveredAtSeconds)
				? deliveredAtSeconds
				: -1f);
		}

		return deliveryTimes;
	}

	private void ApplyCompletedLevelOrderIndexes(List<int> completedIndexes)
	{
		ApplyCompletedLevelOrderState(completedIndexes, null);
	}

	private void ApplyCompletedLevelOrderState(List<int> completedIndexes, List<float> completedDeliveryTimes)
	{
		if (completedIndexes == null)
		{
			return;
		}

		for (int i = 0; i < completedIndexes.Count; i++)
		{
			int orderIndex = completedIndexes[i];
			if (orderIndex < 0)
			{
				continue;
			}

			if (!completedLevelOrderIndexes.Contains(orderIndex))
			{
				MarkLevelOrderDelivered(orderIndex, GetCompletedLevelOrderDeliveryTime(orderIndex, completedDeliveryTimes));
			}
			else if (completedDeliveryTimes != null)
			{
				float deliveredAtSeconds = GetCompletedLevelOrderDeliveryTime(orderIndex, completedDeliveryTimes);
				completedLevelOrderDeliveredAtSeconds[orderIndex] = deliveredAtSeconds;
			}
		}
	}

	private float GetCompletedLevelOrderDeliveryTime(int orderIndex, List<float> completedDeliveryTimes)
	{
		if (completedDeliveryTimes != null
			&& orderIndex >= 0
			&& orderIndex < completedDeliveryTimes.Count
			&& completedDeliveryTimes[orderIndex] >= 0f)
		{
			return completedDeliveryTimes[orderIndex];
		}

		return GetLevelOrderElapsedTime();
	}

	private string GetLevelOrderResultSummaryText()
	{
		int orderCount = levelOrderDefinitions != null ? levelOrderDefinitions.Count : 0;
		int score = GetLevelOrderScore();
		int maximumScore = orderCount * 3;
		StringBuilder text = new StringBuilder();
		text.Append(GameText("Punkte: ", "Points: "));
		text.Append(score);
		text.Append(" / ");
		text.Append(maximumScore);

		if (orderCount <= 0)
		{
			text.Append(GameText("\n\nKeine Rezepte in diesem Level.", "\n\nNo recipes in this level."));
			return text.ToString();
		}

		for (int i = 0; i < orderCount; i++)
		{
			PetriNetLevelOrderDefinition order = levelOrderDefinitions[i];
			if (order == null)
			{
				continue;
			}

			text.Append("\n\n");
			text.Append(i + 1);
			text.Append(". ");
			text.Append(GetLocalizedOrderDishText(order));
			text.Append(GameText("\nGefordert: ", "\nRequired: "));
			text.Append(GetLocalizedOrderRequiredTokenText(order));
			string recipeText = GetLocalizedOrderRecipeText(order);
			if (!string.IsNullOrEmpty(recipeText))
			{
				text.Append(GameText("\nRezept: ", "\nRecipe: "));
				text.Append(recipeText);
			}

			text.Append("\n");

			if (!completedLevelOrderIndexes.Contains(i))
			{
				text.Append(GameText("Nicht abgearbeitet", "Not completed"));
				continue;
			}

			float deliveredAtSeconds = completedLevelOrderDeliveredAtSeconds.TryGetValue(i, out float storedDeliveryTime)
				? storedDeliveryTime
				: 0f;
			float orderDuration = GetLevelOrderCompletionDuration(i, deliveredAtSeconds);
			int orderScore = GetLevelOrderScore(i, deliveredAtSeconds);
			text.Append(GameText("Erledigt nach ", "Completed after "));
			text.Append(FormatLevelOrderTime(orderDuration));
			text.Append(" (+");
			text.Append(orderScore);
			text.Append(orderScore == 1 ? GameText(" Punkt)", " point)") : GameText(" Punkte)", " points)"));
		}

		return text.ToString();
	}

	private int GetLevelOrderCount()
	{
		return levelOrderDefinitions != null ? levelOrderDefinitions.Count : 0;
	}

	private int GetLevelOrderScore()
	{
		int score = 0;
		int orderCount = GetLevelOrderCount();
		for (int orderIndex = 0; orderIndex < orderCount; orderIndex++)
		{
			if (completedLevelOrderIndexes.Contains(orderIndex)
				&& completedLevelOrderDeliveredAtSeconds.TryGetValue(orderIndex, out float deliveredAtSeconds))
			{
				score += GetLevelOrderScore(orderIndex, deliveredAtSeconds);
			}
		}

		return score;
	}

	private int GetLevelOrderScore(int orderIndex, float deliveredAtSeconds)
	{
		if (IsTutorialLevelActive())
		{
			return 3;
		}

		float duration = GetLevelOrderCompletionDuration(orderIndex, deliveredAtSeconds);
		if (duration <= GetLevelOrderThreePointSeconds())
		{
			return 3;
		}

		if (duration <= GetLevelOrderTwoPointSeconds())
		{
			return 2;
		}

		if (duration <= GetLevelOrderOnePointSeconds())
		{
			return 1;
		}

		return 0;
	}

	private float GetLevelOrderThreePointSeconds()
	{
		return IsLastLevelSelected() ? LastLevelOrderThreePointSeconds : LevelOrderThreePointSeconds;
	}

	private float GetLevelOrderTwoPointSeconds()
	{
		return IsLastLevelSelected() ? LastLevelOrderTwoPointSeconds : LevelOrderTwoPointSeconds;
	}

	private float GetLevelOrderOnePointSeconds()
	{
		return IsLastLevelSelected() ? LastLevelOrderOnePointSeconds : LevelOrderOnePointSeconds;
	}

	private float GetLevelOrderCompletionDuration(int orderIndex, float deliveredAtSeconds)
	{
		if (levelOrderDefinitions == null
			|| orderIndex < 0
			|| orderIndex >= levelOrderDefinitions.Count
			|| levelOrderDefinitions[orderIndex] == null)
		{
			return Mathf.Max(0f, deliveredAtSeconds);
		}

		return Mathf.Max(0f, deliveredAtSeconds - levelOrderDefinitions[orderIndex].appearsAtSeconds);
	}

	private int GetLevelResultStarCount()
	{
		int orderCount = GetLevelOrderCount();
		if (orderCount <= 0)
		{
			return 0;
		}

		int score = GetLevelOrderScore();
		int maximumScore = orderCount * 3;
		if (IsLastLevelSelected())
		{
			int threePointOrders = 0;
			for (int orderIndex = 0; orderIndex < orderCount; orderIndex++)
			{
				if (completedLevelOrderIndexes.Contains(orderIndex)
					&& completedLevelOrderDeliveredAtSeconds.TryGetValue(orderIndex, out float deliveredAtSeconds)
					&& GetLevelOrderScore(orderIndex, deliveredAtSeconds) == 3)
				{
					threePointOrders++;
				}
			}

			int requiredThreePointOrders = Mathf.CeilToInt(orderCount * 0.75f);
			if (threePointOrders >= requiredThreePointOrders)
			{
				return 3;
			}
		}

		int allowedMissingForThreeStars = orderCount >= 4 ? 2 : 1;
		if (maximumScore - score <= allowedMissingForThreeStars)
		{
			return 3;
		}

		if (score >= orderCount * 2)
		{
			return 2;
		}

		return score >= orderCount ? 1 : 0;
	}

	private bool IsLastLevelSelected()
	{
		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		return levels != null
			&& levels.Count > 0
			&& selectedLevelIndex == levels.Count - 1;
	}

	private void CleanupExpiredLevelOrderHighlights()
	{
		if (levelOrderPauseStartedTime >= 0f)
		{
			return;
		}

		if (highlightedLevelOrderUntil.Count <= 0)
		{
			return;
		}

		List<int> expiredIndexes = new List<int>();
		foreach (KeyValuePair<int, float> pair in highlightedLevelOrderUntil)
		{
			if (Time.time >= pair.Value)
			{
				expiredIndexes.Add(pair.Key);
			}
		}

		for (int i = 0; i < expiredIndexes.Count; i++)
		{
			highlightedLevelOrderUntil.Remove(expiredIndexes[i]);
		}
	}

	private string NormalizeDishText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return "";
		}

		string preparedText = PrepareDishTextForCanonicalization(text);
		int index = 0;
		return ParseDishExpression(preparedText, ref index);
	}

	private string PrepareDishTextForCanonicalization(string text)
	{
		string lowerText = text.Trim().ToLowerInvariant().Replace("+", ",");
		StringBuilder result = new StringBuilder();
		bool pendingSpace = false;
		for (int i = 0; i < lowerText.Length; i++)
		{
			char c = lowerText[i];
			if (char.IsWhiteSpace(c))
			{
				pendingSpace = true;
				continue;
			}

			if (c == ',' || c == '(' || c == ')')
			{
				TrimTrailingSpace(result);
				result.Append(c);
				pendingSpace = false;
				continue;
			}

			if (pendingSpace && result.Length > 0 && result[result.Length - 1] != '(' && result[result.Length - 1] != ',')
			{
				result.Append(' ');
			}

			result.Append(c);
			pendingSpace = false;
		}

		TrimTrailingSpace(result);
		return result.ToString();
	}

	private string ParseDishExpression(string text, ref int index)
	{
		List<string> items = new List<string>();
		while (index < text.Length)
		{
			SkipDishSpaces(text, ref index);
			if (index >= text.Length || text[index] == ')')
			{
				break;
			}

			string item = ParseDishItem(text, ref index);
			if (!string.IsNullOrEmpty(item))
			{
				items.Add(item);
			}

			SkipDishSpaces(text, ref index);
			if (index < text.Length && text[index] == ',')
			{
				index++;
				continue;
			}

			if (index >= text.Length || text[index] == ')')
			{
				break;
			}
		}

		items.Sort(StringComparer.Ordinal);
		return string.Join(",", items);
	}

	private string ParseDishItem(string text, ref int index)
	{
		SkipDishSpaces(text, ref index);
		if (index >= text.Length)
		{
			return "";
		}

		if (text[index] != '(')
		{
			return ReadDishTextUntilSeparator(text, ref index);
		}

		index++;
		string inner = ParseDishExpression(text, ref index);
		if (index < text.Length && text[index] == ')')
		{
			index++;
		}

		string suffix = ReadDishTextUntilSeparator(text, ref index);
		return string.IsNullOrEmpty(suffix)
			? "(" + inner + ")"
			: "(" + inner + ") " + suffix;
	}

	private string ReadDishTextUntilSeparator(string text, ref int index)
	{
		StringBuilder result = new StringBuilder();
		while (index < text.Length && text[index] != ',' && text[index] != ')')
		{
			result.Append(text[index]);
			index++;
		}

		TrimTrailingSpace(result);
		return result.ToString().Trim();
	}

	private void SkipDishSpaces(string text, ref int index)
	{
		while (index < text.Length && char.IsWhiteSpace(text[index]))
		{
			index++;
		}
	}

	private void TrimTrailingSpace(StringBuilder builder)
	{
		while (builder.Length > 0 && char.IsWhiteSpace(builder[builder.Length - 1]))
		{
			builder.Length--;
		}
	}

	private string WrapLevelOrderText(string text, int maxLineLength)
	{
			if (string.IsNullOrWhiteSpace(text))
			{
				return GameText("Gericht", "Dish");
			}

		string[] words = text.Trim().Split(' ');
		StringBuilder result = new StringBuilder();
		string line = "";
		for (int i = 0; i < words.Length; i++)
		{
			string word = words[i];
			if (string.IsNullOrEmpty(word))
			{
				continue;
			}

			string candidate = string.IsNullOrEmpty(line) ? word : line + " " + word;
			if (candidate.Length > maxLineLength && !string.IsNullOrEmpty(line))
			{
				if (result.Length > 0)
				{
					result.Append('\n');
				}

				result.Append(line);
				line = word;
			}
			else
			{
				line = candidate;
			}
		}

		if (!string.IsNullOrEmpty(line))
		{
			if (result.Length > 0)
			{
				result.Append('\n');
			}

			result.Append(line);
		}

		return result.ToString();
	}

	private int GetLongestWrappedLevelOrderLineLength(string text, int maxLineLength)
	{
		string wrappedText = WrapLevelOrderText(text, maxLineLength);
		int longest = 0;
		int current = 0;
		for (int i = 0; i < wrappedText.Length; i++)
		{
			if (wrappedText[i] == '\n')
			{
				longest = Mathf.Max(longest, current);
				current = 0;
				continue;
			}

			current++;
		}

		return Mathf.Max(longest, current);
	}

	private string GetLevelOrderOverviewText(PetriNetLevelDefinition level)
	{
			if (level == null || level.orders == null || level.orders.Count <= 0)
			{
				return GameText("keine", "none");
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

				text.Append(GetLocalizedOrderDishText(order));
				text.Append(GameText("\n  Gefordert: ", "\n  Required: "));
				text.Append(GetLocalizedOrderRequiredTokenText(order));
				string recipeText = GetLocalizedOrderRecipeText(order);
				if (!string.IsNullOrEmpty(recipeText))
				{
					text.Append(GameText("\n  Rezept: ", "\n  Recipe: "));
					text.Append(recipeText);
				}

		}

			return text.Length <= 0 ? GameText("keine", "none") : text.ToString();
	}

	private string GetOrderRequiredTokenText(PetriNetLevelOrderDefinition order)
	{
		if (order == null)
		{
			return "";
		}

		string requiredTokenText = order.requiredTokenText != null ? order.requiredTokenText.Trim() : "";
		if (!string.IsNullOrEmpty(requiredTokenText))
		{
			return requiredTokenText;
		}

		return order.dishText != null ? order.dishText.Trim() : "";
	}

	private string GetOrderRecipeText(PetriNetLevelOrderDefinition order)
	{
		if (order == null || string.IsNullOrWhiteSpace(order.recipeText))
		{
			return "";
		}

		return order.recipeText.Trim();
	}

	private string FormatLevelOrderTime(float seconds)
	{
		int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
		int minutes = totalSeconds / 60;
		int restSeconds = totalSeconds % 60;
		return minutes + ":" + restSeconds.ToString("00");
	}
}
