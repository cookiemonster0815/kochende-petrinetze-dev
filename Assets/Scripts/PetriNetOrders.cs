using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
	private List<PetriNetLevelOrderDefinition> levelOrderDefinitions = new List<PetriNetLevelOrderDefinition>();
	private float levelOrderStartTime = -1f;
	private Transform levelOrderDisplayRoot;
	private readonly List<GameObject> levelOrderCardObjects = new List<GameObject>();
	private readonly List<SpriteRenderer> levelOrderCardBackgrounds = new List<SpriteRenderer>();
	private readonly List<TextMesh> levelOrderCardTexts = new List<TextMesh>();
	private readonly HashSet<int> completedLevelOrderIndexes = new HashSet<int>();
	private readonly Dictionary<int, float> highlightedLevelOrderUntil = new Dictionary<int, float>();
	private const float LevelOrderHighlightSeconds = 1.25f;

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
			copy.Add(new PetriNetLevelOrderDefinition(order.dishText.Trim(), appearsAt, expiresAt));
		}

		return copy;
	}

	private void StartLevelOrderTimeline()
	{
		levelOrderStartTime = Time.time;
		completedLevelOrderIndexes.Clear();
		highlightedLevelOrderUntil.Clear();
		ClearLevelOrderCards();
	}

	private void StopLevelOrderTimeline()
	{
		levelOrderStartTime = -1f;
		completedLevelOrderIndexes.Clear();
		highlightedLevelOrderUntil.Clear();
		ClearLevelOrderDisplay();
	}

	private void UpdateLevelOrderDisplay()
	{
		if (!gameplayInitialized || levelOrderStartTime < 0f || levelOrderDefinitions == null || levelOrderDefinitions.Count <= 0 || mainCamera == null)
		{
			ClearLevelOrderCards();
			return;
		}

		CleanupExpiredLevelOrderHighlights();
		float elapsed = Time.time - levelOrderStartTime;
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
				&& elapsed >= order.appearsAtSeconds
				&& elapsed < order.expiresAtSeconds;
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

		EnsureGraphRootExists();
		levelOrderDisplayRoot = new GameObject("LevelOrderDisplay").transform;
		levelOrderDisplayRoot.SetParent(petriNetRoot, false);
	}

	private void EnsureLevelOrderCard(int index)
	{
		while (levelOrderCardObjects.Count <= index)
		{
			GameObject card = new GameObject("OrderCard_" + (levelOrderCardObjects.Count + 1));
			card.transform.SetParent(levelOrderDisplayRoot, false);

			GameObject backgroundObject = new GameObject("Background");
			backgroundObject.transform.SetParent(card.transform, false);
			SpriteRenderer background = backgroundObject.AddComponent<SpriteRenderer>();
			background.sprite = GetSquareSprite();
			background.color = new Color(1f, 1f, 1f, 0.94f);
			background.sortingOrder = 92;

			GameObject textObject = new GameObject("Label");
			textObject.transform.SetParent(card.transform, false);
			textObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
			TextMesh text = textObject.AddComponent<TextMesh>();
			text.anchor = TextAnchor.MiddleCenter;
			text.alignment = TextAlignment.Center;
			text.fontSize = 64;
			text.color = Color.black;
			SetTextSortingOrder(text, 94);

			levelOrderCardObjects.Add(card);
			levelOrderCardBackgrounds.Add(background);
			levelOrderCardTexts.Add(text);
		}
	}

	private void UpdateLevelOrderCard(int index, int orderIndex, PetriNetLevelOrderDefinition order, float elapsed, int activeCount)
	{
		float zoomScale = GetLevelOrderZoomScale();
		float screenHeight = mainCamera.orthographicSize * 2f;
		float screenWidth = screenHeight * mainCamera.aspect;
		float margin = 0.28f * zoomScale;
		float gap = 0.16f * zoomScale;
		float cardHeight = 0.78f * zoomScale;
		float availableWidth = screenWidth - margin * 2f - gap * Mathf.Max(0, activeCount - 1);
		float cardWidth = Mathf.Clamp(availableWidth / Mathf.Max(1, activeCount), 1.65f * zoomScale, 2.9f * zoomScale);
		Vector2 cameraGroundCenter = GetCameraGroundCenter();
		float leftX = cameraGroundCenter.x - screenWidth * 0.5f + margin + cardWidth * 0.5f;
		float topY = cameraGroundCenter.y + mainCamera.orthographicSize - 0.5f * zoomScale;
		bool highlighted = highlightedLevelOrderUntil.ContainsKey(orderIndex);

		GameObject card = levelOrderCardObjects[index];
		card.transform.position = new Vector3(leftX + index * (cardWidth + gap), topY, -0.2f);

		if (levelOrderCardBackgrounds[index] != null)
		{
			levelOrderCardBackgrounds[index].transform.localScale = new Vector3(cardWidth, cardHeight, 1f);
			levelOrderCardBackgrounds[index].color = highlighted
				? new Color(0.48f, 0.92f, 0.54f, 0.96f)
				: new Color(1f, 1f, 1f, 0.94f);
		}

		TextMesh text = levelOrderCardTexts[index];
		if (text != null)
		{
			int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(order.expiresAtSeconds - elapsed));
			text.text = highlighted
				? WrapLevelOrderText(order.dishText, 30) + "\nOK"
				: WrapLevelOrderText(order.dishText, 30) + "\n" + remainingSeconds + "s";
			text.characterSize = 0.052f * zoomScale;
			FitLevelOrderCardText(text, new Vector2(cardWidth, cardHeight));
		}
	}

	private float GetLevelOrderZoomScale()
	{
		if (mainCamera == null)
		{
			return 1f;
		}

		return Mathf.Max(0.1f, mainCamera.orthographicSize / Mathf.Max(0.1f, GetLevelOrderReferenceOrthographicSize()));
	}

	private float GetLevelOrderReferenceOrthographicSize()
	{
		return enableSharedTransitionPool ? GetSharedScreenCameraSize() : minZoom;
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

	private void FitLevelOrderCardText(TextMesh text, Vector2 cardSize)
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

		float allowedWidth = cardSize.x * 0.88f;
		float allowedHeight = cardSize.y * 0.78f;
		for (int i = 0; i < 8; i++)
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

			float minCharacterSize = 0.026f * GetLevelOrderZoomScale();
			text.characterSize = Mathf.Max(minCharacterSize, text.characterSize * scale * 0.94f);
		}
	}

	private void HandleDeliveredTokens(List<TokenRuntime> deliveredTokens)
	{
		if (levelOrderStartTime < 0f || deliveredTokens == null || deliveredTokens.Count <= 0)
		{
			return;
		}

		float elapsed = Time.time - levelOrderStartTime;
		List<string> deliveredCandidates = BuildDeliveredDishCandidates(deliveredTokens);
		for (int i = 0; i < deliveredCandidates.Count; i++)
		{
			int matchingOrderIndex = FindMatchingActiveOrderIndex(deliveredCandidates[i], elapsed);
			if (matchingOrderIndex >= 0)
			{
				MarkLevelOrderDelivered(matchingOrderIndex);
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

			if (elapsed < order.appearsAtSeconds || elapsed >= order.expiresAtSeconds)
			{
				continue;
			}

			if (NormalizeDishText(order.dishText) == normalizedDeliveredDish)
			{
				return i;
			}
		}

		return -1;
	}

	private void MarkLevelOrderDelivered(int orderIndex)
	{
		if (orderIndex < 0)
		{
			return;
		}

		completedLevelOrderIndexes.Add(orderIndex);
		highlightedLevelOrderUntil[orderIndex] = Time.time + LevelOrderHighlightSeconds;
	}

	private List<int> GetCompletedLevelOrderIndexes()
	{
		return new List<int>(completedLevelOrderIndexes);
	}

	private void ApplyCompletedLevelOrderIndexes(List<int> completedIndexes)
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
				MarkLevelOrderDelivered(orderIndex);
			}
		}
	}

	private void CleanupExpiredLevelOrderHighlights()
	{
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

		string normalized = text.Trim().ToLowerInvariant();
		normalized = normalized.Replace("+", ",");
		normalized = normalized.Replace(" ,", ",");
		normalized = normalized.Replace(", ", ",");
		while (normalized.Contains("  "))
		{
			normalized = normalized.Replace("  ", " ");
		}

		return normalized;
	}

	private string WrapLevelOrderText(string text, int maxLineLength)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return "Gericht";
		}

		string[] words = text.Trim().Split(' ');
		string result = "";
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
				if (!string.IsNullOrEmpty(result))
				{
					result += "\n";
				}

				result += line;
				line = word;
			}
			else
			{
				line = candidate;
			}
		}

		if (!string.IsNullOrEmpty(line))
		{
			if (!string.IsNullOrEmpty(result))
			{
				result += "\n";
			}

			result += line;
		}

		return result;
	}

	private string GetLevelOrderOverviewText(PetriNetLevelDefinition level)
	{
		if (level == null || level.orders == null || level.orders.Count <= 0)
		{
			return "keine";
		}

		string text = "";
		for (int i = 0; i < level.orders.Count; i++)
		{
			PetriNetLevelOrderDefinition order = level.orders[i];
			if (order == null)
			{
				continue;
			}

			if (!string.IsNullOrEmpty(text))
			{
				text += "\n";
			}

			text += "- " + order.dishText + " (" + FormatLevelOrderTime(order.appearsAtSeconds)
				+ " bis " + FormatLevelOrderTime(order.expiresAtSeconds) + ")";
		}

		return string.IsNullOrEmpty(text) ? "keine" : text;
	}

	private string FormatLevelOrderTime(float seconds)
	{
		int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
		int minutes = totalSeconds / 60;
		int restSeconds = totalSeconds % 60;
		return minutes + ":" + restSeconds.ToString("00");
	}
}
