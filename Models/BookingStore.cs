using System.Text.Json;

namespace WNCAirline.Models;

public static class BookingStore
{
    private static readonly string DataPath = Path.Combine("App_Data", "bookings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly List<BookingRecord> Bookings;

    static BookingStore()
    {
        try
        {
            if (File.Exists(DataPath))
            {
                var json = File.ReadAllText(DataPath);
                var loaded = JsonSerializer.Deserialize<List<BookingRecord>>(json, JsonOptions);
                if (loaded is { Count: > 0 })
                {
                    Bookings = loaded;
                    return;
                }
            }
        }
        catch { }

        Bookings = CreateDefaults();
        SaveInternal();
    }

    private static List<BookingRecord> CreateDefaults() =>
    [
        new() { PNR = "WNX7K9", Contact = "nguyen.minh@demo.vn", Route = "HAN -> SGN", FlightDate = "28/03/2026 09:15", Status = "HELD", Fare = "1.690.000đ", Tax = "360.000đ", Total = "2.050.000đ" },
        new() { PNR = "WNA2P4", Contact = "tran.ha@demo.vn",     Route = "DAD -> HAN", FlightDate = "29/03/2026 16:30", Status = "HELD", Fare = "1.200.000đ", Tax = "290.000đ", Total = "1.490.000đ" },
        new() { PNR = "WNB5L1", Contact = "pham.quynh@demo.vn",  Route = "SGN -> CXR", FlightDate = "02/04/2026 06:45", Status = "PAID", Fare = "980.000đ",   Tax = "210.000đ", Total = "1.190.000đ" },
        new() { PNR = "WNC8R3", Contact = "le.tuan@demo.vn",     Route = "HAN -> PQC", FlightDate = "06/04/2026 11:10", Status = "HELD", Fare = "1.850.000đ", Tax = "400.000đ", Total = "2.250.000đ" },
        new() { PNR = "WND1M6", Contact = "hoang.nam@demo.vn",   Route = "SGN -> HPH", FlightDate = "11/04/2026 20:20", Status = "HELD", Fare = "1.450.000đ", Tax = "330.000đ", Total = "1.780.000đ" }
    ];

    public static IEnumerable<BookingRecord> GetAll()
    {
        lock (Bookings) { return Bookings.ToList(); }
    }

    public static BookingRecord Add(BookingRecord record)
    {
        lock (Bookings)
        {
            Bookings.Add(record);
            SaveInternal();
        }
        return record;
    }

    public static void Save()
    {
        lock (Bookings) { SaveInternal(); }
    }

    private static void SaveInternal()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            File.WriteAllText(DataPath, JsonSerializer.Serialize(Bookings, JsonOptions));
        }
        catch { }
    }
}
