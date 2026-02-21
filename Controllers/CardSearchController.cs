using ApiStudy.Models.Scryfall;
using ApiStudy.Models.Scryfall.ResponseTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Refit;
using System.Net;

namespace ApiStudy.Controllers
{
    //[Authorize]
    public class CardSearchController : Controller
    {
        private readonly IScryfallApi _scryfallApi;

        public CardSearchController(IScryfallApi scryfallApi)
        {
            _scryfallApi = scryfallApi;
        }

        [HttpGet]
        [Route("api/card-search")]
        /// <summary>
        /// Returns a List object containing Cards found using a fulltext search string. This string supports the same fulltext search system that the main site uses.
        /// </summary>
        public async Task<IActionResult> SearchCards(
            [FromQuery] string query,
            [FromQuery] string? unique = null,
            [FromQuery] string? order = null,
            [FromQuery] string? dir = null,
            [FromQuery] bool? includeExtras = null,
            [FromQuery] bool? includeMultilingual = null,
            [FromQuery] bool? includeVariations = null,
            [FromQuery] int? page = null,
            [FromQuery] string? format = null,
            [FromQuery] bool? pretty = null
        )
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Query parameter is required.");
            }

            try
            {
                ScryfallListCardsResponse? cards = await _scryfallApi.GetCardSearchAsync(
                    query, unique, order, dir, includeExtras, includeMultilingual, includeVariations, page, format, pretty);

                if (cards == null || cards?.Data?.Count == 0)
                {
                    return NotFound("No cards found matching the query.");
                }

                return Ok(cards);
            }
            catch (ApiException aex)
            {
                if (aex.StatusCode == HttpStatusCode.NotFound)
                {
                    return StatusCode(404, "Could not found Cards with the specified parameters");
                }

                if (aex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    return StatusCode(429, "External API rate limit exceeded. Try again later.");
                }

                return StatusCode((int)aex.StatusCode, "An unexpected error occurred in the external API.");
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here for brevity)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("api/card-named")]
        /// <summary>
        /// Returns a Card based on a name search string. This method is designed for building chat bots, forum bots, and other services that need card details quickly.
        /// </summary>
        public async Task<IActionResult> GetCardNamed(
            [FromQuery] string exact, 
            [FromQuery] string fuzzy,
            [FromQuery] string set,
            [FromQuery] string format,
            [FromQuery] string face,
            [FromQuery] string version,
            [FromQuery] bool pretty
        )
        {
            {
                if (string.IsNullOrEmpty(exact) && string.IsNullOrEmpty(fuzzy))
                {
                    return BadRequest("Either 'exact' or 'fuzzy' query parameter must be provided.");
                }
                try
                {
                    var card = await _scryfallApi.GetCardByNameAsync(exact, fuzzy.Replace(@"\s", "+"), set, format, face, version, pretty);
                    if (card == null)
                    {
                        return NotFound("No card found with the specified name and parameters.");
                    }

                    return Ok(card);
                }
                catch (ApiException aex)
                {
                    if (aex.StatusCode == HttpStatusCode.NotFound)
                    {
                        return StatusCode(404, "Could not found a Card with the specified parameters");
                    }

                    if (aex.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        return StatusCode(429, "External API rate limit exceeded. Try again later.");
                    }

                    return StatusCode((int)aex.StatusCode, "An unexpected error occurred in the external API.");
                }
                catch (Exception ex)
                {
                    // Log the exception (not implemented here for brevity)
                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }

        [HttpGet]
        [Route("api/card-id/{id}")]
        /// <summary>
        /// Returns a single card with the given Scryfall ID.
        /// </summary>
        public async Task<IActionResult> GetCardById([FromRoute] Guid id)
        {
            try
            {
                var card = await _scryfallApi.GetCardByIdAsync(id);
                if (card == null)
                {
                    return NotFound($"No card found with ID: {id}");
                }
                return Ok(card);
            }
            catch (ApiException aex)
            {
                if (aex.StatusCode == HttpStatusCode.NotFound)
                {
                    return StatusCode(404, "Could not found any Card with the specified Id");
                }

                if (aex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    return StatusCode(429, "External API rate limit exceeded. Try again later.");
                }

                return StatusCode((int)aex.StatusCode, "An unexpected error occurred in the external API.");
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here for brevity)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("api/cards-autocomplete")]
        public async Task<IActionResult> AutocompleteCardNames(
            [FromQuery] string q, 
            [FromQuery] string? format,
            [FromQuery] bool? pretty,
            [FromQuery] bool? includeExtras
        )
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Query parameter 'q' is required.");
            }
            try
            {
                var response = await _scryfallApi.GetAutocompleteAsync(q, format, pretty, includeExtras);
                if (response == null || response.Data == null || response.Data.Count == 0)
                {
                    return NotFound("No autocomplete suggestions found.");
                }
                return Ok(response);
            }
            catch (ApiException aex)
            {
                if (aex.StatusCode == HttpStatusCode.NotFound)
                {
                    return StatusCode(404, "Could not found any autocomplete suggestions with the specified parameters");
                }
                if (aex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    return StatusCode(429, "External API rate limit exceeded. Try again later.");
                }
                return StatusCode((int)aex.StatusCode, "An unexpected error occurred in the external API.");
            }
            catch (Exception ex)
            {
                // Log the exception (not implemented here for brevity)
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
