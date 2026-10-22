# Stremio account integration notes

## Verified upstream behavior

The public [Stremio API client](https://github.com/Stremio/stremio-api-client) documents `https://api.strem.io` as its default endpoint. Its client sends JSON POST requests to `/api/{method}`, includes `authKey` in the request body, and expects a JSON `result` on success. Its store uses `login` with email/password and receives `result.authKey` and `result.user`; logout calls the `logout` method. See the upstream [API client implementation](https://github.com/Stremio/stremio-api-client/blob/master/apiClient.js) and [store implementation](https://github.com/Stremio/stremio-api-client/blob/master/apiStore.js).

This verifies the basic email/password API flow used by this prototype, but does not guarantee the upstream service contract will remain stable. QR/device-code login is not implemented or claimed to be supported.

## Current WatchStream implementation

- `StremioAccountService` posts email/password to `https://api.strem.io/api/login` over HTTPS.
- The password is held transiently in the account page and sign-in method; it is not written to persistent storage.
- Only the returned `authKey` is persisted, behind `ISessionStore`, using the Windows `PasswordVault` adapter.
- Sign-out attempts the API logout and clears local session material even if the remote call fails.
- User-facing errors avoid exposing response bodies, credentials, auth keys, or request payloads.
- The account page supports email/password sign-in only; no QR/device-code flow is assumed.

## Security and verification still required

- Build and run on the target Xbox One to verify `PasswordVault` availability and behavior in the selected modern .NET/UWP project model.
- Verify sign-in success, invalid credentials, offline/timeout, app restart session restore, and sign-out.
- Review session expiry and reauthentication behavior before shipping.
- Confirm current Stremio API terms and supported authentication flow before public distribution.
- Never include real credentials or auth keys in source control, logs, test fixtures, crash reports, or screenshots.
