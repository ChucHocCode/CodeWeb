using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WNCAirline.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    });

var baseMobilePort = 8091;
var resolvedMobilePort = ResolveAvailableHttpPort(baseMobilePort, 50);
builder.Services.AddSingleton(new MobileEndpointSettings(resolvedMobilePort));

builder.WebHost.UseUrls($"http://0.0.0.0:{resolvedMobilePort}");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any(u => u.Email == "admin"))
    {
        db.Users.Add(new User
        {
            Id = "admin001",
            Name = "Admin",
            Email = "admin",
            Password = "123",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        db.SaveChanges();
    }

    var flightEndTarget = new DateTime(2026, 12, 31);
    var needsFlightReseed = !db.Flights.Any()
        || !db.Flights.Any(f => f.DepartureTime >= flightEndTarget.AddDays(-14));

    if (needsFlightReseed)
    {
        if (db.Flights.Any())
        {
            db.Flights.RemoveRange(db.Flights);
            db.SaveChanges();
        }

        var today = DateTime.Today;
        var totalDays = (flightEndTarget - today).Days;
        var rng = new Random(42);
        var toAdd = new List<Flight>();

        void AddRoute(string from, string to, int dur, (string num, string airline, int h, int m, int price)[] sched)
        {
            for (int d = 0; d < totalDays; d++)
            {
                var date = today.AddDays(d);
                foreach (var (num, airline, h, m, price) in sched)
                {
                    var dep = date.Date.AddHours(h).AddMinutes(m);
                    var variation = rng.Next(-50000, 150001);
                    toAdd.Add(new Flight
                    {
                        Id = $"{num}-{date:yyyyMMdd}",
                        FlightNumber = num,
                        Airline = airline,
                        DepartureCity = from,
                        ArrivalCity = to,
                        DepartureTime = dep,
                        ArrivalTime = dep.AddMinutes(dur),
                        Duration = dur,
                        BasePrice = Math.Max(price + variation, 450000),
                        TotalSeats = 180,
                        AvailableSeats = rng.Next(8, 165),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }
            }
        }

        AddRoute("HAN", "SGN", 120, new[]
        {
            ("VN221", "Vietnam Airlines", 5,  30, 1550000),
            ("PA101", "Pacific Airlines", 6,  30, 1100000),
            ("VN223", "Vietnam Airlines", 7,  45, 1750000),
            ("VJ101", "VietJet Air",      9,  0,  1250000),
            ("PA103", "Pacific Airlines", 10, 30, 1050000),
            ("VJ103", "VietJet Air",      12, 15, 980000),
            ("QH201", "Bamboo Airways",   13, 30, 1350000),
            ("VN225", "Vietnam Airlines", 15, 0,  1900000),
            ("PA105", "Pacific Airlines", 16, 15, 1150000),
            ("VJ105", "VietJet Air",      18, 0,  1200000),
            ("QH203", "Bamboo Airways",   19, 30, 1400000),
            ("VN227", "Vietnam Airlines", 21, 0,  1600000),
        });

        AddRoute("SGN", "HAN", 120, new[]
        {
            ("VN222", "Vietnam Airlines", 6,  0,  1550000),
            ("PA102", "Pacific Airlines", 7,  15, 1100000),
            ("VN224", "Vietnam Airlines", 8,  30, 1750000),
            ("VJ102", "VietJet Air",      10, 0,  1250000),
            ("PA104", "Pacific Airlines", 11, 30, 1050000),
            ("VJ104", "VietJet Air",      13, 15, 980000),
            ("QH202", "Bamboo Airways",   14, 30, 1350000),
            ("VN226", "Vietnam Airlines", 16, 0,  1900000),
            ("PA106", "Pacific Airlines", 17, 15, 1150000),
            ("VJ106", "VietJet Air",      19, 0,  1200000),
            ("QH204", "Bamboo Airways",   20, 30, 1400000),
            ("VN228", "Vietnam Airlines", 22, 0,  1600000),
        });

        AddRoute("HAN", "DAD", 75, new[]
        {
            ("VN531", "Vietnam Airlines", 6,  0,  850000),
            ("PA301", "Pacific Airlines", 7,  30, 700000),
            ("VJ301", "VietJet Air",      9,  0,  650000),
            ("QH401", "Bamboo Airways",   10, 30, 750000),
            ("PA303", "Pacific Airlines", 12, 0,  720000),
            ("VN533", "Vietnam Airlines", 13, 30, 950000),
            ("VJ303", "VietJet Air",      15, 0,  680000),
            ("QH403", "Bamboo Airways",   16, 30, 780000),
            ("VN535", "Vietnam Airlines", 18, 0,  900000),
            ("PA305", "Pacific Airlines", 19, 30, 730000),
        });

        AddRoute("DAD", "HAN", 75, new[]
        {
            ("VN532", "Vietnam Airlines", 7,  0,  850000),
            ("PA302", "Pacific Airlines", 8,  30, 700000),
            ("VJ302", "VietJet Air",      10, 0,  650000),
            ("QH402", "Bamboo Airways",   11, 30, 750000),
            ("PA304", "Pacific Airlines", 13, 0,  720000),
            ("VN534", "Vietnam Airlines", 14, 30, 950000),
            ("VJ304", "VietJet Air",      16, 0,  680000),
            ("QH404", "Bamboo Airways",   17, 30, 780000),
            ("VN536", "Vietnam Airlines", 19, 0,  900000),
            ("PA306", "Pacific Airlines", 20, 30, 730000),
        });

        AddRoute("SGN", "DAD", 80, new[]
        {
            ("VN741", "Vietnam Airlines", 6,  30, 900000),
            ("PA501", "Pacific Airlines", 8,  0,  720000),
            ("VJ501", "VietJet Air",      9,  30, 700000),
            ("QH601", "Bamboo Airways",   11, 0,  800000),
            ("PA503", "Pacific Airlines", 12, 30, 750000),
            ("VN743", "Vietnam Airlines", 14, 0,  1000000),
            ("VJ503", "VietJet Air",      15, 30, 750000),
            ("QH603", "Bamboo Airways",   17, 0,  820000),
            ("VN745", "Vietnam Airlines", 18, 30, 950000),
            ("PA505", "Pacific Airlines", 20, 0,  700000),
        });

        AddRoute("DAD", "SGN", 80, new[]
        {
            ("VN742", "Vietnam Airlines", 7,  30, 900000),
            ("PA502", "Pacific Airlines", 9,  0,  720000),
            ("VJ502", "VietJet Air",      10, 30, 700000),
            ("QH602", "Bamboo Airways",   12, 0,  800000),
            ("PA504", "Pacific Airlines", 13, 30, 750000),
            ("VN744", "Vietnam Airlines", 15, 0,  1000000),
            ("VJ504", "VietJet Air",      16, 30, 750000),
            ("QH604", "Bamboo Airways",   18, 0,  820000),
            ("VN746", "Vietnam Airlines", 19, 30, 950000),
            ("PA506", "Pacific Airlines", 21, 0,  700000),
        });

        AddRoute("HAN", "PQC", 100, new[]
        {
            ("VN801", "Vietnam Airlines", 6,  30, 1150000),
            ("PA701", "Pacific Airlines", 8,  0,  950000),
            ("VJ701", "VietJet Air",      10, 0,  900000),
            ("QH801", "Bamboo Airways",   12, 30, 1050000),
            ("PA703", "Pacific Airlines", 14, 0,  980000),
            ("VN803", "Vietnam Airlines", 16, 0,  1250000),
            ("VJ703", "VietJet Air",      18, 30, 920000),
            ("QH803", "Bamboo Airways",   20, 0,  1000000),
        });

        AddRoute("SGN", "PQC", 60, new[]
        {
            ("VN811", "Vietnam Airlines", 6,  30, 600000),
            ("PA711", "Pacific Airlines", 8,  0,  480000),
            ("VJ711", "VietJet Air",      9,  30, 450000),
            ("QH811", "Bamboo Airways",   11, 0,  520000),
            ("PA713", "Pacific Airlines", 12, 30, 500000),
            ("VN813", "Vietnam Airlines", 14, 0,  650000),
            ("VJ713", "VietJet Air",      15, 30, 480000),
            ("QH813", "Bamboo Airways",   17, 0,  540000),
            ("VN815", "Vietnam Airlines", 18, 30, 620000),
            ("PA715", "Pacific Airlines", 20, 0,  470000),
        });

        AddRoute("HAN", "CXR", 95, new[]
        {
            ("VN901", "Vietnam Airlines", 6,  30, 1050000),
            ("PA901", "Pacific Airlines", 8,  0,  880000),
            ("VJ901", "VietJet Air",      10, 0,  850000),
            ("QH901", "Bamboo Airways",   12, 0,  950000),
            ("PA903", "Pacific Airlines", 14, 0,  900000),
            ("VN903", "Vietnam Airlines", 16, 0,  1150000),
            ("VJ903", "VietJet Air",      18, 30, 870000),
            ("QH903", "Bamboo Airways",   20, 0,  980000),
        });

        AddRoute("SGN", "CXR", 75, new[]
        {
            ("VN911", "Vietnam Airlines", 7,  0,  750000),
            ("PA911", "Pacific Airlines", 8,  30, 620000),
            ("VJ911", "VietJet Air",      10, 0,  580000),
            ("QH911", "Bamboo Airways",   11, 30, 660000),
            ("PA913", "Pacific Airlines", 13, 0,  640000),
            ("VN913", "Vietnam Airlines", 14, 30, 800000),
            ("VJ913", "VietJet Air",      16, 0,  600000),
            ("QH913", "Bamboo Airways",   17, 30, 680000),
            ("VN915", "Vietnam Airlines", 19, 0,  780000),
            ("PA915", "Pacific Airlines", 20, 30, 610000),
        });

        AddRoute("SGN", "HPH", 135, new[]
        {
            ("VN451", "Vietnam Airlines", 7,  0,  1400000),
            ("PA451", "Pacific Airlines", 9,  0,  1150000),
            ("VJ451", "VietJet Air",      11, 30, 1100000),
            ("QH451", "Bamboo Airways",   14, 0,  1250000),
            ("VN453", "Vietnam Airlines", 16, 30, 1500000),
            ("PA453", "Pacific Airlines", 19, 0,  1200000),
        });

        AddRoute("HPH", "SGN", 135, new[]
        {
            ("VN452", "Vietnam Airlines", 8,  30, 1400000),
            ("PA452", "Pacific Airlines", 10, 30, 1150000),
            ("VJ452", "VietJet Air",      13, 0,  1100000),
            ("QH452", "Bamboo Airways",   15, 30, 1250000),
            ("VN454", "Vietnam Airlines", 18, 0,  1500000),
            ("PA454", "Pacific Airlines", 20, 30, 1200000),
        });

        db.Flights.AddRange(toAdd);
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Lifetime.ApplicationStarted.Register(() =>
{
    var localUrl = $"http://localhost:{resolvedMobilePort}";

    Console.WriteLine();
    Console.WriteLine("================ WNC URLS ================");
    Console.WriteLine($"Local  : {localUrl}");
    Console.WriteLine("===========================================");
    Console.WriteLine();

    var shouldOpenBrowser = string.Equals(
        Environment.GetEnvironmentVariable("WNC_OPEN_BROWSER"),
        "1",
        StringComparison.OrdinalIgnoreCase);

    if (!shouldOpenBrowser)
    {
        return;
    }

    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = localUrl,
            UseShellExecute = true
        });
    }
    catch
    {
        // Ignore browser open failures; the URL is already printed for manual open.
    }
});

app.Run();

static int ResolveAvailableHttpPort(int startPort, int maxOffset)
{
    for (var offset = 0; offset <= maxOffset; offset++)
    {
        var candidatePort = startPort + offset;
        if (IsPortAvailable(candidatePort))
        {
            return candidatePort;
        }
    }

    return startPort;
}

static bool IsPortAvailable(int port)
{
    try
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        return listeners.All(endpoint => endpoint.Port != port);
    }
    catch
    {
        return false;
    }
}
