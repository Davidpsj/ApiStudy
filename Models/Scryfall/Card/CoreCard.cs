using System.Text.Json.Serialization;

namespace ApiStudy.Models.Scryfall.Card
{
    public class CoreCard
    {
        [JsonPropertyName("id")]
        /// <summary>
        /// A unique ID for this card in Scryfall’s database.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonPropertyName("arena_id")]
        /// <summary>
        /// This card’s Arena ID, if any. A large percentage of cards are not available on Arena and do not have this ID.
        /// </summary>        
        public int ArenaId { get; set; } = 0;

        [JsonPropertyName("mtgo_id")]
        /// <summary>
        /// This card’s Magic Online ID (also known as the Catalog ID), if any. A large percentage of cards are not available on Magic Online and do not have this ID.
        /// </summary>
        public int MtgoId { get; set; } = 0;

        [JsonPropertyName("mtgo_foil_id")]
        /// <summary>
        /// This card’s foil Magic Online ID (also known as the Catalog ID), if any. A large percentage of cards are not available on Magic Online and do not have this ID.
        /// </summary>
        public int MtgoFoilId { get; set; } = 0;

        [JsonPropertyName("tcgplayer_id")]
        /// <summary>
        /// This card’s ID on TCGplayer’s API, also known as the productId.
        /// </summary>
        public int TcgplayerId { get; set; } = 0;

        [JsonPropertyName("tcgplayer_etched_id")]
        /// <summary>
        /// This card’s ID on TCGplayer’s API, for its etched version if that version is a separate product.
        /// </summary>
        public int TcgplayerEtchedId { get; set; } = 0;

        [JsonPropertyName("cardmarket_id")]
        /// <summary>
        /// This card’s ID on Cardmarket’s API, also known as the idProduct.
        /// </summary>
        public int CardmarketId { get; set; } = 0;

        [JsonPropertyName("multiverse_ids")]
        /// <summary>
        /// This card’s multiverse IDs on Gatherer, if any, as an array of integers. Note that Scryfall includes many promo cards, tokens, and other esoteric objects that do not have these identifiers.
        /// </summary>
        public int[] MultiverseId { get; set; } = [];

        [JsonPropertyName("lang")]
        /// <summary>
        /// A language code for this printing.
        /// </summary>
        public string Lang { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        /// <summary>
        /// The English name of this card.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("oracle_id")]
        /// <summary>
        /// A unique ID for this card’s oracle identity. This value is consistent across reprinted card editions, and unique among different cards with the same name (tokens, Unstable variants, etc). Always present except for the reversible_card layout where it will be absent; oracle_id will be found on each face instead.
        /// </summary>
        public Guid OracleId { get; set; } = Guid.NewGuid();

        [JsonPropertyName("object")]
        /// <summary>
        /// A content type for this object, always card
        /// </summary>
        public string Object { get; set; } = "card";

        [JsonPropertyName("layout")]
        /// <summary>
        /// A code for this card’s layout.
        /// </summary>
        public string Layout { get; set; } = string.Empty;

        [JsonPropertyName("prints_search_uri")]
        /// <summary>
        /// A link to where you can begin paginating all re/prints for this card on Scryfall’s API.
        /// </summary>
        public Uri? PrintsSearchUri { get; set; }

        [JsonPropertyName("rulings_uri")]
        /// <summary>
        /// A link to this card’s rulings list on Scryfall’s API.
        /// </summary>
        public Uri? RulingsUri { get; set; }

        [JsonPropertyName("scryfall_uri")]
        /// <summary>
        /// A link to this card’s permapage on Scryfall’s website.
        /// </summary>
        public Uri? ScryfallUri { get; set; }

        [JsonPropertyName("uri")]
        /// <summary>
        /// A link to this card object on Scryfall’s API.
        /// </summary>
        public Uri? Uri { get; set; }
    }
}
