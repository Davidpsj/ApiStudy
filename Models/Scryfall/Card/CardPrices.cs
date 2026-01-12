namespace ApiStudy.Models.Scryfall.Card
{
    /// <summary>
    /// Represents the daily market prices of a card in different currencies and finishes.
    /// Prices are stored as strings to handle potential null values and exact decimal representation.
    /// </summary>
    public class CardPrices
    {
        /// <summary>
        /// Gets or sets the price in US Dollars for the nonfoil version.
        /// </summary>
        public string? Usd { get; set; }

        /// <summary>
        /// Gets or sets the price in US Dollars for the foil version.
        /// </summary>
        public string? UsdFoil { get; set; }

        /// <summary>
        /// Gets or sets the price in US Dollars for the etched foil version.
        /// </summary>
        public string? UsdEtched { get; set; }

        /// <summary>
        /// Gets or sets the price in Euros for the nonfoil version.
        /// </summary>
        public string? Eur { get; set; }

        /// <summary>
        /// Gets or sets the price in Euros for the foil version.
        /// </summary>
        public string? EurFoil { get; set; }

        /// <summary>
        /// Gets or sets the price in Euros for the etched foil version.
        /// </summary>
        public string? EurEtched { get; set; }

        /// <summary>
        /// Gets or sets the price in Magic Online Tickets (TIX).
        /// </summary>
        public string? Tix { get; set; }
    }
}