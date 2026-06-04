using Microsoft.AspNetCore.Mvc;
using SpidCieOidc.Web.Models;
using SpidCieOidc.Web.Services;

namespace SpidCieOidc.Web.Controllers;

public sealed class HomeController(RpConfig rp, IConfiguration cfg) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => RedirectToAction(nameof(Landing));

    [HttpGet("/oidc/rp/landing")]
    public IActionResult Landing()
    {
        var spid = cfg["Oidc:Providers:spid"] ?? "http://trust-anchor.org:8000/oidc/op";
        var cie = cfg["Oidc:Providers:cie"] ?? "http://cie-provider.org:8002/oidc/op";
        return View(new LandingViewModel(rp.Sub, spid, cie));
    }

    [HttpGet("/error")]
    public IActionResult Error() =>
        View(new ErrorViewModel("Errore interno", "Si è verificato un errore imprevisto."));
}
