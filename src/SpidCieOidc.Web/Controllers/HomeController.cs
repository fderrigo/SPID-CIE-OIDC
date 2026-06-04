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

        var all = production.Select(p => new ProviderEntry(p.Profile, p.Name, p.EntityId, false))
            .Concat(local.Select(p => new ProviderEntry(p.Profile, p.Name, p.EntityId, true)))
            .ToList();

        var model = new LandingViewModel(
            ClientId: rp.Sub,
            Spid: all.Where(p => p.Profile == "spid").ToList(),
            Cie: all.Where(p => p.Profile == "cie").ToList());
        return View(model);
    }

    [HttpGet("/error")]
    public IActionResult Error() =>
        View(new ErrorViewModel("Errore interno", "Si è verificato un errore imprevisto."));
}
