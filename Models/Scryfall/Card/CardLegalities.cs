using System.Collections.Generic;

namespace ApiStudy.Models.Scryfall.Card
{
    /// <summary>
    /// Represents the legality of a card across all recognized play formats.
    /// The key is the format name (e.g., "standard") and the value is the status (e.g., "legal", "banned").
    /// </summary>
    public class Legalities : Dictionary<string, string>
    {
        // Inherits from Dictionary<string, string> for flexible format access.
    }
}