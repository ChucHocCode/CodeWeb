using Microsoft.AspNetCore.Mvc;
using WNCAirline.Models;

namespace WNCAirline.Controllers
{
    public class HoldBookingRequest
    {
        public string FlightId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Seat { get; set; } = string.Empty;
    }

    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Login", "Account");
            }

            var today = DateTime.Today;

            // Distinct routes với giá rẻ nhất (cho "Điểm đến nổi bật")
            var minPrices = _context.Flights
                .Where(f => f.DepartureTime >= today)
                .GroupBy(f => new { f.DepartureCity, f.ArrivalCity })
                .Select(g => new { g.Key.DepartureCity, g.Key.ArrivalCity, MinPrice = g.Min(f => f.BasePrice) })
                .ToList();

            ViewBag.Destinations = minPrices.Select(r => new Flight
            {
                DepartureCity = r.DepartureCity,
                ArrivalCity = r.ArrivalCity,
                BasePrice = r.MinPrice
            }).ToList();

            // Chuyến bay khởi hành hôm nay (cho hero banner)
            ViewBag.TodayFlights = _context.Flights
                .Where(f => f.DepartureTime >= today && f.DepartureTime < today.AddDays(1))
                .OrderBy(f => f.DepartureTime)
                .Take(3)
                .ToList();

            ViewBag.Promotions = new List<Promotion>
            {
                new() { PromoId = 1, Title = "Khuyến mãi Hè 2026", Description = "Giảm 25% cho các chuyến bay nội địa trong mùa hè.", PromoCode = "SKYSPRING25", DiscountPercent = 25, MaxDiscountAmount = 700000 },
                new() { PromoId = 2, Title = "Ưu đãi Gia đình", Description = "Mua 3 vé tặng 1 vé cho chuyến bay gia đình.", PromoCode = "FAMILY3PLUS1", DiscountPercent = 0, MaxDiscountAmount = 0 },
                new() { PromoId = 3, Title = "Bay sớm - Giá tốt", Description = "Đặt trước 30 ngày, tiết kiệm đến 20% giá vé.", PromoCode = "EARLYSAVE20", DiscountPercent = 20, MaxDiscountAmount = 500000 }
            };

            return View(new FlightSearchModel());
        }

        public IActionResult SearchFlights(FlightSearchModel search)
        {
            var query = _context.Flights.AsQueryable();

            if (!string.IsNullOrEmpty(search.DepartureCity))
            {
                query = query.Where(f => f.DepartureCity == search.DepartureCity);
            }

            if (!string.IsNullOrEmpty(search.DestinationCity))
            {
                query = query.Where(f => f.ArrivalCity == search.DestinationCity);
            }

            if (search.DepartureDate.HasValue)
            {
                var searchDate = search.DepartureDate.Value.Date;
                query = query.Where(f => f.DepartureTime.Date == searchDate);
            }

            var results = query.ToList();

            ViewBag.Route = (!string.IsNullOrEmpty(search.DepartureCity) && !string.IsNullOrEmpty(search.DestinationCity))
                            ? $"{search.DepartureCity} ➔ {search.DestinationCity}"
                            : "Tất cả chuyến bay hiện có";

            ViewBag.Date = search.DepartureDate?.ToString("dd/MM/yyyy") ?? "Tất cả thời gian";
            ViewBag.AdultCount = search.AdultCount;
            ViewBag.ChildCount = search.ChildCount;
            ViewBag.InfantCount = search.InfantCount;
            ViewBag.PaxCount = search.AdultCount + search.ChildCount + search.InfantCount;

            return PartialView("_FlightResults", results);
        }

        public IActionResult Booking(string? flightId, int pax = 1, int adults = 1, int children = 0, int infants = 0)
        {
            var flight = _context.Flights.FirstOrDefault(f => f.Id == flightId);

            if (flight == null)
            {
                return RedirectToAction("Index");
            }

            var adultCount  = Math.Max(1, Math.Min(9, adults));
            var childCount  = Math.Max(0, Math.Min(8, children));
            var infantCount = Math.Max(0, Math.Min(8, infants));
            var totalPax    = adultCount + childCount + infantCount;
            ViewBag.PassengerCount = totalPax;
            ViewBag.AdultCount  = adultCount;
            ViewBag.ChildCount  = childCount;
            ViewBag.InfantCount = infantCount;
            ViewBag.OccupiedSeats = SeatStore.GetOccupied(flightId ?? string.Empty);
            return View(flight);
        }

        [HttpPost]
        public IActionResult HoldBooking([FromBody] HoldBookingRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.FlightId) || string.IsNullOrWhiteSpace(req.Email))
                return Json(new { success = false, message = "Thiếu thông tin đặt chỗ." });

            var flight = _context.Flights.FirstOrDefault(f => f.Id == req.FlightId);
            if (flight is null)
                return Json(new { success = false, message = "Không tìm thấy chuyến bay." });

            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var pnr = "WN" + new string(Enumerable.Range(0, 4)
                .Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());

            var tax = (int)(flight.BasePrice * 0.12m);
            var total = (int)flight.BasePrice + tax;

            var booking = BookingStore.Add(new Models.BookingRecord
            {
                PNR = pnr,
                Contact = req.Email.Trim(),
                Route = $"{flight.DepartureCity} -> {flight.ArrivalCity}",
                FlightDate = flight.DepartureTime.ToString("dd/MM/yyyy HH:mm"),
                Status = "HELD",
                Fare = $"{flight.BasePrice:N0}đ",
                Tax = $"{tax:N0}đ",
                Total = $"{total:N0}đ"
            });

            SeatStore.ClaimSeat(req.FlightId, req.Seat);

            return Json(new { success = true, pnr = booking.PNR, email = booking.Contact });
        }

        [HttpGet]
        public JsonResult GetAirports(string term)
        {
            var airports = new[] {
                new { label = "Hà Nội (HAN)", value = "HAN" },
                new { label = "TP. Hồ Chí Minh (SGN)", value = "SGN" },
                new { label = "Đà Nẵng (DAD)", value = "DAD" },
                new { label = "Nha Trang (CXR)", value = "CXR" }
            };

            var result = airports.Where(a => a.label.ToLower().Contains(term.ToLower())).ToList();
            return Json(result);
        }
    }
}
