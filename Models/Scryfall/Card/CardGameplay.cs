namespace ApiStudy.Models.Scryfall.Card
{
    public class CardGameplay
    {
        /// <summary>
        /// Gets or sets an array of related card objects if this card is closely related to other cards (e.g., tokens, meld pairs).
        /// </summary>
        /// <value>An array of Related Card Objects. Nullable.</value>
        public object[]? AllParts { get; set; } // Note: object should ideally be replaced with a specific 'RelatedCard' class.

        /// <summary>
        /// Gets or sets an array of Card Face objects, if this card is multifaced.
        /// </summary>
        /// <value>An array of CardFace objects. Nullable.</value>
        public CardFace[]? CardFaces { get; set; }

        // --- Core Rules & Identity ---

        /// <summary>
        /// Gets or sets the card’s mana value (Converted Mana Cost/CMC). Note that some cards have fractional mana costs.
        /// </summary>
        public required decimal Cmc { get; set; }

        /// <summary>
        /// Gets or sets this card’s color identity (used primarily for Commander rules).
        /// </summary>
        /// <value>An array of strings representing the color identity (W, U, B, R, G).</value>
        public required string[] ColorIdentity { get; set; }

        /// <summary>
        /// Gets or sets the colors in this card’s color indicator, if any.
        /// </summary>
        /// <value>An array of strings representing the colors. Nullable.</value>
        public string[]? ColorIndicator { get; set; }

        /// <summary>
        /// Gets or sets this card’s colors, if the overall card has colors defined by the rules.
        /// </summary>
        /// <value>An array of strings representing the card's colors. Nullable.</value>
        public string[]? Colors { get; set; }

        /// <summary>
        /// Gets or sets the mana cost for this card. This is absent for multi-faced cards (where it is on card_faces).
        /// </summary>
        /// <value>The mana cost string. Nullable.</value>
        public string? ManaCost { get; set; }

        /// <summary>
        /// Gets or sets the name of this card. If multifaced, this field contains both names separated by " // ".
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the Oracle text for this card, if any.
        /// </summary>
        /// <value>The Oracle text string. Nullable.</value>
        public string? OracleText { get; set; }

        /// <summary>
        /// Gets or sets the type line of this card (e.g., "Legendary Creature — Human Wizard").
        /// </summary>
        public required string TypeLine { get; set; }

        // --- Combat and Abilities ---

        /// <summary>
        /// Gets or sets this card’s power, if any. Note that some cards have non-numeric powers, such as '*'.
        /// </summary>
        /// <value>The power value string. Nullable.</value>
        public string? Power { get; set; }

        /// <summary>
        /// Gets or sets this card’s toughness, if any. Note that some cards have non-numeric toughnesses, such as '*'.
        /// </summary>
        /// <value>The toughness value string. Nullable.</value>
        public string? Toughness { get; set; }

        /// <summary>
        /// Gets or sets this card’s loyalty, if any. Note that some cards have non-numeric loyalties, such as 'X'.
        /// </summary>
        /// <value>The loyalty value string. Nullable.</value>
        public string? Loyalty { get; set; }

        /// <summary>
        /// Gets or sets this face’s defense value, if any (e.g., for Battles).
        /// </summary>
        /// <value>The defense value string. Nullable.</value>
        public string? Defense { get; set; }

        /// <summary>
        /// Gets or sets an array of keywords that this card uses, such as 'Flying' and 'Cumulative upkeep'.
        /// </summary>
        public required string[] Keywords { get; set; }

        /// <summary>
        /// Gets or sets colors of mana that this card could produce.
        /// </summary>
        /// <value>An array of strings representing the colors of produced mana. Nullable.</value>
        public string[]? ProducedMana { get; set; }

        // --- Game Format Details ---

        /// <summary>
        /// Gets or sets an object describing the legality of this card across play formats.
        /// </summary>
        /// <value>The object mapping format name to legality status (legal, banned, etc.).</value>
        public required object Legalities { get; set; } // Note: object should ideally be replaced with a specific 'Legalities' class.

        /// <summary>
        /// Gets or sets whether this card is on the Reserved List.
        /// </summary>
        public bool Reserved { get; set; }

        // --- Vanguard Details ---

        /// <summary>
        /// Gets or sets this card’s hand modifier, if it is a Vanguard card. This value will contain a delta, such as '-1'.
        /// </summary>
        /// <value>The hand modifier string. Nullable.</value>
        public string? HandModifier { get; set; }

        /// <summary>
        /// Gets or sets this card’s life modifier, if it is a Vanguard card. This value will contain a delta, such as '+2'.
        /// </summary>
        /// <value>The life modifier string. Nullable.</value>
        public string? LifeModifier { get; set; }

        // --- Ranks and Popularity ---

        /// <summary>
        /// Gets or sets this card’s overall rank/popularity on EDHREC. Not all cards are ranked.
        /// </summary>
        /// <value>The EDHREC rank integer. Nullable.</value>
        public int? EdhrecRank { get; set; }

        /// <summary>
        /// Gets or sets whether this card is on the Commander Game Changer list.
        /// </summary>
        /// <value>True if the card is a Game Changer. Nullable.</value>
        public bool? GameChanger { get; set; }

        /// <summary>
        /// Gets or sets this card’s rank/popularity on Penny Dreadful. Not all cards are ranked.
        /// </summary>
        /// <value>The Penny Dreadful rank integer. Nullable.</value>
        public int? PennyRank { get; set; }
    }
}
