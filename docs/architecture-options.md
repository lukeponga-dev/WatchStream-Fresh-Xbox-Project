# Architecture options — provisional

## Candidate A: native UWP shell + platform-neutral core

**Leading candidate, not yet approved.**

- Native controller-first XAML/C# UI.
- Core domain models and service interfaces isolated from Xbox APIs.
- HTTP transport adapter for the Stremio account API, implemented only after request/response contracts are confirmed.
- Add-on v3 client boundary for manifest/catalog/meta/stream requests.
- Native media playback adapter using the platform-supported playback APIs.
- Secure session storage behind an interface, with platform capability validated before selecting a concrete store.

**Why investigate first:** Microsoft documents native UWP/XAML/C# media apps for Xbox and the Xbox MediaPlayer APIs.

**Risks:** exact current SDK/tooling compatibility, package/architecture settings, APIs unavailable on console, media codec variation, and console deployment/certification constraints.

## Candidate B: hosted web UI inside the Xbox WebView

Microsoft's Xbox media-app documentation notes WebView2 is not supported on Xbox. An older/original WebView approach exists in the documentation, but it would need a separate proof-of-compatibility and memory-lifecycle evaluation.

**Status:** do not select without a working console spike.

## Candidate C: WinUI 3 desktop application

**Not selected as the Xbox target by default.** A Windows desktop WinUI 3 app is not evidence of Xbox console compatibility. The existing prototype's framework must not be carried forward without proof.

## Boundary rules

1. UI must not call the Stremio API directly.
2. Account API, add-on protocol, catalog aggregation, playback capability selection, and persistence are separate interfaces.
3. The UI must represent loading, empty, unavailable, unauthorized, unsupported-media, and retry states.
4. A stream is “playable” only after resolution and platform compatibility checks, not merely because it has a URL.
5. No account password is persisted.
6. Do not bundle third-party add-on code or assume every add-on is safe, reliable, or compatible.

## Decision record

Architecture decision: **Pending console spike and API contract verification.**

Proposed first experiment: create the smallest clean native app shell, build/package it with current supported Xbox tooling, deploy to Xbox One and Series X|S, and test controller navigation plus one known H.264/HLS sample. Keep this spike disposable until it passes.
