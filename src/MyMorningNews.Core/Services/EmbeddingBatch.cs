namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Vectors returned by one embedding call, with the model and dimensionality used.
    /// </summary>
    public class EmbeddingBatch
    {
        /// <summary>
        /// Endpoint used.
        /// </summary>
        public string ModelEndpointId { get; set; } = String.Empty;

        /// <summary>
        /// Model used.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Dimensions of each vector.
        /// </summary>
        public int Dimensions { get; set; } = 0;

        /// <summary>
        /// Vectors in input order.
        /// </summary>
        public List<float[]> Vectors { get; set; } = new List<float[]>();
    }
}
