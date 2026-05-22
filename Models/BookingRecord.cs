namespace WNCAirline.Models;

public class BookingRecord
{
    public string PNR { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string FlightDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Fare { get; set; } = string.Empty;
    public string Tax { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string ExchangeTotal { get; set; } = string.Empty;
    public string PaymentDue { get; set; } = string.Empty;
}
