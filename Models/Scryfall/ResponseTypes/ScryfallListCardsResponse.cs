using ApiStudy.Models.Scryfall.Card;
using System.Text.Json.Serialization;

namespace ApiStudy.Models.Scryfall.ResponseTypes
{
    public class ScryfallListCardsResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";
        [JsonPropertyName("total_cards")]
        public int TotalCards { get; set; } = 0;
        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; } = false;
        [JsonPropertyName("next_page")]
        public string? NextPage { get; set; }
        [JsonPropertyName("data")]
        public List<CoreCard>? Data { get; set; }
    }
}
