namespace ApiStudy.Models.Scryfall.Card
{
    /// <summary>
    /// Represents the different image URLs available for a card or card face.
    /// </summary>
    public class CardImageUris
    {
        /// <summary>
        /// Gets or sets a small, cropped, JPEG version of the image.
        /// </summary>
        public Uri? Small { get; set; }

        /// <summary>
        /// Gets or sets a medium-sized, cropped, JPEG version of the image.
        /// </summary>
        public Uri? Normal { get; set; }

        /// <summary>
        /// Gets or sets a large, uncropped, JPEG version of the image.
        /// </summary>
        public Uri? Large { get; set; }

        /// <summary>
        /// Gets or sets a PNG version of the image.
        /// </summary>
        public Uri? Png { get; set; }

        /// <summary>
        /// Gets or sets a custom-sized crop of the image, usually showing just the artwork.
        /// </summary>
        public Uri? ArtCrop { get; set; }

        /// <summary>
        /// Gets or sets a full-card-sized image, designed for viewing at high resolutions.
        /// </summary>
        public Uri? BorderCrop { get; set; }
    }
}