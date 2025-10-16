using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Seido.Utilities.SeedGenerator;
using Travel.Domain;
using Travel.Infrastructure;
using Travel.Infrastructure.Seeding;

namespace Travel.Api.Seeding;

// DataSeeder ansvarar för att fylla databasen med testdata.
// Den skapar länder, kategorier, städer, användare, sevärdheter och kommentarer.
public class DataSeeder
{
    private readonly TravelDbContext _db;
    private readonly SeedGenerator _rand;
    private readonly SeedOptions _opt;

    // Konstruktor där vi injicerar DbContext, SeedGenerator och SeedOptions (från appsettings.json)
    public DataSeeder(TravelDbContext db, SeedGenerator rand, IOptions<SeedOptions> opt)
    {
        _db  = db;
        _rand = rand;
        _opt  = opt.Value;
    }

    // Metod för att fylla databasen med data
    public async Task SeedAsync()
    {
        // ---------------------------------------------------------
        // 1) Countries – skapa länder om de saknas
        // ---------------------------------------------------------
        var fallback = new[] { "SE", "NO", "FI", "DK" };
        var wantCodes = (_opt.Countries?.Length > 0 ? _opt.Countries : fallback)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var known = new Dictionary<string, string>
        { ["SE"] = "Sweden", ["NO"] = "Norway", ["FI"] = "Finland", ["DK"] = "Denmark" };

        var haveCodes = await _db.Countries.AsNoTracking().Select(c => c.Code).ToListAsync();
        foreach (var code in wantCodes.Except(haveCodes, StringComparer.OrdinalIgnoreCase))
            _db.Countries.Add(new Country { Code = code, Name = known.GetValueOrDefault(code, code) });

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync();

        // ---------------------------------------------------------
        // 2) Categories – skapa standardkategorier om de saknas
        // ---------------------------------------------------------
        string[] catNames = { "Restaurant","Cafe","Architecture","Museum","Park","Monument","Viewpoint","Beach","Nature" };
        var haveCats = await _db.Categories.AsNoTracking().Select(c => c.Name).ToListAsync();
        foreach (var n in catNames.Except(haveCats))
            _db.Categories.Add(new Category { Name = n });

        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync();

        // ---------------------------------------------------------
        // 3) Cities – skapa städer för länderna (exakta antal från SeedOptions)
        // ---------------------------------------------------------
        var countries = await _db.Countries.AsNoTracking().ToListAsync();
        int existingCities = await _db.Cities.CountAsync();
        int needCities = Math.Max(0, _opt.Cities - existingCities);

        if (needCities > 0)
        {
            // Fördefinierade stadlistor per land
            var citiesByCountry = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["SE"] = new[] { "Stockholm", "Göteborg", "Malmö", "Uppsala", "Västerås", "Örebro", "Linköping", "Helsingborg", "Jönköping", "Norrköping" },
                ["NO"] = new[] { "Oslo", "Bergen", "Trondheim", "Stavanger", "Drammen", "Fredrikstad" },
                ["DK"] = new[] { "København", "Aarhus", "Odense", "Aalborg", "Esbjerg", "Randers" },
                ["FI"] = new[] { "Helsingfors", "Esbo", "Tammerfors", "Vanda", "Åbo", "Uleåborg" }
            };

            // Kontrollera vilka städer som redan finns (för att undvika dubbletter)
            var cityKeys = new HashSet<string>(
                await _db.Cities.AsNoTracking().Select(x => x.Name + "|" + x.CountryId).ToListAsync());

            // Skapa en blandad lista av möjliga städer
            var pool = new List<(int CountryId, string Code, string Name)>();
            foreach (var co in countries)
            {
                if (citiesByCountry.TryGetValue(co.Code, out var names))
                    pool.AddRange(names.Select(n => (co.Id, co.Code, n)));
                else
                    pool.Add((co.Id, co.Code, $"{co.Name} City"));
            }
            pool = pool.OrderBy(_ => Guid.NewGuid()).ToList();

            int created = 0;

            // Lägg till städer tills vi nått det antal som behövs
            foreach (var p in pool)
            {
                if (created >= needCities) break;
                var key = p.Name + "|" + p.CountryId;
                if (cityKeys.Add(key))
                {
                    _db.Cities.Add(new City { Name = p.Name, CountryId = p.CountryId });
                    if (++created % 200 == 0) await _db.SaveChangesAsync(); // batcha för prestanda
                }
            }

            // Om fler städer behövs, skapa "kopior" med siffror (t.ex. "Stockholm 2")
            for (int k = 2; created < needCities && k < 9999; k++)
            {
                foreach (var p in pool)
                {
                    if (created >= needCities) break;
                    var name = $"{p.Name} {k}";
                    var key  = name + "|" + p.CountryId;
                    if (cityKeys.Add(key))
                    {
                        _db.Cities.Add(new City { Name = name, CountryId = p.CountryId });
                        if (++created % 200 == 0) await _db.SaveChangesAsync();
                    }
                }
            }

            if (created > 0) await _db.SaveChangesAsync();
        }

        // ---------------------------------------------------------
        // 4) Users – generera namn + unika e-postadresser
        // ---------------------------------------------------------
        int existingUsers = await _db.Users.CountAsync();
        int needUsers = Math.Max(0, _opt.Users - existingUsers);

        if (needUsers > 0)
        {
            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var buffer = new List<UserAccount>(capacity: 256);

            for (int i = 0; i < needUsers; i++)
            {
                var full  = _rand.FullName;
                var parts = full.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var first = parts.FirstOrDefault() ?? "User";
                var last  = parts.Length > 1 ? parts[^1] : "Seed";

                var email = _rand.Email(first, last);
                var baseEmail = email; var n = 2;

                // säkerställ unika e-postadresser
                while (!emails.Add(email))
                    email = baseEmail.Insert(baseEmail.IndexOf('@'), $"{n++}");

                buffer.Add(new UserAccount { DisplayName = full, Email = email });

                if (buffer.Count >= 250)
                {
                    _db.Users.AddRange(buffer);
                    buffer.Clear();
                    await _db.SaveChangesAsync();
                }
            }

            if (buffer.Count > 0)
            {
                _db.Users.AddRange(buffer);
                await _db.SaveChangesAsync();
            }
        }

        // ---------------------------------------------------------
        // 5) Attractions – skapa sevärdheter med titlar & beskrivningar
        // ---------------------------------------------------------
        var catRows  = await _db.Categories.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync();
        var cityRows = await _db.Cities.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync();

        int existingAttr = await _db.Attractions.CountAsync();
        int needAttr = Math.Max(0, _opt.Attractions - existingAttr);

        static string Pick(SeedGenerator r, params string[] xs) => xs[r.Next(0, xs.Length)];

        for (int i = 0; i < needAttr; i++)
        {
            var cat  = catRows[_rand.Next(0, catRows.Count)];
            var city = cityRows[_rand.Next(0, cityRows.Count)];

            var adj = Pick(_rand, "Cozy","Classic","Modern","Scenic","Iconic","Hidden","Grand","Riverside","Old Town","Harbor");

            // Titel anpassas beroende på kategori
            string title = cat.Name switch
            {
                "Restaurant"   => $"{adj} {Pick(_rand, "Bistro","Restaurant","Kitchen")} {city.Name}",
                "Cafe"         => $"{adj} Café {city.Name}",
                "Museum"       => $"{adj} {Pick(_rand, "Museum","Gallery")}, {city.Name}",
                "Park"         => $"{adj} Park, {city.Name}",
                "Beach"        => $"{adj} Beach, {city.Name}",
                "Viewpoint"    => $"{adj} Viewpoint, {city.Name}",
                "Monument"     => $"{adj} Monument, {city.Name}",
                "Architecture" => $"{adj} Landmark, {city.Name}",
                _              => $"{adj} {cat.Name} in {city.Name}"
            };

            // Generera beskrivning
            var s1 = _rand.LatinSentence ?? _rand.Quote?.Quote ?? _rand.LatinWordsAsSentence(10);
            var s2 = _rand.Next(0, 100) < 60 ? (_rand.LatinSentence ?? _rand.LatinWordsAsSentence(12)) : null;
            var description = s2 is null ? s1 : $"{s1} {s2}";

            _db.Attractions.Add(new Attraction
            {
                Title         = title,
                Description   = description,
                AddressLine   = $"Street {_rand.Next(1, 1000)}",
                PostalCode    = $"{_rand.Next(10000, 100000)}",
                Latitude      = (decimal)(_rand.Next(-90000000, 90000001)  / 1_000_000.0),
                Longitude     = (decimal)(_rand.Next(-180000000, 180000001) / 1_000_000.0),
                IsRecommended = _rand.Next(0, 100) < 30, // ca 30% rekommenderas
                CategoryId    = cat.Id,
                CityId        = city.Id
            });

            if (i % 200 == 199) await _db.SaveChangesAsync(); // batcha
        }
        if (needAttr > 0) await _db.SaveChangesAsync();

        // ---------------------------------------------------------
        // 6) Comments – generera kommentarer för sevärdheter
        // ---------------------------------------------------------
        var userIds = await _db.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
        var attrIds = await _db.Attractions.AsNoTracking().Select(a => a.Id).ToListAsync();

        int minC = Math.Max(0, Math.Min(_opt.CommentsPerAttractionMin, _opt.CommentsPerAttractionMax));
        int maxC = Math.Max(minC, Math.Max(_opt.CommentsPerAttractionMin, _opt.CommentsPerAttractionMax));

        foreach (var aid in attrIds)
        {
            // hoppa över attraktioner som redan har kommentarer
            if (await _db.Comments.AnyAsync(c => c.AttractionId == aid))
                continue;

            int n = _rand.Next(minC, maxC + 1); // antal kommentarer
            for (int i = 0; i < n; i++)
            {
                var text = _rand.LatinSentence ?? _rand.Quote?.Quote ?? _rand.LatinWordsAsSentence(8);

                _db.Comments.Add(new Comment
                {
                    AttractionId  = aid,
                    UserAccountId = userIds[_rand.Next(0, userIds.Count)],
                    Text          = text,
                    CreatedAt     = _rand.DateAndTime(DateTime.UtcNow.Year - 1, DateTime.UtcNow.Year + 1)
                });
            }

            // Spara då och då för att undvika för mycket i minnet
            if (_db.ChangeTracker.Entries().Count() > 500)
                await _db.SaveChangesAsync();
        }
        await _db.SaveChangesAsync();
    }

    // Metod för att rensa databasen helt (i FK-ordning)
    public async Task ClearAsync()
    {
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM [Comments]");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM [Attractions]");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM [Users]");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM [Cities]");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM [Categories]");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM [Countries]");
    }
}
