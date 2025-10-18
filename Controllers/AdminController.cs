using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Travel.Infrastructure.Data;                 // TravelDbContext
using Travel.Infrastructure.Seed;        // SeedService


namespace Travel.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly SeedService _seeder;
    private readonly TravelDbContext _db;
    private readonly IOptions<SeedOptions> _opt;

    // Konstruktor med Dependency Injection för att få in seeders, databas och options
    public AdminController(SeedService seeder, TravelDbContext db, IOptions<SeedOptions> opt)
    {
        _seeder = seeder;
        _db = db;
        _opt = opt;
    }

    // POST /api/admin/seed
    // Lägger till testdata i databasen (länder, städer, sevärdheter, användare, kommentarer)
    [HttpPost("seed")]
    public async Task<IActionResult> Seed([FromServices] DataSeeder baseSeeder)
    {
        await baseSeeder.SeedAsync();  // Länder, kategorier, städer, sevärdheter
        await _seeder.RunAsync();      // Users + Comments från SeedGenerator
        return Ok(new { message = "Seeded test data." });
    }

    // GET /api/admin/options
    // Hämtar de seed-inställningar som finns i appsettings.json
    [HttpGet("options")]
    public IActionResult Options() => Ok(_opt.Value);

    // GET /api/admin/stats
    // Returnerar statistik över hur mycket data som finns i databasen
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var result = new
        {
            Countries   = await _db.Countries.CountAsync(),
            Cities      = await _db.Cities.CountAsync(),
            Categories  = await _db.Categories.CountAsync(),
            Users       = await _db.Users.CountAsync(),
            Attractions = await _db.Attractions.CountAsync(),
            Comments    = await _db.Comments.CountAsync()
        };
        return Ok(result);
    }

    // DELETE /api/admin/wipe
    // Tar bort all testdata från databasen
    [HttpDelete("wipe")]
    public async Task<IActionResult> Wipe()
    {
        await _seeder.ClearAsync();
        return Ok(new { message = "All test data deleted." });
    }
}
