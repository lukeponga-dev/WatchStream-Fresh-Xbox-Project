# Stremio API integration notes

## Verified upstream behavior

The public [Stremio Core source](https://github.com/Stremio/stremio-core) defines the TV account-link flow in `src/models/link.rs`, `src/types/api/request.rs`, and `src/types/api/response.rs`.

- Link API base: `https://link.stremio.com`.
- Create link: `GET https://link.stremio.com/api/v2/create?type=Create`.
- The create response has a `result` object with `code`, `link`, and `qrcode` string fields.
- Read link state: `GET https://link.stremio.com/api/v2/read?type=Read&code={code}`.
- The approved link response supplies a temporary `result.authKey` token. Exchange it with `POST https://api.strem.io/api/loginWithToken` using JSON `type: "LoginWithToken"` and `token: <temporary-token>`. Persist only the resulting account response's `result.authKey`; do not persist the temporary link token.
- The official `stremio-api-client` sends API methods as POST JSON to `https://api.strem.io/api/{method}`, merging the session `authKey` into method parameters.
- The official add-on collection client uses `addonCollectionGet` with `update: true` and `addFromURL: []` to pull the signed-in user's add-on descriptors.

These are upstream implementation contracts, not a guarantee that undocumented APIs will remain stable.

## Current WatchStream implementation

- `AccountPage` creates a pairing code, shows the code and official link, displays QR data when the returned payload is a supported image data URI or HTTPS image URI, and polls every three seconds while active.
- `StremioLinkService` exchanges the temporary link token with `loginWithToken` and stores only the account session key.
- `SecureSessionStore` persists the session key using Windows `PasswordVault`.
- Discover/catalog search reads manifest-defined catalog resources from installed modern add-ons plus Cinemeta. Library reads the signed-in account's `libraryItem` datastore collection.
- The Add-ons page can install HTTPS `/manifest.json` add-ons and pull compatible descriptors from the user's Stremio add-on collection. This version imports only modern manifest URLs; legacy `/stremio/v1` descriptors are deliberately skipped rather than guessed.
- Details resolve stream resources from locally installed add-ons. The add-on protocol defines `/catalog/{type}/{id}.json`, `/meta/{type}/{id}.json`, and `/stream/{type}/{videoID}.json` resources.

## Remaining verification and limitations

- Local add-on installation is stored per device. This version pulls supported add-on descriptors from the Stremio account but does not yet push locally installed add-ons back to the account.
- Verify the QR payload representation and the pairing UI on Xbox hardware; if the payload is not a supported image URI, the visible code and official link remain the fallback.
- Exercise successful and pending pairing, token exchange failure, network timeouts, session restoration, library parsing, and add-on sync with a real test account.
- Review session expiry and reauthentication behavior before shipping.
- Never include real credentials or auth keys in source control, logs, test fixtures, crash reports, or screenshots.
