namespace MyMorningNews.Core.Helpers
{
    using System;

    /// <summary>
    /// Description of a structured-result property, included in the JSON Schema sent to models.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class DocumentationAttribute : Attribute
    {
        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="description">Description.</param>
        public DocumentationAttribute(string description)
        {
            Description = description ?? String.Empty;
        }
    }
}
