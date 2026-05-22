using System.ComponentModel.DataAnnotations;

namespace WNCAirline.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public int FlightId { get; set; }
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerEmail { get; set; } = string.Empty;
        public string TripType { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
