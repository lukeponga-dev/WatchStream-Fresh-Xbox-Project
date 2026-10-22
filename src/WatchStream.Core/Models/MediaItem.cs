namespace WatchStream.Core.Models
{
    /// <summary>Framework-neutral catalog item. Optional metadata is supplied by Stremio addon responses.</summary>
    public sealed class MediaItem
    {
        public string Id { get; }
        public string Type { get; }
        public string Name { get; }
        public string Poster { get; }
        public string Year { get; }
        public string Description { get; }

        public MediaItem(string id, string type, string name, string poster = "", string year = "", string description = "")
        {
            Id = Require(id, nameof(id));
            Type = Require(type, nameof(type));
            Name = Require(name, nameof(name));
            Poster = poster ?? string.Empty;
            Year = year ?? string.Empty;
            Description = description ?? string.Empty;
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new System.ArgumentException("Value must not be empty.", parameterName);
            return value;
        }
    }
}