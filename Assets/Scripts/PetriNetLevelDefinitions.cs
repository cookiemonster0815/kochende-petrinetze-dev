using System.Collections.Generic;

public enum PetriNetLevelBlockOwner
{
	geteilt,
	spieler1,
	spieler2
}

public enum PetriNetLevelBlockPlace
{
	zwischenstelle,
	ausgabe
}

[System.Serializable]
public class PetriNetLevelBlockDefinition
{
	public PetriNetLevelBlockOwner owner = PetriNetLevelBlockOwner.geteilt;
	public string firstTransitionName = "Start";
	public string secondTransitionName = "Ende";
	public float processingSeconds = 5f;
	public string resultState = "";
	public int outputTokenCount = 1;
	public bool singleTransition = false;

	public PetriNetLevelBlockDefinition()
	{
	}

	public PetriNetLevelBlockDefinition(string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState)
		: this(PetriNetLevelBlockOwner.geteilt, firstTransitionName, secondTransitionName, processingSeconds, resultState, 1)
	{
	}

	public PetriNetLevelBlockDefinition(string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState, int outputTokenCount)
		: this(PetriNetLevelBlockOwner.geteilt, firstTransitionName, secondTransitionName, processingSeconds, resultState, outputTokenCount, false)
	{
	}

	public PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner owner, string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState)
		: this(owner, firstTransitionName, secondTransitionName, processingSeconds, resultState, 1)
	{
	}

	public PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner owner, string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState, int outputTokenCount)
		: this(owner, firstTransitionName, secondTransitionName, processingSeconds, resultState, outputTokenCount, false)
	{
	}

	public PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner owner, string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState, int outputTokenCount, bool singleTransition)
	{
		this.owner = owner;
		this.firstTransitionName = firstTransitionName;
		this.secondTransitionName = secondTransitionName;
		this.processingSeconds = processingSeconds;
		this.resultState = resultState;
		this.outputTokenCount = outputTokenCount;
		this.singleTransition = singleTransition;
	}
}

[System.Serializable]
public class PetriNetLevelInhibitorArcDefinition
{
	// Vorinstallierte Sperrkante: sourceBlock/sourcePlace --o targetTransition.
	// Wenn in der Quell-Stelle Token liegen, kann die Ziel-Transition nicht feuern.
	public string sourceBlockFirstTransitionName = "";
	public PetriNetLevelBlockPlace sourcePlace = PetriNetLevelBlockPlace.zwischenstelle;
	public string targetTransitionName = "";

	public PetriNetLevelInhibitorArcDefinition()
	{
	}

	public PetriNetLevelInhibitorArcDefinition(string sourceBlockFirstTransitionName, PetriNetLevelBlockPlace sourcePlace, string targetTransitionName)
	{
		this.sourceBlockFirstTransitionName = sourceBlockFirstTransitionName;
		this.sourcePlace = sourcePlace;
		this.targetTransitionName = targetTransitionName;
	}
}

[System.Serializable]
public class PetriNetLevelOrderDefinition
{
	public string dishText = "Gericht";
	public string requiredTokenText = "";
	public int amount = 1;
	public float appearsAtSeconds;
	public float expiresAtSeconds = 60f;

	public PetriNetLevelOrderDefinition()
	{
	}

	public PetriNetLevelOrderDefinition(string dishText, float appearsAtSeconds, float expiresAtSeconds)
		: this(dishText, dishText, appearsAtSeconds, expiresAtSeconds, 1)
	{
	}

	public PetriNetLevelOrderDefinition(string dishText, string requiredTokenText, float appearsAtSeconds, float expiresAtSeconds)
		: this(dishText, requiredTokenText, appearsAtSeconds, expiresAtSeconds, 1)
	{
	}

	public PetriNetLevelOrderDefinition(string dishText, string requiredTokenText, float appearsAtSeconds, float expiresAtSeconds, int amount)
	{
		this.dishText = dishText;
		this.requiredTokenText = requiredTokenText;
		this.appearsAtSeconds = appearsAtSeconds;
		this.expiresAtSeconds = expiresAtSeconds;
		this.amount = amount;
	}
}

[System.Serializable]
public class PetriNetLevelDefinition
{
	public string id = "level";
	public string displayName = "Level";
	public List<PetriNetLevelBlockDefinition> blocks = new List<PetriNetLevelBlockDefinition>();
	public List<PetriNetLevelInhibitorArcDefinition> inhibitorArcs = new List<PetriNetLevelInhibitorArcDefinition>();
	public List<string> topIngredients = new List<string>();
	public List<string> bottomIngredients = new List<string>();
	public List<PetriNetLevelOrderDefinition> orders = new List<PetriNetLevelOrderDefinition>();
	public List<string> extras = new List<string>();

	public PetriNetLevelDefinition()
	{
	}

	public PetriNetLevelDefinition(
		string id,
		string displayName,
		List<PetriNetLevelBlockDefinition> blocks,
		List<string> topIngredients,
		List<string> bottomIngredients,
		List<string> extras)
		: this(id, displayName, blocks, topIngredients, bottomIngredients, new List<PetriNetLevelOrderDefinition>(), extras)
	{
	}

	public PetriNetLevelDefinition(
		string id,
		string displayName,
		List<PetriNetLevelBlockDefinition> blocks,
		List<string> topIngredients,
		List<string> bottomIngredients,
		List<PetriNetLevelOrderDefinition> orders,
		List<string> extras)
		: this(id, displayName, blocks, new List<PetriNetLevelInhibitorArcDefinition>(), topIngredients, bottomIngredients, orders, extras)
	{
	}

	public PetriNetLevelDefinition(
		string id,
		string displayName,
		List<PetriNetLevelBlockDefinition> blocks,
		List<PetriNetLevelInhibitorArcDefinition> inhibitorArcs,
		List<string> topIngredients,
		List<string> bottomIngredients,
		List<PetriNetLevelOrderDefinition> orders,
		List<string> extras)
	{
		this.id = id;
		this.displayName = displayName;
		this.blocks = blocks ?? new List<PetriNetLevelBlockDefinition>();
		this.inhibitorArcs = inhibitorArcs ?? new List<PetriNetLevelInhibitorArcDefinition>();
		this.topIngredients = topIngredients ?? new List<string>();
		this.bottomIngredients = bottomIngredients ?? new List<string>();
		this.orders = orders ?? new List<PetriNetLevelOrderDefinition>();
		this.extras = extras ?? new List<string>();
	}
}

public static class PetriNetLevelCatalog
{
	public static readonly List<PetriNetLevelDefinition> Levels = new List<PetriNetLevelDefinition>
	{
		new PetriNetLevelDefinition(
			"l1.1",
			"Level 1: Kartoffelsuppe",
			new List<PetriNetLevelBlockDefinition>
			{
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Kochen Start", "Kochen Ende", 5f, "gekocht"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten"),
			},
			new List<string> { "Kartoffeln" },
			new List<string> { "Suppengemüse" },
			new List<PetriNetLevelOrderDefinition>
			{
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 0f, 60f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 30f, 90f),
			},
			new List<string>()),

		new PetriNetLevelDefinition(
			"l1.2",
			"Level 2: Suppenschlacht",
			new List<PetriNetLevelBlockDefinition>
			{
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Kochen Start", "Kochen Ende", 5f, "gekocht"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Dekorieren Start", "Dekorieren Ende", 12f, "dekoriert"),
			},
			new List<string> { "Kartoffeln", "Tomaten", "Schnittlauch" },
			new List<string> { "Suppengemüse", "Zwiebeln", "Petersilie" },
			new List<PetriNetLevelOrderDefinition>
			{
				new PetriNetLevelOrderDefinition("Tomatensuppe", "((Tomaten geschnitten, Suppengemüse) gekocht, Schnittlauch) dekoriert", 0f, 60f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe", "((Zwiebeln geschnitten, Suppengemüse) gekocht, Petersilie) dekoriert", 0f, 90f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 30f, 90f),
				new PetriNetLevelOrderDefinition("Tomatensuppe", "((Tomaten geschnitten, Suppengemüse) gekocht, Schnittlauch) dekoriert", 60f, 120f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe", "((Zwiebeln geschnitten, Suppengemüse) gekocht, Petersilie) dekoriert", 90f, 150f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 90f, 150f),
			},
			new List<string>()),

		new PetriNetLevelDefinition(
			"l1.3",
			"Level 3: Falschherum",
			new List<PetriNetLevelBlockDefinition>
			{
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.spieler1, "Kochen Start", "Kochen Ende", 5f, "gekocht"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.spieler2, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Dekorieren Start", "Dekorieren Ende", 12f, "dekoriert"),
			},
			new List<string> { "Kartoffeln", "Tomaten", "Schnittlauch" },
			new List<string> { "Suppengemüse", "Zwiebeln", "Petersilie" },
			new List<PetriNetLevelOrderDefinition>
			{
				new PetriNetLevelOrderDefinition("Tomatensuppe", "((Tomaten geschnitten, Suppengemüse) gekocht, Schnittlauch) dekoriert", 0f, 60f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe", "((Zwiebeln geschnitten, Suppengemüse) gekocht, Petersilie) dekoriert", 0f, 90f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 30f, 90f),
				new PetriNetLevelOrderDefinition("Tomatensuppe", "((Tomaten geschnitten, Suppengemüse) gekocht, Schnittlauch) dekoriert", 60f, 120f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe", "((Zwiebeln geschnitten, Suppengemüse) gekocht, Petersilie) dekoriert", 90f, 150f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 90f, 150f),
			},
			new List<string>()),

		new PetriNetLevelDefinition(
			id: "l1.4",
			displayName: "Level 4: Inhibitor-Küche",
			blocks: new List<PetriNetLevelBlockDefinition>
			{
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.spieler1, "Kochen Start", "Kochen Ende", 5f, "gekocht"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.spieler2, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Dekorieren Start", "Dekorieren Ende", 12f, "dekoriert"),
			},
			inhibitorArcs: new List<PetriNetLevelInhibitorArcDefinition>
			{
				// Schneiden-Zwischenstelle --o Kochen Start:
				// Kochen Start ist gesperrt, solange in Schneiden etwas liegt.
				new PetriNetLevelInhibitorArcDefinition(
					sourceBlockFirstTransitionName: "Schneiden Start",
					sourcePlace: PetriNetLevelBlockPlace.zwischenstelle,
					targetTransitionName: "Kochen Start"),

				// Kochen-Zwischenstelle --o Schneiden Start:
				// Schneiden Start ist gesperrt, solange in Kochen etwas liegt.
				new PetriNetLevelInhibitorArcDefinition(
					sourceBlockFirstTransitionName: "Kochen Start",
					sourcePlace: PetriNetLevelBlockPlace.zwischenstelle,
					targetTransitionName: "Schneiden Start"),
			},
			topIngredients: new List<string> { "Kartoffeln", "Tomaten", "Schnittlauch" },
			bottomIngredients: new List<string> { "Suppengemüse", "Zwiebeln", "Petersilie" },
			orders: new List<PetriNetLevelOrderDefinition>
			{
				new PetriNetLevelOrderDefinition("Tomatensuppe", "((Tomaten geschnitten, Suppengemüse) gekocht, Schnittlauch) dekoriert", 0f, 60f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe", "((Zwiebeln geschnitten, Suppengemüse) gekocht, Petersilie) dekoriert", 0f, 90f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 30f, 90f),
				new PetriNetLevelOrderDefinition("Tomatensuppe", "((Tomaten geschnitten, Suppengemüse) gekocht, Schnittlauch) dekoriert", 60f, 120f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe", "((Zwiebeln geschnitten, Suppengemüse) gekocht, Petersilie) dekoriert", 90f, 150f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe", "(Kartoffeln geschnitten, Suppengemüse) gekocht", 90f, 150f),
			},
			extras: new List<string>()),

		new PetriNetLevelDefinition(
			id: "l1.5",
			displayName: "Level 5: Süppchen",
			blocks: new List<PetriNetLevelBlockDefinition>
			{
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Kochen Start", "Kochen Ende", 5f, "gekocht"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Verteilen", "", 0f, "aufgeteilt", 5, true),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Dekorieren Start", "Dekorieren Ende", 6f, "dekoriert"),
			},
			topIngredients: new List<string> { "Kartoffeln", "Tomaten", "Schnittlauch" },
			bottomIngredients: new List<string> { "Suppengemüse", "Petersilie" },
			orders: new List<PetriNetLevelOrderDefinition>
			{
				new PetriNetLevelOrderDefinition("Tomatensüppchen mit Schnittlauch", "(((Tomaten geschnitten, Suppengemüse) gekocht) aufgeteilt, Schnittlauch) dekoriert", 0f, 120f, 3),
				new PetriNetLevelOrderDefinition("Kartoffelsüppchen mit Petersilie", "(((Kartoffeln geschnitten, Suppengemüse) gekocht) aufgeteilt, Petersilie) dekoriert", 20f, 150f, 2),
				new PetriNetLevelOrderDefinition("Tomatensüppchen", "((Tomaten geschnitten, Suppengemüse) gekocht) aufgeteilt", 40f, 180f, 3),
			},
			extras: new List<string>()),
	};
}
