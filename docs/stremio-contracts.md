# Stremio account integration notes

## Verified upstream behavior

The public [Stremio Core source](https://github.com/Stremio/stremio-core) defines the TV account-link flow in `src/models/link.rs`, `src/types/api/request.rs`, and `src/types/api/response.rs`.

- Link API base: `https://link.stremio.com`.
- Create link: `GET https://link.stremio.com/api/v2/create?type=Create`.
- The create response has a `result` object with `code`, `link`, and `qrcode` string fields.
- Read link state: `GET https://link.stremio.com/api/v2/read?type=Read&code={code}`.
- When the link is approved, the read response contains `result.authKey`. The app must persist that key as the account session; it must never ask the phone user to send a password to WatchStream.
- The official web page at [link.stremio.com](https://link.stremio.com/) explicitly asks users to enter the four-character code displayed by the app.
- The account API remains `https://api.strem.io/api/{method}`; the existing email/password fallback uses its documented `login` and `logout` methods.

These details are grounded in the public Stremio Core implementation, not inferred endpoint names. They are still upstream implementation contracts rather than a promise of permanent third-party API stability.

## Current WatchStream implementation

- `StremioLinkService` calls the verified v2 create/read endpoints and reads the returned QR payload, code, and link.
- `AccountPage` displays the QR payload, the four-character code, and the official link page as a manual fallback.
- WatchStream polls the read endpoint every three seconds while the account-link screen is active. It stores the returned `authKey` only when the response includes one, then stops polling.
- A new code can be requested from the screen. The app does not assume or display an expiry duration because the upstream response contract inspected here does not specify one.
- Email/password sign-in remains available as a fallback. The password is not persisted.
- The session key is persisted behind `ISessionStore`, using the Windows `PasswordVault` adapter.
- Sign-out attempts API logout and clears local session material even if the remote call fails.

## Security and verification still required

- Build and run on the target Xbox One to verify `PasswordVault`, `WebView.NavigateToString`, and the account-link screen in the selected modern .NET/UWP project model.
- Verify that the returned `qrcode` value is renderable by the Xbox WebView; if the upstream representation changes, the visible code and official link remain the fallback.
- Test create-link errors, pending reads, approved link, refresh/new code, app navigation cancellation, invalid email/password, offline/timeout, app restart session restore, and sign-out.
- Review session expiry and reauthentication behavior before shipping.
- Confirm current Stremio API terms and supported authentication flow before public distribution.
- Never include real credentials or auth keys in source control, logs, test fixtures, crash reports, or screenshots.
