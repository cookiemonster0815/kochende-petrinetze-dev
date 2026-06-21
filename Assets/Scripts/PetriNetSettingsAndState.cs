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
	[SerializeField] private bool showEditorOverlay = true;
	[SerializeField] private float zoomSpeed = 0.5f;
	[SerializeField] private float minZoom = 1.8f;
	[SerializeField] private float maxZoom = 12f;
	[SerializeField] private float panSpeed = 6f; // For future UI panning

	[Header("Networking")]
	[SerializeField] private bool enableNetworkAuthoritativeSync = true;
	[SerializeField] private bool enableSharedTransitionPool = true;
	[SerializeField] private int sharedPoolTransitionCount = 4;
	[SerializeField] private float sharedPoolY = 4.2f;
	[SerializeField] private float sharedPoolDragThreshold = 0.22f;
	[SerializeField] private float playerZoneXOffset = 6.5f;
	[SerializeField] private float playerZoneYSpacing = 2.7f;

	private readonly Dictionary<string, NodeRuntime> nodesById = new Dictionary<string, NodeRuntime>();
	private readonly Dictionary<string, ArcRuntime> arcsById = new Dictionary<string, ArcRuntime>();
	private readonly Dictionary<Collider2D, string> nodeByCollider = new Dictionary<Collider2D, string>();
	private readonly Dictionary<Collider2D, string> arcByCollider = new Dictionary<Collider2D, string>();

	private Transform petriNetRoot;
	private Camera mainCamera;
	private EditMode currentMode = EditMode.Select;
	private string connectStartNodeId;
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
	private float sharedPoolSlotSpacing = 1.25f;
	private bool networkHandlersRegistered;
	private bool suppressNetworkSend;
	private bool collaborativeLayoutApplied;
	private bool gameplayInitialized;
	private float nextDragNetworkSyncTime;
	private Vector3 lastDragNetworkSyncPosition;
	private string pointerDownNodeId;
	private Vector3 pointerDownWorld;
	private bool pointerDragActive;
	private string pendingClaimedTransitionId;

	// Avatar system
	private Vector3 avatarPosition;
	private float avatarRotation;
	private string heldTransitionId;
	private Vector3 lastAvatarPosition;
	private float lastAvatarRotation;
	private float nextAvatarNetworkSyncTime;
	private float avatarSpeed = 5f;
	private float avatarCollisionRadius = 0.4f; // Matches CircleCollider2D radius on avatar visual
	private float transitionCollisionRadius = 0.45f; // Approximate radius of transition collider
	private float cameraRestAreaMargin = 0.3f; // Rest area margin as % of screen
	private string temporarilyIgnoredCollisionNodeId;
	private float temporarilyIgnoredCollisionUntilTime;
	private float postDropCollisionIgnoreDuration = 0.18f;

	// Remote avatar (other player)
	private Dictionary<ulong, Vector3> remoteAvatarPositions = new Dictionary<ulong, Vector3>();
	private Dictionary<ulong, float> remoteAvatarRotations = new Dictionary<ulong, float>();
	private Dictionary<ulong, string> remoteAvatarInventories = new Dictionary<ulong, string>();

	private const ulong UnassignedOwnerClientId = ulong.MaxValue;
}
