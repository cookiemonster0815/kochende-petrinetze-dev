using System;
using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
	private enum EditMode
	{
		Select,
		CreatePlace,
		CreateTransition,
		Connect,
		Delete,
		TokenAdd,
		TokenRemove
	}

	private enum NodeType
	{
		Place,
		Transition
	}

	[Serializable]
	private class CommandData
	{
		public string action;
		public string id;
		public string fromId;
		public string toId;
		public int amount;
		public int weight;
		public float x;
		public float y;
		public float z;
		public float rotation;
	}

	[Serializable]
	private class SnapshotData
	{
		public int selectedLevelIndex;
		public List<int> completedOrderIndexes = new List<int>();
		public List<NodeState> nodes = new List<NodeState>();
		public List<ArcState> arcs = new List<ArcState>();
		public List<AvatarState> avatars = new List<AvatarState>();
	}

	[Serializable]
	private class AvatarState
	{
		public long clientId;
		public float x;
		public float y;
		public float rotation;
		public string heldTransitionId;
	}

	[Serializable]
	private class LevelSelectionState
	{
		public bool showSelection;
		public int selectedLevelIndex;
	}

	[Serializable]
	private class NodeState
	{
		public string id;
		public int type;
		public int tokens;
		public List<TokenState> typedTokens = new List<TokenState>();
		public float x;
		public float y;
		public long ownerClientId;
		public bool isSharedPoolTransition;
		public bool isSharedPoolAvailable;
		public float processingDuration;
		public float processingRemaining;
	}

	[Serializable]
	private class TokenState
	{
		public string description;
		public List<string> ingredients = new List<string>();
		public List<string> states = new List<string>();
	}

	[Serializable]
	private class ArcState
	{
		public string id;
		public string fromId;
		public string toId;
		public int weight;
		public long ownerClientId;
	}

	private class NodeRuntime
	{
		public string id;
		public string displayName;
		public NodeType type;
		public int tokens;
		public List<TokenRuntime> typedTokens = new List<TokenRuntime>();
		public ulong ownerClientId;
		public bool isSharedPoolTransition;
		public bool isSharedPoolAvailable;
		public Transform transform;
		public SpriteRenderer renderer;
		public Collider2D collider;
		public TextMesh label;
		public Transform tokenRoot;
		public float processingDuration;
		public float processingReadyTime;
		public GameObject processingBarRoot;
		public SpriteRenderer processingBarFill;
	}

	private class TokenRuntime
	{
		public string description;
		public List<string> ingredients = new List<string>();
		public List<string> states = new List<string>();
	}

	private class ArcRuntime
	{
		public string id;
		public string fromId;
		public string toId;
		public int weight;
		public ulong ownerClientId;
		public GameObject gameObject;
		public LineRenderer body;
		public LineRenderer arrow;
		public EdgeCollider2D collider;
	}

	private class CompositeBlockRuntime
	{
		public string id;
		public GameObject gameObject;
		public SpriteRenderer fill;
		public LineRenderer border;
		public BoxCollider2D collider;
	}

	private const string CommandMessageName = "PetriCommand";
	private const string SnapshotMessageName = "PetriSnapshot";
	private const string AvatarMessageName = "PetriAvatar";
	private const string LevelSelectionMessageName = "PetriLevelSelection";
}
