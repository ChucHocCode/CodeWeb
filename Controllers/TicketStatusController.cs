using Microsoft.AspNetCore.Mvc;
using WNCAirline.Models;

namespace WNCAirline.Controllers;

public class TicketStatusItem
{
    public string BookingCode { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string LastEvent { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class TicketStatusPageViewModel
{
    public string Keyword { get; set; } = string.Empty;
    public string ActiveStatus { get; set; } = "all";
    public string Message { get; set; } = string.Empty;
    public List<TicketStatusItem> Tickets { get; set; } = [];
}

public class TicketStatusDetailViewModel
{
    public string BookingCode { get; set; } = string.Empty;
    public string PassengerName { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string LastEvent { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string FlightDate { get; set; } = string.Empty;
    public string Fare { get; set; } = string.Empty;
    public string Tax { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public bool CanGoPayment { get; set; }
    public bool CanRefundExchange { get; set; }
}

public class TicketStatusController : Controller
{
    [HttpGet]
    public IActionResult Index(string? keyword, string? status)
    {
        var normalizedKeyword = keyword?.Trim() ?? string.Empty;
        var activeStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLowerInvariant();

        var ticketData = BookingStore.GetAll().Select(MapFromBooking).ToList();
        var query = ticketData.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            query = query.Where(x =>
                x.BookingCode.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                || x.PassengerName.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)
                || x.Route.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(activeStatus, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.StatusKey.Equals(activeStatus, StringComparison.OrdinalIgnoreCase));
        }

        var model = new TicketStatusPageViewModel
        {
            Keyword = normalizedKeyword,
            ActiveStatus = activeStatus,
            Message = TempData["TicketStatusMessage"] as string ?? string.Empty,
            Tickets = query.ToList()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Detail(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return RedirectToAction(nameof(Index));
        }

        var booking = BookingStore.GetAll().FirstOrDefault(x =>
            string.Equals(x.PNR, code.Trim(), StringComparison.OrdinalIgnoreCase));

        if (booking is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var item = MapFromBooking(booking);
        var model = new TicketStatusDetailViewModel
        {
            BookingCode = item.BookingCode,
            PassengerName = item.PassengerName,
            Route = item.Route,
            StatusKey = item.StatusKey,
            StatusLabel = item.StatusLabel,
            LastEvent = item.LastEvent,
            Reason = item.Reason,
            FlightDate = booking.FlightDate,
            Fare = booking.Fare,
            Tax = booking.Tax,
            Total = ResolveDisplayTotal(booking),
            CanGoPayment = item.StatusKey is "held" or "pending-refund" or "pending-exchange",
            CanRefundExchange = item.StatusKey is "paid" or "exchanged"
        };

        return View(model);
    }

    private static TicketStatusItem MapFromBooking(BookingRecord booking)
    {
        if (IsStatus(booking.Status, "PENDING_REFUND", "REFUND_PENDING", "WAITING_REFUND"))
        {
            return new TicketStatusItem
            {
                BookingCode = booking.PNR,
                PassengerName = booking.Contact,
                Route = booking.Route,
                StatusKey = "pending-refund",
                StatusLabel = "Chờ hoàn vé",
                LastEvent = "Thanh toán tiền hoàn cho người dùng",
                Reason = "Yêu cầu hoàn vé đã được ghi nhận"
            };
        }

        if (IsStatus(booking.Status, "PENDING_EXCHANGE", "EXCHANGE_PENDING", "WAITING_EXCHANGE"))
        {
            var signedAmount = GetSignedAmount(booking.PaymentDue);
            var isCustomerPaysMore = signedAmount > 0;

            return new TicketStatusItem
            {
                BookingCode = booking.PNR,
                PassengerName = booking.Contact,
                Route = booking.Route,
                StatusKey = "pending-exchange",
                StatusLabel = "Chờ đổi vé",
                LastEvent = isCustomerPaysMore
                    ? "Người dùng thanh toán khi đổi vé"
                    : "Thanh toán tiền đổi cho người dùng",
                Reason = isCustomerPaysMore
                    ? "Đang chờ thanh toán phần chênh lệch đổi vé"
                    : "Đang chờ hoàn phần chênh lệch đổi vé"
            };
        }

        if (string.Equals(booking.Status, "REFUNDED", StringComparison.OrdinalIgnoreCase))
        {
            return new TicketStatusItem
            {
                BookingCode = booking.PNR,
                PassengerName = booking.Contact,
                Route = booking.Route,
                StatusKey = "refunded",
                StatusLabel = "Đã hoàn vé",
                LastEvent = "Xử lý hoàn vé thành công",
                Reason = "Yêu cầu hoàn vé đã được xác nhận"
            };
        }

        if (string.Equals(booking.Status, "EXCHANGED", StringComparison.OrdinalIgnoreCase))
        {
            return new TicketStatusItem
            {
                BookingCode = booking.PNR,
                PassengerName = booking.Contact,
                Route = booking.Route,
                StatusKey = "exchanged",
                StatusLabel = "Đã đổi vé",
                LastEvent = "Xử lý đổi vé thành công",
                Reason = "Yêu cầu đổi vé đã được xác nhận"
            };
        }

        if (string.Equals(booking.Status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            return new TicketStatusItem
            {
                BookingCode = booking.PNR,
                PassengerName = booking.Contact,
                Route = booking.Route,
                StatusKey = "paid",
                StatusLabel = "Đã thanh toán",
                LastEvent = "Cập nhật từ thanh toán",
                Reason = "Đã quét QR thành công"
            };
        }

        return new TicketStatusItem
        {
            BookingCode = booking.PNR,
            PassengerName = booking.Contact,
            Route = booking.Route,
            StatusKey = "held",
            StatusLabel = "Đang giữ chỗ",
            LastEvent = "Đặt chỗ thành công",
            Reason = "Chờ thanh toán QR"
        };
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

    private static int GetSignedAmount(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        var cleaned = input.Trim();
        var sign = cleaned.StartsWith("-", StringComparison.Ordinal) ? -1 : 1;
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out var value))
        {
            return 0;
        }

        return sign * value;
    }

    private static string ResolveDisplayTotal(BookingRecord booking)
    {
        if (IsStatus(booking.Status, "PENDING_REFUND", "REFUND_PENDING", "WAITING_REFUND")
            || IsStatus(booking.Status, "PENDING_EXCHANGE", "EXCHANGE_PENDING", "WAITING_EXCHANGE"))
        {
            if (!string.IsNullOrWhiteSpace(booking.PaymentDue))
            {
                return booking.PaymentDue;
            }
        }

        if (!string.IsNullOrWhiteSpace(booking.ExchangeTotal))
        {
            return booking.ExchangeTotal;
        }

        return booking.Total;
    }
}
