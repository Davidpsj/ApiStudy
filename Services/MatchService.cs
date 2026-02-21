using ApiStudy.Models.Match;
using ApiStudy.Repository;

namespace ApiStudy.Services;

public class MatchService
{
    private readonly IRepository<Match> _matchRepo;

    public MatchService(IRepository<Match> matchRepo)
    {
        _matchRepo = matchRepo;
    }

    /// <summary>
    /// Retorna o total de pontos de vida inicial baseado no formato da partida.
    /// Commander: 40 vidas | 2HG: 30 | Brawl multiplayer: 30 | Brawl 1v1: 25 | Demais: 20.
    /// </summary>
    private static int GetInitialLifeByFormat(FormatMatch format) => format switch
    {
        FormatMatch.CommanderEDH => 40,
        FormatMatch.Commander2HG => 30,
        FormatMatch.BrawlMultiPlayers => 30,
        FormatMatch.Brawl2Players => 25,
        _ => 20,
    };

    public async Task<ICollection<Match>> GetAllMatchesByUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be an empty GUID.", nameof(userId));

        var matches = await _matchRepo.GetByQuery(m => m.UserId == userId)
            ?? throw new NullReferenceException("No matches found for the given user.");

        return matches.ToList();
    }

    public async Task<Match> AddNewMatch(Guid userId, IList<Player> players, TypeMatch matchType, FormatMatch formatMatch)
    {
        if (players == null || players.Count == 0)
            throw new ArgumentException("Players collection cannot be null or empty.", nameof(players));
        if (players.Any(p => p == null))
            throw new ArgumentException("Players collection cannot contain null elements.", nameof(players));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be an empty GUID.", nameof(userId));

        var initialLife = GetInitialLifeByFormat(formatMatch);

        // Garante que todos os jogadores começam com o total de vida correto para o formato,
        // independentemente do valor que veio no request.
        foreach (var player in players)
            player.LifeTotal = initialLife;

        var match = new Match
        {
            UserId = userId,
            Players = players,
            MatchType = matchType,
            MatchFormat = formatMatch,
            // Scores inicia igual ao LifeTotal de cada jogador
            Scores = players.ToDictionary(p => p.Id, _ => initialLife)
        };

        var response = await _matchRepo.CreateAsync(match);
        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ?? throw new InvalidOperationException("Falha ao criar a partida: resposta nula do repositório.");
    }

    public async Task<Match> PatchMatch(Match match)
    {
        if (match is null)
            throw new ArgumentException("Match invalid to Patch.", nameof(match));

        // Recupera a entidade já rastreada pelo contexto para evitar o erro:
        // "cannot be tracked because another instance with the same key value..."
        var exists = await _matchRepo.GetByIdAsync(match.Id)
            ?? throw new NullReferenceException("Match could not be found.");

        // Copia apenas os campos atualizáveis, preservando Id e UserId
        if (match.Players != null && match.Players.Count > 0)
            exists.Players = match.Players;

        if (match.Scores != null && match.Scores.Count > 0)
            exists.Scores = match.Scores;

        var response = await _matchRepo.UpdateAsync(exists.Id, exists);
        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ?? throw new InvalidOperationException("Falha ao atualizar a partida: resposta nula do repositório.");
    }

    /// <summary>
    /// Atualiza os pontos de vida de um jogador em uma partida.
    ///
    /// Convenção de sinal (acordada com o cliente):
    ///   - Valor NEGATIVO = dano recebido (ex: -3 remove 3 de vida)
    ///   - Valor POSITIVO = cura / ganho de vida (ex: +5 adiciona 5 de vida)
    ///
    /// O método simplesmente soma o valor ao LifeTotal atual, o que funciona
    /// corretamente para ambos os casos com a convenção acima.
    /// </summary>
    public async Task<Match> UpdateLifepointsAndScores(Guid? matchId, Guid? playerId, int lifepoints)
    {
        if (matchId is null)
            throw new ArgumentException("Match invalid to Update Life points.", nameof(matchId));
        if (playerId is null)
            throw new ArgumentException("Player Id is invalid to Update Life points.", nameof(playerId));
        if (lifepoints == 0)
            throw new ArgumentException("Lifepoints could not be equal to 0 (zero).", nameof(lifepoints));

        var match = await _matchRepo.GetByIdAsync((Guid)matchId)
            ?? throw new NullReferenceException("Match could not be found.");

        var playerEntry = match.Players
            .Select((p, idx) => new { Player = p, Index = idx })
            .FirstOrDefault(x => x.Player.Id == playerId)
            ?? throw new NullReferenceException($"Player {playerId} could not be found in this Match.");

        var idx = playerEntry.Index;

        // Soma diretamente: negativo = dano, positivo = cura.
        // Ex: LifeTotal=40, lifepoints=-5 → 40 + (-5) = 35 (correto)
        // Ex: LifeTotal=35, lifepoints=+3 → 35 + 3  = 38 (correto)
        match.Players[idx].LifeTotal += lifepoints;

        // Registra o evento no histórico de dano (valor positivo = cura, negativo = dano)
        match.Players[idx].DamageSuffered.Add(lifepoints);

        // Sincroniza o score com o LifeTotal atual do jogador
        match.Scores[(Guid)playerId] = match.Players[idx].LifeTotal;

        var response = await _matchRepo.UpdateAsync((Guid)matchId, match);
        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ?? throw new InvalidOperationException(
            $"Não foi possível atualizar os dados da partida {matchId}. Resposta nula do repositório.");
    }

    public async Task<Match> UpdateCounterEffectsToPlayer(Guid matchId, Guid playerId, CounterType counterName, int counterPoints)
    {
        if (matchId == Guid.Empty)
            throw new ArgumentException("Match Id is invalid to Set Counter Effects.", nameof(matchId));
        if (playerId == Guid.Empty)
            throw new ArgumentException("Player Id is invalid to Set Counter Effects.", nameof(playerId));
        if (counterPoints == 0)
            throw new ArgumentException("Counter Points could not be equal to 0 (zero).", nameof(counterPoints));

        var match = await _matchRepo.GetByIdAsync(matchId)
            ?? throw new NullReferenceException("Match could not be found.");

        var playerEntry = match.Players
            .Select((p, idx) => new { Player = p, Index = idx })
            .FirstOrDefault(x => x.Player.Id == playerId)
            ?? throw new NullReferenceException($"Player {playerId} could not be found in this Match.");

        var idx = playerEntry.Index;

        switch (counterName)
        {
            case CounterType.Poison:
                match.Players[idx].PoisonCounters += counterPoints;
                break;
            case CounterType.Experience:
                match.Players[idx].ExperienceCounters += counterPoints;
                break;
            case CounterType.Energy:
                match.Players[idx].EnergyCounters += counterPoints;
                break;
            case CounterType.Charge:
                match.Players[idx].ChargeCounters += counterPoints;
                break;
            case CounterType.Stun:
                match.Players[idx].StunCounters += counterPoints;
                break;
            case CounterType.Time:
            case CounterType.Custom:
                // Contadores genéricos armazenados no dicionário AnotherCounters
                var key = counterName.ToString();
                match.Players[idx].AnotherCounters ??= new Dictionary<string, int>();
                match.Players[idx].AnotherCounters.TryGetValue(key, out var existing);
                match.Players[idx].AnotherCounters[key] = existing + counterPoints;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(counterName), "Invalid counter type.");
        }

        var response = await _matchRepo.UpdateAsync(matchId, match);
        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ?? throw new InvalidOperationException(
            $"Não foi possível atualizar os contadores do player {playerId} na partida {matchId}. Resposta nula do repositório.");
    }
}