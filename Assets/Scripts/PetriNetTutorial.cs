using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class GameManager
{
	private const string TutorialLevelId = "l1.1";
	private const int TutorialStepInactive = 0;
	private const int TutorialStepIntro = 1;
	private const int TutorialStepExplore = 2;
	private const int TutorialStepPickupBlock = 3;
	private const int TutorialStepConnectNodes = 4;
	private const int TutorialStepMoveConnection = 5;
	private const int TutorialStepFireIngredient = 6;
	private const int TutorialStepPlayerExchange = 7;
	private const int TutorialStepOrdersAndDelivery = 8;
	private const int TutorialStepCreateStorage = 9;
	private const int TutorialStepWaitForStoragePlacement = 10;
	private const int TutorialStepDeleteEmptyStorage = 11;
	private const int TutorialStepTrash = 12;
	private const int TutorialStepCompletionMessage = 13;
	private const int TutorialStepDone = 14;
	private const float TutorialBubbleZ = NodeLabelLayerZ - 0.03f;
	private const float TutorialBubbleLineZ = TutorialBubbleZ - 0.012f;
	private const float TutorialBubbleTextZ = TutorialBubbleZ - 0.035f;
	private const int TutorialBubbleFillSortingOrder = 57;
	private const int TutorialBubbleLineSortingOrder = 58;
	private const int TutorialBubbleTextSortingOrder = 59;
	private const int TutorialCompanionBubbleFillSortingOrder = 54;
	private const int TutorialCompanionBubbleLineSortingOrder = 55;
	private const int TutorialCompanionBubbleTextSortingOrder = 56;
	private const int TutorialBubbleOutlineSegments = 72;
	private const int TutorialBubbleDotCount = 3;
	private const int TutorialBubbleDotOutlineSegments = 28;
	private const float TutorialBubbleScale = 1.45f;
	private const float TutorialBubbleTextScale = 2f;
	private const float TutorialBubbleCharacterSize = 0.115f * TutorialBubbleTextScale;
	private const float TutorialBubbleTextWidthRatio = 0.78f;
	private const float TutorialBubbleTextHeightRatio = 0.56f;
	private const float TutorialBubbleEstimatedCharacterWidth = 0.7f;
	private const float TutorialBubbleEstimatedLineHeight = 1.28f;
	private const float TutorialCircleSpriteFilledDiameter = 0.96f;
	private const float TutorialBubbleDotMinimumGap = 0.16f;

	private string activeLevelId = "";
	private int tutorialStep = TutorialStepInactive;
	private static bool levelSelectionMovementTutorialShownThisSession;
	private bool tutorialSkipped;
	private bool tutorialLevelSelectionMovementComplete;
	private bool tutorialLevelSelectionMovementActiveThisVisit;
	private int tutorialCompletionMessageStartFrame = -1;
	private string tutorialPendingBlockId;
	private string tutorialPlacedBlockId;
	private bool inhibitorArcHintFinished;
	private bool weightedArcHintFinished;
	private GameObject tutorialBubbleRoot;
	private SpriteRenderer tutorialBubbleFill;
	private LineRenderer tutorialBubbleBorder;
	private SpriteRenderer[] tutorialBubbleDotFills;
	private LineRenderer[] tutorialBubbleDotBorders;
	private SpriteRenderer[] tutorialBubbleSecondDotFills;
	private LineRenderer[] tutorialBubbleSecondDotBorders;
	private TextMesh tutorialBubbleText;
	private TextMesh tutorialBubbleSkipText;
	private GameObject tutorialCompanionBubbleRoot;
	private SpriteRenderer tutorialCompanionBubbleFill;
	private LineRenderer tutorialCompanionBubbleBorder;
	private SpriteRenderer[] tutorialCompanionBubbleDotFills;
	private LineRenderer[] tutorialCompanionBubbleDotBorders;
	private SpriteRenderer[] tutorialCompanionBubbleSecondDotFills;
	private LineRenderer[] tutorialCompanionBubbleSecondDotBorders;
	private TextMesh tutorialCompanionBubbleText;
	private TextMesh tutorialCompanionBubbleSkipText;
	private Material tutorialBubbleDepthTestedTextMaterial;

	private void OnLevelDefinitionApplied(PetriNetLevelDefinition level)
	{
		string nextLevelId = level != null && level.id != null ? level.id.Trim() : "";
		bool beginsNewLevel = !gameplayInitialized || activeLevelId != nextLevelId;
		activeLevelId = nextLevelId;
		if (beginsNewLevel)
		{
			ResetLevelTutorialState();
		}
	}

	private void UpdateLevelTutorial()
	{
		if (!gameplayInitialized)
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (!IsTutorialLevelActive())
		{
			if (IsInhibitorArcHintLevelActive() && !inhibitorArcHintFinished)
			{
				UpdateInhibitorArcHint();
				return;
			}

			if (IsWeightedArcHintLevelActive() && !weightedArcHintFinished)
			{
				UpdateWeightedArcHint();
				return;
			}

			DestroyLevelTutorialVisuals();
			return;
		}

		if (tutorialSkipped)
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (tutorialStep != TutorialStepIntro
			&& tutorialStep != TutorialStepCompletionMessage
			&& IsTutorialStepSkipPressed())
		{
			SkipCurrentLevelTutorialStep();
			return;
		}

		if (tutorialStep == TutorialStepInactive)
		{
			tutorialStep = TutorialStepIntro;
		}

		if (tutorialStep == TutorialStepIntro)
		{
			if (IsTutorialIntroAdvancePressed())
			{
				DestroyLevelTutorialVisuals();
				tutorialStep = TutorialStepExplore;
				return;
			}

			Vector2 target = new Vector2(avatarPosition.x, avatarPosition.y);
			Vector2 center = GetTutorialBubbleCenter(target, 1.8f);
			UpdateTutorialBubble(
				center,
				new Vector2(8.6f, 2.4f) * TutorialBubbleScale,
				target,
				GetTutorialText(TutorialTextId.Intro),
				TutorialBubbleCharacterSize,
				null,
				false);
			return;
		}

		if (tutorialStep == TutorialStepExplore)
		{
			Vector2 target = new Vector2(avatarPosition.x, avatarPosition.y);
			Vector2 center = GetTutorialBubbleCenter(target, 1.75f);
			UpdateTutorialBubble(
				center,
				new Vector2(5.3f, 1.9f) * TutorialBubbleScale,
				target,
				GetTutorialText(TutorialTextId.Explore),
				TutorialBubbleCharacterSize);
			return;
		}

		if (tutorialStep == TutorialStepConnectNodes)
		{
			TryCompleteTutorialInitialConnection();
		}

		if (tutorialStep == TutorialStepMoveConnection)
		{
			TryCompleteTutorialMovedConnection();
		}

		if (tutorialStep == TutorialStepPickupBlock)
		{
			Vector2 target = GetTutorialPickupBlockTarget();
			Vector2 center = GetTutorialPickupBubbleCenter(target);
			UpdateTutorialBubble(
				center,
				new Vector2(9.6f, 2.55f) * TutorialBubbleScale,
				target,
				GetTutorialPickupBlockText(),
				TutorialBubbleCharacterSize);
			return;
		}

		if (tutorialStep == TutorialStepConnectNodes
			&& TryGetTutorialConnectionTargets(out Vector2 connectionFrom, out Vector2 connectionTo))
		{
			Vector2 size = new Vector2(9.6f, 2.4f) * TutorialBubbleScale;
			Vector2 center = GetTutorialConnectionBubbleCenter(connectionFrom, connectionTo, size);
			UpdateTutorialBubble(
				center,
				size,
				connectionFrom,
				GetTutorialConnectionText(),
				TutorialBubbleCharacterSize,
				connectionTo);
			return;
		}

		if (tutorialStep == TutorialStepMoveConnection
			&& TryGetTutorialMoveConnectionTargets(out Vector2 arcRear, out Vector2 moveTarget))
		{
			Vector2 size = new Vector2(10f, 2.7f) * TutorialBubbleScale;
			Vector2 center = GetTutorialConnectionBubbleCenter(arcRear, moveTarget, size);
			UpdateTutorialBubble(
				center,
				size,
				arcRear,
				GetTutorialMoveConnectionText(),
				TutorialBubbleCharacterSize,
				moveTarget);
			return;
		}

		if (tutorialStep == TutorialStepFireIngredient)
		{
			if (!TryGetTutorialOwnIngredientTransitionTarget(out Vector2 ingredientTransition))
			{
				BeginLevelTutorialOrdersAndDeliveryStep();
				return;
			}

			Vector2 size = new Vector2(8.6f, 2.3f) * TutorialBubbleScale;
			Vector2 center = GetTutorialIngredientBubbleCenter(ingredientTransition);
			UpdateTutorialBubble(
				center,
				size,
				ingredientTransition,
				GetTutorialFireIngredientText(),
				TutorialBubbleCharacterSize);
			return;
		}

		if (tutorialStep == TutorialStepPlayerExchange)
		{
			if (!TryGetTutorialPlayerExchangeTargets(out Vector2 outTransition, out Vector2 inPlace))
			{
				BeginLevelTutorialOrdersAndDeliveryStep();
				return;
			}

			Vector2 size = new Vector2(8.9f, 2.45f) * TutorialBubbleScale;
			Vector2 center = GetTutorialConnectionBubbleCenter(outTransition, inPlace, size);
			UpdateTutorialBubble(
				center,
				size,
				outTransition,
				GetTutorialText(TutorialTextId.PlayerExchange),
				TutorialBubbleCharacterSize,
				inPlace);
			return;
		}

		if (tutorialStep == TutorialStepOrdersAndDelivery)
		{
			GetTutorialRecipeBubbleLayout(
				out Vector2 recipeTarget,
				out Vector2 recipeCenter,
				out Vector2 recipeSize);
			UpdateTutorialBubble(
				recipeCenter,
				recipeSize,
				recipeTarget,
				GetTutorialText(TutorialTextId.Orders),
				TutorialBubbleCharacterSize);

			Vector2 deliveryTarget = GetTutorialDeliveryTarget();
			Vector2 deliveryCenter = GetTutorialDeliveryBubbleCenter(deliveryTarget);
			UpdateTutorialCompanionBubble(
				deliveryCenter,
				new Vector2(6.2f, 2.1f) * TutorialBubbleScale,
				deliveryTarget,
				GetTutorialText(TutorialTextId.Delivery),
				TutorialBubbleCharacterSize);
			return;
		}

		if (tutorialStep == TutorialStepCreateStorage)
		{
			Vector2 target = new Vector2(avatarPosition.x, avatarPosition.y);
			Vector2 center = GetTutorialBubbleCenter(target, 2.1f);
			UpdateTutorialBubble(
				center,
				new Vector2(8.5f, 2.3f) * TutorialBubbleScale,
				target,
				GetTutorialText(TutorialTextId.CreateStorage),
				TutorialBubbleCharacterSize);
			return;
		}

		if (tutorialStep == TutorialStepDeleteEmptyStorage)
		{
			Vector2 target = new Vector2(avatarPosition.x, avatarPosition.y);
			Vector2 center = GetTutorialBubbleCenter(target, 2.1f);
			UpdateTutorialBubble(
				center,
				new Vector2(8.5f, 2.3f) * TutorialBubbleScale,
				target,
				GetTutorialText(TutorialTextId.DeleteStorage),
				TutorialBubbleCharacterSize);
			return;
		}

		if (tutorialStep == TutorialStepTrash)
		{
			UpdateLevelTutorialTrashHint();
			return;
		}

		if (tutorialStep == TutorialStepCompletionMessage)
		{
			if (Time.frameCount > tutorialCompletionMessageStartFrame && IsTutorialCompletionDismissPressed())
			{
				CompleteLevelTutorialCompletionMessageStep();
				return;
			}

			Vector2 target = new Vector2(avatarPosition.x, avatarPosition.y);
			Vector2 center = GetTutorialBubbleCenter(target, 2.1f);
			UpdateTutorialBubble(
				center,
				new Vector2(8.5f, 2.8f) * TutorialBubbleScale,
				target,
				GetTutorialText(TutorialTextId.Completion),
				TutorialBubbleCharacterSize,
				null,
				false);
		}
	}

	private void UpdateLevelSelectionTutorial()
	{
		if (!showLevelSelection || gameplayInitialized)
		{
			tutorialLevelSelectionMovementActiveThisVisit = false;
			return;
		}

		if (!IsSelectedTutorialLevelForLevelSelection()
			|| tutorialSkipped
			|| tutorialLevelSelectionMovementComplete)
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (!tutorialLevelSelectionMovementActiveThisVisit)
		{
			if (levelSelectionMovementTutorialShownThisSession)
			{
				tutorialLevelSelectionMovementComplete = true;
				DestroyLevelTutorialVisuals();
				return;
			}

			levelSelectionMovementTutorialShownThisSession = true;
			tutorialLevelSelectionMovementActiveThisVisit = true;
		}

		Vector2 target = new Vector2(avatarPosition.x, avatarPosition.y);
		string text = GetTutorialText(TutorialTextId.LevelSelectionMovement);
		Vector2 size = GetTightTutorialBubbleSize(text, TutorialBubbleCharacterSize, false);
		Vector2 center = GetLevelSelectionTutorialMovementBubbleCenter(target, size);
		UpdateTutorialBubble(
			center,
			size,
			target,
			text,
			TutorialBubbleCharacterSize,
			null,
			false);
	}

	private void CompleteLevelSelectionTutorialMovementStep()
	{
		if (!IsSelectedTutorialLevelForLevelSelection()
			|| tutorialSkipped
			|| tutorialLevelSelectionMovementComplete)
		{
			return;
		}

		tutorialLevelSelectionMovementComplete = true;
		tutorialLevelSelectionMovementActiveThisVisit = false;
		DestroyLevelTutorialVisuals();
	}

	private void CompleteLevelSelectionTutorialMovementForSession()
	{
		if (tutorialLevelSelectionMovementComplete)
		{
			return;
		}

		tutorialLevelSelectionMovementComplete = true;
		tutorialLevelSelectionMovementActiveThisVisit = false;
		DestroyLevelTutorialVisuals();
	}

	private void EndLevelSelectionTutorialMovementVisit()
	{
		tutorialLevelSelectionMovementActiveThisVisit = false;
		DestroyLevelTutorialVisuals();
	}

	private void CompleteLevelTutorialExploreStep()
	{
		if (!IsTutorialLevelActive() || tutorialSkipped)
		{
			return;
		}

		if (tutorialStep == TutorialStepInactive)
		{
			tutorialStep = TutorialStepIntro;
		}

		if (tutorialStep != TutorialStepExplore)
		{
			return;
		}

		BeginLevelTutorialActionHint(TutorialStepPickupBlock);
	}

	private void CompleteLevelTutorialBlockPickupStep()
	{
		if (!IsTutorialLevelActive() || tutorialSkipped)
		{
			return;
		}

		if (tutorialStep != TutorialStepPickupBlock)
		{
			return;
		}

		if (!IsTutorialExpectedPickupBlockId(heldCompositeBlockId))
		{
			return;
		}

		tutorialPendingBlockId = heldCompositeBlockId;
	}

	private void CompleteLevelTutorialBlockPlacementStep(string blockId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepPickupBlock
			|| string.IsNullOrEmpty(blockId)
			|| !IsTutorialExpectedPickupBlockId(blockId)
			|| (!string.IsNullOrEmpty(tutorialPendingBlockId) && tutorialPendingBlockId != blockId))
		{
			return;
		}

		tutorialPlacedBlockId = blockId;
		tutorialPendingBlockId = null;
		BeginLevelTutorialActionHint(TutorialStepConnectNodes);
	}

	private void CompleteLevelTutorialConnectionStep(string fromId, string toId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| (tutorialStep != TutorialStepConnectNodes && tutorialStep != TutorialStepMoveConnection))
		{
			return;
		}

		if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(toId))
		{
			return;
		}

		if (tutorialStep == TutorialStepConnectNodes)
		{
			TryCompleteTutorialInitialConnection();
		}
		else
		{
			TryCompleteTutorialMovedConnection();
		}
	}

	private void CompleteLevelTutorialIngredientFireStep(string transitionId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepFireIngredient
			|| !IsIngredientTransitionId(transitionId))
		{
			return;
		}

		if (TryGetTutorialExpectedFireIngredientTransitionId(out string expectedTransitionId)
			&& transitionId != expectedTransitionId)
		{
			return;
		}

		BeginLevelTutorialPlayerExchangeStep();
	}

	private void BeginLevelTutorialPlayerExchangeStep()
	{
		if (!IsTutorialMultiplayerSpecificFlowActive()
			|| !TryGetTutorialPlayerExchangeTargets(out _, out _))
		{
			BeginLevelTutorialOrdersAndDeliveryStep();
			return;
		}

		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepPlayerExchange;
	}

	private void CompleteLevelTutorialPlayerExchangeStep(string transitionId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepPlayerExchange
			|| !IsTutorialOwnOutTransitionId(transitionId))
		{
			return;
		}

		BeginLevelTutorialOrdersAndDeliveryStep();
	}

	private void BeginLevelTutorialOrdersAndDeliveryStep()
	{
		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepOrdersAndDelivery;
	}

	private void CompleteLevelTutorialOrdersAndDeliveryStep()
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepOrdersAndDelivery)
		{
			return;
		}

		BeginLevelTutorialActionHint(TutorialStepCreateStorage);
	}

	private void CompleteLevelTutorialCreateStorageStep()
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepCreateStorage
			|| (!pendingCreatedBlockPickup && !IsCreatedCompositeBlockId(heldCompositeBlockId)))
		{
			return;
		}

		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepWaitForStoragePlacement;
	}

	private void CompleteLevelTutorialCreateStoragePlacementStep(string blockId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepWaitForStoragePlacement
			|| !IsCreatedCompositeBlockId(blockId))
		{
			return;
		}

		BeginLevelTutorialDeleteEmptyStorageHint();
	}

	private void CompleteLevelTutorialDeleteEmptyStorageStep()
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepDeleteEmptyStorage)
		{
			return;
		}

		BeginLevelTutorialTrashHint();
	}

	private void CompleteLevelTutorialTrashStep()
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepTrash)
		{
			return;
		}

		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepCompletionMessage;
		tutorialCompletionMessageStartFrame = Time.frameCount;
	}

	private void CompleteLevelTutorialCompletionMessageStep()
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepCompletionMessage)
		{
			return;
		}

		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepDone;
		tutorialCompletionMessageStartFrame = -1;
	}

	private void BeginLevelTutorialActionHint(int nextStep)
	{
		DestroyLevelTutorialVisuals();
		tutorialStep = nextStep;
	}

	private void BeginLevelTutorialDeleteEmptyStorageHint()
	{
		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepDeleteEmptyStorage;
	}

	private void BeginLevelTutorialTrashHint()
	{
		DestroyLevelTutorialVisuals();
		tutorialStep = TutorialStepTrash;
	}

	private void TryCompleteTutorialInitialConnection()
	{
		if (tutorialStep != TutorialStepConnectNodes
			|| !TryGetTutorialInitialConnectionIds(out string fromId, out string toId)
			|| !HasTutorialArc(fromId, toId))
		{
			return;
		}

		AdvanceAfterTutorialInitialConnection();
	}

	private void TryCompleteTutorialMovedConnection()
	{
		if (tutorialStep != TutorialStepMoveConnection
			|| !TryGetTutorialMovedConnectionIds(out string fromId, out string toId)
			|| !HasTutorialArc(fromId, toId))
		{
			return;
		}

		BeginLevelTutorialActionHint(TutorialStepFireIngredient);
	}

	private void CompleteLevelTutorialTrashPickupStep(string transitionId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepTrash
			|| !IsSharedPoolTrashTransitionId(transitionId))
		{
			return;
		}

		CompleteLevelTutorialTrashStep();
	}

	private void CompleteLevelTutorialTrashReturnStep(string transitionId)
	{
		if (!IsTutorialLevelActive()
			|| tutorialSkipped
			|| tutorialStep != TutorialStepTrash
			|| !IsSharedPoolTrashTransitionId(transitionId))
		{
			return;
		}

		CompleteLevelTutorialTrashStep();
	}

	private void UpdateLevelTutorialTrashHint()
	{
		string trashTransitionId = GetSharedPoolTrashTransitionId();
		if (heldTransitionId == trashTransitionId)
		{
			CompleteLevelTutorialTrashStep();
			return;
		}

		Vector2 target = GetTutorialTrashTarget();
		Vector2 center = GetTutorialTrashBubbleCenter(target);
		UpdateTutorialBubble(
			center,
			new Vector2(10.4f, 2.85f) * TutorialBubbleScale,
			target,
			GetTutorialText(TutorialTextId.Trash),
			TutorialBubbleCharacterSize);
	}

	private void SkipCurrentLevelTutorialStep()
	{
		switch (tutorialStep)
		{
			case TutorialStepExplore:
				BeginLevelTutorialActionHint(TutorialStepPickupBlock);
				break;
			case TutorialStepPickupBlock:
				if (string.IsNullOrEmpty(tutorialPlacedBlockId))
				{
					tutorialPlacedBlockId = GetTutorialFallbackCompositeBlockId();
				}

				tutorialPendingBlockId = null;
				BeginLevelTutorialActionHint(TutorialStepConnectNodes);
				break;
			case TutorialStepConnectNodes:
				AdvanceAfterTutorialInitialConnection();
				break;
			case TutorialStepMoveConnection:
				BeginLevelTutorialActionHint(TutorialStepFireIngredient);
				break;
			case TutorialStepFireIngredient:
				BeginLevelTutorialPlayerExchangeStep();
				break;
			case TutorialStepPlayerExchange:
				BeginLevelTutorialOrdersAndDeliveryStep();
				break;
			case TutorialStepOrdersAndDelivery:
				CompleteLevelTutorialOrdersAndDeliveryStep();
				break;
			case TutorialStepCreateStorage:
			case TutorialStepWaitForStoragePlacement:
				BeginLevelTutorialDeleteEmptyStorageHint();
				break;
			case TutorialStepDeleteEmptyStorage:
				BeginLevelTutorialTrashHint();
				break;
			case TutorialStepTrash:
				CompleteLevelTutorialTrashStep();
				break;
			default:
				DestroyLevelTutorialVisuals();
				break;
		}
	}

	private string GetTutorialFallbackCompositeBlockId()
	{
		if (!string.IsNullOrEmpty(heldCompositeBlockId) && IsKnownCompositeBlockId(heldCompositeBlockId))
		{
			return heldCompositeBlockId;
		}

		if (!string.IsNullOrEmpty(tutorialPendingBlockId) && IsKnownCompositeBlockId(tutorialPendingBlockId))
		{
			return tutorialPendingBlockId;
		}

		Vector2 actorPosition = new Vector2(avatarPosition.x, avatarPosition.y);
		ulong actorClientId = GetLocalActorClientId();
		string bestBlockId = null;
		float bestScore = float.MaxValue;
		List<string> blockIds = GetAllCompositeBlockIds();
		for (int i = 0; i < blockIds.Count; i++)
		{
			string blockId = blockIds[i];
			if (string.IsNullOrEmpty(blockId) || !CanActorPickupCompositeBlock(blockId, actorClientId))
			{
				continue;
			}

			string[] nodeIds = GetCompositeBlockNodeIds(blockId);
			if (nodeIds == null
				|| nodeIds.Length <= 0
				|| !nodesById.TryGetValue(nodeIds[0], out NodeRuntime firstNode)
				|| firstNode == null
				|| firstNode.transform == null)
			{
				continue;
			}

			float poolPenalty = IsCompositeBlockInSharedPool(blockId) ? 100f : 0f;
			float score = Vector2.Distance(actorPosition, GetCompositeBlockCenter(blockId)) + poolPenalty;
			if (score < bestScore)
			{
				bestScore = score;
				bestBlockId = blockId;
			}
		}

		return bestBlockId;
	}

	private void UpdateInhibitorArcHint()
	{
		if (inhibitorArcHintFinished)
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (IsLocalHeldBlockConnectedToInhibitorArc())
		{
			inhibitorArcHintFinished = true;
			DestroyLevelTutorialVisuals();
			return;
		}

		if (!TryGetInhibitorArcHintTarget(out Vector2 target))
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (IsTutorialStepSkipPressed())
		{
			inhibitorArcHintFinished = true;
			DestroyLevelTutorialVisuals();
			return;
		}

		Vector2 center = GetInhibitorArcHintBubbleCenter(target);
		UpdateTutorialBubble(
			center,
			new Vector2(10f, 3f) * TutorialBubbleScale,
			target,
			GetTutorialText(TutorialTextId.InhibitorArc),
			TutorialBubbleCharacterSize);
	}

	private bool TryGetInhibitorArcHintTarget(out Vector2 target)
	{
		target = Vector2.zero;

		foreach (ArcRuntime arc in arcsById.Values)
		{
			if (arc == null || arc.kind != ArcKind.Inhibitor)
			{
				continue;
			}

			if (!TryGetArcHintTarget(arc, out target))
			{
				continue;
			}

			return true;
		}

		return false;
	}

	private void UpdateWeightedArcHint()
	{
		if (weightedArcHintFinished)
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (IsLocalHeldBlockConnectedToWeightedArc())
		{
			weightedArcHintFinished = true;
			DestroyLevelTutorialVisuals();
			return;
		}

		if (!TryGetWeightedArcHintTarget(out Vector2 target))
		{
			DestroyLevelTutorialVisuals();
			return;
		}

		if (IsTutorialStepSkipPressed())
		{
			weightedArcHintFinished = true;
			DestroyLevelTutorialVisuals();
			return;
		}

		Vector2 center = GetWeightedArcHintBubbleCenter(target);
		UpdateTutorialBubble(
			center,
			new Vector2(9.8f, 2.7f) * TutorialBubbleScale,
			target,
			GetTutorialText(TutorialTextId.WeightedArc),
			TutorialBubbleCharacterSize);
	}

	private bool TryGetWeightedArcHintTarget(out Vector2 target)
	{
		target = Vector2.zero;
		foreach (ArcRuntime arc in arcsById.Values)
		{
			if (arc == null || arc.kind == ArcKind.Inhibitor || arc.weight <= 1)
			{
				continue;
			}

			if (arc.weightLabel != null && arc.weightLabel.gameObject.activeInHierarchy)
			{
				Vector3 labelPosition = arc.weightLabel.transform.position;
				target = new Vector2(labelPosition.x, labelPosition.y);
				return true;
			}

			if (TryGetArcHintTarget(arc, out target))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryGetArcHintTarget(ArcRuntime arc, out Vector2 target)
	{
		target = Vector2.zero;
		if (arc == null)
		{
			return false;
		}

		if (arc.body != null && arc.body.positionCount >= 2)
		{
			Vector3 start = arc.body.GetPosition(0);
			Vector3 end = arc.body.GetPosition(arc.body.positionCount - 1);
			target = new Vector2((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f);
			return true;
		}

		if (nodesById.TryGetValue(arc.fromId, out NodeRuntime fromNode)
			&& nodesById.TryGetValue(arc.toId, out NodeRuntime toNode)
			&& fromNode != null
			&& toNode != null
			&& fromNode.transform != null
			&& toNode.transform != null)
		{
			Vector3 start = fromNode.transform.position;
			Vector3 end = toNode.transform.position;
			target = new Vector2((start.x + end.x) * 0.5f, (start.y + end.y) * 0.5f);
			return true;
		}

		return false;
	}

	private Vector2 GetInhibitorArcHintBubbleCenter(Vector2 target)
	{
		float sideDirection = IsActorTopSide(GetLocalActorClientId()) ? 1f : -1f;
		return target + new Vector2(3.8f, sideDirection * 5.1f);
	}

	private Vector2 GetWeightedArcHintBubbleCenter(Vector2 target)
	{
		float sideDirection = IsActorTopSide(GetLocalActorClientId()) ? 1f : -1f;
		return target + new Vector2(3.2f, sideDirection * 4.2f);
	}

	private bool IsLocalHeldBlockConnectedToInhibitorArc()
	{
		return IsLocalHeldBlockConnectedToMatchingArc(
			arc => arc != null && arc.kind == ArcKind.Inhibitor);
	}

	private bool IsLocalHeldBlockConnectedToWeightedArc()
	{
		return IsLocalHeldBlockConnectedToMatchingArc(
			arc => arc != null && arc.kind != ArcKind.Inhibitor && arc.weight > 1);
	}

	private bool IsLocalHeldBlockConnectedToMatchingArc(System.Func<ArcRuntime, bool> matchesArc)
	{
		if (string.IsNullOrEmpty(heldCompositeBlockId) || matchesArc == null)
		{
			return false;
		}

		foreach (ArcRuntime arc in arcsById.Values)
		{
			if (!matchesArc(arc))
			{
				continue;
			}

			if (GetCompositeBlockIdForNodeId(arc.fromId) == heldCompositeBlockId
				|| GetCompositeBlockIdForNodeId(arc.toId) == heldCompositeBlockId)
			{
				return true;
			}
		}

		return false;
	}

	private bool IsTutorialLevelActive()
	{
		return showLevelSelection && activeLevelId == TutorialLevelId;
	}

	private bool IsSelectedTutorialLevelForLevelSelection()
	{
		if (!showLevelSelection || gameplayInitialized)
		{
			return false;
		}

		List<PetriNetLevelDefinition> levels = GetLevelDefinitions();
		if (levels == null || levels.Count <= 0)
		{
			return false;
		}

		int safeIndex = Mathf.Clamp(selectedLevelIndex, 0, levels.Count - 1);
		PetriNetLevelDefinition selectedLevel = levels[safeIndex];
		return selectedLevel != null && selectedLevel.id == TutorialLevelId;
	}

	private bool IsInhibitorArcHintLevelActive()
	{
		return showLevelSelection
			&& levelInhibitorArcs != null
			&& levelInhibitorArcs.Count > 0;
	}

	private bool IsWeightedArcHintLevelActive()
	{
		return showLevelSelection
			&& IsLastCatalogLevelActive()
			&& HasWeightedArcForHint();
	}

	private bool IsLastCatalogLevelActive()
	{
		if (PetriNetLevelCatalog.Levels == null || PetriNetLevelCatalog.Levels.Count <= 0)
		{
			return false;
		}

		PetriNetLevelDefinition lastLevel = PetriNetLevelCatalog.Levels[PetriNetLevelCatalog.Levels.Count - 1];
		return lastLevel != null && activeLevelId == lastLevel.id;
	}

	private bool HasWeightedArcForHint()
	{
		foreach (ArcRuntime arc in arcsById.Values)
		{
			if (arc != null && arc.kind != ArcKind.Inhibitor && arc.weight > 1)
			{
				return true;
			}
		}

		return false;
	}

	private void ResetLevelTutorialState()
	{
		tutorialStep = TutorialStepInactive;
		tutorialSkipped = false;
		tutorialCompletionMessageStartFrame = -1;
		tutorialPendingBlockId = null;
		tutorialPlacedBlockId = null;
		inhibitorArcHintFinished = false;
		weightedArcHintFinished = false;
		DestroyLevelTutorialVisuals();
	}

	private void DestroyLevelTutorialVisuals()
	{
		if (tutorialBubbleRoot != null)
		{
			Destroy(tutorialBubbleRoot);
		}

		if (tutorialCompanionBubbleRoot != null)
		{
			Destroy(tutorialCompanionBubbleRoot);
		}

		tutorialBubbleRoot = null;
		tutorialBubbleFill = null;
		tutorialBubbleBorder = null;
		tutorialBubbleDotFills = null;
		tutorialBubbleDotBorders = null;
		tutorialBubbleSecondDotFills = null;
		tutorialBubbleSecondDotBorders = null;
		tutorialBubbleText = null;
		tutorialBubbleSkipText = null;
		tutorialCompanionBubbleRoot = null;
		tutorialCompanionBubbleFill = null;
		tutorialCompanionBubbleBorder = null;
		tutorialCompanionBubbleDotFills = null;
		tutorialCompanionBubbleDotBorders = null;
		tutorialCompanionBubbleSecondDotFills = null;
		tutorialCompanionBubbleSecondDotBorders = null;
		tutorialCompanionBubbleText = null;
		tutorialCompanionBubbleSkipText = null;
	}

	private Vector2 GetTutorialBubbleCenter(Vector2 target, float distanceFromTarget)
	{
		bool topSide = target.y >= sharedPoolY;
		float direction = topSide ? 1f : -1f;
		return target + new Vector2(1.2f, direction * (distanceFromTarget + 2.4f));
	}

	private Vector2 GetLevelSelectionTutorialMovementBubbleCenter(Vector2 target, Vector2 size)
	{
		float direction = IsActorTopSide(GetLocalActorClientId()) ? -1f : 1f;
		Vector2 center = target + new Vector2(2.5f, direction * 2.2f);
		Rect bounds = levelSelectionMovementBounds.width > 0.001f && levelSelectionMovementBounds.height > 0.001f
			? levelSelectionMovementBounds
			: Rect.MinMaxRect(-4.5f, -3.5f, 4.5f, 3.5f);
		Vector2 halfSize = size * 0.5f;
		const float padding = 0.35f;
		float minX = bounds.xMin + halfSize.x + padding;
		float maxX = bounds.xMax - halfSize.x - padding;
		float minY = bounds.yMin + halfSize.y + padding;
		float maxY = bounds.yMax - halfSize.y - padding;
		center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : bounds.center.x;
		center.y = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : bounds.center.y;
		return center;
	}

	private Vector2 GetTutorialPickupBubbleCenter(Vector2 target)
	{
		float direction = IsActorTopSide(GetLocalActorClientId()) ? 1f : -1f;
		float centerY = sharedPoolY + direction * (playerZoneYSpacing + 3.4f);
		return new Vector2(target.x + 1.2f, centerY);
	}

	private Vector2 GetTutorialSharedPoolBlocksTarget()
	{
		int count = GetPoolBlockCount();
		if (count <= 0)
		{
			return new Vector2(0f, sharedPoolY);
		}

		Vector2 sum = Vector2.zero;
		for (int i = 0; i < count; i++)
		{
			sum += GetSharedPoolBlockSlotPositionByIndex(i);
		}

		return sum / count;
	}

	private Vector2 GetTutorialPickupBlockTarget()
	{
		if (TryGetTutorialExpectedPickupBlock(out _, out Vector2 target))
		{
			return target;
		}

		return GetTutorialSharedPoolBlocksTarget();
	}

	private string GetTutorialPickupBlockText()
	{
		if (!IsTutorialGuidedBlockFlowActive())
		{
			return GetTutorialText(TutorialTextId.PickupGeneric);
		}

		if (singlePlayerMode)
		{
			return GetTutorialText(TutorialTextId.PickupCuttingSinglePlayer);
		}

		if (IsActorTopSide(GetLocalActorClientId()))
		{
			return GetTutorialText(TutorialTextId.PickupCutting);
		}

		return GetTutorialText(TutorialTextId.PickupCooking);
	}

	private string GetTutorialConnectionText()
	{
		if (!IsTutorialGuidedBlockFlowActive())
		{
			return GetTutorialText(TutorialTextId.ConnectGeneric);
		}

		if (singlePlayerMode)
		{
			return GetTutorialText(TutorialTextId.ConnectSinglePlayer);
		}

		if (IsActorTopSide(GetLocalActorClientId()))
		{
			return GetTutorialText(TutorialTextId.ConnectTopPlayer);
		}

		return GetTutorialText(TutorialTextId.ConnectBottomPlayer);
	}

	private string GetTutorialMoveConnectionText()
	{
		if (singlePlayerMode)
		{
			return GetTutorialText(TutorialTextId.MoveConnectionToPotatoesSinglePlayer);
		}

		if (IsActorTopSide(GetLocalActorClientId()))
		{
			return GetTutorialText(TutorialTextId.MoveConnectionToPotatoes);
		}

		return GetTutorialText(TutorialTextId.MoveConnectionToIncoming);
	}

	private string GetTutorialFireIngredientText()
	{
		if (NamesMatch(GetTutorialExpectedFireIngredientName(), "Suppengemüse"))
		{
			return GetTutorialText(TutorialTextId.FireSoupVegetables);
		}

		return GetTutorialText(TutorialTextId.FirePotatoes);
	}

	private string GetTutorialExpectedFireIngredientName()
	{
		return IsTutorialExpectedFireIngredientTopSide()
			? "Kartoffeln"
			: "Suppengemüse";
	}

	private bool IsTutorialExpectedFireIngredientTopSide()
	{
		return singlePlayerMode || IsActorTopSide(GetLocalActorClientId());
	}

	private bool TryGetTutorialPlayerExchangeTargets(out Vector2 outTransition, out Vector2 inPlace)
	{
		bool topSide = IsActorTopSide(GetLocalActorClientId());
		bool hasOutTransition = TryGetTutorialNodePosition(topSide ? "T_Top_Out" : "T_Bottom_Out", out outTransition);
		bool hasInPlace = TryGetTutorialNodePosition(topSide ? "P_Top_In" : "P_Bottom_In", out inPlace);
		return hasOutTransition && hasInPlace;
	}

	private bool IsTutorialOwnOutTransitionId(string transitionId)
	{
		if (string.IsNullOrEmpty(transitionId))
		{
			return false;
		}

		return IsActorTopSide(GetLocalActorClientId())
			? transitionId == "T_Top_Out"
			: transitionId == "T_Bottom_Out";
	}

	private bool IsTutorialMultiplayerSpecificFlowActive()
	{
		return !singlePlayerMode
			&& nodesById.ContainsKey("P_Top_In")
			&& nodesById.ContainsKey("P_Bottom_In");
	}

	private bool IsTutorialSinglePlayerSpecificFlowActive()
	{
		return singlePlayerMode
			&& TryGetTutorialIngredientPlaceId(true, "Kartoffeln", out _)
			&& TryGetTutorialIngredientPlaceId(false, "Suppengemüse", out _);
	}

	private bool IsTutorialGuidedBlockFlowActive()
	{
		return IsTutorialMultiplayerSpecificFlowActive() || IsTutorialSinglePlayerSpecificFlowActive();
	}

	private bool TryGetTutorialExpectedPickupBlock(out string blockId, out Vector2 target)
	{
		blockId = null;
		target = Vector2.zero;
		if (!IsTutorialGuidedBlockFlowActive())
		{
			return false;
		}

		if (!TryFindTutorialExpectedPickupBlockId(out blockId))
		{
			return false;
		}

		if (!TryGetCompositeBlockBounds(blockId, out Rect bounds))
		{
			return false;
		}

		target = bounds.center;
		return true;
	}

	private bool TryFindTutorialExpectedPickupBlockId(out string blockId)
	{
		blockId = null;
		bool wantsCuttingBlock = singlePlayerMode || IsActorTopSide(GetLocalActorClientId());
		List<string> blockIds = GetAllCompositeBlockIds();
		for (int i = 0; i < blockIds.Count; i++)
		{
			PoolBlockDefinition definition = GetCompositeBlockDefinition(blockIds[i]);
			if (definition == null)
			{
				continue;
			}

			string firstTransitionName = GetPoolBlockFirstTransitionName(definition);
			bool matches = wantsCuttingBlock
				? IsCuttingName(firstTransitionName)
				: firstTransitionName.IndexOf("koch", System.StringComparison.OrdinalIgnoreCase) >= 0;
			if (!matches)
			{
				continue;
			}

			blockId = blockIds[i];
			return true;
		}

		string fallbackName = wantsCuttingBlock ? "Schneiden Start" : "Kochen Start";
		return TryFindCompositeBlockByFirstTransitionName(fallbackName, out blockId);
	}

	private bool IsTutorialExpectedPickupBlockId(string blockId)
	{
		if (string.IsNullOrEmpty(blockId))
		{
			return false;
		}

		if (!IsTutorialGuidedBlockFlowActive())
		{
			return true;
		}

		return TryGetTutorialExpectedPickupBlock(out string expectedBlockId, out _)
			&& blockId == expectedBlockId;
	}

	private void AdvanceAfterTutorialInitialConnection()
	{
		if (IsTutorialGuidedBlockFlowActive())
		{
			BeginLevelTutorialActionHint(TutorialStepMoveConnection);
			return;
		}

		BeginLevelTutorialActionHint(TutorialStepFireIngredient);
	}

	private bool TryGetTutorialInitialConnectionIds(out string fromId, out string toId)
	{
		fromId = null;
		toId = null;

		if (IsTutorialGuidedBlockFlowActive())
		{
			bool topSide = IsActorTopSide(GetLocalActorClientId());
			if (!TryGetTutorialBlockFirstTransitionId(out toId))
			{
				return false;
			}

			if (singlePlayerMode)
			{
				if (!TryGetTutorialIngredientPlaceId(false, "Suppengemüse", out fromId))
				{
					return false;
				}
			}
			else if (topSide)
			{
				fromId = "P_Top_In";
			}
			else if (!TryGetTutorialIngredientPlaceId(false, "Suppengemüse", out fromId))
			{
				return false;
			}

			return nodesById.ContainsKey(fromId) && nodesById.ContainsKey(toId);
		}

		return TryGetTutorialGenericConnectionIds(out fromId, out toId);
	}

	private bool TryGetTutorialMovedConnectionIds(out string fromId, out string toId)
	{
		fromId = null;
		toId = null;
		if (!IsTutorialGuidedBlockFlowActive()
			|| !TryGetTutorialBlockFirstTransitionId(out toId))
		{
			return false;
		}

		if (singlePlayerMode || IsActorTopSide(GetLocalActorClientId()))
		{
			return TryGetTutorialIngredientPlaceId(true, "Kartoffeln", out fromId);
		}

		fromId = "P_Bottom_In";
		return nodesById.ContainsKey(fromId);
	}

	private bool TryGetTutorialGenericConnectionIds(out string fromId, out string toId)
	{
		fromId = null;
		toId = null;
		if (!TryGetTutorialBlockFirstTransitionId(out toId)
			|| !TryGetTutorialNodePosition(toId, out Vector2 transitionPosition))
		{
			return false;
		}

		bool ownTopSide = IsActorTopSide(GetLocalActorClientId());
		string ownIngredientPrefix = ownTopSide ? "P_Top_Zutat_" : "P_Bottom_Zutat_";
		NodeRuntime closestIngredientPlace = null;
		float closestDistance = float.MaxValue;
		foreach (NodeRuntime node in nodesById.Values)
		{
			if (node == null
				|| node.type != NodeType.Place
				|| node.transform == null
				|| string.IsNullOrEmpty(node.id)
				|| !node.id.StartsWith(ownIngredientPrefix))
			{
				continue;
			}

			float distance = Vector2.Distance(transitionPosition, node.transform.position);
			if (distance < closestDistance)
			{
				closestDistance = distance;
				closestIngredientPlace = node;
			}
		}

		if (closestIngredientPlace == null)
		{
			return false;
		}

		fromId = closestIngredientPlace.id;
		return true;
	}

	private bool TryGetTutorialBlockFirstTransitionId(out string transitionId)
	{
		transitionId = null;
		string blockId = tutorialPlacedBlockId;
		if (IsTutorialGuidedBlockFlowActive())
		{
			if (!TryGetTutorialExpectedPickupBlock(out string expectedBlockId, out _))
			{
				return false;
			}

			blockId = expectedBlockId;
		}
		else if (string.IsNullOrEmpty(blockId))
		{
			blockId = GetTutorialFallbackCompositeBlockId();
		}

		string[] blockNodeIds = GetCompositeBlockNodeIds(blockId);
		if (blockNodeIds == null
			|| blockNodeIds.Length <= 0
			|| !nodesById.TryGetValue(blockNodeIds[0], out NodeRuntime firstTransition)
			|| firstTransition == null
			|| firstTransition.type != NodeType.Transition)
		{
			return false;
		}

		transitionId = firstTransition.id;
		return true;
	}

	private bool TryGetTutorialIngredientPlaceId(bool topSide, string ingredientName, out string placeId)
	{
		placeId = null;
		string prefix = topSide ? "P_Top_Zutat_" : "P_Bottom_Zutat_";
		foreach (NodeRuntime node in nodesById.Values)
		{
			if (node == null
				|| node.type != NodeType.Place
				|| string.IsNullOrEmpty(node.id)
				|| !node.id.StartsWith(prefix))
			{
				continue;
			}

			if (NamesMatch(GetIngredientDisplayNameForNodeId(node.id), ingredientName))
			{
				placeId = node.id;
				return true;
			}
		}

		return false;
	}

	private bool TryGetTutorialNodePosition(string nodeId, out Vector2 position)
	{
		position = Vector2.zero;
		if (!nodesById.TryGetValue(nodeId, out NodeRuntime node)
			|| node == null
			|| node.transform == null)
		{
			return false;
		}

		position = node.transform.position;
		return true;
	}

	private bool HasTutorialArc(string fromId, string toId)
	{
		return TryFindTutorialArc(fromId, toId, out _);
	}

	private bool TryFindTutorialArc(string fromId, string toId, out ArcRuntime foundArc)
	{
		foundArc = null;
		foreach (ArcRuntime arc in arcsById.Values)
		{
			if (arc == null || arc.fromId != fromId || arc.toId != toId)
			{
				continue;
			}

			foundArc = arc;
			return true;
		}

		return false;
	}

	private Vector2 GetTutorialConnectionBubbleCenter(Vector2 firstTarget, Vector2 secondTarget, Vector2 bubbleSize)
	{
		Vector2 midpoint = (firstTarget + secondTarget) * 0.5f;
		float sideDirection = IsActorTopSide(GetLocalActorClientId()) ? 1f : -1f;
		return midpoint + new Vector2(1.6f, sideDirection * (bubbleSize.y * 0.5f + 2.6f));
	}

	private Vector2 GetTutorialIngredientBubbleCenter(Vector2 target)
	{
		float sideDirection = IsActorTopSide(GetLocalActorClientId()) ? 1f : -1f;
		return target + new Vector2(12.5f, sideDirection * 3.5f);
	}

	private Vector2 GetTutorialTrashTarget()
	{
		if (nodesById.TryGetValue(GetSharedPoolTrashTransitionId(), out NodeRuntime trashTransition)
			&& trashTransition != null
			&& trashTransition.transform != null
			&& (!trashTransition.isSharedPoolTransition || trashTransition.isSharedPoolAvailable))
		{
			return trashTransition.transform.position;
		}

		return GetSharedPoolTrashTransitionPosition();
	}

	private Vector2 GetTutorialTrashBubbleCenter(Vector2 target)
	{
		float sideDirection = IsActorTopSide(GetLocalActorClientId()) ? 1f : -1f;
		return target + new Vector2(5.2f, sideDirection * 4.6f);
	}

	private void GetTutorialRecipeBubbleLayout(
		out Vector2 recipeTarget,
		out Vector2 recipeCenter,
		out Vector2 recipeSize)
	{
		float targetViewportX = 0.38f;
		float targetViewportY = 0.82f;
		float orderWidthViewport = 0.38f;
		if (TryGetTutorialOrderScreenBounds(out Rect orderBounds)
			&& Screen.width > 0
			&& Screen.height > 0)
		{
			targetViewportX = Mathf.Clamp(orderBounds.xMax / Screen.width, 0.08f, 0.9f);
			targetViewportY = Mathf.Clamp(orderBounds.center.y / Screen.height, 0.2f, 0.92f);
			orderWidthViewport = Mathf.Clamp(
				(orderBounds.width + LevelOrderCardMargin * 2f) / Screen.width,
				0.38f,
				0.68f);
		}

		Vector3 leftEdge = GetCameraGroundViewportPoint(new Vector2(0f, 0.5f));
		Vector3 rightEdge = GetCameraGroundViewportPoint(new Vector2(1f, 0.5f));
		float viewWidth = Mathf.Abs(rightEdge.x - leftEdge.x);
		float connectorWidth = GetTutorialThoughtDotTotalDiameter()
			+ TutorialBubbleDotMinimumGap * (TutorialBubbleDotCount + 1);
		float connectorWidthViewport = connectorWidth / Mathf.Max(0.001f, viewWidth) + 0.015f;
		float desiredBubbleWidth = Mathf.Clamp(viewWidth * orderWidthViewport, 6.8f, 8.8f * TutorialBubbleScale);
		float availableRightWidth = viewWidth * Mathf.Max(
			0.18f,
			0.97f - targetViewportX - connectorWidthViewport);
		float bubbleWidth = Mathf.Min(desiredBubbleWidth, Mathf.Max(6.8f, availableRightWidth));
		float bubbleHeight = 2.25f * TutorialBubbleScale;
		float bubbleWidthViewport = bubbleWidth / Mathf.Max(0.001f, viewWidth);
		float centerViewportX = Mathf.Clamp(
			targetViewportX + connectorWidthViewport + bubbleWidthViewport * 0.5f,
			bubbleWidthViewport * 0.5f + 0.03f,
			0.97f - bubbleWidthViewport * 0.5f);
		float centerViewportY = Mathf.Clamp(targetViewportY, 0.2f, 0.82f);

		Vector3 target = GetCameraGroundViewportPoint(new Vector2(targetViewportX, targetViewportY));
		Vector3 center = GetCameraGroundViewportPoint(new Vector2(centerViewportX, centerViewportY));
		recipeTarget = new Vector2(target.x, target.y);
		recipeCenter = new Vector2(center.x, center.y);
		recipeSize = new Vector2(bubbleWidth, bubbleHeight);
	}

	private bool TryGetTutorialOrderScreenBounds(out Rect bounds)
	{
		bounds = default;
		bool hasBounds = false;
		Vector3[] corners = new Vector3[4];
		for (int i = 0; i < levelOrderCardObjects.Count; i++)
		{
			GameObject card = levelOrderCardObjects[i];
			if (card == null || !card.activeInHierarchy)
			{
				continue;
			}

			RectTransform rect = card.transform as RectTransform;
			if (rect == null)
			{
				continue;
			}

			rect.GetWorldCorners(corners);
			Canvas canvas = rect.GetComponentInParent<Canvas>();
			Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
				? canvas.worldCamera
				: null;
			for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
			{
				Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[cornerIndex]);
				if (!hasBounds)
				{
					bounds = new Rect(screenPoint, Vector2.zero);
					hasBounds = true;
				}
				else
				{
					bounds.xMin = Mathf.Min(bounds.xMin, screenPoint.x);
					bounds.xMax = Mathf.Max(bounds.xMax, screenPoint.x);
					bounds.yMin = Mathf.Min(bounds.yMin, screenPoint.y);
					bounds.yMax = Mathf.Max(bounds.yMax, screenPoint.y);
				}
			}
		}

		return hasBounds;
	}

	private Vector2 GetTutorialDeliveryTarget()
	{
		if (nodesById.TryGetValue("T_Bottom_Ausliefern", out NodeRuntime delivery)
			&& delivery != null
			&& delivery.transform != null)
		{
			return delivery.transform.position;
		}

		return GetDeliveryTransitionPosition();
	}

	private Vector2 GetTutorialDeliveryBubbleCenter(Vector2 target)
	{
		return target + new Vector2(-8.5f, 3.8f);
	}

	private bool TryGetTutorialOwnIngredientTransitionTarget(out Vector2 ingredientTransitionPosition)
	{
		ingredientTransitionPosition = Vector2.zero;
		if (TryGetTutorialExpectedFireIngredientTransitionId(out string expectedTransitionId)
			&& TryGetTutorialNodePosition(expectedTransitionId, out ingredientTransitionPosition))
		{
			return true;
		}

		bool ownTopSide = IsActorTopSide(GetLocalActorClientId());
		string ownIngredientPrefix = ownTopSide ? "T_Top_Zutat_" : "T_Bottom_Zutat_";
		NodeRuntime firstIngredientTransition = null;
		int bestNumber = int.MaxValue;

		foreach (NodeRuntime node in nodesById.Values)
		{
			if (node == null
				|| node.type != NodeType.Transition
				|| node.transform == null
				|| string.IsNullOrEmpty(node.id)
				|| !node.id.StartsWith(ownIngredientPrefix))
			{
				continue;
			}

			int number = ExtractTrailingNumber(node.id);
			if (number < bestNumber)
			{
				bestNumber = number;
				firstIngredientTransition = node;
			}
		}

		if (firstIngredientTransition == null)
		{
			return false;
		}

		ingredientTransitionPosition = firstIngredientTransition.transform.position;
		return true;
	}

	private bool TryGetTutorialExpectedFireIngredientTransitionId(out string transitionId)
	{
		transitionId = null;
		if (!IsTutorialGuidedBlockFlowActive())
		{
			return false;
		}

		return TryFindTutorialIngredientTransitionId(
			IsTutorialExpectedFireIngredientTopSide(),
			GetTutorialExpectedFireIngredientName(),
			out transitionId);
	}

	private bool TryFindTutorialIngredientTransitionId(bool topSide, string ingredientName, out string transitionId)
	{
		transitionId = null;
		string prefix = topSide ? "T_Top_Zutat_" : "T_Bottom_Zutat_";
		foreach (NodeRuntime node in nodesById.Values)
		{
			if (node == null
				|| node.type != NodeType.Transition
				|| string.IsNullOrEmpty(node.id)
				|| !node.id.StartsWith(prefix))
			{
				continue;
			}

			if (NamesMatch(GetIngredientDisplayNameForNodeId(node.id), ingredientName))
			{
				transitionId = node.id;
				return true;
			}
		}

		return false;
	}

	private bool TryGetTutorialConnectionTargets(out Vector2 ingredientPlacePosition, out Vector2 blockTransitionPosition)
	{
		ingredientPlacePosition = Vector2.zero;
		blockTransitionPosition = Vector2.zero;
		if (!TryGetTutorialInitialConnectionIds(out string fromId, out string toId)
			|| !TryGetTutorialNodePosition(fromId, out ingredientPlacePosition)
			|| !TryGetTutorialNodePosition(toId, out blockTransitionPosition))
		{
			return false;
		}

		return true;
	}

	private bool TryGetTutorialMoveConnectionTargets(out Vector2 arcRearPosition, out Vector2 targetPlacePosition)
	{
		arcRearPosition = Vector2.zero;
		targetPlacePosition = Vector2.zero;
		if (!TryGetTutorialInitialConnectionIds(out string oldFromId, out string toId)
			|| !TryGetTutorialMovedConnectionIds(out string newFromId, out _)
			|| !TryGetTutorialNodePosition(newFromId, out targetPlacePosition))
		{
			return false;
		}

		if (TryFindTutorialArc(oldFromId, toId, out ArcRuntime arc)
			&& TryGetTutorialArcRearTarget(arc, out arcRearPosition))
		{
			return true;
		}

		if (!TryGetTutorialNodePosition(oldFromId, out Vector2 oldFromPosition)
			|| !TryGetTutorialNodePosition(toId, out Vector2 toPosition))
		{
			return false;
		}

		arcRearPosition = Vector2.Lerp(oldFromPosition, toPosition, 0.28f);
		return true;
	}

	private bool TryGetTutorialArcRearTarget(ArcRuntime arc, out Vector2 target)
	{
		target = Vector2.zero;
		if (!TryGetArcSegment(arc, out Vector3 start, out Vector3 end))
		{
			return false;
		}

		target = Vector2.Lerp(new Vector2(start.x, start.y), new Vector2(end.x, end.y), 0.28f);
		return true;
	}

	private void EnsureTutorialBubbleVisual()
	{
		if (tutorialBubbleRoot != null)
		{
			return;
		}

		EnsureGraphRootExists();
		tutorialBubbleRoot = new GameObject("LevelTutorialBubble");
		tutorialBubbleRoot.transform.SetParent(petriNetRoot, false);

		GameObject fillObject = new GameObject("Fill");
		fillObject.transform.SetParent(tutorialBubbleRoot.transform, false);
		tutorialBubbleFill = fillObject.AddComponent<SpriteRenderer>();
		tutorialBubbleFill.sprite = GetCircleSprite();
		tutorialBubbleFill.color = Color.white;
		tutorialBubbleFill.sortingOrder = TutorialBubbleFillSortingOrder;

		GameObject borderObject = new GameObject("Border");
		borderObject.transform.SetParent(tutorialBubbleRoot.transform, false);
		tutorialBubbleBorder = borderObject.AddComponent<LineRenderer>();
		ConfigureGroundLineRenderer(
			tutorialBubbleBorder,
			TutorialBubbleOutlineSegments,
			0.095f,
			TutorialBubbleLineSortingOrder,
			new Color(0.05f, 0.06f, 0.07f, 0.95f),
			8,
			8,
			true);

		tutorialBubbleDotFills = new SpriteRenderer[TutorialBubbleDotCount];
		tutorialBubbleDotBorders = new LineRenderer[TutorialBubbleDotCount];
		tutorialBubbleSecondDotFills = new SpriteRenderer[TutorialBubbleDotCount];
		tutorialBubbleSecondDotBorders = new LineRenderer[TutorialBubbleDotCount];
		for (int i = 0; i < TutorialBubbleDotCount; i++)
		{
			GameObject dotFillObject = new GameObject("ThoughtDotFill_" + (i + 1));
			dotFillObject.transform.SetParent(tutorialBubbleRoot.transform, false);
			SpriteRenderer dotFill = dotFillObject.AddComponent<SpriteRenderer>();
			dotFill.sprite = GetCircleSprite();
			dotFill.color = Color.white;
			dotFill.sortingOrder = TutorialBubbleFillSortingOrder;
			tutorialBubbleDotFills[i] = dotFill;

			GameObject dotBorderObject = new GameObject("ThoughtDotBorder_" + (i + 1));
			dotBorderObject.transform.SetParent(tutorialBubbleRoot.transform, false);
			LineRenderer dotBorder = dotBorderObject.AddComponent<LineRenderer>();
			ConfigureGroundLineRenderer(
				dotBorder,
				TutorialBubbleDotOutlineSegments,
				0.065f,
				TutorialBubbleLineSortingOrder,
				new Color(0.05f, 0.06f, 0.07f, 0.95f),
				8,
				8,
				true);
			tutorialBubbleDotBorders[i] = dotBorder;

			GameObject secondDotFillObject = new GameObject("SecondThoughtDotFill_" + (i + 1));
			secondDotFillObject.transform.SetParent(tutorialBubbleRoot.transform, false);
			SpriteRenderer secondDotFill = secondDotFillObject.AddComponent<SpriteRenderer>();
			secondDotFill.sprite = GetCircleSprite();
			secondDotFill.color = Color.white;
			secondDotFill.sortingOrder = TutorialBubbleFillSortingOrder;
			tutorialBubbleSecondDotFills[i] = secondDotFill;

			GameObject secondDotBorderObject = new GameObject("SecondThoughtDotBorder_" + (i + 1));
			secondDotBorderObject.transform.SetParent(tutorialBubbleRoot.transform, false);
			LineRenderer secondDotBorder = secondDotBorderObject.AddComponent<LineRenderer>();
			ConfigureGroundLineRenderer(
				secondDotBorder,
				TutorialBubbleDotOutlineSegments,
				0.065f,
				TutorialBubbleLineSortingOrder,
				new Color(0.05f, 0.06f, 0.07f, 0.95f),
				8,
				8,
				true);
			tutorialBubbleSecondDotBorders[i] = secondDotBorder;
		}

		GameObject textObject = new GameObject("Text");
		textObject.transform.SetParent(tutorialBubbleRoot.transform, false);
		tutorialBubbleText = textObject.AddComponent<TextMesh>();
		tutorialBubbleText.fontSize = 96;
		tutorialBubbleText.anchor = TextAnchor.MiddleCenter;
		tutorialBubbleText.alignment = TextAlignment.Center;
		tutorialBubbleText.color = Color.black;

		MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
		if (textRenderer != null)
		{
			ApplyTutorialDepthTestedTextMaterial(tutorialBubbleText, textRenderer);
			textRenderer.sortingOrder = TutorialBubbleTextSortingOrder;
		}

		GameObject skipTextObject = new GameObject("SkipText");
		skipTextObject.transform.SetParent(tutorialBubbleRoot.transform, false);
		tutorialBubbleSkipText = skipTextObject.AddComponent<TextMesh>();
		tutorialBubbleSkipText.fontSize = 72;
		tutorialBubbleSkipText.anchor = TextAnchor.MiddleCenter;
		tutorialBubbleSkipText.alignment = TextAlignment.Center;
		tutorialBubbleSkipText.color = new Color(0.12f, 0.13f, 0.15f);

		MeshRenderer skipTextRenderer = skipTextObject.GetComponent<MeshRenderer>();
		if (skipTextRenderer != null)
		{
			ApplyTutorialDepthTestedTextMaterial(tutorialBubbleSkipText, skipTextRenderer);
			skipTextRenderer.sortingOrder = TutorialBubbleTextSortingOrder;
		}
	}

	private void ApplyTutorialDepthTestedTextMaterial(TextMesh text, MeshRenderer renderer)
	{
		if (text == null || renderer == null || text.font == null)
		{
			return;
		}

		if (tutorialBubbleDepthTestedTextMaterial == null)
		{
			Shader shader = Shader.Find("Sprites/Default");
			if (shader == null)
			{
				return;
			}

			tutorialBubbleDepthTestedTextMaterial = new Material(shader)
			{
				name = "TutorialBubbleDepthTestedText"
			};
		}

		Material fontMaterial = text.font.material;
		if (fontMaterial != null && fontMaterial.mainTexture != null)
		{
			tutorialBubbleDepthTestedTextMaterial.mainTexture = fontMaterial.mainTexture;
		}

		renderer.sharedMaterial = tutorialBubbleDepthTestedTextMaterial;
	}

	private void UpdateTutorialBubble(
		Vector2 center,
		Vector2 size,
		Vector2 target,
		string text,
		float characterSize,
		Vector2? secondTarget = null,
		bool showSkipText = true)
	{
		text = LocalizeVisibleText(text);
		EnsureTutorialBubbleVisual();
		if (tutorialBubbleRoot == null)
		{
			return;
		}

		size = GetTightTutorialBubbleSize(text, characterSize, showSkipText);
		for (int i = 0; i < (secondTarget.HasValue ? 4 : 1); i++)
		{
			center = EnsureTutorialThoughtDotClearance(center, size * 0.5f, target);
			if (secondTarget.HasValue)
			{
				center = EnsureTutorialThoughtDotClearance(center, size * 0.5f, secondTarget.Value);
			}
		}

		tutorialBubbleFill.transform.position = new Vector3(center.x, center.y, TutorialBubbleZ);
		tutorialBubbleFill.transform.localScale = new Vector3(
			size.x / TutorialCircleSpriteFilledDiameter,
			size.y / TutorialCircleSpriteFilledDiameter,
			1f);

		SetTutorialBubbleBorder(center, size * 0.5f);
		SetTutorialBubbleThoughtDots(
			center,
			size * 0.5f,
			target,
			tutorialBubbleDotFills,
			tutorialBubbleDotBorders);
		SetTutorialBubbleThoughtDotVisibility(
			tutorialBubbleSecondDotFills,
			tutorialBubbleSecondDotBorders,
			secondTarget.HasValue);
		if (secondTarget.HasValue)
		{
			SetTutorialBubbleThoughtDots(
				center,
				size * 0.5f,
				secondTarget.Value,
				tutorialBubbleSecondDotFills,
				tutorialBubbleSecondDotBorders);
		}

		tutorialBubbleText.transform.position = new Vector3(center.x, center.y + size.y * 0.04f, TutorialBubbleTextZ);
		tutorialBubbleText.text = text;
		tutorialBubbleText.characterSize = characterSize;
		FitTutorialBubbleText(size, characterSize);
		if (showSkipText)
		{
			UpdateTutorialSkipText(center, size);
		}
		else if (tutorialBubbleSkipText != null)
		{
			tutorialBubbleSkipText.gameObject.SetActive(false);
		}
	}

	private void EnsureTutorialCompanionBubbleVisual()
	{
		EnsureTutorialBubbleVisual();
		if (tutorialCompanionBubbleRoot != null || tutorialBubbleRoot == null)
		{
			return;
		}

		tutorialCompanionBubbleRoot = Object.Instantiate(tutorialBubbleRoot, petriNetRoot);
		tutorialCompanionBubbleRoot.name = "LevelTutorialCompanionBubble";
		tutorialCompanionBubbleFill = GetTutorialBubbleComponent<SpriteRenderer>(tutorialCompanionBubbleRoot, "Fill");
		tutorialCompanionBubbleBorder = GetTutorialBubbleComponent<LineRenderer>(tutorialCompanionBubbleRoot, "Border");
		tutorialCompanionBubbleText = GetTutorialBubbleComponent<TextMesh>(tutorialCompanionBubbleRoot, "Text");
		tutorialCompanionBubbleSkipText = GetTutorialBubbleComponent<TextMesh>(tutorialCompanionBubbleRoot, "SkipText");
		tutorialCompanionBubbleDotFills = new SpriteRenderer[TutorialBubbleDotCount];
		tutorialCompanionBubbleDotBorders = new LineRenderer[TutorialBubbleDotCount];
		tutorialCompanionBubbleSecondDotFills = new SpriteRenderer[TutorialBubbleDotCount];
		tutorialCompanionBubbleSecondDotBorders = new LineRenderer[TutorialBubbleDotCount];
		for (int i = 0; i < TutorialBubbleDotCount; i++)
		{
			string number = (i + 1).ToString();
			tutorialCompanionBubbleDotFills[i] =
				GetTutorialBubbleComponent<SpriteRenderer>(tutorialCompanionBubbleRoot, "ThoughtDotFill_" + number);
			tutorialCompanionBubbleDotBorders[i] =
				GetTutorialBubbleComponent<LineRenderer>(tutorialCompanionBubbleRoot, "ThoughtDotBorder_" + number);
			tutorialCompanionBubbleSecondDotFills[i] =
				GetTutorialBubbleComponent<SpriteRenderer>(tutorialCompanionBubbleRoot, "SecondThoughtDotFill_" + number);
			tutorialCompanionBubbleSecondDotBorders[i] =
				GetTutorialBubbleComponent<LineRenderer>(tutorialCompanionBubbleRoot, "SecondThoughtDotBorder_" + number);
		}

		SetTutorialBubbleThoughtDotVisibility(
			tutorialCompanionBubbleSecondDotFills,
			tutorialCompanionBubbleSecondDotBorders,
			false);
		SetTutorialCompanionBubbleSortingOrders();
		if (tutorialCompanionBubbleSkipText != null)
		{
			tutorialCompanionBubbleSkipText.gameObject.SetActive(false);
		}
	}

	private void SetTutorialCompanionBubbleSortingOrders()
	{
		if (tutorialCompanionBubbleFill != null)
		{
			tutorialCompanionBubbleFill.sortingOrder = TutorialCompanionBubbleFillSortingOrder;
		}

		if (tutorialCompanionBubbleBorder != null)
		{
			tutorialCompanionBubbleBorder.sortingOrder = TutorialCompanionBubbleLineSortingOrder;
		}

		SetTutorialBubbleRendererSortingOrders(
			tutorialCompanionBubbleDotFills,
			tutorialCompanionBubbleDotBorders,
			TutorialCompanionBubbleFillSortingOrder,
			TutorialCompanionBubbleLineSortingOrder);
		SetTutorialBubbleRendererSortingOrders(
			tutorialCompanionBubbleSecondDotFills,
			tutorialCompanionBubbleSecondDotBorders,
			TutorialCompanionBubbleFillSortingOrder,
			TutorialCompanionBubbleLineSortingOrder);

		MeshRenderer textRenderer = tutorialCompanionBubbleText != null
			? tutorialCompanionBubbleText.GetComponent<MeshRenderer>()
			: null;
		if (textRenderer != null)
		{
			textRenderer.sortingOrder = TutorialCompanionBubbleTextSortingOrder;
		}
	}

	private void SetTutorialBubbleRendererSortingOrders(
		SpriteRenderer[] fills,
		LineRenderer[] borders,
		int fillSortingOrder,
		int borderSortingOrder)
	{
		for (int i = 0; i < TutorialBubbleDotCount; i++)
		{
			if (fills != null && i < fills.Length && fills[i] != null)
			{
				fills[i].sortingOrder = fillSortingOrder;
			}

			if (borders != null && i < borders.Length && borders[i] != null)
			{
				borders[i].sortingOrder = borderSortingOrder;
			}
		}
	}

	private T GetTutorialBubbleComponent<T>(GameObject root, string childName) where T : Component
	{
		if (root == null)
		{
			return null;
		}

		Transform child = root.transform.Find(childName);
		return child != null ? child.GetComponent<T>() : null;
	}

	private void UpdateTutorialCompanionBubble(
		Vector2 center,
		Vector2 size,
		Vector2 target,
		string text,
		float characterSize)
	{
		text = LocalizeVisibleText(text);
		EnsureTutorialCompanionBubbleVisual();
		if (tutorialCompanionBubbleRoot == null
			|| tutorialCompanionBubbleFill == null
			|| tutorialCompanionBubbleText == null)
		{
			return;
		}

		size = GetTightTutorialBubbleSize(text, characterSize, false);
		center = EnsureTutorialThoughtDotClearance(center, size * 0.5f, target);
		tutorialCompanionBubbleFill.transform.position = new Vector3(center.x, center.y, TutorialBubbleZ);
		tutorialCompanionBubbleFill.transform.localScale = new Vector3(
			size.x / TutorialCircleSpriteFilledDiameter,
			size.y / TutorialCircleSpriteFilledDiameter,
			1f);
		SetTutorialEllipseLine(tutorialCompanionBubbleBorder, center, size * 0.5f, TutorialBubbleLineZ);
		SetTutorialBubbleThoughtDots(
			center,
			size * 0.5f,
			target,
			tutorialCompanionBubbleDotFills,
			tutorialCompanionBubbleDotBorders);

		tutorialCompanionBubbleText.transform.position =
			new Vector3(center.x, center.y + size.y * 0.04f, TutorialBubbleTextZ);
		tutorialCompanionBubbleText.text = text;
		FitTutorialText(
			tutorialCompanionBubbleText,
			size.x * TutorialBubbleTextWidthRatio,
			size.y * TutorialBubbleTextHeightRatio,
			characterSize);
	}

	private Vector2 GetTightTutorialBubbleSize(string text, float characterSize, bool showSkipText)
	{
		int lineCount = CountTutorialTextLines(text);
		int longestLineLength = Mathf.Max(1, GetLongestTutorialTextLineLength(text));
		float mainTextWidth = longestLineLength * characterSize * TutorialBubbleEstimatedCharacterWidth;
		float mainTextHeight = lineCount * characterSize * TutorialBubbleEstimatedLineHeight;
		float bubbleWidth = mainTextWidth / TutorialBubbleTextWidthRatio + 0.35f;
		float bubbleHeight = mainTextHeight / TutorialBubbleTextHeightRatio + 0.3f;

		if (showSkipText)
		{
			float skipCharacterSize = 0.055f * TutorialBubbleTextScale;
			string skipText = GetTutorialText(TutorialTextId.Skip);
			float skipTextWidth = skipText.Length * skipCharacterSize * TutorialBubbleEstimatedCharacterWidth;
			bubbleWidth = Mathf.Max(bubbleWidth, skipTextWidth / 0.58f + 0.25f);
			bubbleHeight += 0.5f;
		}

		float minimumWidth = showSkipText ? 3.6f : 2.7f;
		float minimumHeight = showSkipText ? 1.75f : 1.35f;
		return new Vector2(
			Mathf.Max(minimumWidth, bubbleWidth),
			Mathf.Max(minimumHeight, bubbleHeight));
	}

	private int CountTutorialTextLines(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 1;
		}

		int count = 1;
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n')
			{
				count++;
			}
		}

		return count;
	}

	private int GetLongestTutorialTextLineLength(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0;
		}

		int longest = 0;
		int current = 0;
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\n')
			{
				longest = Mathf.Max(longest, current);
				current = 0;
				continue;
			}

			if (text[i] != '\r')
			{
				current++;
			}
		}

		return Mathf.Max(longest, current);
	}

	private void UpdateTutorialSkipText(Vector2 center, Vector2 bubbleSize)
	{
		if (tutorialBubbleSkipText == null)
		{
			return;
		}

		tutorialBubbleSkipText.transform.position = new Vector3(center.x, center.y - bubbleSize.y * 0.31f, TutorialBubbleTextZ);
		tutorialBubbleSkipText.text = GetTutorialText(TutorialTextId.Skip);
		FitTutorialText(tutorialBubbleSkipText, bubbleSize.x * 0.58f, bubbleSize.y * 0.12f, 0.055f * TutorialBubbleTextScale);
	}

	private void SetTutorialBubbleBorder(Vector2 center, Vector2 radius)
	{
		if (tutorialBubbleBorder == null)
		{
			return;
		}

		SetTutorialEllipseLine(tutorialBubbleBorder, center, radius, TutorialBubbleLineZ);
	}

	private void SetTutorialBubbleThoughtDots(
		Vector2 center,
		Vector2 bubbleRadius,
		Vector2 target,
		SpriteRenderer[] dotFills,
		LineRenderer[] dotBorders)
	{
		if (dotFills == null || dotBorders == null)
		{
			return;
		}

		Vector2 edge = GetTutorialEllipseEdgePoint(center, bubbleRadius, target);
		Vector2 dotVector = target - edge;
		float availableDistance = dotVector.magnitude;
		if (availableDistance <= 0.0001f)
		{
			return;
		}

		Vector2 dotDirection = dotVector / availableDistance;
		float totalDotDiameter = GetTutorialThoughtDotTotalDiameter();
		float gap = Mathf.Max(
			TutorialBubbleDotMinimumGap,
			(availableDistance - totalDotDiameter) / (TutorialBubbleDotCount + 1f));
		float distanceFromEdge = gap;
		for (int i = 0; i < TutorialBubbleDotCount; i++)
		{
			float diameter = GetTutorialThoughtDotDiameter(i);
			float radius = diameter * 0.5f;
			distanceFromEdge += radius;
			Vector2 dotCenter = edge + dotDirection * distanceFromEdge;
			Vector2 dotRadius = Vector2.one * (diameter * 0.5f);
			distanceFromEdge += radius + gap;

			if (dotFills[i] != null)
			{
				dotFills[i].transform.position = new Vector3(dotCenter.x, dotCenter.y, TutorialBubbleZ);
				float fillDiameter = diameter / TutorialCircleSpriteFilledDiameter;
				dotFills[i].transform.localScale = new Vector3(fillDiameter, fillDiameter, 1f);
			}

			if (dotBorders[i] != null)
			{
				SetTutorialEllipseLine(dotBorders[i], dotCenter, dotRadius, TutorialBubbleLineZ);
			}
		}
	}

	private void SetTutorialBubbleThoughtDotVisibility(
		SpriteRenderer[] dotFills,
		LineRenderer[] dotBorders,
		bool visible)
	{
		for (int i = 0; i < TutorialBubbleDotCount; i++)
		{
			if (dotFills != null && i < dotFills.Length && dotFills[i] != null)
			{
				dotFills[i].gameObject.SetActive(visible);
			}

			if (dotBorders != null && i < dotBorders.Length && dotBorders[i] != null)
			{
				dotBorders[i].gameObject.SetActive(visible);
			}
		}
	}

	private Vector2 EnsureTutorialThoughtDotClearance(Vector2 center, Vector2 bubbleRadius, Vector2 target)
	{
		Vector2 directionToTarget = target - center;
		if (directionToTarget.sqrMagnitude <= 0.0001f)
		{
			directionToTarget = Vector2.down;
		}

		directionToTarget.Normalize();
		Vector2 edge = GetTutorialEllipseEdgePoint(center, bubbleRadius, target);
		float signedClearance = Vector2.Dot(target - edge, directionToTarget);
		float requiredClearance = GetTutorialThoughtDotTotalDiameter()
			+ TutorialBubbleDotMinimumGap * (TutorialBubbleDotCount + 1);
		if (signedClearance < requiredClearance)
		{
			center -= directionToTarget * (requiredClearance - signedClearance);
		}

		return center;
	}

	private float GetTutorialThoughtDotTotalDiameter()
	{
		float total = 0f;
		for (int i = 0; i < TutorialBubbleDotCount; i++)
		{
			total += GetTutorialThoughtDotDiameter(i);
		}

		return total;
	}

	private float GetTutorialThoughtDotDiameter(int index)
	{
		if (index == 0)
		{
			return 0.52f;
		}

		if (index == 1)
		{
			return 0.36f;
		}

		return 0.24f;
	}

	private void SetTutorialEllipseLine(LineRenderer line, Vector2 center, Vector2 radius, float z)
	{
		if (line == null || line.positionCount <= 0)
		{
			return;
		}

		int count = line.positionCount;
		for (int i = 0; i < count; i++)
		{
			float angle = i / (float)count * Mathf.PI * 2f;
			float x = center.x + Mathf.Cos(angle) * radius.x;
			float y = center.y + Mathf.Sin(angle) * radius.y;
			line.SetPosition(i, new Vector3(x, y, z));
		}
	}

	private Vector2 GetTutorialEllipseEdgePoint(Vector2 center, Vector2 radius, Vector2 target)
	{
		Vector2 direction = target - center;
		if (direction.sqrMagnitude <= 0.0001f)
		{
			return center;
		}

		float normalizedX = direction.x / Mathf.Max(0.0001f, radius.x);
		float normalizedY = direction.y / Mathf.Max(0.0001f, radius.y);
		float scale = 1f / Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
		return center + direction * scale;
	}

	private void FitTutorialBubbleText(Vector2 bubbleSize, float requestedCharacterSize)
	{
		if (tutorialBubbleText == null)
		{
			return;
		}

		FitTutorialText(tutorialBubbleText, bubbleSize.x * TutorialBubbleTextWidthRatio, bubbleSize.y * TutorialBubbleTextHeightRatio, requestedCharacterSize);
	}

	private void FitTutorialText(TextMesh text, float maxWidth, float maxHeight, float requestedCharacterSize)
	{
		if (text == null)
		{
			return;
		}

		text.characterSize = requestedCharacterSize;
		MeshRenderer renderer = text.GetComponent<MeshRenderer>();
		if (renderer == null)
		{
			return;
		}

		for (int i = 0; i < 6; i++)
		{
			Bounds bounds = renderer.bounds;
			if (bounds.size.x <= 0.0001f || bounds.size.y <= 0.0001f)
			{
				return;
			}

			float widthScale = maxWidth / bounds.size.x;
			float heightScale = maxHeight / bounds.size.y;
			float scale = Mathf.Min(1f, widthScale, heightScale);
			if (scale >= 0.98f)
			{
				return;
			}

			text.characterSize *= Mathf.Clamp(scale * 0.94f, 0.25f, 0.94f);
		}
	}

	private bool IsTutorialStepSkipPressed()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null
			&& (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
	}

	private bool IsTutorialIntroAdvancePressed()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null
			&& (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
	}

	private bool IsTutorialCompletionDismissPressed()
	{
		Keyboard keyboard = Keyboard.current;
		return keyboard != null
			&& (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
	}

}
