using System.Collections.Generic;

namespace BardsTale.UI.ViewModels;

/// <summary>One titled block of the help/tutorial overlay — a heading and its bullet lines.</summary>
public sealed class HelpSectionViewModel
{
    public HelpSectionViewModel(string title, IReadOnlyList<string> lines)
    {
        Title = title;
        Lines = lines;
    }

    public string Title { get; }
    public IReadOnlyList<string> Lines { get; }
}

/// <summary>
/// The onboarding text shown by the help overlay. Built fresh each open so the controls section
/// reflects the player's current (rebindable) movement keys.
/// </summary>
public static class HelpContent
{
    public static IReadOnlyList<HelpSectionViewModel> Build(string fwd, string back, string left, string right)
    {
        HelpSectionViewModel S(string title, params string[] lines) => new(title, lines);

        return new[]
        {
            S("Getting started",
                "Recruit a band at the Adventurers Guild — create heroes one by one, or press Q there for a ready-made party.",
                "Gear up at Garth's Equipment Shoppe, then walk to the catacombs entrance and press Enter to descend.",
                "Your goal: survive twenty floors and destroy Mangar the Mad to free Skara Brae."),

            S("Moving around",
                $"Move with {fwd} / {back} and turn with {left} / {right} — or use the arrow keys (always active).",
                "Press Enter to step into a building you're standing on, or to use a stairway (down, up, or out to town).",
                "Press Esc to back out of any panel, building, or menu."),

            S("Hotkeys & buttons",
                "J — quest journal    ·    B — bestiary    ·    K — accessory-set codex",
                "🎯 Daily — seeded daily challenge    ·    ⚙ Settings — audio, difficulty, accessibility, key rebinding",
                "Save / Load — manage save slots (the game also autosaves on returning to town)    ·    F1 — this help"),

            S("Around town",
                "Adventurers Guild — recruit, arrange the marching order, and change a hero's class.",
                "Garth's Shoppe — buy and sell gear; he identifies unknown items and restocks as you go deeper.",
                "Temple — heal, revive the fallen, and restore drained levels and attributes.",
                "Review Board — spend banked experience to level up.    Inn — rest to fully recover.",
                "Taverns — buy a round for rumours and hints.    Notice Board — pick up side quests.",
                "The Forge — enchant gear with embers.    Bank — stash gold safe from thieves."),

            S("The catacombs",
                "Wandering monsters ambush as you explore; fixed bosses guard each floor's descent.",
                "Treasure chests can be opened or left (some are mimics!). Riddle stones reward a clever answer.",
                "Pull levers to raise barred gates; carry keys to open locked doors; Search walls for secret passages.",
                "Camp to recover HP and spell points — at the risk of a wandering ambush. Return via the up-stairs at the entrance."),

            S("Tricks & blessings of the deep",
                "Not every wall is real — an illusory wall can be walked straight through (a Rogue may sense one first); a one-way door seals behind you.",
                "Spinners quietly turn you about — check the compass and auto-map to get your bearings again.",
                "A mage's Clairvoyance (the 🔮 Scry button) lights up nearby cells and flags hidden traps, teleporters and illusions on the map.",
                "Make an offering at a shrine (🔱) for a dive-long party Boon — Might (harder hits), Warding (harder to hit) or Vigor (mend each round). It fades when you leave the catacombs."),

            S("Combat",
                "Each round you give every able hero an order: Attack/Shoot, Cast a spell, Sing (Bard), Use an item, or Defend.",
                "Pick a target enemy group on the left for offensive actions. Auto fills sensible orders and resolves the round.",
                "Only the front rank (first three heroes) can melee — give back-rankers a bow, a spell, or a song.",
                "Elements matter: hit a foe's weakness for double damage, its resistance for half, and an immunity for nothing at all (all shown in the bestiary).",
                "Cast Scrye Foe to read a monster's weakness, resistance and immunity on the spot — then aim your spells accordingly.",
                "Watch for elite champions (gold) and affixed packs (cyan): Regenerating, Swift, Vampiric, Warded (shrugs off magic) or Savage — and deep floors may carry two affixes at once."),

            S("Tips for survival",
                "Rest at the Inn or Temple between dives; cure poison, sleep and paralysis with spells, items, or rest.",
                "Elemental wards (rings & amulets) halve matching damage — a fire ward against a dragon is worth its weight in gold.",
                "Bard songs are sustained: keep singing to hold the effect. Watch your tunes.",
                "Visit Settings for difficulty modes, a colourblind palette, larger text, and rebindable keys."),
        };
    }
}
