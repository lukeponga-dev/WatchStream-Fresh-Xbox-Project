# Delivery roadmap

## Phase 0 — verify before committing architecture

- [ ] Review current Xbox app model and SDK support.
- [ ] Build a clean, disposable native app shell.
- [ ] Deploy to Xbox One and Xbox Series X|S.
- [ ] Verify controller focus/navigation, lifecycle, packaging, and one H.264/HLS playback test.
- [ ] Inspect Stremio upstream client implementation and tests to freeze API contracts.
- [ ] Record results and decide app model based on evidence.

**Exit:** architecture decision record approved; unsupported assumptions documented.

## Phase 1 — complete application foundation

- [ ] Navigation and controller focus model.
- [ ] App lifecycle and settings.
- [ ] Logging and redaction.
- [ ] Error/loading/empty/offline states.
- [ ] Dependency injection and service boundaries.
- [ ] Unit/contract test harness and CI build.

## Phase 2 — Stremio account

- [ ] Supported sign-in flow.
- [ ] Secure session persistence.
- [ ] User sync, add-on collection sync, logout.
- [ ] Authentication errors and re-auth handling.

## Phase 3 — catalog and add-ons

- [ ] Add-on manifest registry.
- [ ] Catalog and metadata aggregation.
- [ ] Search and detail pages.
- [ ] Stream discovery and resolution.
- [ ] Timeouts and per-add-on failure isolation.

## Phase 4 — playback and resilience

- [ ] Playback capability policy for Xbox One and Series X|S.
- [ ] HLS/DASH and codec validation.
- [ ] Subtitles/audio tracks/seeking where supported.
- [ ] Playback errors, retry, resume, and history.
- [ ] Memory and suspend/resume tests.

## Phase 5 — release readiness

- [ ] Test both console generations on hardware.
- [ ] Accessibility and controller-only audit.
- [ ] Security and privacy review.
- [ ] Current packaging/certification requirements.
- [ ] Release notes and known-compatibility matrix.
