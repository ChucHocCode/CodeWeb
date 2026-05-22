using System.ComponentModel.DataAnnotations;

namespace WNCAirline.Models
{
    public class FlightSearchModel
    {
        public string TripType { get; set; } = "OneWay";

        [Required(ErrorMessage = "Vui lòng chọn điểm đi")]
        public string? DepartureCity { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn điểm đến")]
        public string? DestinationCity { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày khởi hành")]
        public DateTime? DepartureDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        public int AdultCount { get; set; } = 1;
        public int ChildCount { get; set; } = 0;
        public int InfantCount { get; set; } = 0;

        public string SeatClass { get; set; } = "Economy";
    }
}
