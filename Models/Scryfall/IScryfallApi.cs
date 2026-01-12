using ApiStudy.Models.Scryfall.Card;
using ApiStudy.Models.Scryfall.ResponseTypes;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;
using Refit;
using System.Numerics;

namespace ApiStudy.Models.Scryfall
{
    public interface IScryfallApi
    {
        [Get("/cards/search")]
        Task<ScryfallListCardsResponse> GetCardSearchAsync(
            ///<param name="query">A fulltext search query. Make sure that your parameter is properly encoded. Maximum length: 1000 Unicode characters.</param>
            [AliasAs("q")] string query,
            ///<param name="unique">The strategy for omitting similar cards. See below.</param>
            [AliasAs("unique")] string? unique = null,
            ///<param name="order">The method to sort returned cards. See below.</param>
            [AliasAs("order")] string? order = null,
            ///<param name="dir">The direction to sort cards. See below.</param>
            [AliasAs("dir")] string? dir = null,
            ///<param name="include_extras">If true, extra cards (tokens, planes, etc) will be included. Equivalent to adding include:extras to the fulltext search. Defaults to false.</param>
            [AliasAs("include_extras")] bool? includeExtras = null,
            ///<param name="include_multilingual">If true, cards in every language supported by Scryfall will be included. Defaults to false.</param>
            [AliasAs("include_multilingual")] bool? includeMultilingual = null,
            ///<param name="include_variations">If true, rare care variants will be included, like the Hairy Runesword. Defaults to false.</param>
            [AliasAs("include_variations")] bool? includeVariations = null,
            ///<param name="page">The page number to return, default 1.</param>
            [AliasAs("page")] int? page = null,
            ///<param name="format">The data format to return: json or csv. Defaults to json.</param>
            [AliasAs("format")] string? format = null,
            ///<param name="pretty">If true, the returned JSON will be prettified. Avoid using for production code.</param>
            [AliasAs("pretty")] bool? pretty = null
        );

        [Get("/cards/named")]
        Task<CoreCard> GetCardByNameAsync(
            ///<param name="exact">The exact card name to search for, case insenstive.</param>
            [AliasAs("exact")] string exact,
            ///<param name="fuzzy">A fuzzy card name to search for.</param>
            [AliasAs("fuzzy")] string fuzzy,
            ///<param name="set">A set code to limit the search to one set.</param>
            [AliasAs("set")] string? set = null,
            ///<param name="format">The data format to return: json, text, or image. Defaults to json.</param>
            [AliasAs("format")] string? format = null,
            ///<param name="face">If using the image format and this parameter has the value back, the back face of the card will be returned. Will return a 422 if this card has no back face.</param>
            [AliasAs("face")] string? face = null,
            ///<param name="version">The image version to return when using the image format: small, normal, large, png, art_crop, or border_crop. Defaults to large.</param>
            [AliasAs("version")] string? version = null,
            ///<param name="pretty">If true, the returned JSON will be prettified. Avoid using for production code.</param>
            [AliasAs("pretty")] bool? pretty = null
        );

        [Get("/cards/{id}")]
        Task<CoreCard?> GetCardByIdAsync(Guid id);

        [Get("/cards/autocomplete")]
        Task<ScryfallAutocompleteResponse?> GetAutocompleteAsync(
            ///<param name="q">The beginning of a card name to autocomplete.</param>
            [AliasAs("q")] string q,
            ///<param name="format">The data format to return. This method only supports json.</param>
            [AliasAs("format")] string? format = null,
            ///<param name="pretty">If true, the returned JSON will be prettified. Avoid using for production code.</param>
            [AliasAs("pretty")] bool? pretty = null,
            ///<param name="include_extras">If true, extra cards(tokens, planes, vanguards, etc) will be included.Defaults to false.</param>
            [AliasAs("include_extras")] bool? includeExtras = null
        );
    }
}
