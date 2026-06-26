using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BardsTale.Core.Characters;
using BardsTale.Core.Combat;
using BardsTale.Core.Items;
using BardsTale.Core.Town;
using BardsTale.UI.ViewModels;
using BardsTale.UI.Views;

namespace BardsTale.UI;

/// <summary>
/// Drives the running app through every major screen — the town hub, each shop, the
/// catacombs, a battle and the bestiary — rendering each to a PNG in a SCREENSHOTS folder.
/// Triggered by setting the <c>BT_SHOT</c> environment variable; the output directory is
/// <c>BT_SHOT_DIR</c> (defaulting to <c>./SCREENSHOTS</c>). It captures the live UI via
/// <see cref="RenderTargetBitmap"/>, so the images carry the real fonts, theme and layout.
/// </summary>
public static class ScreenshotRunner
{
    private static readonly (TownBuilding Building, string File)[] Shops =
    {
        (TownBuilding.Guild, "guild"),
        (TownBuilding.Shop, "shop"),
        (TownBuilding.Smithy, "smithy"),
        (TownBuilding.Temple, "temple"),
        (TownBuilding.ReviewBoard, "review-board"),
        (TownBuilding.Tavern, "tavern"),
        (TownBuilding.Inn, "inn"),
        (TownBuilding.QuestBoard, "quest-board"),
        (TownBuilding.Bank, "bank"),
    };

    public static async Task RunAsync(MainWindowViewModel vm, Window window)
    {
        var dir = Environment.GetEnvironmentVariable("BT_SHOT_DIR")
                  ?? Path.Combine(Directory.GetCurrentDirectory(), "SCREENSHOTS");
        Directory.CreateDirectory(dir);

        var view = window.GetVisualDescendants().OfType<MainView>().FirstOrDefault() as Control ?? window;

        Setup(vm);
        await Settle();

        // The town hub (first-person streets + party + minimap).
        vm.ShowTownScreen();
        await Settle();
        Capture(view, dir, "town-hub");

        // Each building interior.
        foreach (var (building, file) in Shops)
        {
            EnterBuilding(vm, building);
            DressBuilding(vm, building);
            await Settle();
            Capture(view, dir, file);
        }

        // Back to the town hub so the overlays render over a clean backdrop.
        vm.ShowTownScreen();
        await Settle();

        // The bestiary overlay.
        vm.ShowBestiaryCommand.Execute(null);
        await Settle();
        Capture(view, dir, "bestiary");
        vm.CloseBestiaryCommand.Execute(null);

        // The accessory-set codex overlay.
        vm.ShowSetCodexCommand.Execute(null);
        await Settle();
        Capture(view, dir, "set-codex");
        vm.CloseSetCodexCommand.Execute(null);

        // The town "Cast a Spell" menu (spell points, costs, dimmed-unaffordable).
        vm.ShowTownScreen();
        if (vm.Town is { } town)
        {
            town.SelectedHero = town.Party.FirstOrDefault(h => h.Model.IsSpellcaster) ?? town.Party.FirstOrDefault();
            town.OpenSpellMenuCommand.Execute(null);
            town.SelectedSpell = town.SpellMenu.FirstOrDefault();
        }
        await Settle();
        Capture(view, dir, "spell-menu");

        // The catacombs (dungeon exploration).
        vm.EnterDungeonScreen();
        var explore = vm.Exploration!;
        for (var i = 0; i < 4; i++)
        {
            explore.MoveForwardCommand.Execute(null);
            if (explore.IsAtChest) explore.LeaveChestCommand.Execute(null);
        }
        await Settle();
        Capture(view, dir, "catacombs");

        // A staged battle scene.
        explore.StartCombatForScreenshot(new Encounter(new[]
        {
            new MonsterGroup(Bestiary.Ghoul, 3),
            new MonsterGroup(Bestiary.Orc, 2),
        }));
        await Settle();
        Capture(view, dir, "combat");
    }

    private static void Setup(MainWindowViewModel vm)
    {
        var s = vm.Session;
        s.FillDefaultParty();
        s.Party.Gold = 4200;

        // Equip the lead hero with a set + wards so the badges show in shots.
        var hero = s.Party.Members[0];
        hero.Ring1 = Items.RingOfProtection;
        hero.Ring2 = Items.RingOfProtection;   // completes Twin Bulwark
        hero.Amulet = Items.AmuletOfWarding;

        // Some stash and crafting fuel for the shop/smithy panels.
        s.Party.Inventory.Add(Items.RingOfStormWard);
        s.Party.Inventory.Add(Items.AmuletOfFortune);
        s.Party.Inventory.Add(Items.LongSword);
        for (var i = 0; i < 4; i++) s.Party.Inventory.Add(Items.ForgeEmber);

        // A second hero anchors the Lifeguard set (so the codex shows two assembled).
        var second = s.Party.Members[1];
        second.Ring1 = Items.RingOfVigor;
        second.Amulet = Items.AmuletOfVitality;

        // Make one member a caster with a few town spells (partial SP) so the spell menu has content.
        var mage = s.Party.Members[2];
        mage.Class = CharacterClass.Conjurer;
        mage.MaxSpellPoints = 12;
        mage.SpellPoints = 4; // enough for the cheap spells, not the dear ones
        mage.KnownSpells.Clear();
        mage.KnownSpells.AddRange(new[] { "VOPL", "PURE", "HEPA", "REST" });

        // Fill in a slice of the bestiary so affinities render.
        foreach (var name in new[] { "Giant Centipede", "Gray Ooze", "Skeleton", "Goblin", "Orc", "Ghoul", "Salamander", "Mimic" })
            s.Codex.DiscoverOne(name, 3);

        s.Party.Rest();        // top everyone up to their gear-boosted maxima (no HURT bars in shots)
        mage.SpellPoints = 4;  // ...but leave the caster partly spent so the spell menu shows unaffordable spells
    }

    private static void EnterBuilding(MainWindowViewModel vm, TownBuilding building)
    {
        var pos = vm.Session.Town.Buildings.First(b => b.Building == building).Position;
        vm.Session.TownPosition = pos;
        vm.ShowTownScreen();
        vm.Town!.EnterCommand.Execute(null);
        if (vm.Town!.DeclineQuestOfferCommand.CanExecute(null))
            vm.Town.DeclineQuestOfferCommand.Execute(null); // clear any quest-offer overlay
    }

    private static void DressBuilding(MainWindowViewModel vm, TownBuilding building)
    {
        if (vm.Town is not { } town) return;
        if (building is TownBuilding.Shop or TownBuilding.Smithy)
        {
            town.SelectedHero = town.Party.FirstOrDefault();
            town.SelectedStashItem = town.Stash.FirstOrDefault(s => s.Item.IsAccessory) ?? town.Stash.FirstOrDefault();
        }
    }

    private static async Task Settle()
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(150);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Capture(Control view, string dir, string name)
    {
        var w = (int)Math.Ceiling(view.Bounds.Width);
        var h = (int)Math.Ceiling(view.Bounds.Height);
        if (w <= 0 || h <= 0) { w = 1100; h = 700; }

        using var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        rtb.Render(view);
        rtb.Save(Path.Combine(dir, name + ".png"));
        Console.WriteLine($"[screenshot] {name}.png ({w}x{h})");
    }
}
