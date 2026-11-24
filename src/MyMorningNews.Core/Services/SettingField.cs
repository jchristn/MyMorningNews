namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using MyMorningNews.Core.Settings;

    /// <summary>
    /// One annotated setting discovered by reflection.
    /// </summary>
    public class SettingField
    {
        private readonly List<PropertyInfo> _Parents;
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
            : this(new[] { group }, property, attribute)
        {
        }

        /// <summary>Construct metadata for a nested settings leaf. Parent properties start at ServerSettings and cannot be empty.</summary>
        public SettingField(IEnumerable<PropertyInfo> parents, PropertyInfo property, SettingAttribute attribute)
        {
            ArgumentNullException.ThrowIfNull(parents);
            ArgumentNullException.ThrowIfNull(property);
            ArgumentNullException.ThrowIfNull(attribute);
            _Parents = parents.ToList();
            if (_Parents.Count == 0) throw new ArgumentException("A settings group is required.", nameof(parents));
            Group = _Parents[0];
            Property = property;
            Attribute = attribute;
            GroupName = Camel(Group.Name);
            Name = Camel(property.Name);
            Path = String.Join(".", _Parents.Select(p => Camel(p.Name))) + "." + Name;
        }

        /// <summary>Locate this leaf's containing object in a settings tree. Throws ArgumentException for a null group.</summary>
        public object Container(ServerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            object current = settings;
            foreach (PropertyInfo parent in _Parents) current = parent.GetValue(current) ?? throw new ArgumentException("Settings group " + parent.Name + " cannot be null.");
            return current;
        }

        private static string Camel(string name)
        {
            return String.IsNullOrEmpty(name) ? name : Char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
