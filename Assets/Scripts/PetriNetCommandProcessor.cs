using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
	private void RequestCreatePlace(Vector3 world)
	{
		ExecuteOrSendCommand(new CommandData { action = "CreatePlace", x = world.x, y = world.y });
	}

	private void RequestCreateHeldPlace(Vector3 world)
	{
		ExecuteOrSendCommand(new CommandData { action = "CreateHeldPlace", x = world.x, y = world.y });
	}

	private void RequestCreateTransition(Vector3 world)
	{
		Debug.Log("CreateTransition is disabled. Use shared pool transitions.");
	}

	private void RequestCreateArc(string fromId, string toId)
	{
		ExecuteOrSendCommand(new CommandData { action = "CreateArc", fromId = fromId, toId = toId, weight = 1 });
	}

	private void RequestDeleteNode(string nodeId)
	{
		ExecuteOrSendCommand(new CommandData { action = "DeleteNode", id = nodeId });
	}

	private void RequestDeleteArc(string arcId)
	{
		ExecuteOrSendCommand(new CommandData { action = "DeleteArc", id = arcId });
	}

	private void RequestReverseArc(string arcId)
	{
		ExecuteOrSendCommand(new CommandData { action = "ReverseArc", id = arcId });
	}

	private void RequestChangeTokens(string nodeId, int delta)
	{
		ExecuteOrSendCommand(new CommandData { action = "ChangeTokens", id = nodeId, amount = delta });
	}

	private void RequestFireTransition(string transitionId)
	{
		ExecuteOrSendCommand(new CommandData { action = "FireTransition", id = transitionId });
	}

	private void RequestClaimSharedTransition(string transitionId, Vector3 worldPosition)
	{
		ExecuteOrSendCommand(new CommandData { action = "ClaimSharedTransition", id = transitionId, x = worldPosition.x, y = worldPosition.y });
	}

	private void RequestMoveNode(string nodeId, Vector3 position)
	{
		ExecuteOrSendCommand(new CommandData { action = "MoveNode", id = nodeId, x = position.x, y = position.y });
	}

	private void RequestMoveCompositeBlock(string blockId, Vector3 centerPosition)
	{
		ExecuteOrSendCommand(new CommandData { action = "MoveCompositeBlock", id = blockId, x = centerPosition.x, y = centerPosition.y });
	}

	private void RequestClaimCompositeBlock(string blockId)
	{
		ExecuteOrSendCommand(new CommandData { action = "ClaimCompositeBlock", id = blockId });
	}

	private void RequestReturnCompositeBlock(string blockId)
	{
		ExecuteOrSendCommand(new CommandData { action = "ReturnCompositeBlock", id = blockId });
	}

	private void ExecuteOrSendCommand(CommandData cmd)
	{
		if (suppressNetworkSend)
		{
			return;
		}

		if (IsHostOrOffline())
		{
			bool changed = ApplyCommand(cmd, GetLocalActorClientId());
			if (changed)
			{
				BroadcastSnapshotToClients();
			}
			return;
		}

		SendCommandToHost(cmd);
	}

	private bool ApplyCommand(CommandData cmd, ulong actorClientId)
	{
		if (cmd == null || string.IsNullOrEmpty(cmd.action))
		{
			return false;
		}

		switch (cmd.action)
		{
			case "CreatePlace":
			{
				Vector2 clampedPosition = ClampPositionToPlayerZone(new Vector2(cmd.x, cmd.y), actorClientId);
				if (IsNewPlacePositionBlocked(new Vector3(clampedPosition.x, clampedPosition.y, 0f)))
				{
					return false;
				}

				string newId = GetNextPlaceId();
				CreatePlaceNode(newId, clampedPosition, 0, true, actorClientId, false, false);
				return true;
			}
			case "CreateHeldPlace":
			{
				if (IsPlaceOverSharedTransitionPool(new Vector3(cmd.x, cmd.y, 0f), null))
				{
					return false;
				}

				string newId = GetNextPlaceId();
				CreatePlaceNode(newId, new Vector2(cmd.x, cmd.y), 0, true, actorClientId, false, false);
				return true;
			}
			case "CreateTransition":
				return false;
			case "ReturnToLevelSelection":
				ReturnToLevelSelectionFromHost();
				return false;
			case "ConfirmLevelSelection":
				ConfirmLevelSelection(cmd.amount);
				return false;
			case "CreateArc":
			{
				if (!CanActorCreateArc(cmd.fromId, cmd.toId, actorClientId))
				{
					return false;
				}

				string newArcId = "A_" + arcCounter;
				arcCounter++;
				return CreateArcInternal(newArcId, cmd.fromId, cmd.toId, Mathf.Max(1, cmd.weight), true, actorClientId);
			}
			case "DeleteNode":
			{
				if (!nodesById.TryGetValue(cmd.id, out NodeRuntime node) || !CanActorEditNode(node, actorClientId))
				{
					return false;
				}

				if (!CanDeleteNode(node))
				{
					return false;
				}

				return RemoveNodeInternal(cmd.id);
			}
			case "DeleteArc":
			{
				if (!arcsById.TryGetValue(cmd.id, out ArcRuntime arc) || arc.ownerClientId != actorClientId)
				{
					return false;
				}

				if (arc.kind == ArcKind.Inhibitor || IsIngredientSourceArc(arc) || IsCompositeBlockInternalArc(arc) || IsPlayerExchangeArc(arc))
				{
					return false;
				}

				return RemoveArcInternal(cmd.id);
			}
			case "ReverseArc":
			{
				if (!arcsById.TryGetValue(cmd.id, out ArcRuntime arc))
				{
					return false;
				}

				if (!CanActorReverseArc(arc, actorClientId))
				{
					return false;
				}

				string oldFromId = arc.fromId;
				arc.fromId = arc.toId;
				arc.toId = oldFromId;
				UpdateAllArcVisuals();
				RefreshPetriNetVisuals();
				return true;
			}
			case "ChangeTokens":
			{
				if (!nodesById.TryGetValue(cmd.id, out NodeRuntime place) || place.type != NodeType.Place)
				{
					return false;
				}

				if (!CanActorEditNode(place, actorClientId))
				{
					return false;
				}

				if (place.processingDuration > 0f && cmd.amount > 0 && place.tokens > 0)
				{
					return false;
				}

				EnsureTypedTokenList(place);
				int previousTokens = place.tokens;
				if (cmd.amount > 0)
				{
					int tokensToAdd = place.processingDuration > 0f ? Mathf.Min(1, cmd.amount) : cmd.amount;
					for (int i = 0; i < tokensToAdd; i++)
					{
						AddTokenToPlace(place, CreateUntypedToken());
					}
				}
				else if (cmd.amount < 0)
				{
					int tokensToRemove = Mathf.Min(place.typedTokens.Count, -cmd.amount);
					for (int i = 0; i < tokensToRemove; i++)
					{
						place.typedTokens.RemoveAt(place.typedTokens.Count - 1);
					}

					place.tokens = place.typedTokens.Count;
				}

				HandlePlaceTokensChanged(place, previousTokens);
				RefreshPetriNetVisuals();
				return true;
			}
			case "FireTransition":
				return TryFireTransition(cmd.id, actorClientId);
			case "ClaimSharedTransition":
				return TryClaimSharedTransition(cmd.id, actorClientId, new Vector2(cmd.x, cmd.y));
			case "ClaimCompositeBlock":
				return TryClaimSharedCompositeBlock(cmd.id, actorClientId);
			case "MoveNode":
			{
				if (!nodesById.TryGetValue(cmd.id, out NodeRuntime node))
				{
					return false;
				}

				if (!CanActorEditNode(node, actorClientId))
				{
					return false;
				}

				if (!CanActorMoveNode(node, actorClientId))
				{
					return false;
				}

				Vector2 desired = new Vector2(cmd.x, cmd.y);
				if (TryReturnSharedTransitionToPool(node, actorClientId, desired))
				{
					UpdateAllArcVisuals();
					RefreshPetriNetVisuals();
					return true;
				}

				desired = ClampPositionToActorArea(desired, actorClientId, 0f);
				Vector3 desiredPosition = new Vector3(desired.x, desired.y, 0f);
				if (IsNodeMovePositionBlocked(node, desiredPosition))
				{
					return false;
				}

				node.transform.position = desiredPosition;
				// Placing a pool transition outside the pool makes it a regular owned transition
				if (node.isSharedPoolTransition && !node.isSharedPoolAvailable)
				{
					node.isSharedPoolTransition = false;
					node.ownerClientId = actorClientId;
				}
				UpdateAllArcVisuals();
				RefreshPetriNetVisuals();
				return true;
			}
			case "MoveCompositeBlock":
			{
				if (!CanActorPickupCompositeBlock(cmd.id, actorClientId))
				{
					return false;
				}

				Vector2 desired = ClampCompositeBlockCenterToActorArea(cmd.id, new Vector2(cmd.x, cmd.y), actorClientId);
				if (!MoveCompositeBlockInternal(cmd.id, desired))
				{
					return false;
				}

				SetCompositeBlockNodeHeight(cmd.id, GroundZ);
				SetCompositeBlockSharedPoolState(cmd.id, actorClientId, false, false);
				RefreshPetriNetVisuals();
				return true;
			}
			case "ReturnCompositeBlock":
				return TryReturnSharedCompositeBlockToPool(cmd.id, actorClientId);
			case "ReturnSharedTransition":
			{
				if (!nodesById.TryGetValue(cmd.id, out NodeRuntime node))
				{
					return false;
				}

				Vector2 dropPosition = new Vector2(cmd.x, cmd.y);
				bool result = TryReturnSharedTransitionToPool(node, actorClientId, dropPosition);
				if (result)
				{
					UpdateAllArcVisuals();
					RefreshPetriNetVisuals();
				}
				return result;
			}
			case "UpdateAvatar":
			{
				// Legacy command ignored; avatar sync now uses lightweight PetriAvatar messages.
				return false;
			}
			default:
				return false;
		}
	}

	private string GetNextPlaceId()
	{
		while (nodesById.ContainsKey("P_" + placeCounter))
		{
			placeCounter++;
		}

		string id = "P_" + placeCounter;
		placeCounter++;
		return id;
	}

	private string GetNextTransitionId()
	{
		while (nodesById.ContainsKey("T_" + transitionCounter))
		{
			transitionCounter++;
		}

		string id = "T_" + transitionCounter;
		transitionCounter++;
		return id;
	}

	private bool TryFireTransition(string transitionId, ulong actorClientId)
	{
		if (!nodesById.TryGetValue(transitionId, out NodeRuntime transition) || transition.type != NodeType.Transition)
		{
			return false;
		}

		if (transition.isSharedPoolTransition && transition.isSharedPoolAvailable)
		{
			return false;
		}

		if (!CanActorEditNode(transition, actorClientId))
		{
			return false;
		}

		if (!IsTransitionEnabled(transitionId))
		{
			return false;
		}

		List<ArcRuntime> inputArcs = new List<ArcRuntime>();
		List<ArcRuntime> outputArcs = new List<ArcRuntime>();
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.kind == ArcKind.Inhibitor)
			{
				continue;
			}

			if (arc.toId == transitionId)
			{
				inputArcs.Add(arc);
			}
			else if (arc.fromId == transitionId)
			{
				outputArcs.Add(arc);
			}
		}

		List<TokenRuntime> consumedTokens = new List<TokenRuntime>();
		List<NodeRuntime> touchedPlaces = new List<NodeRuntime>();
		List<int> previousTokenCounts = new List<int>();
		for (int i = 0; i < inputArcs.Count; i++)
		{
			ArcRuntime arc = inputArcs[i];
			if (!nodesById.TryGetValue(arc.fromId, out NodeRuntime place) || place.type != NodeType.Place)
			{
				continue;
			}

			if (!touchedPlaces.Contains(place))
			{
				touchedPlaces.Add(place);
				previousTokenCounts.Add(place.tokens);
			}

			for (int weightIndex = 0; weightIndex < arc.weight; weightIndex++)
			{
				consumedTokens.Add(TakeTokenFromPlace(place));
			}
		}

		for (int i = 0; i < outputArcs.Count; i++)
		{
			ArcRuntime arc = outputArcs[i];
			if (!nodesById.TryGetValue(arc.toId, out NodeRuntime place) || place.type != NodeType.Place)
			{
				continue;
			}

			if (!touchedPlaces.Contains(place))
			{
				touchedPlaces.Add(place);
				previousTokenCounts.Add(place.tokens);
			}

			for (int weightIndex = 0; weightIndex < arc.weight; weightIndex++)
			{
				AddTokenToPlace(place, CreateOutputTokenForTransition(transitionId, consumedTokens));
			}
		}

		for (int i = 0; i < touchedPlaces.Count; i++)
		{
			HandlePlaceTokensChanged(touchedPlaces[i], previousTokenCounts[i]);
		}

		if (IsDeliveryTransitionId(transitionId))
		{
			HandleDeliveredTokens(consumedTokens);
		}

		RefreshPetriNetVisuals();
		return true;
	}

	private bool TryClaimSharedTransition(string transitionId, ulong actorClientId, Vector2 desiredPosition)
	{
		if (!nodesById.TryGetValue(transitionId, out NodeRuntime transition) || transition.type != NodeType.Transition)
		{
			return false;
		}

		if (!transition.isSharedPoolTransition || !transition.isSharedPoolAvailable)
		{
			return false;
		}

		transition.isSharedPoolAvailable = false;
		transition.ownerClientId = actorClientId;

		Vector2 claimedPosition = ClampPositionToActorArea(desiredPosition, actorClientId, 0f);
		transition.transform.position = new Vector3(claimedPosition.x, claimedPosition.y, 0f);
		RefreshPetriNetVisuals();
		return true;
	}

	private bool IsTransitionEnabled(string transitionId)
	{
		if (!nodesById.TryGetValue(transitionId, out NodeRuntime transition) || transition.type != NodeType.Transition)
		{
			return false;
		}

		bool hasInputPlace = false;
		bool hasOutputPlace = false;
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.kind == ArcKind.Inhibitor)
			{
				if (arc.toId != transitionId)
				{
					continue;
				}

				if (!nodesById.TryGetValue(arc.fromId, out NodeRuntime inhibitorPlace) || inhibitorPlace.type != NodeType.Place)
				{
					continue;
				}

				EnsureTypedTokenList(inhibitorPlace);
				if (inhibitorPlace.tokens > 0)
				{
					return false;
				}

				continue;
			}

			if (arc.fromId == transitionId && nodesById.TryGetValue(arc.toId, out NodeRuntime outputPlace) && outputPlace.type == NodeType.Place)
			{
				EnsureTypedTokenList(outputPlace);
				hasOutputPlace = true;
				if (outputPlace.processingDuration > 0f && outputPlace.tokens + Mathf.Max(1, arc.weight) > 1)
				{
					return false;
				}
			}

			if (arc.toId != transitionId)
			{
				continue;
			}

			if (!nodesById.TryGetValue(arc.fromId, out NodeRuntime place) || place.type != NodeType.Place)
			{
				continue;
			}

			EnsureTypedTokenList(place);
			hasInputPlace = true;
			if (place.tokens < arc.weight)
			{
				return false;
			}

			if (IsTimedPlaceProcessing(place))
			{
				return false;
			}
		}

		if (!IsIngredientTransition(transition) && !hasInputPlace)
		{
			return false;
		}

		if (!IsIngredientTransition(transition) && !IsDeliveryTransition(transition) && !IsSharedPoolTrashTransitionId(transition.id) && !hasOutputPlace)
		{
			return false;
		}

		return true;
	}

	private ulong GetLocalActorClientId()
	{
		if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return Unity.Netcode.NetworkManager.Singleton.LocalClientId;
		}

		return 0;
	}

	private bool CanActorEditNode(NodeRuntime node, ulong actorClientId)
	{
		if (node == null)
		{
			return false;
		}

		if (IsCompositeBlockNode(node))
		{
			string blockId = GetCompositeBlockIdForNodeId(node.id);
			if (IsCompositeBlockAvailableInSharedPool(blockId))
			{
				return false;
			}

			return GetCompositeBlockOwner(blockId) == actorClientId;
		}

		if (node.isSharedPoolTransition && node.isSharedPoolAvailable)
		{
			return false;
		}

		return node.ownerClientId == actorClientId;
	}

	private bool IsProtectedInputPlace(NodeRuntime node)
	{
		if (node == null || string.IsNullOrEmpty(node.id))
		{
			return false;
		}

		if (IsIngredientSourceNode(node) || IsDeliveryTransition(node) || IsCompositeBlockNode(node))
		{
			return true;
		}

		return node.type == NodeType.Place && (node.id == "P_Input" || node.id.EndsWith("_In"));
	}

	private bool CanDeleteNode(NodeRuntime node)
	{
		if (node == null)
		{
			return false;
		}

		if (IsProtectedInputPlace(node))
		{
			return false;
		}

		if (node.type == NodeType.Place && node.tokens > 0)
		{
			return false;
		}

		return true;
	}

	private bool CanActorMoveNode(NodeRuntime node, ulong actorClientId)
	{
		if (node == null || !CanActorEditNode(node, actorClientId))
		{
			return false;
		}

		if (IsDeliveryTransition(node) || IsCompositeBlockNode(node))
		{
			return false;
		}

		if (IsIngredientTransition(node))
		{
			return false;
		}

		return true;
	}

	private bool CanActorCreateArc(string fromId, string toId, ulong actorClientId)
	{
		if (!nodesById.TryGetValue(fromId, out NodeRuntime fromNode) || !nodesById.TryGetValue(toId, out NodeRuntime toNode))
		{
			return false;
		}

		if (fromNode.type == toNode.type)
		{
			return false;
		}

		if (!IsArcAllowedByIngredientRules(fromId, toId))
		{
			return false;
		}

		return CanActorEditNode(fromNode, actorClientId) && CanActorEditNode(toNode, actorClientId);
	}

	private bool CanActorReverseArc(ArcRuntime arc, ulong actorClientId)
	{
		if (arc == null || arc.kind == ArcKind.Inhibitor || arc.ownerClientId != actorClientId || IsIngredientSourceArc(arc) || IsCompositeBlockInternalArc(arc) || IsPlayerExchangeArc(arc))
		{
			return false;
		}

		string newFromId = arc.toId;
		string newToId = arc.fromId;
		if (!CanActorCreateArc(newFromId, newToId, actorClientId))
		{
			return false;
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime existing = pair.Value;
			if (existing.id != arc.id && existing.fromId == newFromId && existing.toId == newToId)
			{
				return false;
			}
		}

		return true;
	}

	private bool IsSharedTransitionAvailable(NodeRuntime node)
	{
		return node != null && node.type == NodeType.Transition && node.isSharedPoolTransition && node.isSharedPoolAvailable;
	}
}
