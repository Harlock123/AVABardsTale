using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using BardsTale.Core.Characters;
using BardsTale.Core.Items;

namespace BardsTale.UI.ViewModels;

/// <summary>
/// The accessory-set codex: every set the game offers, its pieces and bonus, where to
/// find it, and whether anyone in the party currently has it assembled. A reward/discovery
/// companion to the bestiary.
/// </summary>
public sealed class SetCodexViewModel : ViewModelBase
{
    public SetCodexViewModel(Party party)
    {
        Entries = new ObservableCollection<SetCodexEntryViewModel>(
            AccessorySets.All.Select(s => new SetCodexEntryViewModel(s, party)));
        var assembled = Entries.Count(e => e.IsActive);
        Heading = $"Accessory Sets — {assembled} of {Entries.Count} assembled";
    }

    public ObservableCollection<SetCodexEntryViewModel> Entries { get; }
    public string Heading { get; }
}

/// <summary>One set's card in the codex.</summary>
public sealed class SetCodexEntryViewModel : ViewModelBase
{
    private readonly AccessorySet _set;

    public SetCodexEntryViewModel(AccessorySet set, Party party)
    {
        _set = set;
        ActiveHeroes = party.Members
            .Where(m => m.ActiveSets.Any(s => s.Name == set.Name))
            .Select(m => m.Name)
            .ToList();
    }

    public string Name => _set.Name;
    public string Pieces => "Pieces: " + DescribePieces(_set.Pieces);
    public string Bonus => "Bonus: " + _set.BonusSummary;
    public string Description => _set.Description;
    public string Source => AccessorySets.SourceHint(_set.Name);

    public IReadOnlyList<string> ActiveHeroes { get; }
    public bool IsActive => ActiveHeroes.Count > 0;

    public string Status => IsActive
        ? $"✓ Active on {string.Join(", ", ActiveHeroes)}"
        : "Not yet assembled";

    public IBrush NameBrush => IsActive ? Gold : Slate;
    public IBrush StatusBrush => IsActive ? Green : Slate;

    // Collapses duplicate pieces: ["Ring of Protection","Ring of Protection"] → "Ring of Protection ×2".
    private static string DescribePieces(IReadOnlyList<string> pieces) =>
        string.Join(" + ", pieces
            .GroupBy(p => p)
            .Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key));

    private static readonly IBrush Gold = new SolidColorBrush(Color.Parse("#E8C56B"));
    private static readonly IBrush Slate = new SolidColorBrush(Color.Parse("#8C93AB"));
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#7FB069"));
}
