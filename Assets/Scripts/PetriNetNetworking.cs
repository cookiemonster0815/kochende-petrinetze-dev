using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class GameManager
{
	private void HandleNetworkHooks()
	{
		if (!enableNetworkAuthoritativeSync || networkHandlersRegistered)
		{
			return;
		}

		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return;
		}

		Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
		Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(CommandMessageName, OnCommandMessageReceived);
		Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessageName, OnSnapshotMessageReceived);
		Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(AvatarMessageName, OnAvatarMessageReceived);
		networkHandlersRegistered = true;

		if (Unity.Netcode.NetworkManager.Singleton.IsHost && nodesById.Count > 0)
		{
			BroadcastSnapshotToClients();
		}
	}

	private void UnregisterNetworkHandlers()
	{
		if (!networkHandlersRegistered || Unity.Netcode.NetworkManager.Singleton == null)
		{
			return;
		}

		Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
		if (Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager != null)
		{
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(CommandMessageName);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessageName);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(AvatarMessageName);
		}

		networkHandlersRegistered = false;
	}

	private void OnClientConnected(ulong clientId)
	{
		if (!enableNetworkAuthoritativeSync || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		if (clientId == NetworkManager.ServerClientId)
		{
			return;
		}

		if (enableSharedTransitionPool && !collaborativeLayoutApplied && Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
		{
			ulong otherClientId = clientId;
			BuildCollaborativeTwoPlayerLayout(NetworkManager.ServerClientId, otherClientId);
		}

		SeedRemoteAvatarStartPosition(clientId);
		SendSnapshotToClient(clientId);
		BroadcastSnapshotToClients();
	}

	private void OnCommandMessageReceived(ulong senderClientId, FastBufferReader reader)
	{
		if (!enableNetworkAuthoritativeSync || Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		CommandData cmd = ReadCommand(reader);
		if (cmd == null)
		{
			return;
		}

		suppressNetworkSend = true;
		bool changed = ApplyCommand(cmd, senderClientId);
		suppressNetworkSend = false;

		if (changed)
		{
			BroadcastSnapshotToClients();
		}
	}

	private void OnSnapshotMessageReceived(ulong senderClientId, FastBufferReader reader)
	{
		if (!enableNetworkAuthoritativeSync)
		{
			return;
		}

		if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		SnapshotData snapshot = ReadSnapshot(reader);
		if (snapshot == null)
		{
			return;
		}

		ApplySnapshot(snapshot);
	}

	private void OnAvatarMessageReceived(ulong senderClientId, FastBufferReader reader)
	{
		if (!enableNetworkAuthoritativeSync || Unity.Netcode.NetworkManager.Singleton == null)
		{
			return;
		}

		AvatarState state = ReadAvatarState(reader);
		if (state == null)
		{
			return;
		}

		if (Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			state.clientId = (long)senderClientId;
			StoreRemoteAvatarState(state);
			BroadcastAvatarState(state, senderClientId);
			return;
		}

		if ((ulong)state.clientId == GetLocalActorClientId())
		{
			return;
		}

		StoreRemoteAvatarState(state);
	}

	private void SendCommandToHost(CommandData cmd)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return;
		}

		string json = JsonUtility.ToJson(cmd);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		using (FastBufferWriter writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp))
		{
			writer.WriteValueSafe(bytes.Length);
			writer.WriteBytesSafe(bytes);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(CommandMessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
		}
	}

	private void SendSnapshotToClient(ulong clientId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		SnapshotData snapshot = BuildSnapshot();
		string json = JsonUtility.ToJson(snapshot);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		using (FastBufferWriter writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp))
		{
			writer.WriteValueSafe(bytes.Length);
			writer.WriteBytesSafe(bytes);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(SnapshotMessageName, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
		}
	}

	private void SendAvatarUpdate(Vector3 position, float rotation, string heldId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return;
		}

		AvatarState state = new AvatarState
		{
			clientId = (long)GetLocalActorClientId(),
			x = position.x,
			y = position.y,
			rotation = rotation,
			heldTransitionId = heldId ?? ""
		};

		if (Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			BroadcastAvatarState(state, GetLocalActorClientId());
			return;
		}

		SendAvatarStateToClient(NetworkManager.ServerClientId, state);
	}

	private void BroadcastAvatarState(AvatarState state, ulong exceptClientId)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		foreach (ulong clientId in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds)
		{
			if (clientId == exceptClientId || clientId == GetLocalActorClientId())
			{
				continue;
			}

			SendAvatarStateToClient(clientId, state);
		}
	}

	private void SendAvatarStateToClient(ulong clientId, AvatarState state)
	{
		string json = JsonUtility.ToJson(state);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		using (FastBufferWriter writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp))
		{
			writer.WriteValueSafe(bytes.Length);
			writer.WriteBytesSafe(bytes);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(AvatarMessageName, clientId, writer, NetworkDelivery.Unreliable);
		}
	}

	private void StoreRemoteAvatarState(AvatarState state)
	{
		ulong clientId = (ulong)state.clientId;
		if (clientId == GetLocalActorClientId())
		{
			return;
		}

		remoteAvatarPositions[clientId] = new Vector3(state.x, state.y, 0f);
		remoteAvatarRotations[clientId] = state.rotation;
		remoteAvatarInventories[clientId] = state.heldTransitionId ?? "";
	}

	private void BroadcastSnapshotToClients()
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		foreach (ulong clientId in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds)
		{
			if (clientId == NetworkManager.ServerClientId)
			{
				continue;
			}

			SendSnapshotToClient(clientId);
		}
	}

	private SnapshotData BuildSnapshot()
	{
		SnapshotData snapshot = new SnapshotData();

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			snapshot.nodes.Add(new NodeState
			{
				id = node.id,
				type = (int)node.type,
				tokens = node.tokens,
				x = node.transform.position.x,
				y = node.transform.position.y,
				ownerClientId = (long)node.ownerClientId,
				isSharedPoolTransition = node.isSharedPoolTransition,
				isSharedPoolAvailable = node.isSharedPoolAvailable,
			});
		}

		foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
		{
			ArcRuntime arc = pair.Value;
			snapshot.arcs.Add(new ArcState
			{
				id = arc.id,
				fromId = arc.fromId,
				toId = arc.toId,
				weight = arc.weight,
				ownerClientId = (long)arc.ownerClientId,
			});
		}

		// Add all avatar states
		snapshot.avatars.Add(new AvatarState
		{
			clientId = (long)GetLocalActorClientId(),
			x = avatarPosition.x,
			y = avatarPosition.y,
			rotation = avatarRotation,
			heldTransitionId = heldTransitionId ?? ""
		});

		// Add remote avatars (from other players)
		if (remoteAvatarPositions != null)
		{
			foreach (KeyValuePair<ulong, Vector3> pair in remoteAvatarPositions)
			{
				ulong clientId = pair.Key;
				Vector3 pos = pair.Value;
				float rotation = remoteAvatarRotations.ContainsKey(clientId) ? remoteAvatarRotations[clientId] : 0f;
				string heldId = remoteAvatarInventories.ContainsKey(clientId) ? remoteAvatarInventories[clientId] : "";
				
				snapshot.avatars.Add(new AvatarState
				{
					clientId = (long)clientId,
					x = pos.x,
					y = pos.y,
					rotation = rotation,
					heldTransitionId = heldId ?? ""
				});
			}
		}

		return snapshot;
	}

	private void ApplySnapshot(SnapshotData snapshot)
	{
		if (snapshot == null)
		{
			return;
		}

		ApplySharedScreenLayoutDefaults();
		suppressNetworkSend = true;

		// Process avatar states first
		if (snapshot.avatars != null && snapshot.avatars.Count > 0)
		{
			for (int i = 0; i < snapshot.avatars.Count; i++)
			{
				AvatarState state = snapshot.avatars[i];
				ulong clientId = (ulong)state.clientId;
				
				// Host/offline applies own avatar state directly.
				// Clients keep local predicted movement to avoid visible rubberbanding.
				if (clientId == GetLocalActorClientId())
				{
					if (IsHostOrOffline())
					{
						avatarPosition = new Vector3(state.x, state.y, 0f);
						avatarRotation = state.rotation;
						heldTransitionId = string.IsNullOrEmpty(state.heldTransitionId) ? null : state.heldTransitionId;
					}
				}
				else
				{
					// Store remote avatar position
					if (remoteAvatarPositions == null)
					{
						remoteAvatarPositions = new Dictionary<ulong, Vector3>();
						remoteAvatarRotations = new Dictionary<ulong, float>();
						remoteAvatarInventories = new Dictionary<ulong, string>();
					}
					remoteAvatarPositions[clientId] = new Vector3(state.x, state.y, 0f);
					remoteAvatarRotations[clientId] = state.rotation;
					remoteAvatarInventories[clientId] = state.heldTransitionId ?? "";
				}
			}
		}

		// Always do merge for pool transitions, never do destructive rebuild
		// This prevents "popping" behavior during networked pool operations
		bool hasPoolTransitions = false;
		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			if (pair.Value.isSharedPoolTransition)
			{
				hasPoolTransitions = true;
				break;
			}
		}

		if (!string.IsNullOrEmpty(draggedNodeId) || !string.IsNullOrEmpty(heldCompositeBlockId) || !string.IsNullOrEmpty(pendingClaimedTransitionId) || hasPoolTransitions)
		{
			int mergeMaxPlace = 0;
			int mergeMaxTransition = 0;
			int mergeMaxArc = 0;
			HashSet<string> snapshotNodeIds = new HashSet<string>();

			for (int i = 0; i < snapshot.nodes.Count; i++)
			{
				NodeState state = snapshot.nodes[i];
				if (state == null || string.IsNullOrEmpty(state.id))
				{
					continue;
				}

				snapshotNodeIds.Add(state.id);
				if ((NodeType)state.type == NodeType.Place)
				{
					mergeMaxPlace = Mathf.Max(mergeMaxPlace, ExtractTrailingNumber(state.id));
				}
				else
				{
					mergeMaxTransition = Mathf.Max(mergeMaxTransition, ExtractTrailingNumber(state.id));
				}

				if (nodesById.TryGetValue(state.id, out NodeRuntime node))
				{
					node.isSharedPoolTransition = state.isSharedPoolTransition;
					node.isSharedPoolAvailable = state.isSharedPoolAvailable;
					node.ownerClientId = (ulong)state.ownerClientId;
					node.tokens = state.tokens;
					// Only update position for nodes that don't belong to me or aren't being dragged
					// Nodes I own (or am dragging) keep their local position
					bool heldByLocal = node.id == heldTransitionId || node.id == heldPlaceId || IsHeldCompositeBlockNode(node);
					if (node.id != draggedNodeId && !heldByLocal && node.ownerClientId != GetLocalActorClientId())
					{
						node.transform.position = new Vector3(state.x, state.y, 0f);
					}

					// Clear pending claim flag if host confirmed this transition is ours
					if (node.id == pendingClaimedTransitionId && node.ownerClientId == GetLocalActorClientId())
					{
						pendingClaimedTransitionId = null;
					}
				}
				else if ((NodeType)state.type == NodeType.Place)
				{
					CreatePlaceNode(state.id, new Vector2(state.x, state.y), state.tokens, false, (ulong)state.ownerClientId, state.isSharedPoolTransition, state.isSharedPoolAvailable);
				}
				else
				{
					CreateTransitionNode(state.id, new Vector2(state.x, state.y), false, (ulong)state.ownerClientId, state.isSharedPoolTransition, state.isSharedPoolAvailable);
				}
			}

			List<string> nodeIdsToRemove = new List<string>();
			foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
			{
				if (!snapshotNodeIds.Contains(pair.Key))
				{
					nodeIdsToRemove.Add(pair.Key);
				}
			}

			for (int i = 0; i < nodeIdsToRemove.Count; i++)
			{
				string nodeId = nodeIdsToRemove[i];
				if (heldTransitionId == nodeId)
				{
					heldTransitionId = null;
				}

				if (heldPlaceId == nodeId)
				{
					heldPlaceId = null;
				}

				RemoveNodeInternal(nodeId);
			}

			HashSet<string> snapshotArcIds = new HashSet<string>();
			for (int i = 0; i < snapshot.arcs.Count; i++)
			{
				ArcState state = snapshot.arcs[i];
				if (state == null || string.IsNullOrEmpty(state.id))
				{
					continue;
				}

				snapshotArcIds.Add(state.id);
				mergeMaxArc = Mathf.Max(mergeMaxArc, ExtractTrailingNumber(state.id));
				if (arcsById.TryGetValue(state.id, out ArcRuntime arc))
				{
					arc.fromId = state.fromId;
					arc.toId = state.toId;
					arc.weight = Mathf.Max(1, state.weight);
					arc.ownerClientId = (ulong)state.ownerClientId;
					UpdateArcVisual(arc);
				}
				else
				{
					CreateArcInternal(state.id, state.fromId, state.toId, state.weight, false, (ulong)state.ownerClientId);
				}
			}

			List<string> arcIdsToRemove = new List<string>();
			foreach (KeyValuePair<string, ArcRuntime> pair in arcsById)
			{
				if (!snapshotArcIds.Contains(pair.Key))
				{
					arcIdsToRemove.Add(pair.Key);
				}
			}

			for (int i = 0; i < arcIdsToRemove.Count; i++)
			{
				RemoveArcInternal(arcIdsToRemove[i]);
			}

			placeCounter = Mathf.Max(placeCounter, mergeMaxPlace + 1);
			transitionCounter = Mathf.Max(transitionCounter, mergeMaxTransition + 1);
			arcCounter = Mathf.Max(arcCounter, mergeMaxArc + 1);

			TryAttachPendingCreatedPlace();
			EnsureLocalAvatarStartPosition();
			RefreshPetriNetVisuals();
			suppressNetworkSend = false;
			return;
		}

		ClearGraph();
		EnsureGraphRootExists();
		if (enableSharedTransitionPool)
		{
			RebuildSharedPoolVisual();
		}

		int maxPlace = 0;
		int maxTransition = 0;
		int maxArc = 0;

		for (int i = 0; i < snapshot.nodes.Count; i++)
		{
			NodeState state = snapshot.nodes[i];
			if (state == null || string.IsNullOrEmpty(state.id))
			{
				continue;
			}

			if ((NodeType)state.type == NodeType.Place)
			{
				CreatePlaceNode(state.id, new Vector2(state.x, state.y), state.tokens, false, (ulong)state.ownerClientId, state.isSharedPoolTransition, state.isSharedPoolAvailable);
				maxPlace = Mathf.Max(maxPlace, ExtractTrailingNumber(state.id));
			}
			else
			{
				CreateTransitionNode(state.id, new Vector2(state.x, state.y), false, (ulong)state.ownerClientId, state.isSharedPoolTransition, state.isSharedPoolAvailable);
				maxTransition = Mathf.Max(maxTransition, ExtractTrailingNumber(state.id));
			}
		}

		for (int i = 0; i < snapshot.arcs.Count; i++)
		{
			ArcState state = snapshot.arcs[i];
			if (state == null || string.IsNullOrEmpty(state.id))
			{
				continue;
			}

			CreateArcInternal(state.id, state.fromId, state.toId, state.weight, false, (ulong)state.ownerClientId);
			maxArc = Mathf.Max(maxArc, ExtractTrailingNumber(state.id));
		}

		placeCounter = Mathf.Max(1, maxPlace + 1);
		transitionCounter = Mathf.Max(1, maxTransition + 1);
		arcCounter = Mathf.Max(1, maxArc + 1);

		RefreshPetriNetVisuals();
		EnsureLocalAvatarStartPosition();
		gameplayInitialized = true;
		pendingClaimedTransitionId = null;
		TryAttachPendingCreatedPlace();
		suppressNetworkSend = false;
	}

	private CommandData ReadCommand(FastBufferReader reader)
	{
		reader.ReadValueSafe(out int length);
		if (length <= 0)
		{
			return null;
		}

		byte[] bytes = new byte[length];
		reader.ReadBytesSafe(ref bytes, length);
		string json = Encoding.UTF8.GetString(bytes);
		return JsonUtility.FromJson<CommandData>(json);
	}

	private SnapshotData ReadSnapshot(FastBufferReader reader)
	{
		reader.ReadValueSafe(out int length);
		if (length <= 0)
		{
			return null;
		}

		byte[] bytes = new byte[length];
		reader.ReadBytesSafe(ref bytes, length);
		string json = Encoding.UTF8.GetString(bytes);
		return JsonUtility.FromJson<SnapshotData>(json);
	}

	private AvatarState ReadAvatarState(FastBufferReader reader)
	{
		reader.ReadValueSafe(out int length);
		if (length <= 0)
		{
			return null;
		}

		byte[] bytes = new byte[length];
		reader.ReadBytesSafe(ref bytes, length);
		string json = Encoding.UTF8.GetString(bytes);
		return JsonUtility.FromJson<AvatarState>(json);
	}

	private bool IsHostOrOffline()
	{
		if (!enableNetworkAuthoritativeSync)
		{
			return true;
		}

		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return true;
		}

		return Unity.Netcode.NetworkManager.Singleton.IsHost;
	}

	private string GetNetworkRoleLabel()
	{
		if (!enableNetworkAuthoritativeSync)
		{
			return "Offline (sync disabled)";
		}

		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return "Offline";
		}

		if (Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return "Host (authoritative)";
		}

		if (Unity.Netcode.NetworkManager.Singleton.IsClient)
		{
			return "Client";
		}

		return "Unknown";
	}

	private int ExtractTrailingNumber(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return 0;
		}

		int index = id.Length - 1;
		while (index >= 0 && char.IsDigit(id[index]))
		{
			index--;
		}

		if (index == id.Length - 1)
		{
			return 0;
		}

		string numberPart = id.Substring(index + 1);
		if (int.TryParse(numberPart, out int value))
		{
			return value;
		}

		return 0;
	}
}
