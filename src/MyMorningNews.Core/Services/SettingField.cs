namespace MyMorningNews.Core.Services
{
    using System;
    using System.Reflection;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// One annotated setting discovered by reflection.
    /// </summary>
    public class SettingField
    {
        /// <summary>
        /// Group property on the root settings object.
        /// </summary>
        public PropertyInfo Group { get; }

        /// <summary>
        /// Field property on the group.
        /// </summary>
        public PropertyInfo Property { get; }

        /// <summary>
        /// Metadata.
        /// </summary>
        public SettingAttribute Attribute { get; }

        /// <summary>
        /// camelCase group name.
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// camelCase field name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Dotted path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="group">Group property.</param>
        /// <param name="property">Field property.</param>
        /// <param name="attribute">Metadata.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SettingField(PropertyInfo group, PropertyInfo property, SettingAttribute attribute)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(attribute);
            Group = group;
            Property = property;
            Attribute = attribute;
            GroupName = Camel(group.Name);
            Name = Camel(property.Name);
            Path = GroupName + "." + Name;
        }

        private static string Camel(string name)
        {
            return String.IsNullOrEmpty(name) ? name : Char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
