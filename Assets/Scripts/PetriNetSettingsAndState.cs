using System.Collections.Generic;
using UnityEngine;

public partial class GameManager
{
	[Header("Scene Setup")]
	[SerializeField] private bool createDefaultCameraIfMissing = true;
	[SerializeField] private bool createDefaultLightIfMissing = true;

	[Header("Initial Net")]
	[SerializeField] private bool buildPetriNetOnStart = true;
	[SerializeField] private string petriNetRootName = "GeneratedPetriNet";
	[SerializeField] private bool showLevelSelection = true;

	[Header("Visuals")]
	[SerializeField] private Material placeMaterial;
	[SerializeField] private Material transitionMaterial;
	[SerializeField] private Material arcMaterial;
	[SerializeField] private Color placeColor = new Color(0.15f, 0.58f, 0.95f);
	[SerializeField] private Color tokenColor = new Color(0.98f, 0.98f, 1f);
	[SerializeField] private Color transitionEnabledColor = new Color(0.97f, 0.53f, 0.12f);
	[SerializeField] private Color transitionDisabledColor = new Color(0.62f, 0.62f, 0.62f);
	[SerializeField] private float arcWidth = 0.08f;
	[SerializeField] private float arrowHeadLength = 0.36f;
	[SerializeField] private float arrowHeadAngle = 30f;

	[Header("Editor")]
	[SerializeField] private float zoomSpeed = 0.5f;
	[SerializeField] private float minZoom = 1.8f;
	[SerializeField] private float maxZoom = 12f;

	[Header("Networking")]
	[SerializeField] private bool enableNetworkAuthoritativeSync = true;
	[SerializeField] private bool enableSharedTransitionPool = true;
	[SerializeField] private float sharedPoolY = 0f;
	[SerializeField] private float sharedPoolHalfHeight = 1f;
	[SerializeField] private float sharedPoolDragThreshold = 0.22f;
	[SerializeField] private float playerZoneXOffset = 6.5f;
	[SerializeField] private float playerZoneYSpacing = 2.7f;

	[System.Serializable]
	private class PoolBlockDefinition
	{
		public string firstTransitionName = "Start";
		public string secondTransitionName = "Ende";
		public float processingSeconds = 5f;
		public string resultState = "";

		public PoolBlockDefinition()
		{
		}

		public PoolBlockDefinition(string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState)
		{
			this.firstTransitionName = firstTransitionName;
			this.secondTransitionName = secondTransitionName;
			this.processingSeconds = processingSeconds;
			this.resultState = resultState;
		}
	}

	[Header("Blocks")]
	[SerializeField] private List<PoolBlockDefinition> sharedPoolBlocks = new List<PoolBlockDefinition>
	{
		new PoolBlockDefinition("Kochen Start", "Kochen Ende", 5f, "gekocht"),
		new PoolBlockDefinition("Schneiden Start", "Schneiden Ende", 10f, "geschnitten"),
	};
	[SerializeField] private List<PoolBlockDefinition> topPlayerBlocks = new List<PoolBlockDefinition>();
	[SerializeField] private List<PoolBlockDefinition> bottomPlayerBlocks = new List<PoolBlockDefinition>();
	[SerializeField] private string sharedPoolTrashTransitionName = "Müll";
	[SerializeField] private float sharedPoolItemGap = 0.65f;

	private static readonly string[] DefaultTopIngredientNames = { "Käse", "Tomate" };
	private static readonly string[] DefaultBottomIngredientNames = { "Traube", "Ananas", "Wirsing", "Paprika", "Zwiebel", "Salat", "Aubergine", "Pilz" };

	[Header("Ingredients")]
	[SerializeField] private List<string> topIngredientNames = new List<string>(DefaultTopIngredientNames);
	[SerializeField] private List<string> bottomIngredientNames = new List<string>(DefaultBottomIngredientNames);
	[SerializeField] private float ingredientTransitionSpacing = 1.65f;

	private readonly Dictionary<string, NodeRuntime> nodesById = new Dictionary<string, NodeRuntime>();
	private readonly Dictionary<string, ArcRuntime> arcsById = new Dictionary<string, ArcRuntime>();
	private readonly Dictionary<Collider2D, string> nodeByCollider = new Dictionary<Collider2D, string>();
	private readonly Dictionary<Collider2D, string> arcByCollider = new Dictionary<Collider2D, string>();
	private readonly Dictionary<string, CompositeBlockRuntime> compositeBlocksById = new Dictionary<string, CompositeBlockRuntime>();
	private readonly Dictionary<Collider2D, string> compositeBlockByCollider = new Dictionary<Collider2D, string>();

	private Transform petriNetRoot;
	private Camera mainCamera;
	private EditMode currentMode = EditMode.Select;
	private string connectStartNodeId;
	private string craneConnectStartNodeId;
	private bool craneConnectReversed;
	private string draggedNodeId;
	private Vector3 dragOffset;
	private bool isMiddlePanning;
	private Vector3 panReferenceWorld;
	private int placeCounter = 1;
	private int transitionCounter = 1;
	private int arcCounter = 1;
	private Sprite circleSprite;
	private Sprite squareSprite;
	private Material runtimeArcMaterial;
	private Transform sharedPoolVisualRoot;
	private bool networkHandlersRegistered;
	private bool suppressNetworkSend;
	private bool collaborativeLayoutApplied;
	private bool gameplayInitialized;
	private float nextDragNetworkSyncTime;
	private Vector3 lastDragNetworkSyncPosition;
	private string pointerDownNodeId;
	private string pointerDownCompositeBlockId;
	private Vector3 pointerDownWorld;
	private bool pointerDragActive;
	private string pendingClaimedTransitionId;
	private string draggedCompositeBlockId;

	// Avatar system
	private Vector3 avatarPosition;
	private float avatarRotation;
	private bool avatarStartPositionApplied;
	private string heldTransitionId;
	private string heldPlaceId;
	private string heldCompositeBlockId;
	private Vector2 heldCompositeBlockOffset;
	private bool pendingCreatedPlacePickup;
	private Vector3 pendingCreatedPlacePickupPosition;
	private HashSet<string> pendingCreatedPlaceExistingIds = new HashSet<string>();
	private Vector3 lastAvatarPosition;
	private float lastAvatarNetworkSyncRotation;
	private string lastAvatarNetworkSyncHeldId = "";
	private float nextAvatarNetworkSyncTime;
	private float avatarNetworkSyncInterval = 0.05f;
	private float avatarSpeed = 8f;
	private float avatarSprintMultiplier = 1.5f;
	private float avatarCollisionRadius = 0.4f; // Matches CircleCollider2D radius on avatar visual
	private float transitionCollisionRadius = 0.45f; // Approximate radius of transition collider
	private float cameraRestAreaMargin = 0.3f; // Rest area margin as % of screen
	private float avatarCraneRestHeight = 0.65f;
	private float avatarCraneLoweredHeight = 0.12f;
	private float avatarCraneCurrentHeight = 0.65f;
	private float avatarCraneAnimationStartTime = -10f;
	private float avatarCraneAnimationDuration = 0.36f;

	// Remote avatar (other player)
	private Dictionary<ulong, Vector3> remoteAvatarPositions = new Dictionary<ulong, Vector3>();
	private Dictionary<ulong, float> remoteAvatarRotations = new Dictionary<ulong, float>();
	private Dictionary<ulong, string> remoteAvatarInventories = new Dictionary<ulong, string>();

	private const ulong UnassignedOwnerClientId = ulong.MaxValue;
}
