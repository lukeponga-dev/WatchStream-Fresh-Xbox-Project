# Compatibility plan and evidence register

Last reviewed: 2026-10-22

## Current evidence

### Xbox app model

Microsoft's *Xbox Media Application Architecture* describes two patterns for media apps: a website in a WebView, or a native UWP application using XAML/C# (or C++ variants) and MediaPlayer APIs. The page explicitly says WebView2 is not supported on Xbox.

Source: https://learn.microsoft.com/en-us/windows/uwp/apps-for-xbox/application-architecture

**Implication:** native UWP is the leading candidate for a new Xbox-first media app. This is not proof that every current Windows UI framework or every NuGet package is supported on both consoles. WinUI 3 must not be assumed compatible.

### Media formats and codec differences

Microsoft lists MPEG-DASH and HLS as supported Xbox streaming formats. Its codec table lists H.264 up to 1080p60 for all consoles, HEVC up to 4K60 except the original Xbox One, and VP9 support for Xbox One X and Series X|S.

Source: https://learn.microsoft.com/en-us/windows/uwp/apps-for-xbox/supported-technologies

**Implication:** playback must be capability-aware. Original Xbox One is the baseline constraint; a stream URL being HTTP(S) is not enough to establish playable media. Containers, codecs, DRM, manifests, redirects, subtitles, and add-on-specific requirements need testing.

### Package and CPU architecture

Microsoft's Xbox FAQ says Xbox no longer supports x86 app development/submissions. Verify the current packaging and target architecture against the current Xbox SDK/Dev Mode tooling before choosing build settings.

Source: https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/frequently-asked-questions

## Required gates before architecture is locked

| Gate | Evidence required | Status |
|---|---|---|
| G1 — App model | Current Microsoft documentation and an empty native shell deployed to Xbox One and Series X|S | Open |
| G2 — Build/package | Successful clean build and package using supported SDK/tooling; verify x64 and app manifest requirements | Open |
| G3 — Controller | Focus, navigation, text entry, back, suspend/resume tested with controller on both consoles | Open |
| G4 — Playback | Test HLS, DASH, direct MP4 where supported, subtitles, seek, audio tracks, resume and failure recovery | Open |
| G5 — Codec baseline | At minimum, H.264 stream test on both generations; document unsupported HEVC/VP9 cases | Open |
| G6 — Stremio auth | Verify current login method, session/auth-key lifecycle, errors, logout and storage requirements | Open |
| G7 — Account/add-on sync | Verify request schemas, timestamps/conflict behavior, and add-on collection get/set behavior | Open |
| G8 — Add-on v3 | Verify manifest and catalog/meta/stream resource calls, CORS/network behavior, redirects, timeouts and malformed responses | Open |
| G9 — Store/distribution | Verify current Partner Center/console certification requirements separately from local Dev Mode deployment | Open |

## Exit criteria

Do not mark architecture “verified” until G1–G4 and G6–G8 have reproducible evidence. Store certification is a later gate and must not be implied by a successful local deployment.
