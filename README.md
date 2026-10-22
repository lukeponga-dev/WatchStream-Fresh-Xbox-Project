# WatchStream — fresh foundation

A clean-room project foundation for a controller-first Stremio client targeting Xbox One and Xbox Series X|S.

> **Status: architecture verification in progress.** This package intentionally does not copy implementation code from older WatchStream prototypes. Platform-specific decisions remain provisional until the compatibility gates in `docs/compatibility-plan.md` pass.

## Goals

- Controller-first media browsing and playback.
- Sign in to an existing Stremio account using only a verified supported flow.
- Synchronize the account and configured add-on collection.
- Discover catalogs, metadata, and streams through documented add-on interfaces.
- Handle playback capabilities per console generation.
- Keep authentication, Stremio API access, add-on protocol, UI, and playback behind separate boundaries.

## Project layout

- `docs/compatibility-plan.md` — evidence-based platform and API gates.
- `docs/architecture-options.md` — candidate options and decision criteria.
- `docs/stremio-contracts.md` — upstream contracts to verify before implementation.
- `docs/security.md` — credential and session requirements.
- `docs/roadmap.md` — phased delivery and acceptance criteria.
- `src/WatchStream.Core/` — platform-neutral domain/service boundaries, with no UI framework dependency.
- `tests/WatchStream.Core.Tests/` — initial contract-boundary tests.

## Current candidate direction

Microsoft's Xbox media-app guidance documents native UWP/XAML/C# media apps and notes that WebView2 is not supported on Xbox. That makes **native UWP a candidate to validate**, not an unconditional architecture commitment. Do not substitute WinUI 3, desktop MSIX, or an assumption of cross-generation compatibility without testing the actual Xbox target.

Stremio's public JavaScript client documents account login/logout, user synchronization, and add-on collection synchronization against `https://api.strem.io`. This does not, by itself, prove a QR/device-code flow or the availability of every stream on Xbox. Those remain explicit verification gates.

## Build status

This is a source scaffold, not a certified or hardware-tested Xbox application. It has not been built with the Xbox SDK, deployed to a console, or tested against a live Stremio account.
