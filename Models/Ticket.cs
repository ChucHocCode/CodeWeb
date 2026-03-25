namespace WNCAirline.Models;

public class Ticket
{
    public string Code { get; set; } = string.Empty;
    public string Passenger { get; set; } = string.Empty;
    public int Price { get; set; }
    public DateTime FlightDate { get; set; }
    public string FlightTime { get; set; } = string.Empty;
    public string DepartureCity { get; set; } = string.Empty;
    public string ArrivalCity { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
