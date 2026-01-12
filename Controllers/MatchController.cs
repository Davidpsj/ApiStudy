using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApiStudy.Models.Match;
using ApiStudy.Filters;

namespace ApiStudy.Controllers;

public class MatchPatchUpdatesBaseRequest
{
    public Guid MatchId { get; set; }
    public Guid PlayerId { get; set; }
}

// DTO local para evitar que a validação automática do [ApiController]
// exija a propriedade 'User' no body JSON.
public class MatchCreateRequest
{
    public List<Player>? Players { get; set; }
    public int MatchFormat { get; set; }
    public int MatchType { get; set; }

    // incluir outras propriedades do body conforme necessário,
    // mas sem a propriedade 'User' que causa validação automática 400.
}

public class MatchPatchRequest
{
    public Guid Id { get; set; }
    public IList<Player> Players { get; set; } = null!;
}

public class MatchPatchLifepointsRequest : MatchPatchUpdatesBaseRequest
{
    public int Lifepoints { get; set; }
}

public class MatchCountersRequest : MatchPatchUpdatesBaseRequest
{
    public CounterType CounterType { get; set; }
    public int CounterValue { get; set; }
}


[Authorize]
[ApiController]
[Route("api/[controller]")]
[EnsureUser]
public class MatchController : Controller
{
    private readonly Services.MatchService _matchService;
    public MatchController(Services.MatchService matchService)
    {
        _matchService = matchService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllMatches()
    {
        try
        {
            var loggedInUserId = HttpContext.GetLoggedInUserId();

            var response = await _matchService.GetAllMatchesByUserId(loggedInUserId);
            
            return Ok(response);
        }
        catch (ArgumentException aex)
        {
            return BadRequest(aex.Message);
        }
        catch (NullReferenceException nex)
        {
            return NotFound(nex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateMatch(MatchCreateRequest request)
    {
        try
        {
            var loggedInUserId = HttpContext.GetLoggedInUserId();

            // Construir a entidade Match a partir do DTO, adicionando o UserId
            var match = new Match
            {
                UserId = loggedInUserId,
                Players = request.Players ?? new List<Player>(),
                MatchFormat = (FormatMatch)request.MatchFormat,
                MatchType = (TypeMatch)request.MatchType
            };

            var response = await _matchService.AddNewMatch(loggedInUserId, match.Players, match.MatchType, match.MatchFormat);

            return Ok(response);
        } 
        catch (ArgumentException aex)
        {
            return BadRequest(aex.Message);
        }
        catch (NullReferenceException nex)
        {
            return BadRequest(nex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpPatch]
    public async Task<IActionResult> UpdateMatch(MatchPatchRequest request)
    {
        try
        {
            var loggedInUserId = HttpContext.GetLoggedInUserId();

            var match = new Match() 
            {
                Id = request.Id,
                UserId = loggedInUserId,
                Players = request.Players
            };

            var response = await _matchService.PatchMatch(match);

            return Ok(response);
        }
        catch (ArgumentNullException anex)
        {
            return BadRequest(anex.Message);
        }
        catch (NullReferenceException nrex) 
        { 
            return BadRequest(nrex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpPatch]
    [Route("lifepoints")]
    public async Task<IActionResult> UpdateLifepointsAndScores(MatchPatchLifepointsRequest request)
    {
        try
        {
            var loggedInUserId = HttpContext.GetLoggedInUserId();

            if (loggedInUserId == Guid.Empty)
                return Unauthorized("User not logged in.");

            var response = await _matchService.UpdateLifepointsAndScores(request.MatchId, request.PlayerId, request.Lifepoints);
            return Ok(response);
        }
        catch (ArgumentNullException anex)
        {
            return BadRequest(anex.Message);
        }
        catch (NullReferenceException nrex)
        {
            return BadRequest(nrex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpPatch]
    [Route("update-counters")]
    public async Task<IActionResult> UpdatePlayerCounters(MatchCountersRequest request)
    {
        try
        {
            var loggedInUserId = HttpContext.GetLoggedInUserId();

            if (loggedInUserId == Guid.Empty)
                return Unauthorized("User not logged in.");

            var response = await _matchService.UpdateCounterEffectsToPlayer(request.MatchId, request.PlayerId, request.CounterType, request.CounterValue);
            
            return Ok(response);
        }
        catch (ArgumentNullException anex)
        {
            return BadRequest(anex.Message);
        }
        catch (NullReferenceException nrex)
        {
            return BadRequest(nrex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}