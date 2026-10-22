# Stremio integration contract checklist

## Upstream references

- Official API client: https://github.com/Stremio/stremio-api-client
- Official add-on client: https://github.com/Stremio/stremio-addon-client
- Add-on protocol documentation linked from the add-on client repository.

## Account API

The public `stremio-api-client` README documents:

- Default endpoint: `https://api.strem.io`
- A stateless API client with an `authKey` option and `request(method, params)`
- Store operations including `login({ email, password, fbLoginToken? })`, `logout()`, `pushUser()`, `pullUser()`, `pushAddonCollection()`, and `pullAddonCollection()`
- An Apple authentication method is documented by the client README.

This documentation does **not** establish a QR/device-code sign-in flow. Do not design one into the UI until a supported, current protocol is verified.

## Contract tests required before implementation

- [ ] Inspect upstream implementation/tests for current method names and payload schemas.
- [ ] Confirm login response, auth key/session semantics, expiry/re-auth behavior and logout behavior.
- [ ] Confirm user sync and add-on collection sync payloads and conflict behavior.
- [ ] Confirm error envelopes and rate-limit/network failure behavior.
- [ ] Confirm whether direct HTTP implementation is appropriate or whether a compatible supported client/runtime is required.
- [ ] Never include real account credentials or auth keys in tests, fixtures, logs, crash reports, or screenshots.

## Add-on protocol

The public `stremio-addon-client` README describes the v3 protocol, add-on manifests, `isSupported(resource, type, id)`, and `get(resource, type, id, extra)` patterns.

Required tests:
- [ ] Manifest parsing and validation.
- [ ] Supported resource/type detection.
- [ ] Catalog and metadata requests.
- [ ] Stream request and response parsing.
- [ ] URL normalization without silently changing signed URLs.
- [ ] Timeout, TLS, redirect, invalid JSON, and unavailable add-on handling.
- [ ] Add-on response content must be treated as untrusted data.
- [ ] Add-on stream result must pass the playback capability check.

## Scope guard

Syncing a configured add-on collection means WatchStream knows which add-ons the account configured. It does not guarantee those add-ons are online, supported by the console, or authorized to provide a particular stream.
