namespace WNCAirline.Models;

public class Transaction
{
    public int Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int Fee { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
