using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Seido.Utilities.SeedGenerator;      // <— från external/SeedGenerator
using Travel.Infrastructure;               // TravelDbContext
using Travel.Api.Seeding;                  // DataSeeder
using Travel.Infrastructure.Seeding;       // SeedService + SeedOptions

var builder = WebApplication.CreateBuilder(args);

// ----- Connection string (rätt nyckel!) -----
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("[DB] Using connection: " + (conn ?? "<null>").Replace("Password=", "Password=****"));

// ----- Endast SQL Server -----
builder.Services.AddDbContext<TravelDbContext>(o =>
{
    o.UseSqlServer(conn, sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));
    o.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    o.EnableDetailedErrors(builder.Environment.IsDevelopment());
    o.EnableSensitiveDataLogging(false);
});

// ----- MVC + Swagger -----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ----- Seeding (krav i uppgiften) -----
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection("Seed"));
builder.Services.PostConfigure<SeedOptions>(opt =>
{
    var fallback = new[] { "SE", "NO", "FI", "DK" };
    opt.Countries = (opt.Countries ?? Array.Empty<string>())
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.ToUpperInvariant())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    foreach (var code in fallback)
        if (!opt.Countries.Contains(code, StringComparer.OrdinalIgnoreCase))
            opt.Countries = opt.Countries.Append(code).ToArray();

    opt.Users       = Math.Max(opt.Users, 50);
    opt.Cities      = Math.Max(opt.Cities, 100);
    opt.Attractions = Math.Max(opt.Attractions, 1000);
    opt.CommentsPerAttractionMin = 0;
    opt.CommentsPerAttractionMax = 20;
});

builder.Services.AddSingleton<SeedGenerator>();
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped(services => new SeedService(
    services.GetRequiredService<TravelDbContext>(),
    services.GetRequiredService<SeedGenerator>(),
    services.GetRequiredService<Microsoft.Extensions.Options.IOptions<SeedOptions>>()
));

var app = builder.Build();

// ----- Automatiska migrationer + seeding -----
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<TravelDbContext>();
        db.Database.Migrate();
        Console.WriteLine("[DB] Migrate OK");

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync(); // om din DataSeeder har async, annars seeder.Seed();
        Console.WriteLine("[DB] Seed OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine("[DB] Migrate/Seed failed: " + ex.Message);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
