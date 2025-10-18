using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travel.Infrastructure.Data; // TravelDbContext

namespace Travel.Api.Controllers;

// API-kontroller för att hantera städer.
// Exponerar en endpoint för att lista alla städer, med möjlighet att filtrera på land.
[ApiController]
[Route("api/[controller]")]
public class CitiesController : ControllerBase
{
    private readonly TravelDbContext _db;

    // Dependency Injection: databaskopplingen injiceras via konstruktorn
    public CitiesController(TravelDbContext db) => _db = db;

    // ---------------------------------------------------------
    // GET /api/cities?country=SE
    // Hämtar alla städer. Om queryparametern "country" anges,
    // filtreras resultatet så att endast städer från det landet returneras.
    // ---------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? country)
    {
        // Utgå från alla städer (AsNoTracking förbättrar prestanda för read-only queries)
        var q = _db.Cities.AsNoTracking();

        // Filtrera på landskod (om queryparametern finns)
        if (!string.IsNullOrWhiteSpace(country))
        {
            var up = country.ToUpperInvariant();
            q = q.Where(c => c.Country.Code == up);
        }

        // Projektera resultatet till en DTO för att inte exponera hela databasen
        var data = await q
            .Select(c => new CityDto
            {
                Id      = c.Id,
                Name    = c.Name,
                Country = c.Country.Code
            })
            .OrderBy(c => c.Country)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return Ok(data);
    }

    // DTO (Data Transfer Object) som returneras från API:t
    public sealed class CityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Country { get; set; } = null!;
    }
}
