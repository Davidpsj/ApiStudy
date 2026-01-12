using ApiStudy.Models.Cards;
using ApiStudy.Models.Scryfall;
using ApiStudy.Models.Scryfall.Card;
using ApiStudy.Repository;
using System.ComponentModel;

namespace ApiStudy.Services;

public class CardServices
{
    private readonly IRepository<Collection> _repository;
    private readonly IRepository<Card> _cardRepository;
    private readonly IRepository<Deck> _deckRepository;
    private readonly IScryfallApi _scryfallApi;

    public CardServices(
        IRepository<Collection> repository,
        IRepository<Card> cardRepository,
        IRepository<Deck> deckRepository,
        IScryfallApi scryfallApi)
    {
        _repository = repository;
        _cardRepository = cardRepository;
        _deckRepository = deckRepository;
        _scryfallApi = scryfallApi;
    }

    public async Task<Collection> AddNewCollectionAsync(Collection collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection), "Collection cannot be null.");
        }

        var result = await _repository.CreateAsync(collection);

        if (!_repository.UsingTransaction) await _repository.SaveChangesAsync();

        if (result == null)
        {
            throw new NullReferenceException("Failed to create the collection.");
        }

        return result;
    }

    public async Task<IList<CollectionResponse>> GetCollectionsAsync()
    {
        var collections = await _repository.GetAllAsync(c => c.Cards);

        if (collections == null)
        {
            throw new NullReferenceException("Failed to get collections");
        }

        // Inicializa a lista de resposta com a capacidade correta
        List<CollectionResponse> responseCollection = new(collections.Count);

        var listResultComplete = new List<IList<Task<CoreCard?>>>();

        foreach (var collection in collections)
        {
            var listCollection = new List<Task<CoreCard?>>();

            foreach (var card in collection.Cards)
            {
                listCollection.Add(_scryfallApi.GetCardByIdAsync(card.Id));
            }

            listResultComplete.Add(listCollection);
        }

        // Para cada grupo de tasks, aguarda e cria um CollectionResponse correspondente
        for (int i = 0; i < listResultComplete.Count; i++)
        {
            var taskCol = listResultComplete[i];
            var itens = await Task.WhenAll(taskCol);
            var nonNullCards = itens?.Where(x => x is not null).Cast<CoreCard>().ToList() ?? new List<CoreCard>();

            // Associa o UserCollection original (mesma ordem que collections) com as cartas resolvidas
            var response = new CollectionResponse
            {
                Collection = collections[i],
                Cards = nonNullCards
            };

            responseCollection.Add(response);
        }

        return responseCollection;
    }

    public async Task<ICollection<CollectionResponse>> GetCollectionsAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(userId), "UserId cannot be empty.");
        }

        var collectionsResult = await _repository.GetByQuery(u => u.UserId == userId, c => c.Cards) ?? throw new NullReferenceException("Failed to get collections");
        var collections = collectionsResult.ToList();
        
        // Inicializa a lista de resposta com a capacidade correta
        List<CollectionResponse> responseCollection = new(collections.ToList().Count);

        var listResultComplete = new List<IList<Task<CoreCard?>>>();

        foreach (var collection in collections)
        {
            var listCollection = new List<Task<CoreCard?>>();

            foreach (var card in collection.Cards)
            {
                listCollection.Add(_scryfallApi.GetCardByIdAsync(card.Id));
            }

            listResultComplete.Add(listCollection);
        }

        // Para cada grupo de tasks, aguarda e cria um CollectionResponse correspondente
        for (int i = 0; i < listResultComplete.Count; i++)
        {
            var taskCol = listResultComplete[i];
            var itens = await Task.WhenAll(taskCol);
            var nonNullCards = itens?.Where(x => x is not null).Cast<CoreCard>().ToList() ?? [];

            // Associa o UserCollection original (mesma ordem que collections) com as cartas resolvidas
            var response = new CollectionResponse
            {
                Collection = collections[i],
                Cards = nonNullCards
            };

            responseCollection.Add(response);
        }

        return responseCollection;
    }

    public async Task<Collection> AddNewCardToCollection(AddCardToCollectionRequest request)
    {
        if (request is null)
        {
            throw new InvalidEnumArgumentException("Não há dados a serem acrescentados.");
        }

        var collectionId = request.CollectionId;
        var card = request.Card;

        if (collectionId == Guid.Empty || card == Guid.Empty)
        {
            throw new NullReferenceException("Collection or card not defined or is invalid.");
        }

        var collection = await _repository.GetByIdAsync(collectionId);

        if (collection != null)
        {
            var existCard = await _cardRepository.GetByIdAsync(card);

            if (existCard is null)
            {
                var cardData = new Card
                {
                    Id = card
                };
                var result = await _cardRepository.CreateAsync(cardData);
                if (!_cardRepository.UsingTransaction) await _cardRepository.SaveChangesAsync();
                if (result is null)
                {
                    throw new Exception("Failed to create the card before add to collection.");
                }

                collection.Cards.Add(result);

                var updatedCollection = await _repository.UpdateAsync(collectionId, collection);
                if (!_repository.UsingTransaction) await _repository.SaveChangesAsync();

                return collection;
            }
            else
            {
                collection.Cards.Add(existCard);
                var result = await _repository.UpdateAsync(collectionId, collection);
                if (!_repository.UsingTransaction) await _repository.SaveChangesAsync();
                return result 
                    ?? throw new NullReferenceException("Could not persist new Card to this Collection. Null returned by repository.");
            }
        }
        else
        {
            throw new NullReferenceException("Collection not found.");
        }
    }


}
