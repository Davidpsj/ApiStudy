namespace ApiStudy.Models.Scryfall.Card
{
    /// <summary>
    /// Represents a specific printing (version) of a Magic: The Gathering card,
    /// detailing print-specific information like set, rarity, prices, and artist.
    /// </summary>
    public class PrintFaces
    {
        /// <summary>
        /// Gets or sets the name of the illustrator of this card.
        /// </summary>
        /// <value>The name of the illustrator. May be null for newly spoiled cards.</value>
        public string? Artist { get; set; }

        /// <summary>
        /// Gets or sets the IDs of the artists that illustrated this card.
        /// </summary>
        /// <value>An array of unique illustrator IDs. May be null for newly spoiled cards.</value>
        public Guid[]? ArtistIds { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier for the card artwork that remains consistent across reprints.
        /// </summary>
        /// <value>The unique illustration ID. Nullable for newly spoiled cards.</value>
        public Guid? IllustrationId { get; set; }

        // --- Print Details ---

        /// <summary>
        /// Gets or sets the lit Unfinity attractions lights on this card, if any.
        /// </summary>
        /// <value>An array of strings representing attraction lights. Nullable.</value>
        public string[]? AttractionLights { get; set; }

        /// <summary>
        /// Gets or sets whether this card is found in boosters.
        /// </summary>
        public bool Booster { get; set; }

        /// <summary>
        /// Gets or sets this card’s border color: black, white, borderless, yellow, silver, or gold.
        /// </summary>
        public required string BorderColor { get; set; }

        /// <summary>
        /// Gets or sets the Scryfall ID for the card back design present on this card.
        /// </summary>
        public required Guid CardBackId { get; set; }

        /// <summary>
        /// Gets or sets this card’s collector number. Note that collector numbers can contain non-numeric characters.
        /// </summary>
        public required string CollectorNumber { get; set; }

        /// <summary>
        /// Gets or sets whether to consider avoiding use of this print downstream.
        /// </summary>
        /// <value>True if content warnings apply. Nullable.</value>
        public bool? ContentWarning { get; set; }

        /// <summary>
        /// Gets or sets whether this card was only released in a video game (e.g., MTGO or Arena-only).
        /// </summary>
        public bool Digital { get; set; }

        /// <summary>
        /// Gets or sets an array of computer-readable flags that indicate available finishes (foil, nonfoil, etched).
        /// </summary>
        public required string[] Finishes { get; set; }

        /// <summary>
        /// Gets or sets the just-for-fun name printed on the card (e.g., Godzilla series cards).
        /// </summary>
        /// <value>The flavor name. Nullable.</value>
        public string? FlavorName { get; set; }

        /// <summary>
        /// Gets or sets the flavor text, if any.
        /// </summary>
        /// <value>The flavor text string. Nullable.</value>
        public string? FlavorText { get; set; }

        /// <summary>
        /// Gets or sets this card’s frame effects, if any.
        /// </summary>
        /// <value>An array of frame effects strings. Nullable.</value>
        public string[]? FrameEffects { get; set; }

        /// <summary>
        /// Gets or sets this card’s frame layout (e.g., "new", "old", "showcase").
        /// </summary>
        public required string Frame { get; set; }

        /// <summary>
        /// Gets or sets whether this card’s artwork is larger than normal.
        /// </summary>
        public bool FullArt { get; set; }

        /// <summary>
        /// Gets or sets a list of games that this card print is available in (paper, arena, mtgo).
        /// </summary>
        public required string[] Games { get; set; }

        /// <summary>
        /// Gets or sets whether this card’s imagery is high resolution.
        /// </summary>
        public bool HighresImage { get; set; }

        /// <summary>
        /// Gets or sets a computer-readable indicator for the state of this card’s image (missing, placeholder, lowres, highres_scan).
        /// </summary>
        public required string ImageStatus { get; set; }

        /// <summary>
        /// Gets or sets an object listing available imagery for this card.
        /// </summary>
        /// <value>The object containing image URIs. Nullable. (Type should ideally be a dedicated ImageUris class).</value>
        public object? ImageUris { get; set; }

        /// <summary>
        /// Gets or sets whether this card is oversized.
        /// </summary>
        public bool Oversized { get; set; }

        /// <summary>
        /// Gets or sets an object containing daily price information for this card.
        /// </summary>
        /// <value>The object containing price strings (usd, eur, tix, etc.).</value>
        public required object Prices { get; set; }

        /// <summary>
        /// Gets or sets the localized name printed on this card, if any.
        /// </summary>
        /// <value>The printed and localized name. Nullable.</value>
        public string? PrintedName { get; set; }

        /// <summary>
        /// Gets or sets the localized text printed on this card, if any.
        /// </summary>
        /// <value>The printed and localized text. Nullable.</value>
        public string? PrintedText { get; set; }

        /// <summary>
        /// Gets or sets the localized type line printed on this card, if any.
        /// </summary>
        /// <value>The printed and localized type line. Nullable.</value>
        public string? PrintedTypeLine { get; set; }

        /// <summary>
        /// Gets or sets whether this card is a promotional print.
        /// </summary>
        public bool Promo { get; set; }

        /// <summary>
        /// Gets or sets an array of strings describing what categories of promo cards this card falls into.
        /// </summary>
        /// <value>The array of promo types. Nullable.</value>
        public string[]? PromoTypes { get; set; }

        /// <summary>
        /// Gets or sets an object providing URIs to this card’s listing on major marketplaces. Omitted if the card is unpurchaseable.
        /// </summary>
        /// <value>The object of purchase URIs. Nullable.</value>
        public object? PurchaseUris { get; set; }

        /// <summary>
        /// Gets or sets this card’s rarity: common, uncommon, rare, special, mythic, or bonus.
        /// </summary>
        public required string Rarity { get; set; }

        /// <summary>
        /// Gets or sets an object providing URIs to this card’s listing on other Magic: The Gathering online resources.
        /// </summary>
        public required object RelatedUris { get; set; }

        /// <summary>
        /// Gets or sets the date this card was first released.
        /// </summary>
        public required DateTime ReleasedAt { get; set; }

        /// <summary>
        /// Gets or sets whether this card is a reprint.
        /// </summary>
        public bool Reprint { get; set; }

        // --- Set Details ---

        /// <summary>
        /// Gets or sets a link to this card’s set on Scryfall’s website.
        /// </summary>
        public required Uri ScryfallSetUri { get; set; }

        /// <summary>
        /// Gets or sets this card’s full set name.
        /// </summary>
        public required string SetName { get; set; }

        /// <summary>
        /// Gets or sets a link to where you can begin paginating this card’s set on the Scryfall API.
        /// </summary>
        public required Uri SetSearchUri { get; set; }

        /// <summary>
        /// Gets or sets the type of set this printing is in.
        /// </summary>
        public required string SetType { get; set; }

        /// <summary>
        /// Gets or sets a link to this card’s set object on Scryfall’s API.
        /// </summary>
        public required Uri SetUri { get; set; }

        /// <summary>
        /// Gets or sets this card’s set code (e.g., "MOM", "LTR").
        /// </summary>
        public required string Set { get; set; }

        /// <summary>
        /// Gets or sets this card’s Set object UUID.
        /// </summary>
        public required Guid SetId { get; set; }

        // --- Other Print Flags ---

        /// <summary>
        /// Gets or sets whether this card is a Story Spotlight.
        /// </summary>
        public bool StorySpotlight { get; set; }

        /// <summary>
        /// Gets or sets whether the card is printed without text.
        /// </summary>
        public bool Textless { get; set; }

        /// <summary>
        /// Gets or sets whether this card is a variation of another printing.
        /// </summary>
        public bool Variation { get; set; }

        /// <summary>
        /// Gets or sets the printing ID of the printing this card is a variation of.
        /// </summary>
        /// <value>The printing ID UUID. Nullable.</value>
        public Guid? VariationOf { get; set; }

        /// <summary>
        /// Gets or sets the security stamp on this card, if any (e.g., oval, triangle, acorn).
        /// </summary>
        /// <value>The security stamp string. Nullable.</value>
        public string? SecurityStamp { get; set; }

        /// <summary>
        /// Gets or sets this card’s watermark, if any.
        /// </summary>
        /// <value>The watermark text. Nullable.</value>
        public string? Watermark { get; set; }

        // --- Preview Details (Nested Properties) ---

        /// <summary>
        /// Contains details about when and where this card was previewed.
        /// </summary>
        public PreviewData? Preview { get; set; }
    }

    /// <summary>
    /// Nested class to handle preview data, as it is structured under a "preview" object.
    /// </summary>
    public class PreviewData
    {
        /// <summary>
        /// Gets or sets the date this card was previewed.
        /// </summary>
        /// <value>The preview date. Nullable.</value>
        public DateTime? PreviewedAt { get; set; }

        /// <summary>
        /// Gets or sets a link to the preview source for this card.
        /// </summary>
        /// <value>The source URI. Nullable.</value>
        public Uri? SourceUri { get; set; }

        /// <summary>
        /// Gets or sets the name of the source that previewed this card.
        /// </summary>
        /// <value>The name of the source. Nullable.</value>
        public string? Source { get; set; }
    }
}