namespace WatchStream.Core.Models
{
    /// <summary>Framework-neutral catalog item. Add fields only when grounded in upstream schemas.</summary>
    public sealed class MediaItem
    {
        public string Id { get; }
        public string Type { get; }
        public string Name { get; }

        public MediaItem(string id, string type, string name)
        {
            Id = Require(id, nameof(id));
            Type = Require(type, nameof(type));
            Name = Require(name, nameof(name));
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new System.ArgumentException("Value must not be empty.", parameterName);

            return value;
        }
    }
}
