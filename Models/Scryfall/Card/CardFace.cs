namespace ApiStudy.Models.Scryfall.Card
{
    /// <summary>
    /// Represents a single face of a Magic: The Gathering card,
    /// typically used for double-faced cards or cards with multiple faces.
    /// </summary>
    public class CardFace
    {
        /// <summary>
        /// Gets or sets the name of the illustrator of this card face.
        /// </summary>
        /// <value>The name of the illustrator. May be null for newly spoiled cards.</value>
        public string? Artist { get; set; }

        /// <summary>
        /// Gets or sets the unique ID of the illustrator of this card face.
        /// </summary>
        /// <value>The unique ID of the illustrator. May be null for newly spoiled cards.</value>
        public Guid? ArtistId { get; set; }

        /// <summary>
        /// Gets or sets the mana value (Converted Mana Cost) of this particular face, if the card is reversible.
        /// </summary>
        /// <value>The mana value of this face. Nullable.</value>
        public decimal? Cmc { get; set; }

        /// <summary>
        /// Gets or sets the colors in this face’s color indicator, if any.
        /// </summary>
        /// <value>An array of strings representing the colors in the color indicator. Nullable.</value>
        public string[]? ColorIndicator { get; set; }

        /// <summary>
        /// Gets or sets this face’s colors, if the game defines colors for the individual face of this card.
        /// </summary>
        /// <value>An array of strings representing the face's colors. Nullable.</value>
        public string[]? Colors { get; set; }

        /// <summary>
        /// Gets or sets this face’s defense value, if any (e.g., for Battles).
        /// </summary>
        /// <value>The defense value. Nullable.</value>
        public string? Defense { get; set; }

        /// <summary>
        /// Gets or sets the flavor text printed on this face, if any.
        /// </summary>
        /// <value>The flavor text. Nullable.</value>
        public string? FlavorText { get; set; }

        /// <summary>
        /// Gets or sets a unique identifier for the card face artwork that remains consistent across reprints.
        /// </summary>
        /// <value>The illustration ID. May be null for newly spoiled cards.</value>
        public Guid? IllustrationId { get; set; }

        /// <summary>
        /// Gets or sets an object providing URIs to imagery for this face, if this is a double-sided card.
        /// </summary>
        /// <value>The object of image URIs. Nullable. The type can be refined to a specific class.</value>
        public object? ImageUris { get; set; }

        /// <summary>
        /// Gets or sets the layout of this card face, if the card is reversible.
        /// </summary>
        /// <value>The layout of the face. Nullable.</value>
        public string? Layout { get; set; }

        /// <summary>
        /// Gets or sets this face’s loyalty value, if any (e.g., for Planeswalkers).
        /// </summary>
        /// <value>The loyalty value. Nullable.</value>
        public string? Loyalty { get; set; }

        /// <summary>
        /// Gets or sets the mana cost for this face.
        /// </summary>
        /// <value>The mana cost string. This value will be an empty string ("") if the cost is absent.</value>
        public required string ManaCost { get; set; }

        /// <summary>
        /// Gets or sets the name of this particular face.
        /// </summary>
        /// <value>The name of the face.</value>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets a content type for this object, which is always "card_face".
        /// </summary>
        /// <value>The object content type string.</value>
        public required string Object { get; set; } = "card_face";

        /// <summary>
        /// Gets or sets the Oracle ID of this particular face, if the card is reversible.
        /// </summary>
        /// <value>The Oracle ID of the face. Nullable.</value>
        public Guid? OracleId { get; set; }

        /// <summary>
        /// Gets or sets the Oracle text for this face, if any.
        /// </summary>
        /// <value>The Oracle text. Nullable.</value>
        public string? OracleText { get; set; }

        /// <summary>
        /// Gets or sets this face’s power, if any. Note that some cards have powers that are not numeric, such as "*".
        /// </summary>
        /// <value>The power value. Nullable.</value>
        public string? Power { get; set; }

        /// <summary>
        /// Gets or sets the localized name printed on this face, if any.
        /// </summary>
        /// <value>The printed and localized name. Nullable.</value>
        public string? PrintedName { get; set; }

        /// <summary>
        /// Gets or sets the localized text printed on this face, if any.
        /// </summary>
        /// <value>The printed and localized text. Nullable.</value>
        public string? PrintedText { get; set; }

        /// <summary>
        /// Gets or sets the localized type line printed on this face, if any.
        /// </summary>
        /// <value>The printed and localized type line. Nullable.</value>
        public string? PrintedTypeLine { get; set; }

        /// <summary>
        /// Gets or sets this face’s toughness, if any.
        /// </summary>
        /// <value>The toughness value. Nullable.</value>
        public string? Toughness { get; set; }

        /// <summary>
        /// Gets or sets the type line of this particular face, if the card is reversible.
        /// </summary>
        /// <value>The type line of the face. Nullable.</value>
        public string? TypeLine { get; set; }

        /// <summary>
        /// Gets or sets the watermark on this particulary card face, if any.
        /// </summary>
        /// <value>The watermark text. Nullable.</value>
        public string? Watermark { get; set; }
    }
}
