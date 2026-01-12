using System.Text.Json.Serialization;

namespace ApiStudy.Models.Scryfall.ResponseTypes
{
    public class ScryfallAutocompleteResponse
    {
        [JsonPropertyName("object")]
        public string Object { get; set; } = "list";
        [JsonPropertyName("total_values")]
        public int TotalValues { get; set; } = 0;
        [JsonPropertyName("data")]
        public List<string>? Data { get; set; }
    }
}
