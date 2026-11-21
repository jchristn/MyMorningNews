namespace MyMorningNews.Core.Helpers
{
    using System;
    using System.Text;

    /// <summary>
    /// Keeps internal names out of messages shown to people: the "(Parameter 'x')" suffix .NET adds to argument
    /// exceptions, and C# type names such as DimensionProposalResult.
    /// Thread safety: stateless; safe for concurrent use.
    /// </summary>
    public static class ErrorText
    {
        /// <summary>
        /// The message of an exception without the parameter-name suffix of an <see cref="ArgumentException"/>, and with
        /// the missing value named for an <see cref="ArgumentNullException"/> that only says "Value cannot be null."
        /// </summary>
        /// <param name="ex">Exception.</param>
        /// <returns>Message.</returns>
        /// <exception cref="ArgumentNullException">ex is null.</exception>
        public static string Plain(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            string message = ex.Message;
            // .NET's own "Value cannot be null." says nothing about which value; name it instead.
            if (ex is ArgumentNullException ane && !String.IsNullOrEmpty(ane.ParamName) && message.StartsWith("Value cannot be null.", StringComparison.Ordinal))
                return "A required value is missing: " + Char.ToLowerInvariant(ane.ParamName[0]) + ane.ParamName.Substring(1) + ".";
            if (ex is ArgumentException ae && !String.IsNullOrEmpty(ae.ParamName))
            {
                string suffix = " (Parameter '" + ae.ParamName + "')";
                if (message.EndsWith(suffix, StringComparison.Ordinal)) message = message.Substring(0, message.Length - suffix.Length);
            }

            return message;
        }

        /// <summary>
        /// Describe a structured output type in words: "DimensionProposalResult" becomes "dimension proposal".
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Lowercase words.</returns>
        /// <exception cref="ArgumentNullException">type is null.</exception>
        public static string Describe(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            string name = type.Name;
            int tick = name.IndexOf('`', StringComparison.Ordinal);
            if (tick > 0) name = name.Substring(0, tick);
            foreach (string suffix in new[] { "Result", "Response", "Output" })
            {
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            return Words(name);
        }

        /// <summary>
        /// Split a PascalCase or camelCase name into lowercase words: "BlockedDomain" becomes "blocked domain".
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Lowercase words.</returns>
        /// <exception cref="ArgumentNullException">name is null.</exception>
        public static string Words(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && Char.IsUpper(c) && !Char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(Char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }
    }
}
