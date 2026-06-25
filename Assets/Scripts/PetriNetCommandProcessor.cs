using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
	private void RequestCreatePlace(Vector3 world)
	{
		ExecuteOrSendCommand(new CommandData { action = "CreatePlace", x = world.x, y = world.y });
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
				string newId = GetNextPlaceId();
				Vector2 clampedPosition = ClampPositionToPlayerZone(new Vector2(cmd.x, cmd.y), actorClientId);
				CreatePlaceNode(newId, clampedPosition, 0, true, actorClientId, false, false);
				return true;
			}
			case "CreateTransition":
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

				return RemoveNodeInternal(cmd.id);
			}
			case "DeleteArc":
			{
				if (!arcsById.TryGetValue(cmd.id, out ArcRuntime arc) || arc.ownerClientId != actorClientId)
				{
					return false;
				}

				return RemoveArcInternal(cmd.id);
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

				place.tokens = Mathf.Max(0, place.tokens + cmd.amount);
				RefreshPetriNetVisuals();
				return true;
			}
			case "FireTransition":
				return TryFireTransition(cmd.id, actorClientId);
			case "ClaimSharedTransition":
				return TryClaimSharedTransition(cmd.id, actorClientId, new Vector2(cmd.x, cmd.y));
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

				Vector2 desired = new Vector2(cmd.x, cmd.y);
				if (TryReturnSharedTransitionToPool(node, actorClientId, desired))
				{
					UpdateAllArcVisuals();
					RefreshPetriNetVisuals();
					return true;
				}

				Vector2 targetPosition = desired;
				if (node.type == NodeType.Place)
				{
					targetPosition = ClampPositionToPlayerZone(desired, actorClientId);
				}

				node.transform.position = new Vector3(targetPosition.x, targetPosition.y, 0f);
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
				// Avatar position syncing disabled - remote display is off.
				// Return false so no snapshot broadcast is triggered.
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

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.toId == transitionId && nodesById.TryGetValue(arc.fromId, out NodeRuntime place) && place.type == NodeType.Place)
			{
				place.tokens -= arc.weight;
			}
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.fromId == transitionId && nodesById.TryGetValue(arc.toId, out NodeRuntime place) && place.type == NodeType.Place)
			{
				place.tokens += arc.weight;
			}
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

		Vector2 claimedPosition = desiredPosition;
		transition.transform.position = new Vector3(claimedPosition.x, claimedPosition.y, 0f);
		RefreshPetriNetVisuals();
		return true;
	}

	private bool IsTransitionEnabled(string transitionId)
	{
		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			if (arc.toId != transitionId)
			{
				continue;
			}

			if (!nodesById.TryGetValue(arc.fromId, out NodeRuntime place) || place.type != NodeType.Place)
			{
				continue;
			}

			if (place.tokens < arc.weight)
			{
				return false;
			}
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

		if (node.isSharedPoolTransition && node.isSharedPoolAvailable)
		{
			return false;
		}

		return node.ownerClientId == actorClientId;
	}

	private bool CanActorCreateArc(string fromId, string toId, ulong actorClientId)
	{
		if (!nodesById.TryGetValue(fromId, out NodeRuntime fromNode) || !nodesById.TryGetValue(toId, out NodeRuntime toNode))
		{
			return false;
		}

		return CanActorEditNode(fromNode, actorClientId) && CanActorEditNode(toNode, actorClientId);
	}

	private bool IsSharedTransitionAvailable(NodeRuntime node)
	{
		return node != null && node.type == NodeType.Transition && node.isSharedPoolTransition && node.isSharedPoolAvailable;
	}
}
