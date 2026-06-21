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
		networkHandlersRegistered = true;

		if (Unity.Netcode.NetworkManager.Singleton.IsHost)
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

		if (!string.IsNullOrEmpty(draggedNodeId) || !string.IsNullOrEmpty(pendingClaimedTransitionId) || hasPoolTransitions)
		{
			for (int i = 0; i < snapshot.nodes.Count; i++)
			{
				NodeState state = snapshot.nodes[i];
				if (state == null || string.IsNullOrEmpty(state.id))
				{
					continue;
				}

				if (nodesById.TryGetValue(state.id, out NodeRuntime node))
				{
					node.isSharedPoolAvailable = state.isSharedPoolAvailable;
					node.ownerClientId = (ulong)state.ownerClientId;
					node.tokens = state.tokens;
					// Only update position for nodes that don't belong to me or aren't being dragged
					// Nodes I own (or am dragging) keep their local position
					if (node.id != draggedNodeId && node.ownerClientId != GetLocalActorClientId())
					{
						node.transform.position = new Vector3(state.x, state.y, 0f);
					}
					
					// Clear pending claim flag if host confirmed this transition is ours
					if (node.id == pendingClaimedTransitionId && node.ownerClientId == GetLocalActorClientId())
					{
						pendingClaimedTransitionId = null;
					}
				}
			}
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
		gameplayInitialized = true;
		pendingClaimedTransitionId = null;
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
