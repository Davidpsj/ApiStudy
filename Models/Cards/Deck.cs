using ApiStudy.Models.Auth;

namespace ApiStudy.Models.Cards;

public class Deck : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GameFormat { get; set; } = "commander";
    public ICollection<Card>? Cards { get; set; }
}
