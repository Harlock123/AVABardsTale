using System.Linq;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Game;
using BardsTale.Core.Items;
using BardsTale.Core.Persistence;
using BardsTale.Core.Util;
using Xunit;

namespace BardsTale.Tests;

public class MagicItemTests
{
    [Fact]
    public void Enchanting_adds_a_named_bonus()
    {
        var sword = Items.Enchant(Items.LongSword, 2);
        Assert.Equal("Long Sword +2", sword.Name);
        Assert.Equal(2, sword.MagicBonus);
        Assert.True(sword.IsMagic);
        Assert.Equal(Items.LongSword.DamageBonus + 2, sword.DamageBonus);
        Assert.True(sword.Value > Items.LongSword.Value);

        var armor = Items.Enchant(Items.ChainMail, 1);
        Assert.Equal(Items.ChainMail.ArmorBonus + 1, armor.ArmorBonus);
    }

    [Fact]
    public void Magic_armour_improves_armour_class()
    {
        var hero = new CharacterFactory(new SystemRandomSource(seed: 1))
            .Create("Tank", Race.Dwarf, CharacterClass.Warrior);

        hero.Armor = Items.ChainMail;
        var plain = hero.ArmorClass;
        hero.Armor = Items.Enchant(Items.ChainMail, 2);
        var enchanted = hero.ArmorClass;

        Assert.True(enchanted < plain, "lower armour class is better, and +2 armour should lower it");
    }

    [Fact]
    public void Every_magic_item_resolves_by_name_for_saves()
    {
        Assert.NotEmpty(Items.MagicItems);
        Assert.All(Items.MagicItems, item => Assert.Equal(item, Items.Find(item.Name)));
        Assert.NotNull(Items.Find("Long Sword +3"));
    }

    [Fact]
    public void Loot_occasionally_drops_a_magic_item()
    {
        var rng = new SystemRandomSource(seed: 7);
        var encounter = new Encounter(new[] { new MonsterGroup(Bestiary.Skeleton, 3) });

        var sawMagic = false;
        for (var i = 0; i < 2000 && !sawMagic; i++)
            sawMagic = Loot.Roll(encounter, rng).Any(item => item.IsMagic);

        Assert.True(sawMagic, "magic items should drop within 2000 rolls");
    }

    [Fact]
    public void A_magic_weapon_survives_a_save_round_trip()
    {
        var session = new GameSession(seed: 1);
        session.FillDefaultParty();
        session.Party.Members[0].Weapon = Items.Enchant(Items.LongSword, 2);

        var loaded = GameSerializer.FromJson(GameSerializer.ToJson(session));
        var weapon = loaded.Party.Members[0].Weapon;

        Assert.Equal("Long Sword +2", weapon!.Name);
        Assert.Equal(2, weapon.MagicBonus);
    }
}
