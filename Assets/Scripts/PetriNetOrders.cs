using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
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
	private readonly HashSet<int> completedLevelOrderIndexes = new HashSet<int>();
	private readonly HashSet<int> lateCompletedLevelOrderIndexes = new HashSet<int>();
	private readonly Dictionary<int, float> completedLevelOrderDeliveredAtSeconds = new Dictionary<int, float>();
	private readonly Dictionary<int, float> highlightedLevelOrderUntil = new Dictionary<int, float>();
	private const float LevelOrderHighlightSeconds = 1.25f;
	private const float LevelOrderCardMargin = 24f;
	private const float LevelOrderCardGap = 16f;
	private const float LevelOrderCardHeight = 108f;
	private const float LevelOrderCardMinWidth = 240f;
	private const float LevelOrderCardMaxWidth = 420f;
	private const float LevelOrderCardPadding = 10f;
	private const int LevelOrderCanvasSortingOrder = 5000;
	private const int LevelOrderTextFontSize = 24;
	private const int LevelOrderMinTextFontSize = 15;
	private const int LevelOrderTextWrapLength = 28;
	private static readonly Color LevelOrderDefaultCardColor = new Color(1f, 1f, 1f, 0.94f);
	private static readonly Color LevelOrderHighlightedCardColor = new Color(0.48f, 0.92f, 0.54f, 0.96f);
	private static readonly Color LevelOrderLateHighlightedCardColor = new Color(1f, 0.88f, 0.24f, 0.96f);

	private void SetLevelOrders(List<PetriNetLevelOrderDefinition> orders)
	{
		levelOrderDefinitions = CopyLevelOrders(orders);
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
			float expiresAt = Mathf.Max(appearsAt + 1f, order.expiresAtSeconds);
			int amount = Mathf.Max(1, order.amount);
			for (int copyIndex = 0; copyIndex < amount; copyIndex++)
			{
				copy.Add(new PetriNetLevelOrderDefinition(order.dishText.Trim(), GetOrderRequiredTokenText(order), appearsAt, expiresAt));
			}
		}

		return copy;
	}

	private void StartLevelOrderTimeline()
	{
		levelOrderStartTime = Time.time;
		levelOrderPauseStartedTime = -1f;
		completedLevelOrderIndexes.Clear();
		lateCompletedLevelOrderIndexes.Clear();
		completedLevelOrderDeliveredAtSeconds.Clear();
		highlightedLevelOrderUntil.Clear();
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		levelEnded = false;
		levelResultScrollPosition = Vector2.zero;
		ClearLevelOrderCards();
	}

	private void StopLevelOrderTimeline()
	{
		levelOrderStartTime = -1f;
		levelOrderPauseStartedTime = -1f;
		completedLevelOrderIndexes.Clear();
		lateCompletedLevelOrderIndexes.Clear();
		completedLevelOrderDeliveredAtSeconds.Clear();
		highlightedLevelOrderUntil.Clear();
		gameplayMenuOpen = false;
		gameplayMenuOwnerClientId = NoGameplayMenuOwnerClientId;
		ClearLevelOrderDisplay();
	}

	private void UpdateLevelOrderDisplay()
	{
		if (!gameplayInitialized || levelOrderStartTime < 0f || levelOrderDefinitions == null || levelOrderDefinitions.Count <= 0)
		{
			ClearLevelOrderCards();
			return;
		}

		CleanupExpiredLevelOrderHighlights();
		float elapsed = GetLevelOrderElapsedTime();
		List<int> activeOrderIndexes = GetVisibleLevelOrderIndexes(elapsed);
		if (activeOrderIndexes.Count <= 0)
		{
			ClearLevelOrderCards();
			return;
		}

		EnsureLevelOrderDisplayRoot();
		TrimLevelOrderCards(activeOrderIndexes.Count);
		for (int i = 0; i < activeOrderIndexes.Count; i++)
		{
			EnsureLevelOrderCard(i);
			int orderIndex = activeOrderIndexes[i];
			UpdateLevelOrderCard(i, orderIndex, levelOrderDefinitions[orderIndex], elapsed, activeOrderIndexes.Count);
		}
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

	private void EnsureLevelOrderDisplayRoot()
	{
		if (levelOrderDisplayRoot != null)
		{
			return;
		}

		GameObject canvasObject = new GameObject("LevelOrderDisplay", typeof(RectTransform));
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
			text.resizeTextForBestFit = true;
			text.resizeTextMinSize = LevelOrderMinTextFontSize;
			text.resizeTextMaxSize = LevelOrderTextFontSize;
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			text.verticalOverflow = VerticalWrapMode.Truncate;
			text.supportRichText = false;
			text.raycastTarget = false;

			levelOrderCardObjects.Add(card);
			levelOrderCardBackgrounds.Add(background);
			levelOrderCardTexts.Add(text);
		}
	}

	private void UpdateLevelOrderCard(int index, int orderIndex, PetriNetLevelOrderDefinition order, float elapsed, int activeCount)
	{
		float uiScale = GetGameplayMenuUiScale();
		float screenWidth = Mathf.Max(1f, Screen.width);
		float margin = LevelOrderCardMargin * uiScale;
		float gap = LevelOrderCardGap * uiScale;
		float cardHeight = LevelOrderCardHeight * uiScale;
		float availableWidth = screenWidth - margin * 2f - gap * Mathf.Max(0, activeCount - 1);
		float cardWidth = Mathf.Clamp(
			availableWidth / Mathf.Max(1, activeCount),
			LevelOrderCardMinWidth * uiScale,
			LevelOrderCardMaxWidth * uiScale);
		bool highlighted = highlightedLevelOrderUntil.ContainsKey(orderIndex);

		GameObject card = levelOrderCardObjects[index];
		RectTransform cardRect = card.transform as RectTransform;
		if (cardRect != null)
		{
			cardRect.anchoredPosition = new Vector2(margin + index * (cardWidth + gap), -margin);
			cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
		}

		if (levelOrderCardBackgrounds[index] != null)
		{
			levelOrderCardBackgrounds[index].color = highlighted
				? GetLevelOrderHighlightedCardColor(orderIndex)
				: LevelOrderDefaultCardColor;
		}

		Text text = levelOrderCardTexts[index];
		if (text != null)
		{
			RectTransform textRect = text.transform as RectTransform;
			if (textRect != null)
			{
				float padding = LevelOrderCardPadding * uiScale;
				textRect.offsetMin = new Vector2(padding, padding);
				textRect.offsetMax = new Vector2(-padding, -padding);
			}

			int textMaxSize = Mathf.RoundToInt(LevelOrderTextFontSize * uiScale);
			int textMinSize = Mathf.RoundToInt(LevelOrderMinTextFontSize * uiScale);
			int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(order.expiresAtSeconds - elapsed));
			string wrappedDish = WrapLevelOrderText(order.dishText, LevelOrderTextWrapLength);
			text.text = highlighted
				? wrappedDish + "\nOK"
				: wrappedDish + "\n" + remainingSeconds + "s";
			text.fontSize = textMaxSize;
			text.resizeTextMinSize = textMinSize;
			text.resizeTextMaxSize = textMaxSize;
		}
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

		for (int i = 0; i < levelOrderDefinitions.Count; i++)
		{
			PetriNetLevelOrderDefinition order = levelOrderDefinitions[i];
			if (order == null || completedLevelOrderIndexes.Contains(i))
			{
				continue;
			}

			if (elapsed < order.appearsAtSeconds)
			{
				continue;
			}

			if (NormalizeDishText(GetOrderRequiredTokenText(order)) == normalizedDeliveredDish)
			{
				return i;
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
		if (IsLevelOrderLate(orderIndex, deliveredElapsed))
		{
			lateCompletedLevelOrderIndexes.Add(orderIndex);
		}
		else
		{
			lateCompletedLevelOrderIndexes.Remove(orderIndex);
		}

		highlightedLevelOrderUntil[orderIndex] = Time.time + LevelOrderHighlightSeconds;
	}

	private Color GetLevelOrderHighlightedCardColor(int orderIndex)
	{
		return lateCompletedLevelOrderIndexes.Contains(orderIndex)
			? LevelOrderLateHighlightedCardColor
			: LevelOrderHighlightedCardColor;
	}

	private bool IsLevelOrderLate(int orderIndex, float elapsed)
	{
		return levelOrderDefinitions != null
			&& orderIndex >= 0
			&& orderIndex < levelOrderDefinitions.Count
			&& levelOrderDefinitions[orderIndex] != null
			&& elapsed >= levelOrderDefinitions[orderIndex].expiresAtSeconds;
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
				if (IsLevelOrderLate(orderIndex, deliveredAtSeconds))
				{
					lateCompletedLevelOrderIndexes.Add(orderIndex);
				}
				else
				{
					lateCompletedLevelOrderIndexes.Remove(orderIndex);
				}
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
		int score = GetOnTimeLevelOrderScore();
		StringBuilder text = new StringBuilder();
		text.Append("Punkte: ");
		text.Append(score);
		text.Append(" / ");
		text.Append(orderCount);

		if (orderCount <= 0)
		{
			text.Append("\n\nKeine Rezepte in diesem Level.");
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
			text.Append(order.dishText);
			text.Append("\nGefordert: ");
			text.Append(GetOrderRequiredTokenText(order));
			text.Append("\n");

			if (!completedLevelOrderIndexes.Contains(i))
			{
				text.Append("Nicht abgearbeitet");
				continue;
			}

			float deliveredAtSeconds = completedLevelOrderDeliveredAtSeconds.TryGetValue(i, out float storedDeliveryTime)
				? storedDeliveryTime
				: 0f;
			bool late = IsLevelOrderLate(i, deliveredAtSeconds);
			text.Append(late ? "Zu spät" : "Rechtzeitig");
			text.Append(" um ");
			text.Append(FormatLevelOrderTime(deliveredAtSeconds));
			text.Append(late ? " (+0)" : " (+1)");
		}

		return text.ToString();
	}

	private int GetLevelOrderCount()
	{
		return levelOrderDefinitions != null ? levelOrderDefinitions.Count : 0;
	}

	private int GetOnTimeLevelOrderScore()
	{
		int score = 0;
		int orderCount = GetLevelOrderCount();
		foreach (int orderIndex in completedLevelOrderIndexes)
		{
			if (orderIndex >= 0 && orderIndex < orderCount && !lateCompletedLevelOrderIndexes.Contains(orderIndex))
			{
				score++;
			}
		}

		return score;
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
			return "Gericht";
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

	private string GetLevelOrderOverviewText(PetriNetLevelDefinition level)
	{
		if (level == null || level.orders == null || level.orders.Count <= 0)
		{
			return "keine";
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

			text.Append(order.dishText);
			text.Append("\n  Gefordert: ");
			text.Append(GetOrderRequiredTokenText(order));
			text.Append(" (");
			text.Append(FormatLevelOrderTime(order.appearsAtSeconds));
			text.Append(" bis ");
			text.Append(FormatLevelOrderTime(order.expiresAtSeconds));
			text.Append(")");
		}

		return text.Length <= 0 ? "keine" : text.ToString();
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

	private string FormatLevelOrderTime(float seconds)
	{
		int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
		int minutes = totalSeconds / 60;
		int restSeconds = totalSeconds % 60;
		return minutes + ":" + restSeconds.ToString("00");
	}
}
