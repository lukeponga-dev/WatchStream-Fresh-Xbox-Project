# WatchStream release readiness

## Current release decision: NOT APPROVED for public distribution

### 1. Stremio Addons directory permission - external blocker

The directory API documentation describes the public REST API as experimental and read-only. The Terms of Use permit personal/non-commercial use but restrict using directory data to compete with, substitute for, or harm Stremio Addons without prior written permission. WatchStream embeds an add-on directory in a Stremio-compatible client, so do not assume the intended public release is covered.

Before shipping a build that exposes the directory:
1. Contact the Stremio Addons maintainers at hello@stremio-addons.net (listed on https://docs.stremio-addons.net/contact); a draft request is in docs/STREMIO_ADDONS_PERMISSION_REQUEST.md.
2. Describe WatchStream, its target devices, the embedded directory/search/install flow, whether the app is commercial, expected request volume, caching, and attribution.
3. Obtain explicit written permission for this specific use and keep the response in project release records.
4. Confirm the required visible attribution/link, API caching rules, acceptable request rate, and whether a branded directory integration is permitted.
5. If permission is denied or unavailable, keep the directory feature disabled in distributed builds. The code is now disabled by default; only set the MSBuild property WatchStreamAddonDirectoryPermissionGranted=true after written permission is on file. Do not treat a successful API response as permission.

Relevant official sources:
- https://docs.stremio-addons.net/developers/api
- https://docs.stremio-addons.net/legal/terms-of-use
- https://docs.stremio-addons.net/account

The directory is community-maintained; a listed add-on is not necessarily endorsed, safe, or maintained by Stremio.

## 2. Network and add-on security

The service now disables automatic redirects, validates each redirect target, limits redirects, rejects local/private literal IP destinations, and reads response bodies through a byte-limited reader. This is defense-in-depth, not a complete sandbox: DNS can change between validation and connection, and a trusted-looking add-on can still return malicious stream URLs or content.

Before release:
- DNS resolution now rejects hostnames that resolve to private/reserved addresses before each request; test this on the actual UWP/Xbox runtime and assess the remaining DNS-rebinding time-of-check/time-of-use window.
- Basic manifest schema, field-length, and array-count validation is implemented. Continue testing real-world manifests and add any fields required by the full upstream manifest schema.
- Stream results now accept HTTPS URLs only and reject embedded credentials, local hostnames, private/reserved literal IPs, and hostnames that resolve to non-public addresses. Verify compatibility with supported add-ons; HTTP and magnet streams are intentionally filtered.
- Manifest URLs are now stored in Windows PasswordVault; the JSON metadata file keeps only opaque references. Verify PasswordVault behavior and migration from legacy clear-text records on Xbox hardware. Do not include full URLs or query parameters in logs, diagnostics, screenshots, or bug reports.
- Keep the Stremio account key in PasswordVault; never put it in add-on URLs or ordinary JSON settings.
- Add request cancellation/concurrency limits and an explicit timeout for all service operations.
- Keep directory attribution visible and cache results according to the API's current terms. Do not attempt to bypass rate limits or caching.

## 3. Encoding and UI review

Search source and documentation files (excluding bin/obj) for Unicode replacement characters and common mojibake sequences. Replace damaged text with the intended Unicode or simple ASCII; changing file encoding alone will not repair already-corrupted strings. Review the account pairing page, add-ons page, player, catalog/details pages, profile, and settings at 1080p/4K scale.

## 4. Account and add-on sync contract

Use a dedicated Stremio test account only. Verify the official device-link flow, token exchange, PasswordVault session restoration, expired session, sign-out, and account collection response against current upstream source. The docs/stremio-contracts.md file records that the official collection request includes update=true and addFromURL=[]; current request code must be compared with that contract before relying on sync.

Keep these states separate in the UI:
- Directory entry found
- Installed locally on this device
- Present in the Stremio account
- Imported but unsupported (legacy transport)
- Unavailable (manifest could not be loaded)

Local install must not be described as account sync unless a verified write-to-account operation actually succeeds. Confirm how removing an account-managed add-on behaves before modifying the account collection.

## 5. Automated tests and CI

CI should run the Xbox x64 build, unit tests, and public API contract checks on every push and pull request. Keep live external API checks distinct from deterministic unit tests so transient upstream outages are clearly reported. Test unsafe URLs, redirects, oversized responses, malformed manifests, cancellation, deduplication, and sync reconciliation.

## 6-7. Xbox hardware acceptance

Run the complete checklist separately on Xbox One and Xbox Series X|S; a Windows desktop build is not a substitute.

- [ ] Fresh install, launch, suspend/resume, and relaunch
- [ ] D-pad and left-stick navigation; no lost focus; A selects; B returns correctly
- [ ] Pair using QR/link; approve on phone/computer; pending and failure states; session survives restart
- [ ] Search directory, empty results, offline/error state, attribution, install, remove, duplicate install, and configure action
- [ ] Sync account add-ons; test legacy and unavailable entries; confirm local-only installs remain local
- [ ] Browse catalog/details; resolve streams; play, pause, seek, stop, back, and recover from a broken stream
- [ ] Verify 1080p/4K layout, text legibility, and focus indicators at couch distance
- [ ] Confirm no desktop-only dependency is required on console
- [ ] Repeat with network disconnect/reconnect and after the console resumes from sleep

Record console model, OS version, app package version, test date, and results without including account tokens or personal data.

## 8. Package and release preparation

- Package identity is currently pongadev.WatchStream, publisher is CN=lukeg, and manifest version is 1.0.2.0. Confirm the intended publisher identity and version policy before publishing; changing identity after distribution can break upgrades.
- Never ship the temporary development certificate as a release signing identity. Configure the real publisher-matched certificate securely in the release pipeline; do not commit the certificate or password.
- Replace the old private-LAN app-installer URI with a stable HTTPS release host only when hosting and TLS are ready. Keep app-installer generation off by default for local/CI builds.
- Define versioning, release notes, rollback, update cadence, privacy/data handling, support contact, third-party notices, and an add-on safety disclaimer.
- Verify PFX, CER, MSIX, APPX, app-installer files, tokens, and generated packages are not committed unless a specific artifact is intentionally published.
- Test clean install and upgrade on both Xbox targets using the actual release signing identity and hosting URL.

## Release gate

Do not label WatchStream production-ready until the directory permission decision is documented, the security tests pass, the real account contract has been verified, CI is green, and both console test runs are recorded. Signing/hosting and physical-console checks require release-owner action and cannot be certified by a desktop build alone.
