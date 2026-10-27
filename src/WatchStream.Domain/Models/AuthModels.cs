namespace WatchStream.Domain.Models;

public record DeviceAuthRequest(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresIn
);

public record DeviceAuthResponse(
    bool Success,
    string? Token,
    string? Error
);
