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

	public PetriNetLevelBlockDefinition(
		PetriNetLevelBlockOwner owner,
		string firstTransitionName,
		string secondTransitionName,
		float processingSeconds,
		string resultState,
		int outputTokenCount = 1,
		bool singleTransition = false)
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
	public string recipeText = "";
	public int amount = 1;
	public float appearsAtSeconds;

	public PetriNetLevelOrderDefinition()
	{
	}

	public PetriNetLevelOrderDefinition(
		string dishText,
		string requiredTokenText,
		string recipeText,
		float appearsAtSeconds,
		int amount = 1)
	{
		this.dishText = dishText;
		this.requiredTokenText = requiredTokenText;
		this.recipeText = recipeText;
		this.appearsAtSeconds = appearsAtSeconds;
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
	private static PetriNetLevelBlockDefinition Cooking(PetriNetLevelBlockOwner owner = PetriNetLevelBlockOwner.geteilt)
	{
		return new PetriNetLevelBlockDefinition(owner, "Kochen Start", "Kochen Ende", 5f, "gekocht");
	}

	private static PetriNetLevelBlockDefinition Cutting(PetriNetLevelBlockOwner owner = PetriNetLevelBlockOwner.geteilt)
	{
		return new PetriNetLevelBlockDefinition(owner, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten");
	}

	private static PetriNetLevelBlockDefinition Decorating(
		float processingSeconds,
		PetriNetLevelBlockOwner owner = PetriNetLevelBlockOwner.geteilt)
	{
		return new PetriNetLevelBlockDefinition(owner, "Dekorieren Start", "Dekorieren Ende", processingSeconds, "dekoriert");
	}

	private static PetriNetLevelBlockDefinition Distributing()
	{
		return new PetriNetLevelBlockDefinition(
			PetriNetLevelBlockOwner.geteilt,
			"Verteilen",
			"",
			0f,
			"aufgeteilt",
			5,
			true);
	}

	private static PetriNetLevelInhibitorArcDefinition InhibitorArc(string sourceBlock, string targetTransition)
	{
		return new PetriNetLevelInhibitorArcDefinition(
			sourceBlock,
			PetriNetLevelBlockPlace.zwischenstelle,
			targetTransition);
	}

	private static List<string> Ingredients(params string[] names)
	{
		return new List<string>(names);
	}

	private static List<string> NoExtras()
	{
		return new List<string>();
	}

	private static List<string> StandardTopIngredients()
	{
		return Ingredients("Kartoffeln", "Tomaten", "Schnittlauch");
	}

	private static List<string> StandardBottomIngredients()
	{
		return Ingredients("Suppengemüse", "Zwiebeln", "Petersilie");
	}

	private static PetriNetLevelOrderDefinition PotatoSoup(float appearsAtSeconds)
	{
		return new PetriNetLevelOrderDefinition(
			"Kartoffelsuppe",
			"(Kartoffeln geschnitten, Suppengemüse) gekocht",
			"Schneide Kartoffeln und koche sie mit Suppengemüse",
			appearsAtSeconds);
	}

	private static PetriNetLevelOrderDefinition TomatoSoup(float appearsAtSeconds)
	{
		return DecoratedSoup(
			"Tomatensuppe",
			"Tomaten",
			"Schnittlauch",
			appearsAtSeconds);
	}

	private static PetriNetLevelOrderDefinition OnionSoup(float appearsAtSeconds)
	{
		return DecoratedSoup(
			"Zwiebelsuppe",
			"Zwiebeln",
			"Petersilie",
			appearsAtSeconds);
	}

	private static PetriNetLevelOrderDefinition DecoratedSoup(
		string dishName,
		string ingredient,
		string garnish,
		float appearsAtSeconds)
	{
		return new PetriNetLevelOrderDefinition(
			dishName,
			"((" + ingredient + " geschnitten, Suppengemüse) gekocht, " + garnish + ") dekoriert",
			"Schneide " + ingredient + ", koche sie mit Suppengemüse und dekoriere sie mit " + garnish,
			appearsAtSeconds);
	}

	private static PetriNetLevelOrderDefinition TomatoMiniSoup(float appearsAtSeconds, string garnish = null)
	{
		return MiniSoup("Tomatensüppchen", "Tomaten", appearsAtSeconds, garnish);
	}

	private static PetriNetLevelOrderDefinition OnionMiniSoup(float appearsAtSeconds, string garnish = null)
	{
		return MiniSoup("Zwiebelsüppchen", "Zwiebeln", appearsAtSeconds, garnish);
	}

	private static PetriNetLevelOrderDefinition PotatoMiniSoup(float appearsAtSeconds, string garnish = null)
	{
		return MiniSoup("Kartoffelsüppchen", "Kartoffeln", appearsAtSeconds, garnish);
	}

	private static PetriNetLevelOrderDefinition MiniSoup(
		string dishName,
		string ingredient,
		float appearsAtSeconds,
		string garnish)
	{
		string baseToken = "((" + ingredient + " geschnitten, Suppengemüse) gekocht) aufgeteilt";
		string requiredToken = string.IsNullOrEmpty(garnish)
			? baseToken
			: "(" + baseToken + ", " + garnish + ") dekoriert";
		string displayName = string.IsNullOrEmpty(garnish)
			? dishName
			: dishName + " mit " + garnish;
		string recipe = string.IsNullOrEmpty(garnish)
			? "Schneide " + ingredient + ", koche sie mit Suppengemüse und teile sie auf."
			: "Schneide " + ingredient + ", koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit " + garnish + ".";
		return new PetriNetLevelOrderDefinition(displayName, requiredToken, recipe, appearsAtSeconds);
	}

	public static readonly List<PetriNetLevelDefinition> Levels = new List<PetriNetLevelDefinition>
	{
		new PetriNetLevelDefinition(
			"l1.1",
			"Level 1: Tutorial",
			new List<PetriNetLevelBlockDefinition>
			{
				Cooking(),
				Cutting(),
			},
			Ingredients("Kartoffeln"),
			Ingredients("Suppengemüse"),
			new List<PetriNetLevelOrderDefinition>
			{
				PotatoSoup(0f),
				PotatoSoup(0f),
				PotatoSoup(0f),
			},
			NoExtras()),

		new PetriNetLevelDefinition(
			"l1.2",
			"Level 2: Suppenschlacht",
			new List<PetriNetLevelBlockDefinition>
			{
				Cooking(),
				Cutting(),
				Decorating(10f),
			},
			StandardTopIngredients(),
			StandardBottomIngredients(),
			new List<PetriNetLevelOrderDefinition>
			{
				TomatoSoup(0f),
				OnionSoup(15f),
				PotatoSoup(45f),
				TomatoSoup(60f),
				OnionSoup(90f),
				PotatoSoup(105f),
			},
			NoExtras()),

		new PetriNetLevelDefinition(
			"l1.3",
			"Level 3: Falschherum",
			new List<PetriNetLevelBlockDefinition>
			{
				Cooking(PetriNetLevelBlockOwner.spieler1),
				Cutting(PetriNetLevelBlockOwner.spieler2),
				Decorating(10f),
			},
			StandardTopIngredients(),
			StandardBottomIngredients(),
			new List<PetriNetLevelOrderDefinition>
			{
				TomatoSoup(0f),
				TomatoSoup(15f),
				OnionSoup(30f),
				PotatoSoup(45f),
				OnionSoup(75f),
				TomatoSoup(105f),
				OnionSoup(120f),
				PotatoSoup(135f),
			},
			NoExtras()),

		new PetriNetLevelDefinition(
			id: "l1.4",
			displayName: "Level 4: Inhibitor-Küche",
			blocks: new List<PetriNetLevelBlockDefinition>
			{
				Cooking(PetriNetLevelBlockOwner.spieler1),
				Cutting(PetriNetLevelBlockOwner.spieler2),
				Decorating(10f),
			},
			inhibitorArcs: new List<PetriNetLevelInhibitorArcDefinition>
			{
				// Schneiden-Zwischenstelle --o Kochen Start:
				// Kochen Start ist gesperrt, solange in Schneiden etwas liegt.
				InhibitorArc("Schneiden Start", "Kochen Start"),

				// Kochen-Zwischenstelle --o Schneiden Start:
				// Schneiden Start ist gesperrt, solange in Kochen etwas liegt.
				InhibitorArc("Kochen Start", "Schneiden Start"),
			},
			topIngredients: StandardTopIngredients(),
			bottomIngredients: StandardBottomIngredients(),
			orders: new List<PetriNetLevelOrderDefinition>
			{
				OnionSoup(0f),
				PotatoSoup(15f),
				OnionSoup(45f),
				TomatoSoup(60f),
				OnionSoup(90f),
				TomatoSoup(105f),
				PotatoSoup(120f),
			},
			extras: NoExtras()),

		new PetriNetLevelDefinition(
			id: "l1.5",
			displayName: "Level 5: Süppchen",
			blocks: new List<PetriNetLevelBlockDefinition>
			{
				Cooking(),
				Cutting(),
				Distributing(),
				Decorating(4f),
			},
			topIngredients: StandardTopIngredients(),
			bottomIngredients: StandardBottomIngredients(),
			orders: new List<PetriNetLevelOrderDefinition>
			{
				TomatoMiniSoup(0f, "Schnittlauch"),
				OnionMiniSoup(20f, "Schnittlauch"),
				PotatoMiniSoup(45f, "Petersilie"),
				TomatoMiniSoup(55f, "Schnittlauch"),
				TomatoMiniSoup(70f, "Petersilie"),
				PotatoMiniSoup(90f, "Petersilie"),
				TomatoMiniSoup(110f),
				OnionMiniSoup(130f),
				OnionMiniSoup(150f, "Petersilie"),
				OnionMiniSoup(175f, "Petersilie"),
				PotatoMiniSoup(200f, "Schnittlauch"),
				PotatoMiniSoup(220f, "Petersilie"),
				PotatoMiniSoup(250f, "Schnittlauch"),
				PotatoMiniSoup(270f, "Petersilie"),
			},
			extras: NoExtras()),
	};
}
