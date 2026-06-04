using Microsoft.AspNetCore.Mvc;
using SpidCieOidc.Web.Models;
using SpidCieOidc.Web.Services;

namespace SpidCieOidc.Web.Controllers;

/// <summary>RP authorization-code flow endpoints: begin, callback, refresh, logout.</summary>
public sealed class AuthController(
    RpConfig rp, OidcClient client, IConfiguration cfg, IWebHostEnvironment env, ILogger<AuthController> log) : Controller
{
    private const string StateCookie = "rp_state";

    // Fallback when the button did not pass an explicit ?provider=: prefer the local
    // provider in Development, otherwise the first production provider of that profile.
    private string ProviderFor(string profile)
    {
        if (env.IsDevelopment())
        {
            var local = cfg.GetSection("Oidc:LocalProviders").Get<List<ProviderOption>>()
                ?.FirstOrDefault(p => p.Profile == profile);
            if (local is not null) return local.EntityId;
        }
        var prod = cfg.GetSection("Oidc:ProductionProviders").Get<List<ProviderOption>>()
            ?.FirstOrDefault(p => p.Profile == profile);
        return prod?.EntityId
            ?? (profile == "cie" ? "http://cie-provider.org:8002/oidc/op" : "http://trust-anchor.org:8000/oidc/op");
    }

    [HttpGet("/oidc/rp/authorization")]
    public async Task<IActionResult> Authorization(string? provider, string? profile, CancellationToken ct)
    {
        var prof = profile ?? "spid";
        var op = provider ?? ProviderFor(prof);
        try
        {
            var url = await client.BeginAsync(op, prof, rp.AuthorityHints[0], ct);
            return Redirect(url);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Authorization request failed");
            return View("Error", new ErrorViewModel("Richiesta di autorizzazione fallita", ex.Message));
        }
    }

    [HttpGet("/oidc/rp/callback")]
    public async Task<IActionResult> Callback(
        string? code, string? state, string? iss, string? error, string? error_description, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
            return View("Error", new ErrorViewModel(error, error_description ?? ""));
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return View("Error", new ErrorViewModel("invalid_request", "Parametri code/state mancanti"));
        try
        {
            var session = await client.CompleteAsync(state, code, iss, ct);
            SetStateCookie(state);
            return View("Profile", ToViewModel(session, refreshed: false));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Callback failed");
            return View("Error", new ErrorViewModel("Callback fallita", ex.Message));
        }
    }

    [HttpGet("/oidc/rp/refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var state = Request.Cookies[StateCookie];
        if (string.IsNullOrEmpty(state)) return RedirectToAction("Landing", "Home");
        try
        {
            var session = await client.RefreshAsync(state, ct);
            return View("Profile", ToViewModel(session, refreshed: true));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Refresh failed");
            return View("Error", new ErrorViewModel("Refresh fallito", ex.Message));
        }
    }

    [HttpGet("/oidc/rp/logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var state = Request.Cookies[StateCookie];
        if (!string.IsNullOrEmpty(state)) await client.LogoutAsync(state, ct);
        Response.Cookies.Delete(StateCookie);
        return View("LoggedOut");
    }

    private void SetStateCookie(string state) =>
        Response.Cookies.Append(StateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });

    private static ProfileViewModel ToViewModel(AuthSession s, bool refreshed) =>
        new(s.Profile!, s.AccessToken, s.AccessTokenExp, s.RefreshToken, s.RefreshTokenExp, refreshed);
}
