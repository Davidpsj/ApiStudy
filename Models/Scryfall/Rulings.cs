namespace ApiStudy.Models.Scryfall
{
    /// <summary>
    /// Rulings represent Oracle rulings, Wizards of the Coast set release notes, or Scryfall notes for a particular card.
    /// If two cards have the same name, they will have the same set of rulings objects. If a card has rulings, it usually has more than one.
    /// Rulings with a scryfall source have been added by the Scryfall team, either to provide additional context for the card, or explain how the card works in an unofficial format (such as Duel Commander).
    /// </summary>
    public class Rulings
    {
        /// <summary>
        /// A content type for this object, always ruling.
        /// </summary>
        public string Object { get; set; } = "ruling";

        /// <summary>
        /// The Oracle ID of the card this ruling is associated with.
        /// </summary>
        public Guid OracleId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// A computer-readable string indicating which company produced this ruling, either wotc or scryfall.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// The date when the ruling or note was published.
        /// </summary>
        public DateTime PublishedAt { get; set; } = DateTime.MinValue;

        /// <summary>
        /// The text of the ruling.
        /// </summary>
        public string Comment { get; set; } = string.Empty;
    }
}
