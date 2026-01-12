using ApiStudy.Models.Match;
using ApiStudy.Repository;
using System.Linq;

namespace ApiStudy.Services;

public class MatchService
{
    private readonly IRepository<Match> _matchRepo;

    public MatchService(IRepository<Match> matchRepo)
    {
        _matchRepo = matchRepo;
    }

    private int GetScoresByFormatMatch(FormatMatch format)
    {
        return format switch
        {
            FormatMatch.CommanderEDH => 40,
            FormatMatch.Commander2HG => 30,
            FormatMatch.Brawl2Players => 25,
            FormatMatch.BrawlMultiPlayers => 30,
            _ => 20,
        };
    }

    public async Task<ICollection<Match>> GetAllMatchesByUserId(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be an empty GUID.", nameof(userId));

        var matches = await _matchRepo.GetByQuery(m => m.UserId == userId) ?? 
            throw new NullReferenceException("No matches found for the given user.");

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

        var match = new Match
        {
            UserId = userId,
            Players = players,
            MatchType = matchType,
            MatchFormat = formatMatch,
            Scores = players.ToDictionary(
                p => p.Id,
                p => GetScoresByFormatMatch(formatMatch)
            )
        };

        var response = await _matchRepo.CreateAsync(match);
        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        // Garantir que não retorna nulo
        if (response is null)
            throw new InvalidOperationException("Falha ao criar a partida: resposta nula do repositório.");

        return response;
    }

    public async Task<Match> PatchMatch(Match match)
    {
        // Pseudocódigo detalhado (plano):
        // 1. Validar parâmetro 'match' (não nulo).
        // 2. Recuperar a entidade existente do repositório por Id.
        // 3. Se não existir, lançar exceção apropriada.
        // 4. Para evitar erro de "tracking" do EF (duas instâncias com mesmo Id):
        //    4.1. NÃO anexar a instância 'match' diretamente ao contexto se já houver uma instância rastreada.
        //    4.2. Em vez disso, copiar os valores alteráveis da instância recebida para a instância já rastreada ('exists').
        //    4.3. Só atualizar propriedades que fazem sentido atualizar (evitar alterar 'Id' e 'UserId').
        // 5. Chamar o método de update do repositório passando a instância rastreada atualizada.
        // 6. Salvar mudanças se não estiver usando transação externa.
        // 7. Garantir que a resposta não seja nula e retornar a entidade atualizada.
        //
        // Observações de implementação:
        // - Preservar comportamentos de validação/erros já presentes no serviço.
        // - Copiar coleções e dicionários substituindo-os somente quando não nulos na entrada.
        // - Manter mensagens de erro existentes (em inglês/português conforme o projeto).
        if (match is null)
            throw new ArgumentException("Match invalid to Patch.", nameof(match));

        // Recupera a entidade já rastreada pelo contexto
        var exists = await _matchRepo.GetByIdAsync(match.Id);

        if (exists is null)
            throw new NullReferenceException("Match could not found");

        // Copiar somente os campos atualizáveis para a entidade rastreada 'exists'
        // Evita anexar 'match' (nova instância) ao DbContext quando 'exists' já está sendo rastreada,
        // que é a causa do erro: "cannot be tracked because another instance with the same key value..."
        if (match.Players != null && match.Players.Count > 0)
            exists.Players = match.Players;

        // Se for aceitável sobrescrever MatchType/MatchFormat mesmo quando valores padrão são passados,
        // mantém a atribuição direta. Ajuste conforme a semântica de patch desejada.
        //exists.MatchType = match.MatchType;
        //exists.MatchFormat = match.MatchFormat;

        if (match.Scores != null && match.Scores.Count > 0)
        {
            exists.Scores = match.Scores;
        }

        // Caso existam outras propriedades que devam ser atualizadas, copiar aqui de forma controlada.
        // Ex.: exists.SomeProp = match.SomeProp ?? exists.SomeProp;

        var response = await _matchRepo.UpdateAsync(exists.Id, exists);
        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ??
            throw new InvalidOperationException("Falha ao atualizar a partida: resposta nula do repositório.");
    }

    public async Task<Match> UpdateLifepointsAndScores(Guid? matchId, Guid? playerId, int lifepoints)
    {
        if (matchId is null)
            throw new ArgumentException("Match invalid to Update Life points", nameof(matchId));

        if (playerId is null)
            throw new ArgumentException("Player Id is invalid to Update Life points", nameof(playerId));

        if (lifepoints == 0)
            throw new ArgumentException("Lifepoints could not be equal to 0 (zero)", nameof(lifepoints));

        var match = await _matchRepo.GetByIdAsync((Guid)matchId) ?? 
            throw new NullReferenceException("Match could not be found");

        var playerIdx = match.Players.Select((p, idx) => new { Item = p, Index = idx })
                .FirstOrDefault(x => x.Item.Id == playerId)
                ?.Index ?? throw new NullReferenceException($"Player {playerId} could not be found in this Match");

        if (lifepoints < 0)
        {
            // Damage
            match.Players[playerIdx].LifeTotal -= lifepoints;
        } else {
            // Lifegain
            match.Players[playerIdx].LifeTotal += lifepoints;            
        }
        
        match.Players[playerIdx].DamageSuffered.Add(lifepoints);
        match.Scores[(Guid)playerId] = match.Players[playerIdx].LifeTotal;

        var response = await _matchRepo.UpdateAsync((Guid)matchId, match);

        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ??
            throw new InvalidOperationException(
                $"Não foi possível atualizar os dados da partida {matchId}. Resposta nula do repositório.");
    }

    public async Task<Match> UpdateCounterEffectsToPlayer(Guid matchId, Guid playerId, CounterType counterName, int counterPoints)
    {
        if (matchId == Guid.Empty)
            throw new ArgumentException("Match Id is invalid to Set Counter Effects", nameof(matchId));

        if (playerId == Guid.Empty)
            throw new ArgumentException("Player Id is invalid to Set Counter Effects", nameof(playerId));

        if (counterPoints == 0)
            throw new ArgumentException("Counter Points could not be equal to 0 (zero)", nameof(counterPoints));

        var match = await _matchRepo.GetByIdAsync(matchId) ??
            throw new NullReferenceException("Match could not be found");

        var playerIdx = match.Players.Select((p, idx) => new { Item = p, Index = idx })
                .FirstOrDefault(x => x.Item.Id == playerId)
                ?.Index ?? throw new NullReferenceException($"Player {playerId} could not be found in this Match");

        switch(counterName)
        {
            case CounterType.Poison:
                match.Players[playerIdx].PoisonCounters += counterPoints;
                break;
            case CounterType.Experience:
                match.Players[playerIdx].ExperienceCounters += counterPoints;
                break;
            case CounterType.Energy:
                match.Players[playerIdx].EnergyCounters += counterPoints;
                break;
            case CounterType.Charge:
                match.Players[playerIdx].ChargeCounters += counterPoints;
                break;
            case CounterType.Stun:
                match.Players[playerIdx].StunCounters += counterPoints;
                break;
            case CounterType.Time:
                match.Players[playerIdx].AnotherCounters ??= new Dictionary<string, int>();
                if (match.Players[playerIdx].AnotherCounters.TryGetValue("Time", out var existingPoints))
                {
                    match.Players[playerIdx].AnotherCounters["Time"] = existingPoints + counterPoints;
                }
                else
                {
                    match.Players[playerIdx].AnotherCounters["Time"] = counterPoints;
                }
                break;
            case CounterType.Custom:
                match.Players[playerIdx].AnotherCounters ??= new Dictionary<string, int>();
                if (match.Players[playerIdx].AnotherCounters.TryGetValue("Custom", out var existingCustomPoints))
                {
                    match.Players[playerIdx].AnotherCounters["Custom"] = existingCustomPoints + counterPoints;
                }
                else
                {
                    match.Players[playerIdx].AnotherCounters["Custom"] = counterPoints;
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(counterName), "Invalid counter type.");
        }

        var response = await _matchRepo.UpdateAsync(matchId, match);

        if (!_matchRepo.UsingTransaction) await _matchRepo.SaveChangesAsync();

        return response ??
            throw new InvalidOperationException(
                $"Não foi possível atualizar os contadores do player {playerId} nesta partida {matchId}. Resposta nula do repositório.");
    }
}