using Microsoft.AspNetCore.Mvc;
using WNCAirline.Models;

namespace WNCAirline.Controllers;

public class ExchangeFlightOptionViewModel
{
    public string Key { get; set; } = string.Empty;
    public string FlightNumber { get; set; } = string.Empty;
    public string Airline { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string DepartureText { get; set; } = string.Empty;
    public string ArrivalText { get; set; } = string.Empty;
    public string PriceText { get; set; } = string.Empty;
    public int Price { get; set; }
    public DateTime DepartureTime { get; set; }
}

public class RefundExchangeViewModel
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public BookingRecord? Booking { get; set; }
    public bool CanProcess { get; set; }
    public string BaseFare { get; set; } = "0 đ";
    public string TaxAmount { get; set; } = "0 đ";
    public string ServiceFee { get; set; } = "0 đ";
    public string RefundTotal { get; set; } = "0 đ";
    public string CurrentTotal { get; set; } = "0 đ";
    public string ExchangeFee { get; set; } = "0 đ";
    public List<ExchangeFlightOptionViewModel> ExchangeOptions { get; set; } = [];
    public bool ShowSuccessPopup { get; set; }
    public string SuccessPopupTitle { get; set; } = string.Empty;
    public string SuccessPopupMessage { get; set; } = string.Empty;
}

public class RefundExchangeController : Controller
{
    private readonly AppDbContext _context;

    public RefundExchangeController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index(string? code)
    {
        var model = BuildViewModel(code);

        if (TempData["RefundMessage"] is string message)
        {
            model.Message = message;
        }

        if (TempData["SuccessPopupTitle"] is string title && TempData["SuccessPopupMessage"] is string popupMessage)
        {
            model.ShowSuccessPopup = true;
            model.SuccessPopupTitle = title;
            model.SuccessPopupMessage = popupMessage;
        }

        return View(model);
    }

    [HttpPost]
    public IActionResult SubmitRequest(string code, string mode, string? reason, string? note, string? selectedFlightKey)
    {
        var booking = FindBooking(code);
        if (booking is null)
        {
            TempData["RefundMessage"] = "Không tìm thấy vé để xử lý hoàn/đổi.";
            return RedirectToAction(nameof(Index), new { code });
        }

        var isEligible = string.Equals(booking.Status, "PAID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "EXCHANGED", StringComparison.OrdinalIgnoreCase);
        if (!isEligible)
        {
            TempData["RefundMessage"] = "Chỉ vé đã thanh toán hoặc đã đổi mới được hoàn/đổi.";
            return RedirectToAction(nameof(Index), new { code = booking.PNR });
        }

        var isExchange = string.Equals(mode, "exchange", StringComparison.OrdinalIgnoreCase);

        if (isExchange)
        {
            if (string.IsNullOrWhiteSpace(selectedFlightKey))
            {
                TempData["RefundMessage"] = "Vui lòng chọn chuyến bay mới để đổi vé.";
                return RedirectToAction(nameof(Index), new { code = booking.PNR });
            }

            var currentFlightTime = ParseFlightDate(booking.FlightDate);
            if (currentFlightTime is null)
            {
                TempData["RefundMessage"] = "Không đọc được thời gian chuyến bay hiện tại.";
                return RedirectToAction(nameof(Index), new { code = booking.PNR });
            }

            var exchangeOptions = BuildExchangeOptions(booking, currentFlightTime.Value, DateTime.Now);
            var selectedOption = exchangeOptions.FirstOrDefault(x => string.Equals(x.Key, selectedFlightKey.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selectedOption is null)
            {
                TempData["RefundMessage"] = "Chuyến bay mới không hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Index), new { code = booking.PNR });
            }

            if (selectedOption.DepartureTime <= currentFlightTime.Value)
            {
                TempData["RefundMessage"] = "Giờ chuyến bay mới phải lớn hơn giờ chuyến hiện tại.";
                return RedirectToAction(nameof(Index), new { code = booking.PNR });
            }

            if (selectedOption.DepartureTime <= DateTime.Now)
            {
                TempData["RefundMessage"] = "Chuyến bay đổi phải ở thời điểm tương lai.";
                return RedirectToAction(nameof(Index), new { code = booking.PNR });
            }

            var oldTotal = ParseMoney(booking.ExchangeTotal);
            if (oldTotal <= 0)
            {
                oldTotal = ParseMoney(booking.Total);
            }
            const int exchangeFee = 200000;
            var netDifference = selectedOption.Price + exchangeFee - oldTotal;

            booking.Route = selectedOption.Route;
            booking.FlightDate = selectedOption.DepartureTime.ToString("dd/MM/yyyy HH:mm");
            booking.ExchangeTotal = FormatMoney(selectedOption.Price);
            booking.PaymentDue = FormatSignedMoney(netDifference);
            booking.Status = "PENDING_EXCHANGE";
            BookingStore.Save();

            return RedirectToAction("FromTicket", "Payment", new { code = booking.PNR });
        }

        var baseFare = ParseMoney(booking.Fare);
        var taxAmount = ParseMoney(booking.Tax);
        var totalAmount = ParseMoney(booking.Total);
        var airportFee = Math.Max(totalAmount - baseFare - taxAmount, 0);
        var combinedTaxAndAirportFee = taxAmount + airportFee;
        const int serviceFee = 300000;
        var refundableAmount = Math.Max(baseFare - combinedTaxAndAirportFee, 0);
        var refundTotal = Math.Max(refundableAmount - serviceFee, 0);

        booking.Status = "PENDING_REFUND";
        booking.PaymentDue = FormatSignedMoney(refundTotal);
        BookingStore.Save();

        return RedirectToAction("FromTicket", "Payment", new { code = booking.PNR });
    }

    private RefundExchangeViewModel BuildViewModel(string? code)
    {
        var booking = FindBooking(code);
        if (booking is null)
        {
            return new RefundExchangeViewModel
            {
                Code = code?.Trim() ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(code)
                    ? string.Empty
                    : "Nhập đúng mã đặt chỗ (PNR) để tải thông tin vé từ trạng thái thanh toán."
            };
        }

        var baseFare = ParseMoney(booking.Fare);
        var taxAmount = ParseMoney(booking.Tax);
        var totalAmount = ParseMoney(booking.ExchangeTotal);
        if (totalAmount <= 0)
        {
            totalAmount = ParseMoney(booking.Total);
        }
        if (totalAmount <= 0)
        {
            totalAmount = baseFare + taxAmount;
        }
        var airportFee = Math.Max(totalAmount - baseFare - taxAmount, 0);
        var combinedTaxAndAirportFee = taxAmount + airportFee;

        var serviceFee = 300000;
        var refundableAmount = Math.Max(baseFare - combinedTaxAndAirportFee, 0);
        var refundTotal = Math.Max(refundableAmount - serviceFee, 0);
        var isPaid = string.Equals(booking.Status, "PAID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(booking.Status, "EXCHANGED", StringComparison.OrdinalIgnoreCase);
        var isRefunded = string.Equals(booking.Status, "REFUNDED", StringComparison.OrdinalIgnoreCase);
        var isExchanged = string.Equals(booking.Status, "EXCHANGED", StringComparison.OrdinalIgnoreCase);
        var isPendingRefund = IsStatus(booking.Status, "PENDING_REFUND", "REFUND_PENDING", "WAITING_REFUND");
        var isPendingExchange = IsStatus(booking.Status, "PENDING_EXCHANGE", "EXCHANGE_PENDING", "WAITING_EXCHANGE");
        var currentFlightTime = ParseFlightDate(booking.FlightDate) ?? DateTime.Now;
        const int exchangeFee = 200000;
        var exchangeOptions = BuildExchangeOptions(booking, currentFlightTime, DateTime.Now);

        var statusMessage = isPaid
            ? "Vé đã thanh toán, bạn có thể gửi yêu cầu hoàn hoặc đổi vé."
            : isPendingRefund
                ? "Yêu cầu hoàn vé đang chờ thanh toán tiền hoàn cho người dùng."
                : isPendingExchange
                    ? "Yêu cầu đổi vé đang chờ xử lý thanh toán chênh lệch."
            : isRefunded
                ? "Vé này đã hoàn thành công. Không thể thực hiện hoàn/đổi thêm."
                : isExchanged
                    ? "Vé này đã đổi thành công. Không thể thực hiện hoàn/đổi thêm."
                    : "Vé chưa thanh toán thành công. Chỉ vé PAID mới được hoàn/đổi.";

        return new RefundExchangeViewModel
        {
            Code = booking.PNR,
            Booking = booking,
            CanProcess = isPaid,
            Message = statusMessage,
            BaseFare = FormatMoney(baseFare),
            TaxAmount = FormatMoney(combinedTaxAndAirportFee),
            ServiceFee = FormatMoney(serviceFee),
            RefundTotal = FormatMoney(refundTotal),
            CurrentTotal = FormatMoney(totalAmount),
            ExchangeFee = FormatMoney(exchangeFee),
            ExchangeOptions = exchangeOptions
        };
    }

    private List<ExchangeFlightOptionViewModel> BuildExchangeOptions(BookingRecord booking, DateTime currentFlightTime, DateTime minDepartureTime)
    {
        var route = ParseRoute(booking.Route);
        if (route.Departure == "---" || route.Arrival == "---")
            return [];

        var minTime = currentFlightTime > minDepartureTime ? currentFlightTime : minDepartureTime;
        var maxTime = minTime.Date.AddDays(35);

        var flights = _context.Flights
            .Where(f => f.DepartureCity == route.Departure && f.ArrivalCity == route.Arrival
                     && f.DepartureTime > minTime && f.DepartureTime <= maxTime)
            .OrderBy(f => f.DepartureTime)
            .ToList();

        return flights
            .GroupBy(f => f.DepartureTime.Date)
            .SelectMany(g => g.Take(7))
            .Select(f => new ExchangeFlightOptionViewModel
            {
                Key = f.Id,
                FlightNumber = f.FlightNumber,
                Airline = f.Airline,
                Route = booking.Route,
                DepartureText = f.DepartureTime.ToString("HH:mm"),
                ArrivalText = f.ArrivalTime.ToString("HH:mm"),
                PriceText = FormatMoney(f.BasePrice),
                Price = f.BasePrice,
                DepartureTime = f.DepartureTime
            })
            .ToList();
    }

    private static (string Departure, string Arrival) ParseRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return ("---", "---");
        }

        var routeParts = route.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (routeParts.Length < 2)
        {
            return (routeParts[0], "---");
        }

        return (routeParts[0], routeParts[1]);
    }

    private static DateTime? ParseFlightDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (DateTime.TryParseExact(input.Trim(), "dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(input.Trim(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static BookingRecord? FindBooking(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return BookingStore.GetAll().FirstOrDefault(x =>
            string.Equals(x.PNR, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static int ParseMoney(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : 0;
    }

    private static string FormatMoney(int value)
    {
        return $"{value:N0} đ".Replace(',', '.');
    }

    private static string FormatSignedMoney(int value)
    {
        return value < 0
            ? $"-{FormatMoney(Math.Abs(value))}"
            : FormatMoney(value);
    }

    private static bool IsStatus(string? value, params string[] acceptedStatuses)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var status in acceptedStatuses)
        {
            if (string.Equals(value, status, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
