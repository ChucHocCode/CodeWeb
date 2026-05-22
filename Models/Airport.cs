using System.ComponentModel.DataAnnotations;

namespace WNCAirline.Models
{
    public class Airport
    {
        [Key]
        public string AirportCode { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public string AirportName { get; set; } = string.Empty;
    }
}
