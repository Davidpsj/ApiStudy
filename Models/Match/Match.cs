using ApiStudy.Models.Auth;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace ApiStudy.Models.Match
{
    public enum TypeMatch
    {
        Casual = 0,
        Competitive,
        Tournament
    }

    public enum FormatMatch
    {
        CommanderEDH = 0,
        Commander2HG,
        DraftedSealed,
        Brawl2Players,
        BrawlMultiPlayers,
        Pauper,
        Standard,
        Pioneer,
        Modern,
        Legacy,
        Vintage,
        Oathbreaker,
        Alchemy,
        Historic,
        Timeless,
        Penny,
    }

    public class Match : BaseEntity
    {
        [JsonIgnore]
        public User User { get; set; } = null!;
        [JsonIgnore]
        public Guid UserId { get; set; }
        [Required]
        public IList<Player> Players { get; set; } = [];
        [Required]
        public FormatMatch MatchFormat { get; set; }
        [Required]
        public TypeMatch MatchType { get; set; }
        [Required]
        public IDictionary<Guid, int> Scores { get; set; } = new Dictionary<Guid, int>();
        [Required]
        public IDictionary<string, string> PlayerEffects { get; set; } = new Dictionary<string, string>();
    }
}
