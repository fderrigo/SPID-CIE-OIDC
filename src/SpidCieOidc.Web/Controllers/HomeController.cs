using Microsoft.AspNetCore.Mvc;
using SpidCieOidc.Web.Models;
using SpidCieOidc.Web.Services;

namespace SpidCieOidc.Web.Controllers;

public sealed class HomeController(RpConfig rp, IConfiguration cfg, IWebHostEnvironment env) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => RedirectToAction(nameof(Landing));

    [HttpGet("/oidc/rp/landing")]
    public IActionResult Landing()
    {
        var production = cfg.GetSection("Oidc:ProductionProviders").Get<List<ProviderOption>>() ?? new();
        var local = env.IsDevelopment()
            ? cfg.GetSection("Oidc:LocalProviders").Get<List<ProviderOption>>() ?? new()
            : new();

        var all = production.Select(p => new ProviderEntry(p.Profile, p.Name, p.EntityId, false, p.Logo))
            .Concat(local.Select(p => new ProviderEntry(p.Profile, p.Name, p.EntityId, true, p.Logo)))
            .ToList();

        // AgID rule: the SPID IdP order MUST be randomized at every render.
        var spid = all.Where(p => p.Profile == "spid").ToArray();
        Random.Shared.Shuffle(spid);

        var model = new LandingViewModel(
            ClientId: rp.Sub,
            Spid: spid,
            Cie: all.Where(p => p.Profile == "cie").ToList());
        return View(model);
    }

    [HttpGet("/error")]
    public IActionResult Error() =>
        View(new ErrorViewModel("Errore interno", "Si è verificato un errore imprevisto."));
}
