using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
	private const float AvatarActionPromptScreenMargin = 10f;
	private Texture2D avatarActionPromptBackgroundTexture;

	private void DrawAvatarActionPrompt()
	{
		Event currentEvent = Event.current;
		if (currentEvent != null && currentEvent.type != EventType.Repaint)
		{
			return;
		}

		if (!gameplayInitialized || forceLobbyStartScreen || IsGameplayMenuOpen())
		{
			return;
		}

		List<string> lines = new List<string>();
		CollectAvatarActionPromptLines(lines);
		if (lines.Count <= 0)
		{
			return;
		}

		float uiScale = Mathf.Clamp(Mathf.Min(Screen.width / 1600f, Screen.height / 900f), 0.78f, 1.35f);
		int fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.018f, 15f, 24f) * uiScale);
		GUIStyle textStyle = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleLeft,
			fontSize = fontSize,
			fontStyle = FontStyle.Bold,
			wordWrap = false,
			clipping = TextClipping.Overflow
		};
		SetStaticGuiTextColor(textStyle, new Color(0.05f, 0.06f, 0.07f, 1f));

		float paddingX = 11f * uiScale;
		float paddingY = 7f * uiScale;
		float textWidth = 0f;
		for (int i = 0; i < lines.Count; i++)
		{
			textWidth = Mathf.Max(textWidth, textStyle.CalcSize(new GUIContent(lines[i])).x);
		}

		float lineHeight = Mathf.Max(fontSize + 4f * uiScale, textStyle.lineHeight);
		float width = Mathf.Max(96f * uiScale, textWidth + paddingX * 2f);
		float height = lineHeight * lines.Count + paddingY * 2f;
		float margin = AvatarActionPromptScreenMargin * uiScale;
		float guiX = margin;
		float guiY = Mathf.Max(margin, Screen.height - height - margin);
		if (TryGetTutorialScreenFallbackPanelRect(out Rect tutorialPanel))
		{
			guiX = tutorialPanel.x;
			guiY = Mathf.Max(margin, tutorialPanel.y - height - 6f * uiScale);
		}

		Rect panel = new Rect(guiX, guiY, width, height);
		Color previousColor = GUI.color;
		Color previousBackgroundColor = GUI.backgroundColor;
		GUI.color = Color.white;
		GUI.DrawTexture(panel, GetAvatarActionPromptBackgroundTexture());
		DrawAvatarActionPromptBorder(panel, new Color(0.05f, 0.07f, 0.09f, 0.45f), Mathf.Max(1f, 2f * uiScale));
		GUI.color = previousColor;

		float lineY = panel.y + paddingY;
		for (int i = 0; i < lines.Count; i++)
		{
			GUI.Label(new Rect(panel.x + paddingX, lineY, panel.width - paddingX * 2f, lineHeight), lines[i], textStyle);
			lineY += lineHeight;
		}

		GUI.backgroundColor = previousBackgroundColor;
	}

	private Texture2D GetAvatarActionPromptBackgroundTexture()
	{
		if (avatarActionPromptBackgroundTexture != null)
		{
			return avatarActionPromptBackgroundTexture;
		}

		avatarActionPromptBackgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
		{
			hideFlags = HideFlags.HideAndDontSave,
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp
		};
		avatarActionPromptBackgroundTexture.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.48f));
		avatarActionPromptBackgroundTexture.Apply();
		return avatarActionPromptBackgroundTexture;
	}

	private void DrawAvatarActionPromptBorder(Rect rect, Color color, float thickness)
	{
		Color previousColor = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
		GUI.color = previousColor;
	}

	private void CollectAvatarActionPromptLines(List<string> lines)
	{
		if (lines == null)
		{
			return;
		}

		if (CanShowAvatarFirePrompt())
		{
			lines.Add(GameText("F: feuern", "F: fire"));
		}

		if (CanShowAvatarDropPrompt())
		{
			lines.Add(GameText("Leertaste: absetzen", "Space: set down"));
		}
		else if (CanShowAvatarPickupPrompt())
		{
			lines.Add(GameText("Leertaste: hochheben", "Space: pick up"));
		}

		if (CanShowAvatarConnectionPrompt())
		{
			lines.Add(GameText("Q: Verbindung", "Q: connection"));
		}

		if (CanShowAvatarDeletePrompt())
		{
			lines.Add(GameText("R: löschen", "R: delete"));
		}

		if (CanShowAvatarStoragePrompt())
		{
			lines.Add(GameText("E: Lager", "E: storage"));
		}
	}

	private bool CanShowAvatarFirePrompt()
	{
		if (IsHoldingCraneObject() || !string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			return false;
		}

		return TryGetTransitionAtCraneTarget(out NodeRuntime transition)
			&& IsTransitionEnabled(transition.id);
	}

	private bool CanShowAvatarDropPrompt()
	{
		return IsHoldingCraneObject();
	}

	private bool CanShowAvatarPickupPrompt()
	{
		if (IsHoldingCraneObject() || !string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			return false;
		}

		if (TryGetCompositeBlockAtCraneTarget(out CompositeBlockRuntime targetBlock) && targetBlock != null)
		{
			return true;
		}

		if (TryGetPlaceAtCraneTarget(out NodeRuntime targetPlace) && targetPlace != null)
		{
			return true;
		}

		if (TryGetPickupTransitionAtCraneTargetForPrompt(out NodeRuntime targetTransition) && targetTransition != null)
		{
			return true;
		}

		return TryGetArcAtCraneTarget(out ArcRuntime arc)
			&& CanPickupArcAtCraneTargetForPrompt(arc);
	}

	private bool CanShowAvatarConnectionPrompt()
	{
		if (IsHoldingCraneObject())
		{
			return false;
		}

		if (!string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			return true;
		}

		if (TryGetNodeAtCraneTarget(out NodeRuntime node))
		{
			return CanUseNodeAsExternalConnectionEndpoint(node);
		}

		return TryGetArcAtCraneTarget(out ArcRuntime arc)
			&& CanActorReverseArc(arc, GetLocalActorClientId());
	}

	private bool CanShowAvatarDeletePrompt()
	{
		if (!string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			return true;
		}

		ulong actorClientId = GetLocalActorClientId();
		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return CanDeleteCreatedCompositeBlock(heldCompositeBlockId, actorClientId);
		}

		if (!string.IsNullOrEmpty(heldPlaceId) && nodesById.TryGetValue(heldPlaceId, out NodeRuntime heldPlace))
		{
			return CanActorEditNode(heldPlace, actorClientId) && CanDeleteNode(heldPlace);
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return false;
		}

		if (TryGetCompositeBlockAtCraneTarget(out CompositeBlockRuntime block))
		{
			return CanDeleteCreatedCompositeBlock(block.id, actorClientId);
		}

		if (TryGetPlaceAtCraneTarget(out NodeRuntime place))
		{
			return CanActorEditNode(place, actorClientId) && CanDeleteNode(place);
		}

		return TryGetArcAtCraneTarget(out ArcRuntime arc)
			&& CanActorSelectArc(arc, actorClientId);
	}

	private bool CanShowAvatarStoragePrompt()
	{
		return !pendingCreatedBlockPickup
			&& string.IsNullOrEmpty(heldCompositeBlockId)
			&& string.IsNullOrEmpty(heldTransitionId)
			&& string.IsNullOrEmpty(heldPlaceId)
			&& string.IsNullOrEmpty(craneConnectStartNodeId)
			&& !IsInsideSharedPoolZone(avatarPosition);
	}

	private bool TryGetPickupTransitionAtCraneTargetForPrompt(out NodeRuntime closestTransition)
	{
		closestTransition = null;
		float closestDistance = float.MaxValue;
		Vector2 craneTarget = GetCraneTarget2D();

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			if (node == null || node.type != NodeType.Transition || node.transform == null || !node.transform.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (IsIngredientTransition(node) || IsCompositeBlockNode(node))
			{
				continue;
			}

			bool isAvailablePoolTransition = node.isSharedPoolTransition && node.isSharedPoolAvailable;
			bool isOwnedPlacedTransition = !node.isSharedPoolTransition && node.ownerClientId == GetLocalActorClientId();
			if (!isAvailablePoolTransition && !isOwnedPlacedTransition)
			{
				continue;
			}

			Rect pickupBounds = ExpandRect(GetTransitionPlacementBounds(node, node.transform.position), avatarCollisionRadius + 0.2f);
			float distance = GetPointRectDistance(craneTarget, pickupBounds);
			if (distance <= 0f && distance < closestDistance)
			{
				closestDistance = distance;
				closestTransition = node;
			}
		}

		return closestTransition != null;
	}

	private bool CanPickupArcAtCraneTargetForPrompt(ArcRuntime arc)
	{
		if (arc == null || !TryGetArcSegment(arc, out Vector3 start, out Vector3 end))
		{
			return false;
		}

		Vector2 craneTarget = GetCraneTarget2D();
		float distanceToStart = Vector2.Distance(craneTarget, new Vector2(start.x, start.y));
		float distanceToEnd = Vector2.Distance(craneTarget, new Vector2(end.x, end.y));
		string fixedNodeId = distanceToStart <= distanceToEnd ? arc.toId : arc.fromId;

		return nodesById.TryGetValue(fixedNodeId, out NodeRuntime fixedNode)
			&& CanUseNodeAsExternalConnectionEndpoint(fixedNode)
			&& CanActorEditNode(fixedNode, GetLocalActorClientId());
	}
}
