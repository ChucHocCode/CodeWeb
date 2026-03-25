namespace WNCAirline.Models;

public class Flight
{
    public string Id { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int Duration { get; set; }
    public int BasePrice { get; set; }
    public int AvailableSeats { get; set; }
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
