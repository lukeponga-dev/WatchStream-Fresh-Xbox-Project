using System;

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
        public bool Watched { get; }
        public long TimeOffset { get; }
        public long Duration { get; }
        public bool Removed { get; }
        public bool Temporary { get; }

        public MediaItem(string id, string type, string name, string poster = "", string year = "", string description = "", bool watched = false, long timeOffset = 0, long duration = 0, bool removed = false, bool temporary = false)
        {
            Id = Require(id, nameof(id));
            Type = Require(type, nameof(type));
            Name = Require(name, nameof(name));
            Poster = poster ?? string.Empty;
            Year = year ?? string.Empty;
            Description = description ?? string.Empty;
            Watched = watched;
            TimeOffset = Math.Max(0, timeOffset);
            Duration = Math.Max(0, duration);
            Removed = removed;
            Temporary = temporary;
        }

        private static string Require(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new System.ArgumentException("Value must not be empty.", parameterName);
            return value;
        }
    }
}