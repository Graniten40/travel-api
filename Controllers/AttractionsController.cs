using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travel.Infrastructure;
using Travel.Domain.Dtos;
using Travel.Domain;

namespace Travel.Api.Controllers;

// API-kontroller för att hantera sevärdheter (CRUD + kommentarer).
// Här finns endpoints för att lista, söka, filtrera och lägga till kommentarer.
[ApiController]
[Route("api/[controller]")]
public class AttractionsController : ControllerBase
{
    private readonly TravelDbContext _db;

    // Dependency Injection: databaskopplingen injiceras via konstruktorn
    public AttractionsController(TravelDbContext db) => _db = db;

    // ---------------------------
    // 1) Lista sevärdheter (med filter och pagination)
    // ---------------------------
    // GET /api/attractions?... (olika queryparametrar)
    [HttpGet("")]
    public async Task<IActionResult> List(
        [FromQuery] string? category,
        [FromQuery] string? title,
        [FromQuery] string? description,
        [FromQuery] string? country,
        [FromQuery] string? city,
        [FromQuery] bool? recommended,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        // Säkerställ giltiga värden för sidnummer och sidstorlek
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Basquery: hämta sevärdheter med kategori, stad och land
        var q = _db.Attractions
            .AsNoTracking() // förbättrar prestanda när vi bara läser
            .Include(a => a.Category)
            .Include(a => a.City).ThenInclude(ci => ci.Country)
            .AsQueryable();

        // Filtrera på olika queryparametrar (om de skickas in)
        if (!string.IsNullOrWhiteSpace(category))
        {
            var v = category.Trim();
            q = q.Where(a => EF.Functions.Like(a.Category.Name, $"%{v}%"));
        }
        // ... samma princip för title, description, country, city

        if (recommended is true)
        {
            q = q.Where(a => a.IsRecommended);
        }

        var total = await q.CountAsync(); // totala antalet träffar

        // Hämta den sida användaren begär, sorterad på titel
        var items = await q
            .OrderBy(a => a.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                Title = a.Title,
                Description = a.Description,
                Category = a.Category.Name,
                City = a.City.Name,
                Country = a.City.Country.Code,
                IsRecommended = a.IsRecommended
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    // GET /api/attractions/{id}/comments?page=1&pageSize=50
[HttpGet("{id:int}/comments")]
public async Task<IActionResult> GetComments(
    int id,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50)
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 200);

    // Finns sevärdheten?
    var exists = await _db.Attractions.AsNoTracking().AnyAsync(a => a.Id == id);
    if (!exists) return NotFound(new { message = "Attraction not found." });

    var q = _db.Comments
        .AsNoTracking()
        .Where(c => c.AttractionId == id);

    var total = await q.CountAsync();

    var items = await q
        .OrderByDescending(c => c.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(c => new CommentItemDto
        {
            Id = c.Id,
            Text = c.Text,
            CreatedAt = c.CreatedAt,
            User = new UserMiniDto
            {
                Id = c.UserAccount.Id,
                DisplayName = c.UserAccount.DisplayName
            },
            Attraction = new AttractionMiniDto
            {
                Id = c.Attraction.Id,
                Title = c.Attraction.Title,
                CityId = c.Attraction.CityId,
                City = c.Attraction.City.Name,
                Country = c.Attraction.City.Country.Code
            }
        })
        .ToListAsync();

    return Ok(new { total, page, pageSize, items });
}

    // ---------------------------------------------------------
    // 3) Lägg till kommentar på en sevärdhet
    // ---------------------------------------------------------
    // POST /api/attractions/{id}/comments
    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentDto input)
    {
        // Kontrollera att sevärdheten finns
        var exists = await _db.Attractions.AsNoTracking().AnyAsync(a => a.Id == id);
        if (!exists) return NotFound(new { message = "Attraction not found." });

        // Kontrollera att användaren finns
        var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == input.UserId);
        if (!userExists) return BadRequest(new { message = "UserId not found." });

        if (string.IsNullOrWhiteSpace(input.Text))
            return BadRequest(new { message = "Text is required." });

        // Skapa ny kommentar
        var comment = new Comment
        {
            AttractionId = id,
            UserAccountId = input.UserId,
            Text = input.Text.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        // Returnera det skapade objektet med CreatedAtAction
        var view = await _db.Comments
            .AsNoTracking()
            .Where(c => c.Id == comment.Id)
            .Select(c => new CommentItemDto
            {
                Id = c.Id,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                User = new UserMiniDto
                {
                    Id = c.UserAccount.Id,
                    DisplayName = c.UserAccount.DisplayName
                },
                Attraction = new AttractionMiniDto
                {
                    Id = c.Attraction.Id,
                    Title = c.Attraction.Title,
                    CityId = c.Attraction.CityId,
                    City = c.Attraction.City.Name,
                    Country = c.Attraction.City.Country.Code
                }
            })
            .FirstAsync();

        return CreatedAtAction(nameof(GetComments), new { id }, view);
    }

    // ---------------------------------------------------------
    // DTOs används för att skicka begränsad information till/från API:et
    // ---------------------------------------------------------
}
