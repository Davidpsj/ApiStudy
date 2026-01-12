namespace ApiStudy.Models.Scryfall.Card
{
    public class RelatedCard
    {
        /// <summary>
        /// An unique ID for this card in Scryfall’s database.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();
        /// <summary>
        /// A content type for this object, always related_card.
        /// </summary>
        public string Object { get; set; } = "related_card";
        /// <summary>
        /// A field explaining what role this card plays in this relationship, one of token, meld_part, meld_result, or combo_piece.
        /// </summary>
        public string Component { get; set; } = "token"; // e.g., token, meld_part, meld_result or combo_piece
        /// <summary>
        /// The name of this particular related card.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// The type line of this card.
        /// </summary>
        public string TypeLine { get; set; } = string.Empty;
        /// <summary>
        /// 	A URI where you can retrieve a full object describing this card on Scryfall’s API.
        /// </summary>
        public Uri Uri { get; set; } = new("");
    }
}
