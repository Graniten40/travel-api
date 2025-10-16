using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travel.Infrastructure;

namespace Travel.Api.Controllers;

// API-kontroller för att hantera användare.
// Här kan vi bl.a. hämta användare tillsammans med deras kommentarer.
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly TravelDbContext _db;

    // Dependency Injection: vi får in databaskopplingen via konstruktorn
    public UsersController(TravelDbContext db) => _db = db;

    // ---------------------------------------------------------
    // GET /api/users/comments?onlyWithComments=true&page=1&pageSize=50
    // Hämtar en lista av användare, med möjlighet att:
    // - endast ta med de som har kommentarer (onlyWithComments=true)
    // - använda pagination (page och pageSize)
    // Returnerar även varje användares senaste kommentarer.
    // ---------------------------------------------------------
    [HttpGet("comments")]
    public async Task<IActionResult> UsersWithComments(
        [FromQuery] bool onlyWithComments = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        // Säkerställ giltiga värden för sidnummer och sidstorlek
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Basquery: hämta alla användare (utan tracking för bättre prestanda)
        var q = _db.Users
            .AsNoTracking()
            .AsQueryable();

        // Om onlyWithComments = true, filtrera bort användare utan kommentarer
        if (onlyWithComments)
            q = q.Where(u => u.Comments.Any());

        // Antal användare i totalresultatet
        var total = await q.CountAsync();

        // Hämta den efterfrågade sidan, sorterat på DisplayName
        var items = await q
            .OrderBy(u => u.DisplayName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.DisplayName,
                u.Email,
                // Hämtar användarens kommentarer, senaste först
                Comments = u.Comments
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
                    {
                        c.Id,
                        c.Text,
                        c.CreatedAt,
                        Attraction = c.Attraction.Title // visar vilken sevärdhet kommentaren hör till
                    })
                    .ToList()
            })
            .ToListAsync();

        // Returnerar resultatet som JSON
        return Ok(new { total, page, pageSize, items });
    }
}
