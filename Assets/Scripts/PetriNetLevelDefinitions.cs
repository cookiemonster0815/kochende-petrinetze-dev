using System.Collections.Generic;

public enum PetriNetLevelBlockOwner
{
	geteilt,
	spieler1,
	spieler2
}

[System.Serializable]
public class PetriNetLevelBlockDefinition
{
	public PetriNetLevelBlockOwner owner = PetriNetLevelBlockOwner.geteilt;
	public string firstTransitionName = "Start";
	public string secondTransitionName = "Ende";
	public float processingSeconds = 5f;
	public string resultState = "";

	public PetriNetLevelBlockDefinition()
	{
	}

	public PetriNetLevelBlockDefinition(string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState)
	{
		owner = PetriNetLevelBlockOwner.geteilt;
		this.firstTransitionName = firstTransitionName;
		this.secondTransitionName = secondTransitionName;
		this.processingSeconds = processingSeconds;
		this.resultState = resultState;
	}

	public PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner owner, string firstTransitionName, string secondTransitionName, float processingSeconds, string resultState)
	{
		this.owner = owner;
		this.firstTransitionName = firstTransitionName;
		this.secondTransitionName = secondTransitionName;
		this.processingSeconds = processingSeconds;
		this.resultState = resultState;
	}
}

[System.Serializable]
public class PetriNetLevelOrderDefinition
{
	public string dishText = "Gericht";
	public float appearsAtSeconds;
	public float expiresAtSeconds = 60f;

	public PetriNetLevelOrderDefinition()
	{
	}

	public PetriNetLevelOrderDefinition(string dishText, float appearsAtSeconds, float expiresAtSeconds)
	{
		this.dishText = dishText;
		this.appearsAtSeconds = appearsAtSeconds;
		this.expiresAtSeconds = expiresAtSeconds;
	}
}

[System.Serializable]
public class PetriNetLevelDefinition
{
	public string id = "level";
	public string displayName = "Level";
	public List<PetriNetLevelBlockDefinition> blocks = new List<PetriNetLevelBlockDefinition>();
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
	{
		this.id = id;
		this.displayName = displayName;
		this.blocks = blocks ?? new List<PetriNetLevelBlockDefinition>();
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
			"Kartoffelsuppe",
			new List<PetriNetLevelBlockDefinition>
			{
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Kochen Start", "Kochen Ende", 5f, "gekocht"),
				new PetriNetLevelBlockDefinition(PetriNetLevelBlockOwner.geteilt, "Schneiden Start", "Schneiden Ende", 8f, "geschnitten"),
			},
			new List<string> { "Kartoffeln" },
			new List<string> { "Suppengemüse" },
			new List<PetriNetLevelOrderDefinition>
			{
				new PetriNetLevelOrderDefinition("Kartoffelsuppe: Kartoffeln geschnitten und mit Suppengemüse gekocht", 0f, 60f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe: Kartoffeln geschnitten und mit Suppengemüse gekocht", 30f, 90f),
			},
			new List<string>()),

		new PetriNetLevelDefinition(
			"l1.2",
			"Suppenschlacht",
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
				new PetriNetLevelOrderDefinition("Tomatensuppe: Tomaten geschnitten und mit Suppengemüse gekocht und mit Schnittlauch dekoriert", 0f, 60f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe: Zwiebeln geschnitten und mit Suppengemüse gekocht und mit Petersilie dekoriert", 0f, 90f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe: Kartoffeln geschnitten und mit Suppengemüse gekocht", 30f, 90f),
				new PetriNetLevelOrderDefinition("Tomatensuppe: Tomaten geschnitten und mit Suppengemüse gekocht und mit Schnittlauch dekoriert", 60f, 120f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe: Zwiebeln geschnitten und mit Suppengemüse gekocht und mit Petersilie dekoriert", 90f, 150f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe: Kartoffeln geschnitten und mit Suppengemüse gekocht", 90f, 150f),
			},
			new List<string>()),

		new PetriNetLevelDefinition(
			"l1.3",
			"Falschherum",
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
				new PetriNetLevelOrderDefinition("Tomatensuppe: Tomaten geschnitten und mit Suppengemüse gekocht und mit Schnittlauch dekoriert", 0f, 60f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe: Zwiebeln geschnitten und mit Suppengemüse gekocht und mit Petersilie dekoriert", 0f, 90f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe: Kartoffeln geschnitten und mit Suppengemüse gekocht", 30f, 90f),
				new PetriNetLevelOrderDefinition("Tomatensuppe: Tomaten geschnitten und mit Suppengemüse gekocht und mit Schnittlauch dekoriert", 60f, 120f),
				new PetriNetLevelOrderDefinition("Zwiebelsuppe: Zwiebeln geschnitten und mit Suppengemüse gekocht und mit Petersilie dekoriert", 90f, 150f),
				new PetriNetLevelOrderDefinition("Kartoffelsuppe: Kartoffeln geschnitten und mit Suppengemüse gekocht", 90f, 150f),
			},
			new List<string>()),
	};
}
