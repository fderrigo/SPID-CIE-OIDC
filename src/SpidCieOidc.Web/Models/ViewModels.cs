using SpidCieOidc.Web.Services;

namespace SpidCieOidc.Web.Models;

public sealed record LandingViewModel(string ClientId, string SpidProvider, string CieProvider);

public sealed record ProfileViewModel(
    UserProfile Profile,
    string? AccessToken,
    long AccessTokenExp,
    string? RefreshToken,
    long RefreshTokenExp,
    bool Refreshed)
{
    public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);

    public static string Short(string? jwt) =>
        string.IsNullOrEmpty(jwt) ? "—" : jwt.Length <= 24 ? jwt : jwt[..12] + "…" + jwt[^8..];

    public static string Ttl(long exp)
    {
        if (exp <= 0) return "n/d";
        var s = exp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return s <= 0 ? "scaduto" : $"{s}s";
    }
}

public sealed record ErrorViewModel(string Title, string Detail);
