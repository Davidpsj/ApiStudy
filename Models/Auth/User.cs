using ApiStudy.Models.Cards;

namespace ApiStudy.Models.Auth;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;

    public ICollection<Collection> Collections { get; set; } = [];

    public ICollection<Deck> Decks { get; set; } = [];

    public ICollection<Feature> Features { get; set; } = [];

    public ICollection<Match.Match> Matches { get; set; } = [];
}
