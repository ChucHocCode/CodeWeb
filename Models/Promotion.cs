using System.ComponentModel.DataAnnotations;

namespace WNCAirline.Models
{
    public class Promotion
    {
        [Key]
        public int PromoId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string PromoCode { get; set; } = string.Empty;
        public int DiscountPercent { get; set; }
        public decimal MaxDiscountAmount { get; set; }
        public string SubText { get; set; } = string.Empty;
    }
}
