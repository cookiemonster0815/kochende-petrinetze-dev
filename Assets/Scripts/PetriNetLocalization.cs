using System.Collections.Generic;
using UnityEngine;

public enum PetriNetGameLanguage
{
	German,
	English
}

public partial class GameManager
{
	[SerializeField] private PetriNetGameLanguage gameLanguage = PetriNetGameLanguage.German;

	private enum TutorialTextId
	{
		Skip,
		PreviousStep,
		Intro,
		LevelSelectionMovement,
		Explore,
		ExploreSinglePlayer,
		FirePotatoes,
		FireSoupVegetables,
		PlayerExchange,
		Orders,
		Delivery,
		CreateStorage,
		DeleteStorage,
		Completion,
		Trash,
		InhibitorArc,
		WeightedArc,
		PickupGeneric,
		PickupCuttingSinglePlayer,
		PickupCutting,
		PickupCooking,
		ConnectGeneric,
		ConnectSinglePlayer,
		ConnectTopPlayer,
		ConnectBottomPlayer,
		MoveConnectionToPotatoesSinglePlayer,
		MoveConnectionToPotatoes,
		MoveConnectionToIncoming
	}

	private struct LocalizedTutorialText
	{
		public readonly string german;
		public readonly string english;

		public LocalizedTutorialText(string german, string english)
		{
			this.german = german;
			this.english = english;
		}
	}

	// Tutorial bubble text lives here so editing localized tutorial wording does not require touching tutorial flow logic.
	private static readonly Dictionary<TutorialTextId, LocalizedTutorialText> TutorialTextById = new Dictionary<TutorialTextId, LocalizedTutorialText>
	{
		{
			TutorialTextId.Skip,
			new LocalizedTutorialText(
				"Schritt überspringen: Enter",
				"Skip step: Enter")
		},
		{
			TutorialTextId.PreviousStep,
			new LocalizedTutorialText(
				"Schritt zurück: Backspace",
				"Previous step: Backspace")
		},
		{
			TutorialTextId.Intro,
			new LocalizedTutorialText(
				"In diesem Spiel kochst du Gerichte.\nDabei lernst du Konzepte von Petrinetzen kennen.\nDrücke jederzeit esc um zu pausieren und alle möglichen Tastenbelegungen zu sehen.\nDrücke Enter, um loszulegen.",
				"In this game, you cook dishes.\nAlong the way, you learn concepts from Petri nets.\nPress Esc at any time to pause and see all key bindings.\nPress Enter to get started.")
		},
		{
			TutorialTextId.LevelSelectionMovement,
			new LocalizedTutorialText(
				"Bewege dich mit WASD",
				"Move with WASD")
		},
		{
			TutorialTextId.Explore,
			new LocalizedTutorialText(
				"Zoome mit Mausrad und erkunde die Umgebung per Drag & Drop.\nDu kannst dich nur auf deiner Hälfte des Spielfelds bewegen",
				"Zoom with the mouse wheel and explore by dragging the map.\nYou can only move on your half of the playing field")
		},
		{
			TutorialTextId.ExploreSinglePlayer,
			new LocalizedTutorialText(
				"Zoome mit Mausrad und erkunde die Umgebung per Drag & Drop.",
				"Zoom with the mouse wheel and explore by dragging the map.")
		},
		{
			TutorialTextId.FirePotatoes,
			new LocalizedTutorialText(
				"Wenn du eine Transition feuerst, holt sie dabei ein Token\n(also eine Zutat oder ein Gericht) aus jeder Stelle mit\nVerbindung zur Transition und legt in jede Zielstelle ein Token (bzw. Zutat/Gericht).\nAber Vorsicht: mit Transitionen wie\n'Kochen start' änderst du ihren Zustand.\nFeuere jetzt die Transition 'Kartoffeln' mit F, um eine Kartoffel zu erschaffen.",
				"If you fire a transition, it retrieves a token from\neach place connected to the transition and places a token\n(ingredient/dish) in each destination place.\nCareful: transitions like 'cooking start' change their state.\nFire the 'Potatoes' transition with F to create a potato.")
		},
		{
			TutorialTextId.FireSoupVegetables,
			new LocalizedTutorialText(
				"Wenn du eine Transition feuerst, holt sie dabei ein Token\n(also eine Zutat oder ein Gericht) aus jeder Stelle mit\nVerbindung zur Transition und legt in jede Zielstelle ein Token (bzw. Zutat/Gericht).\nAber Vorsicht: mit Transitionen wie\n'Kochen start' änderst du ihren Zustand.\nFeuere jetzt die Transition 'Suppengemüse' mit F, um Suppengemüse zu erschaffen.",
				"If you fire a transition, it retrieves a token from\neach place connected to the transition and places a token\n(ingredient/dish) in each destination place.\nCareful: transitions like 'cooking start' change their state.\nFire the 'Soup Vegetables' transition with F to create soup vegetables.")
		},
		{
			TutorialTextId.PlayerExchange,
			new LocalizedTutorialText(
				"Schicke deinem Mitspieler Token über die Out Transition\nund erhalte Token über die Stelle auf deiner Seite",
				"Send tokens to your teammate through the Out transition\nand receive tokens through the place on your side")
		},
		{
			TutorialTextId.Orders,
			new LocalizedTutorialText(
				"Koche Bestellungen\n(klappe ihre Rezepte mit C aus)...",
				"Cook orders\n(show their recipes with C)...")
		},
		{
			TutorialTextId.Delivery,
			new LocalizedTutorialText(
				"...und liefere sie rechtzeitig aus. Aber nichts\nfalsches, sonst gibt es Minuspunkte",
				"...and deliver them on time. But nothing wrong,\notherwise you get penalty points")
		},
		{
			TutorialTextId.CreateStorage,
			new LocalizedTutorialText(
				"Die Zahl über einer Stelle heißt Kapazität und zeigt an,\nwie viele Zutaten sie enthalten kann.\nErstelle Lagerblöcke mit E, um Zutaten zwischenzulagern.",
				"The number above a place is called capacity and shows\nhow many ingredients it can hold.\nCreate storage blocks with E to temporarily store ingredients.")
		},
		{
			TutorialTextId.DeleteStorage,
			new LocalizedTutorialText(
				"Lösche leere Lager und\nVerbindungen mit R",
				"Delete empty storage blocks\nand connections with R")
		},
		{
			TutorialTextId.Completion,
			new LocalizedTutorialText(
				"Tutorial abgeschlossen! Koche jetzt 3 Kartoffelsuppen.\nSchritt abschließen mit Enter.",
				"Tutorial complete. Now cook 3 potato soups.\nComplete this step with Enter.")
		},
		{
			TutorialTextId.Trash,
			new LocalizedTutorialText(
				"Nutze den Müll, um falsch zubereitete Gerichte\nzu entfernen. Aber Achtung: dabei entstehen Reset Arcs,\ndie alles entfernen, was an der Stelle liegt!",
				"Use the trash to remove incorrectly\nprepared dishes. Careful: it creates reset arcs\nthat remove everything from that place!")
		},
		{
			TutorialTextId.InhibitorArc,
			new LocalizedTutorialText(
				"Die Anzahl der Arbeitsflächen ist begrenzt:\nInhibitor Arcs verhindern, dass eine Transition feuert,\nwenn in der verbundenen Stelle ein Token liegt.\n(Es kann also nicht gleichzeitig gekocht und geschnitten werden).\nZugleich kann an jeder Stelle nur ein Token liegen, dies wird B/E Netz\nbzw. Bedingungs-/Ereignisnetz genannt, im Gegensatz zu\nS/T Netzen (Stellen-Transitions-Netzen)",
				"Work surfaces are limited:\nInhibitor arcs prevent a transition from firing\nwhen the connected place contains a token.\n(Therefore, cooking and cutting cannot happen at the same time).\nAt the same time, only one token can be in each place, which is called a C/E net\nor condition/event net, as opposed to\nP/T nets (place-transition nets)")
		},
		{
			TutorialTextId.WeightedArc,
			new LocalizedTutorialText(
				"Über Kanten mit Gewichten\ngehen so viele Token, wie das Gewicht vorgibt.\nIn diesem Level können mehrere Token im Lager liegen",
				"Weighted arcs move\nas many tokens\nas the weight specifies.\nIn this level, multiple tokens can be in storage")
		},
		{
			TutorialTextId.PickupGeneric,
			new LocalizedTutorialText(
				"Du kannst Objekte mit Leertaste hochheben\nund sie wieder absetzen.\nNimm Blöcke aus dem Lager,\num sie zu benutzen",
				"You can pick up objects with Space\nand set them down again.\nTake blocks from storage\nto use them")
		},
		{
			TutorialTextId.PickupCuttingSinglePlayer,
			new LocalizedTutorialText(
				"Du kannst Objekte mit Leertaste hochheben\nwenn du darüber schwebst und sie wieder absetzen.\nNimm den Schneideblock aus dem Bereich, um ihn zu benutzen.\nInnerhalb des Bereichs kannst du ihn nicht benutzen!",
				"You can pick up objects with Space\nwhen hovering over them and set them down again.\nTake the cutting block from the area to use it.\nYou cannot use it within the area!")
		},
		{
			TutorialTextId.PickupCutting,
			new LocalizedTutorialText(
				"Du kannst Objekte mit Leertaste hochheben\nwenn du darüber schwebst und sie wieder absetzen.\nNimm den Schneideblock aus dem geteilten Bereich, um ihn zu benutzen.\nInnerhalb des geteilten Bereichs kannst du ihn nicht benutzen!",
				"You can pick up objects with Space\nwhen hovering over them and set them down again.\nTake the cutting block from the shared area to use it.\nYou cannot use it within the shared area!")
		},
		{
			TutorialTextId.PickupCooking,
			new LocalizedTutorialText(
				"Du kannst Objekte mit Leertaste hochheben\nwenn du darüber schwebst und sie wieder absetzen.\nNimm den Kochblock aus dem geteilten Bereich, um ihn zu benutzen.\nInnerhalb des geteilten Bereichs kannst du ihn nicht benutzen!",
				"You can pick up objects with Space\nwhen hovering over them and set them down again.\nTake the cooking block from the shared area to use it.\nYou cannot use it within the shared area!")
		},
		{
			TutorialTextId.ConnectGeneric,
			new LocalizedTutorialText(
				"Ziehe mit Q Verbindungen\nzwischen Stellen und Transitionen",
				"Draw connections with Q\nbetween places and transitions")
		},
		{
			TutorialTextId.ConnectSinglePlayer,
			new LocalizedTutorialText(
				"In Stellen können Zutaten und Gerichte,\nin Petrinetzen Token genannt, liegen, die von Transitionen\nvon einer Stelle zur nächsten bewegt werden können.\nZiehe mit Q eine Verbindung zwischen der Stelle (Kreis)\ndes Suppengemüses und der 'Schneiden Start'-Transition (Rechteck).\nHinweis: wenn du ausversehen eine falsche Verbindung gezogen hast,\nkannst du sie mit R entfernen. Auch in der Luft",
				"Places can contain ingredients and dishes,\ncalled tokens in Petri nets, which can be moved from one place to another by transitions.\nDraw a connection with Q between the soup-vegetable\nplace (circle) and the \"Cutting Start\" transition (rectangle).\nHint: if you accidentally drew a wrong connection, you can\nremove it with R. Even in the air.")
		},
		{
			TutorialTextId.ConnectTopPlayer,
			new LocalizedTutorialText(
				"In Stellen können Zutaten und Gerichte,\nin Petrinetzen Token genannt, liegen, die von Transitionen\nvon einer Stelle zur nächsten bewegt werden können.\nZiehe mit Q eine Verbindung zwischen der eingehenden Stelle (Kreis)\nvon deinem Mitspieler und der 'Schneiden Start'-Transition (Rechteck).\nHinweis: wenn du ausversehen eine falsche Verbindung gezogen hast,\nkannst du sie mit R entfernen. Auch in der Luft",
				"Places can contain ingredients and dishes,\ncalled tokens in Petri nets, which can be moved from one place to another by transitions.\nDraw a connection with Q between the incoming place (circle)\nand the \"Cutting Start\" transition (rectangle).\nHint: if you accidentally drew a wrong connection, you can remove\nit with R. Even in the air.")
		},
		{
			TutorialTextId.ConnectBottomPlayer,
			new LocalizedTutorialText(
				"In Stellen können Zutaten und Gerichte,\nin Petrinetzen Token genannt, liegen, die von Transitionen\nvon einer Stelle zur nächsten bewegt werden können.\nZiehe mit Q eine Verbindung zwischen der Stelle (Kreis) des Suppengemüses und der 'Kochen Start'-Transition (Rechteck).\nHinweis: wenn du ausversehen eine falsche Verbindung gezogen hast,\nkannst du sie mit R entfernen. Auch in der Luft",
				"Places can contain ingredients and dishes,\ncalled tokens in Petri nets, which can be moved from one place to another by transitions.\nDraw a connection with Q between the soup-vegetable place (circle)\nand the \"Cooking Start\" transition (rectangle).\nHint: if you accidentally drew a wrong connection, you can remove\nit with R. Even in the air.")
		},
		{
			TutorialTextId.MoveConnectionToPotatoesSinglePlayer,
			new LocalizedTutorialText(
				"Verschiebe die gerade gezogene Verbindung\nvon der Stelle des Suppengemüses zu den Kartoffeln, indem du\nden hinteren Teil mit Leertaste wieder hochhebst\nund an der Stelle der Kartoffeln anbringst",
				"Move the connection you've just made from\nthe soup-vegetable place to the potato place by picking up\nthe rear part with Space\nand attaching it to the potato place")
		},
		{
			TutorialTextId.MoveConnectionToPotatoes,
			new LocalizedTutorialText(
				"Verschiebe die gerade gezogene Verbindung\nzwischen der vom Mitspieler eingehenden Stelle zu den Kartoffeln, indem du\nden hinteren Teil mit Leertaste wieder hochhebst\nund an der Stelle der Kartoffeln anbringst",
				"Move the connection you've just made from\nthe incoming place from your teammate to the potato place by picking up\nthe rear part with Space\nand attaching it to the potato place")
		},
		{
			TutorialTextId.MoveConnectionToIncoming,
			new LocalizedTutorialText(
				"Verschiebe die gerade gezogene Verbindung zwischen der Stelle\ndes Suppengemüses und der vom Mitspieler eingehenden Stelle, indem du\nden hinteren Teil mit Leertaste wieder hochhebst\nund an der eingehenden Stelle anbringst",
				"Move the connection you've just made from the soup-vegetable place\nto the incoming place from your teammate by picking up the rear part\nwith Space and attaching it to the incoming place")
		}
	};

	private string GetTutorialText(TutorialTextId textId)
	{
		if (textId == TutorialTextId.Explore && singlePlayerMode)
		{
			textId = TutorialTextId.ExploreSinglePlayer;
		}

		if (!TutorialTextById.TryGetValue(textId, out LocalizedTutorialText text))
		{
			return "";
		}

		return GameText(text.german, text.english);
	}

	private bool IsEnglishLanguage()
	{
		return gameLanguage == PetriNetGameLanguage.English;
	}

	private string GameText(string german, string english)
	{
		return IsEnglishLanguage() ? english : german;
	}

	private string GameText(PetriNetGameLanguage language, string german, string english)
	{
		return language == PetriNetGameLanguage.English ? english : german;
	}

	private string GetSharedAreaLabelText()
	{
		return singlePlayerMode
			? GameText("Aufbewahrungsbereich", "Holding Area")
			: GameText("Geteilter Bereich", "Shared Area");
	}

	private string GetLanguageToggleButtonText()
	{
		return IsEnglishLanguage() ? "Deutsch" : "English";
	}

	private void ToggleGameLanguage()
	{
		gameLanguage = IsEnglishLanguage() ? PetriNetGameLanguage.German : PetriNetGameLanguage.English;
		RefreshLocalizedGameText();
	}

	private void RefreshLocalizedGameText()
	{
		UpdateLevelSelectionVisuals();
		InvalidateLevelOrderDisplayLayout();
		UpdateLevelOrderDisplay();
		RefreshPetriNetVisuals();
		RefreshStaticLocalizedLabels();
	}

	private void RefreshStaticLocalizedLabels()
	{
		if (sharedPoolVisualRoot == null)
		{
			return;
		}

		TextMesh[] labels = sharedPoolVisualRoot.GetComponentsInChildren<TextMesh>(true);
		for (int i = 0; i < labels.Length; i++)
		{
			TextMesh label = labels[i];
			if (label != null && label.gameObject.name == "ZutatenLabel")
			{
				label.text = GameText("Zutaten", "Ingredients");
			}
			else if (label != null && label.gameObject.name == "SharedAreaLabel")
			{
				label.text = GetSharedAreaLabelText();
			}
		}
	}

	private string GetLocalizedLevelDisplayName(PetriNetLevelDefinition level)
	{
		return LocalizeVisibleText(GetFallbackText(level != null ? level.displayName : null, "Level"));
	}

	private string GetLocalizedLevelDisplayName(PetriNetLevelDefinition level, PetriNetGameLanguage language)
	{
		return LocalizeVisibleText(GetFallbackText(level != null ? level.displayName : null, "Level"), language);
	}

	private string GetLocalizedOrderDishText(PetriNetLevelOrderDefinition order)
	{
		return LocalizeVisibleText(order != null ? order.dishText : "");
	}

	private string GetLocalizedOrderRequiredTokenText(PetriNetLevelOrderDefinition order)
	{
		return LocalizeVisibleText(GetOrderRequiredTokenText(order));
	}

	private string GetLocalizedOrderRecipeText(PetriNetLevelOrderDefinition order)
	{
		return LocalizeVisibleText(GetOrderRecipeText(order));
	}

	private string GetLocalizedNodeDisplayName(NodeRuntime node)
	{
		return LocalizeVisibleText(GetNodeDisplayName(node));
	}

	private string GetLocalizedTokenDescription(TokenRuntime token)
	{
		return LocalizeVisibleText(GetTokenDescription(token));
	}

	private string LocalizeVisibleText(string text)
	{
		return LocalizeVisibleText(text, gameLanguage);
	}

	private string LocalizeVisibleText(string text, PetriNetGameLanguage language)
	{
		if (language != PetriNetGameLanguage.English || string.IsNullOrEmpty(text))
		{
			return text ?? "";
		}

		if (EnglishExactTextByGerman.TryGetValue(text, out string exactText))
		{
			return exactText;
		}

		string localized = text;
		for (int i = 0; i < EnglishReplacementPairs.Length; i++)
		{
			localized = localized.Replace(EnglishReplacementPairs[i].Key, EnglishReplacementPairs[i].Value);
		}

		return localized;
	}

	private static readonly Dictionary<string, string> EnglishExactTextByGerman = new Dictionary<string, string>
	{
		{ "Levelübersicht", "Level Overview" },
		{ "Bestätigen", "Confirm" },
		{ "Level beendet", "Level ended" },
		{ "Menü", "Menu" },
		{ "Auswertung", "Results" },
		{ "Zur Levelübersicht", "Return to level overview" },
		{ "Spiel pausiert.", "Game paused." },
		{ "Weiter", "Continue" },
		{ "Level beenden", "End level" },
		{ "Level abgeschlossen!", "Level complete!" },
		{ "Nächstes Level", "Next level" },
		{ "Letztes Level", "Last level" },
		{ "Tasten", "Controls" },
		{ "Ein Spieler", "A player" },
		{ "Du", "You" },
		{ "Spieler 1", "Player 1" },
		{ "Spieler 2", "Player 2" },
		{ "Zutaten", "Ingredients" },
		{ "Geteilter Bereich", "Shared Area" },
		{ "Aufbewahrungsbereich", "Holding Area" },
		{ "Müll", "Trash" },
		{ "Lager", "Storage" },
		{ "Kochblock", "Cooking Block" },
		{ "Schneideblock", "Cutting Block" },
		{ "Verteilblock", "Distribution Block" },
		{ "Dekorierblock", "Decoration Block" },
		{ "Lagerblock", "Storage Block" },
		{ "Block", "Block" },
		{ "Ausliefern", "Deliver" },
		{ "nichts", "nothing" },
		{ "nichts gekocht", "nothing cooked" },
		{ "nichts geschnitten", "nothing cut" },
		{ "nichts dekoriert", "nothing garnished" },
		{ "nichts aufgeteilt", "nothing split" },
		{ "keine", "none" },
		{ "keine\n", "none\n" },
		{ "Start", "Start" },
		{ "Ende", "End" },
		{ "kein Zustand", "no state" },
		{ "Zwischenstelle", "intermediate place" },
		{ "Ausgabe-Stelle", "output place" },
		{ "Gericht", "Dish" },
		{ "Rezept:", "Recipe:" },
		{ "Einklappen:", "Collapse:" },
		{ "unbekannt", "unknown" },
		{ "Level 1: Tutorial", "Level 1: Tutorial" },
		{ "Level 2: Suppenschlacht", "Level 2: Soup Rush" },
		{ "Level 3: Falschherum", "Level 3: Backwards" },
		{ "Level 4: Inhibitor-Küche", "Level 4: Inhibitor Kitchen" },
		{ "Level 5: Süppchen", "Level 5: Mini Soups" },
		{ "Kochen Start", "Cooking Start" },
		{ "Kochen Ende", "Cooking End" },
		{ "Kochen start", "cooking start" },
		{ "Schneiden Start", "Cutting Start" },
		{ "Schneiden Ende", "Cutting End" },
		{ "Schneidebeginn-Transition", "cutting-start transition" },
		{ "Kochenbeginn-Transition", "cooking-start transition" },
		{ "Dekorieren Start", "Garnishing Start" },
		{ "Dekorieren Ende", "Garnishing End" },
		{ "Verteilen", "Split" },
		{ "Zutat ", "Ingredient " },
		{ "Kartoffelsuppe", "Potato Soup" },
		{ "Tomatensuppe", "Tomato Soup" },
		{ "Zwiebelsuppe", "Onion Soup" },
		{ "Tomatensüppchen", "Mini Tomato Soup" },
		{ "Zwiebelsüppchen", "Mini Onion Soup" },
		{ "Kartoffelsüppchen", "Mini Potato Soup" },
		{ "Kartoffeln", "Potatoes" },
		{ "Tomaten", "Tomatoes" },
		{ "Zwiebeln", "Onions" },
		{ "Suppengemüse", "Soup Vegetables" },
		{ "Schnittlauch", "Chives" },
		{ "Petersilie", "Parsley" },
		{ "Käse", "Cheese" },
		{ "Tomate", "Tomato" },
		{ "Traube", "Grape" },
		{ "Ananas", "Pineapple" },
		{ "Wirsing", "Savoy Cabbage" },
		{ "Paprika", "Bell Pepper" },
		{ "Zwiebel", "Onion" },
		{ "Salat", "Lettuce" },
		{ "Aubergine", "Eggplant" },
		{ "Pilz", "Mushroom" },
		{ "Tomatensüppchen mit Schnittlauch", "Mini Tomato Soup with Chives" },
		{ "Tomatensüppchen mit Petersilie", "Mini Tomato Soup with Parsley" },
		{ "Zwiebelsüppchen mit Schnittlauch", "Mini Onion Soup with Chives" },
		{ "Zwiebelsüppchen mit Petersilie", "Mini Onion Soup with Parsley" },
		{ "Kartoffelsüppchen mit Schnittlauch", "Mini Potato Soup with Chives" },
		{ "Kartoffelsüppchen mit Petersilie", "Mini Potato Soup with Parsley" },
		{ "Schneide Kartoffeln und koche sie mit Suppengemüse", "Cut potatoes and cook them with soup vegetables" },
		{ "Schneide Tomaten, koche sie mit Suppengemüse und dekoriere sie mit Schnittlauch", "Cut tomatoes, cook them with soup vegetables, and garnish them with chives" },
		{ "Schneide Zwiebeln, koche sie mit Suppengemüse und dekoriere sie mit Petersilie", "Cut onions, cook them with soup vegetables, and garnish them with parsley" },
		{ "Schneide Tomaten, koche sie mit Suppengemüse und teile sie auf.", "Cut tomatoes, cook them with soup vegetables, and split them." },
		{ "Schneide Zwiebeln, koche sie mit Suppengemüse und teile sie auf.", "Cut onions, cook them with soup vegetables, and split them." },
		{ "Schneide Kartoffeln, koche sie mit Suppengemüse und teile sie auf.", "Cut potatoes, cook them with soup vegetables, and split them." },
		{ "Schneide Tomaten, koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit Schnittlauch.", "Cut tomatoes, cook them with soup vegetables, split them, and garnish them with chives." },
		{ "Schneide Tomaten, koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit Petersilie.", "Cut tomatoes, cook them with soup vegetables, split them, and garnish them with parsley." },
		{ "Schneide Zwiebeln, koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit Schnittlauch.", "Cut onions, cook them with soup vegetables, split them, and garnish them with chives." },
		{ "Schneide Zwiebeln, koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit Petersilie.", "Cut onions, cook them with soup vegetables, split them, and garnish them with parsley." },
		{ "Schneide Kartoffeln, koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit Schnittlauch.", "Cut potatoes, cook them with soup vegetables, split them, and garnish them with chives." },
		{ "Schneide Kartoffeln, koche sie mit Suppengemüse, teile sie auf und dekoriere sie mit Petersilie.", "Cut potatoes, cook them with soup vegetables, split them, and garnish them with parsley." },
	};

	private static readonly KeyValuePair<string, string>[] EnglishReplacementPairs =
	{
		new KeyValuePair<string, string>("nichts", "nothing"),
		new KeyValuePair<string, string>("Suppengemüse", "soup vegetables"),
		new KeyValuePair<string, string>("Kartoffeln", "potatoes"),
		new KeyValuePair<string, string>("Kartoffel", "potato"),
		new KeyValuePair<string, string>("Tomaten", "tomatoes"),
		new KeyValuePair<string, string>("Tomate", "tomato"),
		new KeyValuePair<string, string>("Zwiebeln", "onions"),
		new KeyValuePair<string, string>("Zwiebel", "onion"),
		new KeyValuePair<string, string>("Schnittlauch", "chives"),
		new KeyValuePair<string, string>("Petersilie", "parsley"),
		new KeyValuePair<string, string>("Wirsing", "savoy cabbage"),
		new KeyValuePair<string, string>("Aubergine", "eggplant"),
		new KeyValuePair<string, string>("Paprika", "bell pepper"),
		new KeyValuePair<string, string>("Ananas", "pineapple"),
		new KeyValuePair<string, string>("Salat", "lettuce"),
		new KeyValuePair<string, string>("Pilz", "mushroom"),
		new KeyValuePair<string, string>("Käse", "cheese"),
		new KeyValuePair<string, string>("Traube", "grape"),
		new KeyValuePair<string, string>("gekocht", "cooked"),
		new KeyValuePair<string, string>("geschnitten", "cut"),
		new KeyValuePair<string, string>("dekoriert", "garnished"),
		new KeyValuePair<string, string>("aufgeteilt", "split"),
		new KeyValuePair<string, string>("Kochen Start", "Cooking Start"),
		new KeyValuePair<string, string>("Kochen Ende", "Cooking End"),
		new KeyValuePair<string, string>("Schneiden Start", "Cutting Start"),
		new KeyValuePair<string, string>("Schneiden Ende", "Cutting End"),
		new KeyValuePair<string, string>("Dekorieren Start", "Garnishing Start"),
		new KeyValuePair<string, string>("Dekorieren Ende", "Garnishing End"),
		new KeyValuePair<string, string>("Verteilen", "Split"),
		new KeyValuePair<string, string>("Ende", "End"),
		new KeyValuePair<string, string>("Ausliefern", "Deliver"),
		new KeyValuePair<string, string>("Müll", "Trash"),
		new KeyValuePair<string, string>("Lager", "Storage"),
	};
}
