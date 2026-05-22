namespace WNCAirline.Models;

public static class SeatStore
{
    private static readonly Dictionary<string, HashSet<string>> OccupiedByFlight = new();
    private static readonly object Lock = new();

    public static IReadOnlyCollection<string> GetOccupied(string flightId)
    {
        lock (Lock)
        {
            return OccupiedByFlight.TryGetValue(flightId, out var set)
                ? set.ToList()
                : [];
        }
    }

    public static void ClaimSeat(string flightId, string seat)
    {
        if (string.IsNullOrWhiteSpace(flightId) || string.IsNullOrWhiteSpace(seat) || seat == "N/A")
            return;
        lock (Lock)
        {
            if (!OccupiedByFlight.TryGetValue(flightId, out var set))
                OccupiedByFlight[flightId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            set.Add(seat.Trim());
        }
    }
}
