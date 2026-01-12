using ApiStudy.Models.Auth;
using System.Text.Json.Serialization;

namespace ApiStudy.Models.Cards;

public class Card : BaseEntity
{
    public ICollection<Collection> Collections { get; set; } = [];
    public ICollection<Deck> Decks { get; set; } = [];
}
