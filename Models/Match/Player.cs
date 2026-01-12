
namespace ApiStudy.Models.Match;

public enum CounterType
{
    Poison = 0,
    Experience,
    Energy,
    Charge,
    Stun,
    Time,
    Custom
}

public class Player : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int LifeTotal { get; set; } = 40;
    public int ChargeCounters { get; set; } = 0;
    public int PoisonCounters { get; set; } = 0;
    public int EnergyCounters { get; set; } = 0;
    public int ExperienceCounters { get; set; } = 0;
    public int StunCounters { get; set; } = 0;

    public IDictionary<string, int> AnotherCounters { get; set; } = new Dictionary<string, int>();
    public IList<int> DamageSuffered { get; set; } = [];
    public IList<IDictionary<string, ICollection<int>>> CommanderDamageSuffered { get; set; } = [];
}
