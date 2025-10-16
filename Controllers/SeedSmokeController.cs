using Microsoft.AspNetCore.Mvc;
using Seido.Utilities.SeedGenerator;

namespace Travel.Api.Controllers;

// En enkel testkontroller ("smoke test") för att verifiera att SeedGenerator fungerar.
// Returnerar två slumpmässiga tal när man anropar /api/seed/smoke.
[ApiController]
[Route("api/seed")]
public class SeedSmokeController : ControllerBase
{
    private readonly SeedGenerator _seed;

    // Dependency Injection: vi får in SeedGenerator via konstruktorn
    public SeedSmokeController(SeedGenerator seed) => _seed = seed;

    // ---------------------------------------------------------
    // GET /api/seed/smoke
    // Returnerar två slumpmässiga nummer för att testa att SeedGenerator fungerar.
    // n1: ett tal mellan 1 och 10
    // n2: ett tal mellan 1000 och 9999
    // ---------------------------------------------------------
    [HttpGet("smoke")]
    public IActionResult Smoke() => Ok(new {
        n1 = _seed.Next(1, 10),
        n2 = _seed.Next(1000, 9999)
    });
}
