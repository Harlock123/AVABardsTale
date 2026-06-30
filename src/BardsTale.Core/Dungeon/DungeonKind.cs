namespace BardsTale.Core.Dungeon;

/// <summary>
/// Which of Skara Brae's two delves the party is exploring. The catacombs are the main quest
/// (twenty floors down to Mangar, who wins the game); the Gloomy Tower is a shorter, optional
/// side-delve with its own boss band that does <em>not</em> win the game.
/// </summary>
public enum DungeonKind
{
    Catacombs,
    Tower
}
