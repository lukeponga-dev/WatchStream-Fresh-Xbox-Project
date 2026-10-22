# Security baseline

- Never write a Stremio password to disk, settings, telemetry, logs, or test fixtures.
- Keep session/auth-key storage behind `ISessionStore`; select a concrete secure store only after confirming it is supported on the target Xbox app model.
- Redact credentials, authorization headers, signed stream URLs, and user identifiers from logs.
- Use HTTPS by default; reject certificate validation bypasses.
- Treat add-on manifests, metadata, URLs, and stream responses as untrusted input.
- Validate URI schemes and disallow unexpected local/private-network destinations unless there is an explicit reviewed use case.
- Set request timeouts, response-size limits, cancellation, and bounded retries.
- Clear local session state on logout and provide a visible signed-in/account state.
- Keep any optional analytics off by default until privacy requirements are defined.
