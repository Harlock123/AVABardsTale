using BardsTale.Core.Items;
using BardsTale.Core.Magic;
using BardsTale.Core.Util;

namespace BardsTale.Core.Characters;

/// <summary>Builds fully-initialised characters: rolls attributes, derives HP/SP and equips starters.</summary>
public sealed class CharacterFactory
{
    private readonly IRandomSource _rng;

    public CharacterFactory(IRandomSource rng) => _rng = rng;

    public Character Create(string name, Race race, CharacterClass cls)
    {
        var attrs = RollAttributes();
        Races.Get(race).ApplyTo(attrs);
        ClampAttributes(attrs);

        var def = Classes.Get(cls);

        var character = new Character
        {
            Name = name,
            Race = race,
            Class = cls,
            Attributes = attrs,
        };

        // Starting health: one hit die plus constitution bonus, minimum 1.
        var conBonus = (attrs.Constitution - 12) / 4;
        character.MaxHitPoints = Math.Max(1, _rng.Roll(1, def.HitDieSides, def.BaseHitBonus + conBonus));
        character.HitPoints = character.MaxHitPoints;

        if (def.IsSpellcaster)
        {
            character.MaxSpellPoints = Math.Max(1, attrs.Intelligence / 2);
            character.SpellPoints = character.MaxSpellPoints;
            foreach (var spell in Spells.KnownAtLevel(def.School, character.Level))
                character.KnownSpells.Add(spell.Id);
        }

        if (character.IsBard)
        {
            foreach (var song in Songs.KnownAtLevel(character.Level))
                character.KnownSongs.Add(song.Id);
        }

        EquipStarter(character, def);
        character.Gold = _rng.Roll(2, 6) * 10;
        return character;
    }

    private AttributeSet RollAttributes() => new()
    {
        // 3d6 drop-and-reroll style: roll 4d6 keep best 3 for slightly heroic adventurers.
        Strength = RollAttribute(),
        Intelligence = RollAttribute(),
        Dexterity = RollAttribute(),
        Constitution = RollAttribute(),
        Luck = RollAttribute()
    };

    private int RollAttribute()
    {
        Span<int> rolls = [_rng.Next(1, 7), _rng.Next(1, 7), _rng.Next(1, 7), _rng.Next(1, 7)];
        var min = Math.Min(Math.Min(rolls[0], rolls[1]), Math.Min(rolls[2], rolls[3]));
        return rolls[0] + rolls[1] + rolls[2] + rolls[3] - min;
    }

    private static void ClampAttributes(AttributeSet attrs)
    {
        foreach (Attribute a in Enum.GetValues<Attribute>())
            attrs[a] = Math.Clamp(attrs[a], 3, 18);
    }

    private static void EquipStarter(Character c, ClassDefinition def)
    {
        c.Weapon = c.Class switch
        {
            CharacterClass.Warrior or CharacterClass.Paladin => Items.Items.LongSword,
            CharacterClass.Rogue => Items.Items.Dagger,
            CharacterClass.Bard or CharacterClass.Hunter => Items.Items.ShortSword,
            CharacterClass.Monk => Items.Items.Fists,
            _ => Items.Items.Staff
        };

        c.Armor = def.CanWearHeavyArmor ? Items.Items.LeatherArmor : Items.Items.Robes;
        if (def is { CanWearHeavyArmor: true } && c.Class is CharacterClass.Warrior or CharacterClass.Paladin)
            c.Shield = Items.Items.SmallShield;
    }
}
