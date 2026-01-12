using System.Text.Json.Serialization;
using ApiStudy.Models.Auth;

namespace ApiStudy.Models.Cards;

public class Collection : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [JsonIgnore]
    public Guid UserId { get; set; }

    [JsonIgnore]
    public ICollection<Card> Cards { get; set; } = [];
}
