namespace BardsTale.Core.Characters;

/// <summary>The five core attributes used throughout The Bard's Tale.</summary>
public enum Attribute
{
    Strength,
    Intelligence,
    Dexterity,
    Constitution,
    Luck
}

/// <summary>Mutable container for a character's five primary attributes.</summary>
public sealed class AttributeSet
{
    public int Strength { get; set; }
    public int Intelligence { get; set; }
    public int Dexterity { get; set; }
    public int Constitution { get; set; }
    public int Luck { get; set; }

    public int this[Attribute attr]
    {
        get => attr switch
        {
            Attribute.Strength => Strength,
            Attribute.Intelligence => Intelligence,
            Attribute.Dexterity => Dexterity,
            Attribute.Constitution => Constitution,
            Attribute.Luck => Luck,
            _ => 0
        };
        set
        {
            switch (attr)
            {
                case Attribute.Strength: Strength = value; break;
                case Attribute.Intelligence: Intelligence = value; break;
                case Attribute.Dexterity: Dexterity = value; break;
                case Attribute.Constitution: Constitution = value; break;
                case Attribute.Luck: Luck = value; break;
            }
        }
    }

    public AttributeSet Clone() => new()
    {
        Strength = Strength,
        Intelligence = Intelligence,
        Dexterity = Dexterity,
        Constitution = Constitution,
        Luck = Luck
    };

    public static string Abbreviation(Attribute attr) => attr switch
    {
        Attribute.Strength => "ST",
        Attribute.Intelligence => "IQ",
        Attribute.Dexterity => "DX",
        Attribute.Constitution => "CN",
        Attribute.Luck => "LK",
        _ => "??"
    };
}
