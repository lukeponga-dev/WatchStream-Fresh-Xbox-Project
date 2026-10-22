namespace WatchStream.Core.Contracts
{
    public interface IPlaybackCapabilityPolicy
    {
        PlaybackAssessment Assess(PlaybackCandidate candidate);
    }

    public sealed class PlaybackCandidate
    {
        public string Uri { get; }
        public string? MimeType { get; }
        public string? ManifestType { get; }

        public PlaybackCandidate(string uri, string? mimeType = null, string? manifestType = null)
        {
            Uri = string.IsNullOrWhiteSpace(uri)
                ? throw new System.ArgumentException("URI is required.", nameof(uri))
                : uri;
            MimeType = mimeType;
            ManifestType = manifestType;
        }
    }

    public sealed class PlaybackAssessment
    {
        public bool IsSupported { get; }
        public string Reason { get; }

        public PlaybackAssessment(bool isSupported, string reason)
        {
            IsSupported = isSupported;
            Reason = reason ?? string.Empty;
        }
    }
}
