namespace ApiStudy.Models.Scryfall
{
    public static class SetTypeValues
    {
        /// <summary>
        /// A yearly Magic core set (Tenth Edition, etc)
        /// </summary>
        public const string Core = "core";
        /// <summary>
        /// A rotational expansion set in a block (Zendikar, etc)
        /// </summary>
        public const string Expansion = "expansion";
        /// <summary>
        /// A reprint set that contains no new cards (Modern Masters, etc)
        /// </summary>
        public const string Masters = "masters";
        /// <summary>
        /// A set of new cards that only get added to high-power formats
        /// </summary>
        public const string Eternal = "eternal";
        /// <summary>
        /// An Arena set designed for Alchemy
        /// </summary>
        public const string Alchemy = "alchemy";
        /// <summary>
        /// Masterpiece Series premium foil cards
        /// </summary>
        public const string Masterpiece = "masterpiece";
        /// <summary>
        /// A Commander-oriented gift set
        /// </summary>
        public const string Arsenal = "arsenal";
        /// <summary>
        /// From the Vault gift sets
        /// </summary>
        public const string FromTheVault = "from_the_vault";
        /// <summary>
        /// Spellbook series gift sets
        /// </summary>
        public const string Spellbook = "spellbook";
        /// <summary>
        /// Premium Deck Series decks
        /// </summary>
        public const string PremiumDeck = "premium_deck";
        /// <summary>
        /// Duel Decks
        /// </summary>
        public const string DuelDeck = "duel_deck";
        /// <summary>
        /// Special draft sets, like Conspiracy and Battlebond
        /// </summary>
        public const string DraftInnovation = "draft_innovation";
        /// <summary>
        /// Magic Online treasure chest prize sets
        /// </summary>
        public const string TreasureChest = "treasure_chest";
        /// <summary>
        /// Commander preconstructed decks
        /// </summary>
        public const string Commander = "commander";
        /// <summary>
        /// Planechase sets
        /// </summary>
        public const string Planechase = "planechase";
        /// <summary>
        /// Archenemy sets
        /// </summary>
        public const string Archenemy = "archenemy";
        /// <summary>
        /// Vanguard card sets
        /// </summary>
        public const string Vanguard = "vanguard";
        /// <summary>
        /// A funny un-set or set with funny promos (Unglued, Happy Holidays, etc)
        /// </summary>
        public const string Funny = "funny";
        /// <summary>
        /// A starter/introductory set (Portal, etc)
        /// </summary>
        public const string Starter = "starter";
        /// <summary>
        /// A gift box set
        /// </summary>
        public const string Box = "box";
        /// <summary>
        /// A set that contains purely promotional cards
        /// </summary>
        public const string Promo = "promo";
        /// <summary>
        /// A set made up of tokens and emblems.
        /// </summary>
        public const string Token = "token";
        /// <summary>
        /// A set made up of gold-bordered, oversize, or trophy cards that are not legal
        /// </summary>
        public const string Memorabilia = "memorabilia";
        /// <summary>
        /// A set that contains minigame card inserts from booster packs
        /// </summary>
        public const string Minigame = "minigame";
    }

    public class SetTypes
    {
        /// <summary>
        /// A type to set types from scrifall api structure. See SetTypeValues.[props]
        /// </summary>
        public string Type { get; set; } = string.Empty;
        /// <summary>
        /// A description of the set.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
