# Core test plan

These tests are intentionally contract-oriented. Add a concrete test project after the target test runner and supported SDK versions are chosen during Phase 0.

Required initial tests:
- Media item rejects empty identifiers/type/name.
- Account state transitions for sign-in, expiry, sign-out, and failed network calls.
- API request/response fixtures use synthetic credentials only.
- Add-on manifest and resource capability checks.
- Playback policy rejects unsupported schemes and identifies unsupported/unknown formats.
- Session store never accepts a password field and clears state on sign-out.
- Controller navigation state is deterministic and has no UI-framework dependency.
