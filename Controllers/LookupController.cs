using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travel.Infrastructure;

namespace Travel.Api.Controllers;

// API-kontroller för att hämta uppslagsdata (countries och cities).
// Den används ofta för dropdowns, filter eller annan "lookup"-data i frontend.
[ApiController]
[Route("api/[controller]")]
public class LookupController : ControllerBase
{
    private readonly TravelDbContext _db;

    // Dependency Injection: vi får in databaskopplingen via konstruktorn
    public LookupController(TravelDbContext db) => _db = db;

    // ---------------------------------------------------------
    // GET /api/lookup/countries
    // Hämtar alla länder med Id, kod (t.ex. "SE"), namn och antal städer.
    // ---------------------------------------------------------
    [HttpGet("countries")]
    public async Task<IActionResult> Countries()
    {
        var data = await _db.Countries
            .AsNoTracking() // förbättrar prestanda för read-only queries
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Name,
                CityCount = c.Cities.Count // Räknar hur många städer landet har
            })
            .OrderBy(c => c.Code)
            .ToListAsync();

        return Ok(data);
    }

    // ---------------------------------------------------------
    // GET /api/lookup/cities?country=SE
    // Hämtar alla städer. Om queryparametern "country" anges
    // filtreras resultatet till städer i det landet.
    // ---------------------------------------------------------
    [HttpGet("cities")]
    public async Task<IActionResult> Cities([FromQuery] string? country)
    {
        // Starta query med Include så vi får med landsinformation
        var q = _db.Cities
            .AsNoTracking()
            .Include(ci => ci.Country)
            .AsQueryable();

        // Om en landskod skickas in (t.ex. SE), filtrera resultatet
        if (!string.IsNullOrWhiteSpace(country))
        {
            var up = country.Trim().ToUpperInvariant();
            q = q.Where(ci => ci.Country.Code == up);
        }

        // Projektera till en anonym typ med både stad och land
        var data = await q
            .Select(ci => new
            {
                ci.Id,
                ci.Name,
                CountryCode = ci.Country.Code,
                CountryName = ci.Country.Name
            })
            .OrderBy(x => x.CountryCode).ThenBy(x => x.Name)
            .ToListAsync();

        return Ok(data);
    }
}
