using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travel.Infrastructure.Data; // TravelDbContext

namespace Travel.Api.Controllers;

// API-kontroller för att hantera länder.
// Exponerar en endpoint för att lista alla länder med information om hur många städer varje land har.
[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    private readonly TravelDbContext _db;

    // Dependency Injection: TravelDbContext injiceras via konstruktorn
    public CountriesController(TravelDbContext db) => _db = db;

    // ---------------------------------------------------------
    // GET /api/countries
    // Hämtar alla länder från databasen.
    // Varje land returneras med Id, kod (t.ex. "SE"), namn och antal städer.
    // ---------------------------------------------------------
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _db.Countries
            .AsNoTracking() // Ingen tracking behövs för read-only queries → bättre prestanda
            .Select(c => new CountryDto
            {
                Id     = c.Id,
                Code   = c.Code,
                Name   = c.Name,
                Cities = c.Cities.Count // Räknar antalet städer som hör till landet
            })
            .OrderBy(c => c.Code) // Sortera på landskod (t.ex. SE, NO, DK)
            .ToListAsync();

        return Ok(data);
    }

    // DTO (Data Transfer Object) för att bara returnera den information vi vill exponera.
    public sealed class CountryDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public int Cities { get; set; } // Antal städer i landet
    }
}
