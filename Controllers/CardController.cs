using ApiStudy.Filters;
using ApiStudy.Models.Auth;
using ApiStudy.Models.Cards;
using ApiStudy.Models.Scryfall;
using ApiStudy.Models.Scryfall.Card;
using ApiStudy.Repository;
using ApiStudy.Repository.Context;
using ApiStudy.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ApiStudy
{
    public class AddCardToCollectionRequest
    {
        public Guid Card { get; set; }
        public Guid CollectionId { get; set; }
    }

    public class CollectionResponse
    {
        public Collection Collection { get; set; } = null!;

        public List<CoreCard> Cards { get; set; } = [];
    }
}

namespace ApiStudy.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CardController : Controller
    {
        private readonly CardServices _userCardServices;
        private readonly IScryfallApi _scryfallApi;

        public CardController(
            CardServices userCardServices,
            IScryfallApi scryfallApi)
        {
            _userCardServices = userCardServices;
            _scryfallApi = scryfallApi;
        }

        [HttpPost]
        [Route("new-collection")]
        public async Task<IActionResult> AddNewCollectionAsync(Collection collection)
        {
            try
            {
                // 1. Obter o ID do usuário logado (usando o método discutido anteriormente)
                var user = this.User;

                if (user is null || !Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid loggedInUserId))
                {
                    return Unauthorized("Usuário não autenticado ou ID inválido.");
                }

                collection.UserId = loggedInUserId;

                var result = await _userCardServices.AddNewCollectionAsync(collection);

                return Ok(result);
            }
            catch (ArgumentException argEx)
            {
                return BadRequest(argEx.Message);
            }
            catch (NullReferenceException nullEx)
            {
                return NotFound(nullEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet]
        [EnsureUser]
        public async Task<IActionResult> GetCollectionsAsync()
        {
            try
            {
                var loggedInUserId = HttpContext.GetLoggedInUserId();

                var response = await _userCardServices.GetCollectionsAsync(loggedInUserId);

                return Ok(response);
            } 
            catch (NullReferenceException nullEx)
            {
                return NotFound(nullEx.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("cards-collection")]
        public async Task<IActionResult> AddNewCardToCollection([FromBody] ApiStudy.AddCardToCollectionRequest body)
        {
            try
            {
                var response = await _userCardServices.AddNewCardToCollection(body);
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
}
