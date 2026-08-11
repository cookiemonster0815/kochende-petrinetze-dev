using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class GameManager
{
	private void HandleNetworkHooks()
	{
		if (singlePlayerMode || !enableNetworkAuthoritativeSync || networkHandlersRegistered)
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
		Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(LevelSelectionMessageName, OnLevelSelectionMessageReceived);
		networkHandlersRegistered = true;

		if (Unity.Netcode.NetworkManager.Singleton.IsHost && nodesById.Count > 0)
		{
			BroadcastSnapshotToClients();
		}
	}

	private void UnregisterNetworkHandlers()
	{
		if (!networkHandlersRegistered)
		{
			return;
		}

		if (Unity.Netcode.NetworkManager.Singleton == null)
		{
			networkHandlersRegistered = false;
			return;
		}

		Unity.Netcode.NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
		if (Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager != null)
		{
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(CommandMessageName);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessageName);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(AvatarMessageName);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(LevelSelectionMessageName);
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

		if (showLevelSelection && !gameplayInitialized)
		{
			EnsureLevelSelectionAvatarStartPosition();
			SeedRemoteAvatarLevelSelectionStartPosition(clientId, true);
			SendLevelSelectionStateToClient(clientId, new LevelSelectionState
			{
				showSelection = true,
				selectedLevelIndex = selectedLevelIndex
			});
			SendAvatarStateToClient(clientId, BuildLocalAvatarState(avatarPosition, avatarRotation, heldTransitionId), true);
			return;
		}

		if (enableSharedTransitionPool && gameplayInitialized && !collaborativeLayoutApplied && Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
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

		ApplyCommandAvatarState(cmd, senderClientId);
		suppressNetworkSend = true;
		bool changed = ApplyCommand(cmd, senderClientId);
		suppressNetworkSend = false;

		if (changed)
		{
			BroadcastSnapshotToClients();
		}
	}

	private void ApplyCommandAvatarState(CommandData cmd, ulong senderClientId)
	{
		if (cmd == null || !cmd.hasAvatarState)
		{
			return;
		}

		AvatarState state = new AvatarState
		{
			clientId = (long)senderClientId,
			x = cmd.avatarX,
			y = cmd.avatarY,
			rotation = cmd.avatarRotation,
			craneHeight = cmd.avatarCraneHeight,
			heldTransitionId = cmd.avatarHeldTransitionId ?? "",
			heldObjectId = cmd.avatarHeldObjectId ?? "",
			heldObjectKind = cmd.avatarHeldObjectKind,
			heldOffsetX = cmd.avatarHeldOffsetX,
			heldOffsetY = cmd.avatarHeldOffsetY,
			sceneMode = cmd.avatarSceneMode,
			connectStartNodeId = cmd.avatarConnectStartNodeId ?? "",
			connectReversed = cmd.avatarConnectReversed,
		};
		if (!ShouldAcceptIncomingAvatarState(state))
		{
			return;
		}

		StoreRemoteAvatarState(state);
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
			if (!ShouldAcceptIncomingAvatarState(state))
			{
				return;
			}

			RemoteHeldObjectState incomingHeldState = GetRemoteHeldObjectState(state);
			RemoteHeldObjectState previousHeldState = null;
			if (remoteAvatarInventories != null)
			{
				remoteAvatarInventories.TryGetValue(senderClientId, out previousHeldState);
			}

			RemoteCraneConnectState previousConnectState = null;
			if (remoteCraneConnectStates != null)
			{
				remoteCraneConnectStates.TryGetValue(senderClientId, out previousConnectState);
			}

			bool heldObjectChanged = GetHeldNetworkKey(previousHeldState) != GetHeldNetworkKey(incomingHeldState)
				|| GetCraneConnectNetworkKey(previousConnectState) != GetCraneConnectNetworkKey(state);
			StoreRemoteAvatarState(state);
			BroadcastAvatarState(state, senderClientId, heldObjectChanged);
			return;
		}

		if ((ulong)state.clientId == GetLocalActorClientId())
		{
			return;
		}

		if (!ShouldAcceptIncomingAvatarState(state))
		{
			return;
		}

		StoreRemoteAvatarState(state);
	}

	private void OnLevelSelectionMessageReceived(ulong senderClientId, FastBufferReader reader)
	{
		if (!enableNetworkAuthoritativeSync)
		{
			return;
		}

		if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		LevelSelectionState state = ReadLevelSelectionState(reader);
		if (state == null)
		{
			return;
		}

		ApplyLevelSelectionState(state);
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

	private void SendAvatarUpdate(Vector3 position, float rotation, string heldId, bool reliable = false)
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening)
		{
			return;
		}

		AvatarState state = BuildLocalAvatarState(position, rotation, heldId);

		if (Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			BroadcastAvatarState(state, GetLocalActorClientId(), reliable);
			return;
		}

		SendAvatarStateToClient(NetworkManager.ServerClientId, state, reliable);
	}

	private AvatarState BuildLocalAvatarState(Vector3 position, float rotation, string transitionFallbackId = null)
	{
		HeldObjectKind heldKind = GetCurrentHeldObjectKind();
		string heldId = GetCurrentHeldObjectId();
		if (heldKind == HeldObjectKind.None && !string.IsNullOrEmpty(transitionFallbackId))
		{
			heldKind = HeldObjectKind.Transition;
			heldId = transitionFallbackId;
		}

		Vector2 heldOffset = heldKind == HeldObjectKind.CompositeBlock ? heldCompositeBlockOffset : Vector2.zero;
		return new AvatarState
		{
			clientId = (long)GetLocalActorClientId(),
			x = position.x,
			y = position.y,
			rotation = rotation,
			craneHeight = avatarCraneCurrentHeight,
			heldTransitionId = heldKind == HeldObjectKind.Transition ? (heldId ?? "") : "",
			heldObjectId = heldId ?? "",
			heldObjectKind = (int)heldKind,
			heldOffsetX = heldOffset.x,
			heldOffsetY = heldOffset.y,
			sceneMode = GetCurrentAvatarSceneMode(),
			connectStartNodeId = craneConnectStartNodeId ?? "",
			connectReversed = craneConnectReversed,
		};
	}

	private int GetCurrentAvatarSceneMode()
	{
		return gameplayInitialized ? AvatarSceneModeGameplay : AvatarSceneModeLevelSelection;
	}

	private bool ShouldAcceptIncomingAvatarState(AvatarState state)
	{
		if (state == null)
		{
			return false;
		}

		if (showLevelSelection && !gameplayInitialized)
		{
			return state.sceneMode == AvatarSceneModeLevelSelection;
		}

		return !gameplayInitialized || state.sceneMode == AvatarSceneModeGameplay;
	}

	private string GetCurrentHeldObjectId()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return heldCompositeBlockId;
		}

		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			return heldPlaceId;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return heldTransitionId;
		}

		return "";
	}

	private HeldObjectKind GetCurrentHeldObjectKind()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId))
		{
			return HeldObjectKind.CompositeBlock;
		}

		if (!string.IsNullOrEmpty(heldPlaceId))
		{
			return HeldObjectKind.Place;
		}

		if (!string.IsNullOrEmpty(heldTransitionId))
		{
			return HeldObjectKind.Transition;
		}

		return HeldObjectKind.None;
	}

	private string GetCurrentHeldNetworkKey()
	{
		HeldObjectKind kind = GetCurrentHeldObjectKind();
		string id = GetCurrentHeldObjectId();
		string heldKey = "";
		if (kind != HeldObjectKind.None && !string.IsNullOrEmpty(id))
		{
			heldKey = ((int)kind).ToString() + ":" + id;
		}

		string connectKey = GetCurrentCraneConnectNetworkKey();
		if (string.IsNullOrEmpty(heldKey))
		{
			return connectKey;
		}

		if (string.IsNullOrEmpty(connectKey))
		{
			return heldKey;
		}

		return heldKey + "|" + connectKey;
	}

	private string GetHeldNetworkKey(RemoteHeldObjectState heldState)
	{
		if (heldState == null || heldState.kind == HeldObjectKind.None || string.IsNullOrEmpty(heldState.id))
		{
			return "";
		}

		return ((int)heldState.kind).ToString() + ":" + heldState.id;
	}

	private string GetCurrentCraneConnectNetworkKey()
	{
		if (string.IsNullOrEmpty(craneConnectStartNodeId))
		{
			return "";
		}

		return "C:" + craneConnectStartNodeId + ":" + (craneConnectReversed ? "1" : "0");
	}

	private string GetCraneConnectNetworkKey(RemoteCraneConnectState connectState)
	{
		if (connectState == null || string.IsNullOrEmpty(connectState.startNodeId))
		{
			return "";
		}

		return "C:" + connectState.startNodeId + ":" + (connectState.reversed ? "1" : "0");
	}

	private string GetCraneConnectNetworkKey(AvatarState state)
	{
		if (state == null || string.IsNullOrEmpty(state.connectStartNodeId))
		{
			return "";
		}

		return "C:" + state.connectStartNodeId + ":" + (state.connectReversed ? "1" : "0");
	}

	private RemoteHeldObjectState GetRemoteHeldObjectState(AvatarState state)
	{
		if (state == null)
		{
			return new RemoteHeldObjectState { kind = HeldObjectKind.None, id = "", offset = Vector2.zero };
		}

		HeldObjectKind kind = (HeldObjectKind)Mathf.Clamp(state.heldObjectKind, (int)HeldObjectKind.None, (int)HeldObjectKind.CompositeBlock);
		string id = state.heldObjectId ?? "";
		if (kind == HeldObjectKind.None && !string.IsNullOrEmpty(state.heldTransitionId))
		{
			kind = HeldObjectKind.Transition;
			id = state.heldTransitionId;
		}

		if (string.IsNullOrEmpty(id))
		{
			kind = HeldObjectKind.None;
		}

		return new RemoteHeldObjectState
		{
			kind = kind,
			id = id,
			offset = new Vector2(state.heldOffsetX, state.heldOffsetY),
		};
	}

	private void ApplyLocalHeldObjectState(AvatarState state)
	{
		RemoteHeldObjectState heldState = GetRemoteHeldObjectState(state);
		heldTransitionId = heldState.kind == HeldObjectKind.Transition ? heldState.id : null;
		heldPlaceId = heldState.kind == HeldObjectKind.Place ? heldState.id : null;
		heldCompositeBlockId = heldState.kind == HeldObjectKind.CompositeBlock ? heldState.id : null;
		heldCompositeBlockOffset = heldState.kind == HeldObjectKind.CompositeBlock ? heldState.offset : Vector2.zero;
	}

	private bool IsNodeHeldByRemoteAvatar(string nodeId)
	{
		if (string.IsNullOrEmpty(nodeId) || remoteAvatarInventories == null)
		{
			return false;
		}

		foreach (KeyValuePair<ulong, RemoteHeldObjectState> pair in remoteAvatarInventories)
		{
			RemoteHeldObjectState heldState = pair.Value;
			if (heldState == null || heldState.kind == HeldObjectKind.None || string.IsNullOrEmpty(heldState.id))
			{
				continue;
			}

			if ((heldState.kind == HeldObjectKind.Place || heldState.kind == HeldObjectKind.Transition) && heldState.id == nodeId)
			{
				return true;
			}

			if (heldState.kind == HeldObjectKind.CompositeBlock && GetCompositeBlockIdForNodeId(nodeId) == heldState.id)
			{
				return true;
			}
		}

		return false;
	}

	private void BroadcastAvatarState(AvatarState state, ulong exceptClientId, bool reliable = false)
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

			SendAvatarStateToClient(clientId, state, reliable);
		}
	}

	private void SendAvatarStateToClient(ulong clientId, AvatarState state, bool reliable = false)
	{
		string json = JsonUtility.ToJson(state);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		using (FastBufferWriter writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp))
		{
			writer.WriteValueSafe(bytes.Length);
			writer.WriteBytesSafe(bytes);
			NetworkDelivery delivery = reliable ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.Unreliable;
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(AvatarMessageName, clientId, writer, delivery);
		}
	}

	private void BroadcastLevelSelectionStateToClients()
	{
		if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsHost)
		{
			return;
		}

		LevelSelectionState state = new LevelSelectionState
		{
			showSelection = true,
			selectedLevelIndex = selectedLevelIndex
		};

		foreach (ulong clientId in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds)
		{
			if (clientId == NetworkManager.ServerClientId)
			{
				continue;
			}

			SendLevelSelectionStateToClient(clientId, state);
		}
	}

	private void SendLevelSelectionStateToClient(ulong clientId, LevelSelectionState state)
	{
		string json = JsonUtility.ToJson(state);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		using (FastBufferWriter writer = new FastBufferWriter(sizeof(int) + bytes.Length, Allocator.Temp))
		{
			writer.WriteValueSafe(bytes.Length);
			writer.WriteBytesSafe(bytes);
			Unity.Netcode.NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(LevelSelectionMessageName, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
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
		remoteAvatarInventories[clientId] = GetRemoteHeldObjectState(state);
		remoteAvatarCraneHeights[clientId] = state.craneHeight >= 0f ? state.craneHeight : avatarCraneRestHeight;
		StoreRemoteCraneConnectState(clientId, state.connectStartNodeId, state.connectReversed);
		NormalizeCompositeBlockSorting();
	}

	private void StoreRemoteCraneConnectState(ulong clientId, string startNodeId, bool reversed)
	{
		string trimmedStartNodeId = startNodeId ?? "";
		if (string.IsNullOrEmpty(trimmedStartNodeId))
		{
			remoteCraneConnectStates.Remove(clientId);
			return;
		}

		remoteCraneConnectStates[clientId] = new RemoteCraneConnectState
		{
			startNodeId = trimmedStartNodeId,
			reversed = reversed,
		};
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
		SnapshotData snapshot = new SnapshotData
		{
			selectedLevelIndex = selectedLevelIndex,
			gameplayMenuOpen = gameplayMenuOpen,
			gameplayMenuOwnerClientId = GetSerializableGameplayMenuOwnerClientId(),
			levelEnded = this.levelEnded,
			completedOrderIndexes = GetCompletedLevelOrderIndexes(),
			completedOrderDeliveryTimes = GetCompletedLevelOrderDeliveryTimes(),
			wrongOrderDeliveryPenaltyCount = GetWrongLevelOrderDeliveryPenaltyCount()
		};

		foreach (KeyValuePair<string, NodeRuntime> pair in nodesById)
		{
			NodeRuntime node = pair.Value;
			snapshot.nodes.Add(new NodeState
			{
				id = node.id,
				type = (int)node.type,
				tokens = node.tokens,
				typedTokens = BuildTypedTokenSnapshot(node),
				x = node.transform.position.x,
				y = node.transform.position.y,
				ownerClientId = (long)node.ownerClientId,
				isSharedPoolTransition = node.isSharedPoolTransition,
				isSharedPoolAvailable = node.isSharedPoolAvailable,
				processingDuration = node.processingDuration,
				processingRemaining = GetTimedPlaceProcessingRemaining(node),
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
				kind = (int)arc.kind,
			});
		}

		// Add all avatar states
		snapshot.avatars.Add(BuildLocalAvatarState(avatarPosition, avatarRotation, heldTransitionId));

		// Add remote avatars (from other players)
		if (remoteAvatarPositions != null)
		{
			foreach (KeyValuePair<ulong, Vector3> pair in remoteAvatarPositions)
			{
				ulong clientId = pair.Key;
				Vector3 pos = pair.Value;
				float rotation = remoteAvatarRotations.ContainsKey(clientId) ? remoteAvatarRotations[clientId] : 0f;
				float craneHeight = remoteAvatarCraneHeights.ContainsKey(clientId) ? remoteAvatarCraneHeights[clientId] : avatarCraneRestHeight;
				RemoteHeldObjectState heldState = remoteAvatarInventories.ContainsKey(clientId)
					? remoteAvatarInventories[clientId]
					: new RemoteHeldObjectState { kind = HeldObjectKind.None, id = "", offset = Vector2.zero };
				RemoteCraneConnectState connectState = remoteCraneConnectStates.ContainsKey(clientId)
					? remoteCraneConnectStates[clientId]
					: null;

				snapshot.avatars.Add(new AvatarState
				{
					clientId = (long)clientId,
					x = pos.x,
					y = pos.y,
					rotation = rotation,
					craneHeight = craneHeight,
					heldTransitionId = heldState.kind == HeldObjectKind.Transition ? (heldState.id ?? "") : "",
					heldObjectId = heldState.id ?? "",
					heldObjectKind = (int)heldState.kind,
					heldOffsetX = heldState.offset.x,
					heldOffsetY = heldState.offset.y,
					sceneMode = AvatarSceneModeGameplay,
					connectStartNodeId = connectState != null ? (connectState.startNodeId ?? "") : "",
					connectReversed = connectState != null && connectState.reversed,
				});
			}
		}

		return snapshot;
	}

	private List<TokenState> BuildTypedTokenSnapshot(NodeRuntime node)
	{
		List<TokenState> states = new List<TokenState>();
		if (node == null || node.type != NodeType.Place)
		{
			return states;
		}

		EnsureTypedTokenList(node);
		for (int i = 0; i < node.typedTokens.Count; i++)
		{
			TokenRuntime token = node.typedTokens[i];
			TokenState state = new TokenState();
			if (token != null)
			{
				state.description = token.description ?? "";
				CopyTokenValues(token.ingredients, state.ingredients);
				CopyTokenValues(token.states, state.states);
				state.ingredients.Sort(StringComparer.OrdinalIgnoreCase);
			}

			states.Add(state);
		}

		return states;
	}

	private void ApplyTypedTokenSnapshot(NodeRuntime node, List<TokenState> states, int fallbackTokenCount)
	{
		if (node == null || node.type != NodeType.Place)
		{
			return;
		}

		node.typedTokens = new List<TokenRuntime>();
		if (states != null)
		{
			for (int i = 0; i < states.Count; i++)
			{
				TokenState state = states[i];
				TokenRuntime token = new TokenRuntime();
				if (state != null)
				{
					token.description = state.description ?? "";
					CopyTokenValues(state.ingredients, token.ingredients);
					CopyTokenValues(state.states, token.states);
					NormalizeTokenIngredients(token);
				}

				node.typedTokens.Add(token);
			}
		}

		while (node.typedTokens.Count < fallbackTokenCount)
		{
			node.typedTokens.Add(CreateUntypedToken());
		}

		while (node.typedTokens.Count > fallbackTokenCount)
		{
			node.typedTokens.RemoveAt(node.typedTokens.Count - 1);
		}

		node.tokens = node.typedTokens.Count;
	}

	private void ApplyNodeProcessingSnapshot(NodeRuntime node, float duration, float remaining)
	{
		if (node == null || node.type != NodeType.Place)
		{
			return;
		}

		float configuredDuration = GetTimedPlaceProcessingDuration(node.id);
		node.processingDuration = duration > 0f ? duration : configuredDuration;
		if (node.processingDuration <= 0f || node.tokens <= 0)
		{
			node.processingReadyTime = 0f;
			return;
		}

		node.processingReadyTime = Time.time + Mathf.Clamp(remaining, 0f, node.processingDuration);
		EnsureTimedPlaceProcessingVisual(node);
	}

	private void ApplySnapshot(SnapshotData snapshot)
	{
		if (snapshot == null)
		{
			return;
		}

		bool wasGameplayInitialized = gameplayInitialized;
		ApplySnapshotLevelDefinition(snapshot.selectedLevelIndex);
		DestroyLevelSelectionScreen();
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
						avatarCraneCurrentHeight = state.craneHeight >= 0f ? state.craneHeight : avatarCraneRestHeight;
						ApplyLocalHeldObjectState(state);
					}
				}
				else
				{
					// Store remote avatar position
					if (remoteAvatarPositions == null)
					{
						remoteAvatarPositions = new Dictionary<ulong, Vector3>();
						remoteAvatarRotations = new Dictionary<ulong, float>();
						remoteAvatarInventories = new Dictionary<ulong, RemoteHeldObjectState>();
						remoteAvatarCraneHeights = new Dictionary<ulong, float>();
						remoteCraneConnectStates = new Dictionary<ulong, RemoteCraneConnectState>();
					}

					remoteAvatarPositions[clientId] = new Vector3(state.x, state.y, 0f);
					remoteAvatarRotations[clientId] = state.rotation;
					remoteAvatarInventories[clientId] = GetRemoteHeldObjectState(state);
					remoteAvatarCraneHeights[clientId] = state.craneHeight >= 0f ? state.craneHeight : avatarCraneRestHeight;
					StoreRemoteCraneConnectState(clientId, state.connectStartNodeId, state.connectReversed);
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

		if (!string.IsNullOrEmpty(draggedNodeId)
			|| !string.IsNullOrEmpty(heldTransitionId)
			|| !string.IsNullOrEmpty(heldPlaceId)
			|| !string.IsNullOrEmpty(heldCompositeBlockId)
			|| !string.IsNullOrEmpty(craneConnectStartNodeId)
			|| !string.IsNullOrEmpty(pendingClaimedTransitionId)
			|| hasPoolTransitions)
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
					ApplyTypedTokenSnapshot(node, state.typedTokens, state.tokens);
					ApplyNodeProcessingSnapshot(node, state.processingDuration, state.processingRemaining);
					// Only update position for nodes that don't belong to me or aren't being dragged
					// Nodes I own (or am dragging) keep their local position
					bool heldByLocal = node.id == heldTransitionId || node.id == heldPlaceId || IsHeldCompositeBlockNode(node);
					bool heldByRemote = IsNodeHeldByRemoteAvatar(node.id);
					if (node.id != draggedNodeId && !heldByLocal && !heldByRemote && node.ownerClientId != GetLocalActorClientId())
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
					if (nodesById.TryGetValue(state.id, out NodeRuntime createdPlace))
					{
						ApplyTypedTokenSnapshot(createdPlace, state.typedTokens, state.tokens);
						ApplyNodeProcessingSnapshot(createdPlace, state.processingDuration, state.processingRemaining);
					}
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
					arc.kind = GetEffectiveArcKind(state.fromId, state.toId, (ArcKind)state.kind);
					UpdateArcVisual(arc);
				}
				else
				{
					CreateArcInternal(state.id, state.fromId, state.toId, state.weight, false, (ulong)state.ownerClientId, (ArcKind)state.kind);
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

			if (!wasGameplayInitialized)
			{
				gameplayInitialized = true;
				avatarStartPositionApplied = false;
				StartLevelOrderTimeline();
			}

			ApplyCompletedLevelOrderState(snapshot.completedOrderIndexes, snapshot.completedOrderDeliveryTimes);
			ApplyWrongLevelOrderDeliveryPenaltyCount(snapshot.wrongOrderDeliveryPenaltyCount);
			ApplyGameplayMenuSnapshotState(snapshot.gameplayMenuOpen, snapshot.gameplayMenuOwnerClientId);
			ApplyLevelEndSnapshotState(snapshot.levelEnded);
			EnsureLocalAvatarStartPosition();
			RefreshPetriNetVisuals();
			TryAttachPendingCreatedBlock();
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
				if (nodesById.TryGetValue(state.id, out NodeRuntime createdPlace))
				{
					ApplyTypedTokenSnapshot(createdPlace, state.typedTokens, state.tokens);
					ApplyNodeProcessingSnapshot(createdPlace, state.processingDuration, state.processingRemaining);
				}
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

			CreateArcInternal(state.id, state.fromId, state.toId, state.weight, false, (ulong)state.ownerClientId, (ArcKind)state.kind);
			maxArc = Mathf.Max(maxArc, ExtractTrailingNumber(state.id));
		}

		placeCounter = Mathf.Max(1, maxPlace + 1);
		transitionCounter = Mathf.Max(1, maxTransition + 1);
		arcCounter = Mathf.Max(1, maxArc + 1);

		RefreshPetriNetVisuals();
		if (!wasGameplayInitialized)
		{
			avatarStartPositionApplied = false;
		}

		EnsureLocalAvatarStartPosition();
		gameplayInitialized = true;
		if (!wasGameplayInitialized)
		{
			StartLevelOrderTimeline();
		}

		ApplyCompletedLevelOrderState(snapshot.completedOrderIndexes, snapshot.completedOrderDeliveryTimes);
		ApplyWrongLevelOrderDeliveryPenaltyCount(snapshot.wrongOrderDeliveryPenaltyCount);
		ApplyGameplayMenuSnapshotState(snapshot.gameplayMenuOpen, snapshot.gameplayMenuOwnerClientId);
		ApplyLevelEndSnapshotState(snapshot.levelEnded);
		pendingClaimedTransitionId = null;
		TryAttachPendingCreatedBlock();
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

	private LevelSelectionState ReadLevelSelectionState(FastBufferReader reader)
	{
		reader.ReadValueSafe(out int length);
		if (length <= 0)
		{
			return null;
		}

		byte[] bytes = new byte[length];
		reader.ReadBytesSafe(ref bytes, length);
		string json = Encoding.UTF8.GetString(bytes);
		return JsonUtility.FromJson<LevelSelectionState>(json);
	}

	private bool IsHostOrOffline()
	{
		if (singlePlayerMode || !enableNetworkAuthoritativeSync)
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
		if (singlePlayerMode)
		{
			return "Single Player";
		}

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
