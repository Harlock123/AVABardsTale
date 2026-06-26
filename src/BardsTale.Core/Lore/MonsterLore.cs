using BardsTale.Core.Characters;
using BardsTale.Core.Combat;

namespace BardsTale.Core.Lore;

/// <summary>
/// Bestiary flavour: an evocative line of lore for every monster (curated for the bosses and
/// the marquee foes, generated from the creature's nature for the rank-and-file), plus a hint
/// at the spoils it tends to yield. Keyed by the creature's base name.
/// </summary>
public static class MonsterLore
{
    /// <summary>A line of flavour text for a monster — bespoke where it's been written, generated otherwise.</summary>
    public static string FlavorFor(MonsterTemplate t)
        => Curated.TryGetValue(t.BaseName, out var line) ? line : Generate(t);

    /// <summary>A hint at what the creature is worth — gold tier, and the guaranteed boss drop.</summary>
    public static string DropHint(MonsterTemplate t)
    {
        if (t.BaseName == Bosses.Mangar.Name)
            return "Bears his legendary staff — slay him to free Skara Brae and claim it.";
        if (Bosses.FloorOf(t.BaseName) is not null)
            return "A lair boss — guaranteed to drop a magic item, atop a king's ransom in gold.";

        return t.GoldValue switch
        {
            >= 120 => "Said to hoard a small fortune in treasure.",
            >= 40 => "Worth picking over — it carries a fair purse.",
            >= 10 => "Carries a few coins, little more.",
            _ => "Carries nothing of worth — fought for survival, not plunder."
        };
    }

    // Generated flavour: a base line from the creature's kind, sharpened by what it does in a fight.
    private static string Generate(MonsterTemplate t)
    {
        var name = t.BaseName.ToLowerInvariant();
        var baseLine = Kind(name);

        var rider = t switch
        {
            { InflictsStatus: StatusEffect.Poisoned } => " Its strike runs thick with venom.",
            { InflictsStatus: StatusEffect.Paralyzed } => " A single touch can freeze a hero stiff.",
            { InflictsStatus: StatusEffect.Asleep } => " It lulls the unwary into a fatal slumber.",
            { Ability: MonsterAbility.DrainLevel } => " It feeds on the living's hard-won vigour.",
            { Ability: MonsterAbility.DrainStat } => " Its presence withers body and mind alike.",
            { Ability: MonsterAbility.StealGold } => " Quick fingers lift coin while you bleed.",
            { Spell: not null } => " It weaves dark magic from the shadows.",
            _ => ""
        };
        return baseLine + rider;
    }

    private static string Kind(string name)
    {
        bool Has(params string[] words) => words.Any(name.Contains);

        // Specific creature kinds are matched before the broad "giant ___" brute category, so a
        // Giant Rat reads as vermin rather than a towering brute.
        if (Has("dragon", "wyrm", "wyvern", "drake")) return "A scaled terror of the deep, its breath a weapon in itself.";
        if (Has("spider", "scorpion", "centipede", "wasp", " ant", "ettercap", "stirge", "snake")) return "A chittering thing of fang and venom, lurking where the light fails.";
        if (Has("rat", "bat", "frog", "ooze", "kobold", "goblin", "vermin")) return "Vermin of the upper halls — weak alone, deadly in a swarm.";
        if (Has("skeleton", "zombie", "ghoul", "ghast", "wight", "wraith", "lich", "vampire", "shadow", "banshee", "bone", "nightwalker", "mummy", "spectre")) return "An undead horror that should have stayed in its grave.";
        if (Has("demon", "devil", "balor", "fiend", "imp", "pit", "mephit")) return "A fiend dragged up from the nether dark, all malice and fire.";
        if (Has("golem", "armor", "armour", "construct", "gargoyle")) return "A lifeless construct, tireless, patient and utterly without mercy.";
        if (Has("beholder", "tyrant", "mind flayer", "elder brain", "naga", "aberration")) return "An aberration whose very gaze warps flesh and reason.";
        if (Has("wolf", "hound", "jackal", "worg", "bear", "owlbear", "manticore", "chimera")) return "A savage predator that hunts the dark in hungry packs.";
        if (Has("witch", "acolyte", "adept", "necromancer", "coven", "cultist", "mage", "hex")) return "A twisted spellcaster, trading sanity for forbidden power.";
        if (Has("giant", "ogre", "troll", "ettin", "cyclops", "titan", "minotaur", "hydra")) return "A towering brute that can fell a hero with a single blow.";
        return "A denizen of the catacombs, dangerous to any who wander unprepared.";
    }

    // Bespoke lore for the lair bosses and a few unmistakable foes.
    private static readonly Dictionary<string, string> Curated = new()
    {
        // --- Lair bosses ---
        ["Skeleton Lord"] = "A crowned ruin of bone that commands the restless dead with a rattle of its jaw.",
        ["Coven Matron"] = "Eldest of the witches, her lullaby drops a whole party where they stand.",
        ["Goblin King"] = "A fat, cruel tyrant on a throne of stolen shields, fearless behind his horde.",
        ["Crypt Tyrant"] = "A withered thing that has ruled these crypts so long it has forgotten it ever died.",
        ["Orc Warlord"] = "Scarred from a hundred battles, he leads his warband with axe and bellowed fury.",
        ["Medusa"] = "Meet her gaze and your limbs turn to cold, useless stone.",
        ["Demon Lord"] = "A horned sovereign of the pit, wreathed in hellfire that drinks the courage from heroes.",
        ["Troll King"] = "Vast and regenerating, he laughs off wounds that would lay a giant low.",
        ["Werewolf Alpha"] = "The pack's monstrous leader, faster than sight and twice as savage by moonlight.",
        ["Vampire Lord"] = "An ancient noble of the night who drains the years from the living to fuel his own.",
        ["Stone Titan"] = "A mountain given hateful purpose — slow, unstoppable, and impossibly hard to dent.",
        ["Lich King"] = "A sorcerer-king who traded his soul for undeath, raising the slain to fight anew.",
        ["Wyvern Matriarch"] = "Mother of the brood, her venomed sting can drop a warhorse mid-stride.",
        ["Beholder Tyrant"] = "A floating nightmare of eyes, each one able to unmake a hero with a glance.",
        ["Frost King"] = "His blizzard buries the hall in killing cold before his blade ever falls.",
        ["Death Tyrant"] = "A skull wreathed in necrotic flame that snuffs out life and level alike.",
        ["Pit Lord"] = "Greatest of the fiends below, his hellfire scours the very stones black.",
        ["Archlich"] = "A lich grown so old and mighty its mere word reaps souls like wheat.",
        ["Dragon Tyrant"] = "An ancient wyrm whose cataclysm breath has ended more parties than any sword.",
        ["Mangar the Mad"] = "The Mad Wizard who froze Skara Brae in eternal winter — and the reason you came down here at all.",

        // --- Marquee wandering foes ---
        ["Mimic"] = "A predator that wears the shape of a treasure chest, waiting for greedy hands.",
        ["Tarrasque"] = "The legend made flesh — a engine of teeth and hide that armies have failed to stop.",
        ["Mind Flayer"] = "It feasts on thought itself, and its mind-blast leaves heroes screaming and empty.",
        ["Beholder"] = "A drifting orb of eyes, each glare a different doom.",
        ["Vampire"] = "Charming, ancient and merciless, it drinks both blood and the years from your life.",
        ["Ghoul"] = "A ravenous corpse whose paralysing touch leaves you helpless as it feeds.",
    };
}
