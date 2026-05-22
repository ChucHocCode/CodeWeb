using Microsoft.AspNetCore.Mvc;
using QRCoder;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WNCAirline.Models;

namespace WNCAirline.Controllers;

public class PaymentViewModel
{
    public string? PNR { get; set; }
    public string? Email { get; set; }
    public bool Found { get; set; }
    public string? Route { get; set; }
    public string? FlightDate { get; set; }
    public string? Status { get; set; }
    public string? Fare { get; set; }
    public string? Tax { get; set; }
    public string? Total { get; set; }
    public string? ExchangeTotal { get; set; }
    public string? PaymentDue { get; set; }
    public string? CurrentTotal { get; set; }
    public string? ExchangeFee { get; set; }
    public string? RefundBaseFare { get; set; }
    public string? RefundTaxAndAirportFee { get; set; }
    public string? RefundServiceFee { get; set; }
    public string? RefundTotal { get; set; }
    public bool IsExchangePayment { get; set; }
    public bool IsPayoutToUser { get; set; }
    public string? Message { get; set; }
    public List<BookingRecord> SearchResults { get; set; } = [];
    public List<BookingRecord> TestData { get; set; } = [];
}

public class PaymentQrViewModel
{
    public string PNR { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string FlightDate { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string AmountLabel { get; set; } = string.Empty;
    public bool IsExchangePayment { get; set; }
    public bool IsPayoutToUser { get; set; }
    public string QrContent { get; set; } = string.Empty;
    public string QrImageDataUrl { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string MobileScanUrl { get; set; } = string.Empty;
    public string StatusPollUrl { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
}

public class MobileScanViewModel
{
    public string SessionId { get; set; } = string.Empty;
    public string PNR { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string FlightDate { get; set; } = string.Empty;
    public string AmountLabel { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string ExpiresAt { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public bool IsPaid { get; set; }
    public bool IsPayoutToUser { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientBankAccount { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class PaymentSuccessViewModel
{
    public string PNR { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string FlightDate { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public bool IsExchangePayment { get; set; }
    public bool IsPayoutToUser { get; set; }
    public string PaidAt { get; set; } = string.Empty;
}

public class PaymentFailedViewModel
{
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class PaymentGatewayResponse
{
    public string? Status { get; set; }
    public string? ResponseCode { get; set; }
    public string? TransactionStatus { get; set; }
    public string? OrderInfo { get; set; }
}

public class PaymentController : Controller
{
    private static readonly ConcurrentDictionary<string, QrPaymentSession> QrSessions = new();
    private static readonly ConcurrentDictionary<string, string> BookingSessions = new();
    private static string? LatestSessionId;
    private readonly MobileEndpointSettings _mobileEndpointSettings;

    public PaymentController(MobileEndpointSettings mobileEndpointSettings)
    {
        _mobileEndpointSettings = mobileEndpointSettings;
    }

    [HttpGet]
    public IActionResult FromTicket(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            TempData["PaymentMessage"] = "Thiếu mã vé để mở trang thanh toán.";
            return RedirectToAction(nameof(Index));
        }

        var booking = BookingStore.GetAll().FirstOrDefault(x =>
            string.Equals(x.PNR, code.Trim(), StringComparison.OrdinalIgnoreCase));

        if (booking is null)
        {
            TempData["PaymentMessage"] = "Không tìm thấy vé tương ứng.";
            return RedirectToAction(nameof(Index));
        }

        if (IsPaymentLockedStatus(booking.Status))
        {
            TempData["TicketStatusMessage"] = BuildPaymentLockedMessage(booking.Status);
            return RedirectToAction("Index", "TicketStatus");
        }

        var model = CreateBaseModel(booking.PNR, booking.Contact);
        model.SearchResults = [CloneRecord(booking)];
        ApplyBookingToModel(model, booking);
        model.Message = BuildPaymentFlowMessage(booking);

        return View("Index", model);
    }

    [HttpGet]
    public IActionResult Index()
    {
        var model = CreateBaseModel(null, null);
        if (TempData["PaymentMessage"] is string message)
        {
            model.Message = message;
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Success(string? pnr, string? email)
    {
        var booking = FindExactBooking(pnr, email);
        if (booking is not null)
        {
            return View("PaymentSuccess", CreateSuccessModel(booking));
        }

        return View("PaymentSuccess", new PaymentSuccessViewModel
        {
            PNR = pnr ?? string.Empty,
            Email = email ?? string.Empty,
            PaidAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        });
    }

    [HttpGet]
    public IActionResult Failed(string? message, string? code)
    {
        var model = new PaymentFailedViewModel
        {
            Message = string.IsNullOrWhiteSpace(message)
                ? "Thanh toán thất bại. Vui lòng thử lại."
                : message,
            Code = code ?? string.Empty
        };

        return View("PaymentFailed", model);
    }

    [HttpPost]
    public IActionResult Index(string? PNR, string? Email, string? action, string? Agree)
    {
        var model = CreateBaseModel(PNR, Email);
        var isAgreed = IsAgreementAccepted(Agree);

        if (action == "find")
        {
            var matches = FindMatches(model.PNR, model.Email).ToList();
            model.SearchResults = matches.Select(CloneRecord).ToList();

            if (matches.Count == 0)
            {
                model.Found = false;
                model.Message = "Không tìm thấy đặt chỗ phù hợp. Hãy kiểm tra lại PNR hoặc email.";
            }
            else
            {
                ApplyBookingToModel(model, matches[0]);
                if (matches.Count > 1)
                {
                    model.Message = $"Tìm thấy {matches.Count} kết quả. Đang hiển thị kết quả đầu tiên.";
                }
            }
        }
        else if (action == "pay")
        {
            var matches = FindMatches(model.PNR, model.Email).ToList();
            model.SearchResults = matches.Select(CloneRecord).ToList();

            if (matches.Count == 0)
            {
                model.Message = "Không tìm thấy đặt chỗ để thanh toán.";
            }
            else if (matches.Count > 1)
            {
                model.Message = "Có nhiều kết quả khớp. Vui lòng nhập chính xác hơn PNR hoặc email.";
            }
            else
            {
                var selectedBooking = matches[0];
                ApplyBookingToModel(model, selectedBooking);

                if (!isAgreed)
                {
                    model.Message = "Bạn phải đồng ý điều khoản.";
                }
                else if (IsPaymentLockedStatus(selectedBooking.Status))
                {
                    model.Message = BuildPaymentLockedMessage(selectedBooking.Status);
                }
                else if (!CanProceedToQr(selectedBooking))
                {
                    model.Message = BuildPaymentFlowMessage(selectedBooking);
                }
                else
                {
                    return RedirectToAction(nameof(Qr), new
                    {
                        pnr = selectedBooking.PNR,
                        email = selectedBooking.Contact
                    });
                }
            }
        }
        else if (action == "cancel")
        {
            model.Message = "Đã hủy giao dịch.";
            model.Found = false;
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Qr(string? pnr, string? email)
    {
        var booking = FindExactBooking(pnr, email);
        if (booking is null)
        {
            TempData["PaymentMessage"] = "Không tìm thấy đơn đặt chỗ để tạo mã QR.";
            return RedirectToAction(nameof(Index));
        }

        if (IsPaymentLockedStatus(booking.Status))
        {
            TempData["PaymentMessage"] = BuildPaymentLockedMessage(booking.Status);
            return RedirectToAction(nameof(Index));
        }

        if (!CanProceedToQr(booking))
        {
            TempData["PaymentMessage"] = BuildPaymentFlowMessage(booking);
            return RedirectToAction(nameof(Index));
        }

        var sessionId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.Now.AddMinutes(10);
        var statusPollUrl = BuildStatusPollUrl(sessionId);
        var mobileScanUrl = BuildMobileScanUrl(sessionId);
        var isPayoutToUser = IsPayoutToUser(booking);

        QrSessions[sessionId] = new QrPaymentSession
        {
            SessionId = sessionId,
            PNR = booking.PNR,
            Email = booking.Contact,
            ExpiresAt = expiresAt,
            IsPayoutToUser = isPayoutToUser
        };
        BookingSessions[BuildBookingKey(booking.PNR, booking.Contact)] = sessionId;
        LatestSessionId = sessionId;

        var qrModel = new PaymentQrViewModel
        {
            PNR = booking.PNR,
            Email = booking.Contact,
            Route = booking.Route,
            FlightDate = booking.FlightDate,
            Total = ResolveQrDisplayAmount(booking),
            AmountLabel = ResolveQrAmountLabel(booking),
            IsExchangePayment = IsExchangeBooking(booking),
            IsPayoutToUser = isPayoutToUser,
            SessionId = sessionId,
            MobileScanUrl = mobileScanUrl,
            StatusPollUrl = statusPollUrl,
            QrContent = mobileScanUrl,
            ExpiresAt = expiresAt.ToString("dd/MM/yyyy HH:mm")
        };

        qrModel.QrImageDataUrl = BuildScannableQrDataUrl(qrModel.QrContent);

        return View(qrModel);
    }

    [HttpGet("/m")]
    public IActionResult MobileEntry(string? sid)
    {
        if (!string.IsNullOrWhiteSpace(sid))
        {
            return Redirect($"/pay/{sid}");
        }

        if (TryGetLatestActiveSessionId(out var latestSid))
        {
            return Redirect($"/pay/{latestSid}");
        }

        return View("MobileScan", new MobileScanViewModel
        {
            IsExpired = true,
            Message = "Không tìm thấy phiên quét QR đang hoạt động. Vui lòng quét lại mã mới."
        });
    }

    [HttpGet("/pay/{sessionId}")]
    public IActionResult MobileScan(string sessionId)
    {
        if (!QrSessions.TryGetValue(sessionId, out var session))
        {
            return View(new MobileScanViewModel
            {
                SessionId = sessionId,
                IsExpired = true,
                Message = "Mã QR không hợp lệ hoặc đã hết hạn."
            });
        }

        var booking = FindExactBooking(session.PNR, session.Email);
        var now = DateTime.Now;
        var isExpired = session.ExpiresAt <= now;

        if (booking is null)
        {
            return View(new MobileScanViewModel
            {
                SessionId = sessionId,
                IsExpired = true,
                Message = "Không tìm thấy đơn đặt chỗ để thanh toán."
            });
        }

        return View(new MobileScanViewModel
        {
            SessionId = sessionId,
            PNR = booking.PNR,
            Email = booking.Contact,
            Route = booking.Route,
            FlightDate = booking.FlightDate,
            AmountLabel = ResolveQrAmountLabel(booking),
            Total = ResolveQrDisplayAmount(booking),
            ExpiresAt = session.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
            IsExpired = isExpired,
            IsPaid = session.IsPaid,
            IsPayoutToUser = session.IsPayoutToUser,
            RecipientName = session.RecipientName ?? string.Empty,
            RecipientBankAccount = session.RecipientBankAccount ?? string.Empty,
            Message = session.IsPaid
                ? (session.IsPayoutToUser ? "Giao dịch hoàn tiền đã được xác nhận." : "Giao dịch đã được xác nhận.")
                : isExpired
                    ? "Mã QR đã hết hạn."
                    : session.IsPayoutToUser
                        ? "Nhập tên người nhận và số tài khoản, sau đó bấm Đồng ý để xác nhận hoàn tiền."
                        : "Xác nhận thanh toán trên điện thoại để hoàn tất giao dịch."
        });
    }

    [HttpPost]
    public IActionResult MobileScanConfirm(string sessionId, string? recipientName, string? recipientBankAccount)
    {
        if (!QrSessions.TryGetValue(sessionId, out var session))
        {
            return View("MobileScan", new MobileScanViewModel
            {
                SessionId = sessionId,
                IsExpired = true,
                Message = "Mã QR không hợp lệ hoặc đã hết hạn."
            });
        }

        var booking = FindExactBooking(session.PNR, session.Email);
        var now = DateTime.Now;
        var isExpired = session.ExpiresAt <= now;

        if (booking is null)
        {
            return View("MobileScan", new MobileScanViewModel
            {
                SessionId = sessionId,
                IsExpired = true,
                Message = "Không tìm thấy đơn đặt chỗ để thanh toán."
            });
        }

        if (isExpired)
        {
            return View("MobileScan", new MobileScanViewModel
            {
                SessionId = sessionId,
                PNR = booking.PNR,
                Email = booking.Contact,
                Route = booking.Route,
                FlightDate = booking.FlightDate,
                AmountLabel = ResolveQrAmountLabel(booking),
                Total = ResolveQrDisplayAmount(booking),
                ExpiresAt = session.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
                IsExpired = true,
                IsPayoutToUser = session.IsPayoutToUser,
                RecipientName = recipientName?.Trim() ?? string.Empty,
                RecipientBankAccount = recipientBankAccount?.Trim() ?? string.Empty,
                Message = "Mã QR đã hết hạn, vui lòng tạo lại mã mới."
            });
        }

        var normalizedRecipientName = recipientName?.Trim() ?? string.Empty;
        var normalizedRecipientBankAccount = recipientBankAccount?.Trim() ?? string.Empty;

        if (session.IsPayoutToUser
            && (string.IsNullOrWhiteSpace(normalizedRecipientName)
                || string.IsNullOrWhiteSpace(normalizedRecipientBankAccount)))
        {
            return View("MobileScan", new MobileScanViewModel
            {
                SessionId = sessionId,
                PNR = booking.PNR,
                Email = booking.Contact,
                Route = booking.Route,
                FlightDate = booking.FlightDate,
                AmountLabel = ResolveQrAmountLabel(booking),
                Total = ResolveQrDisplayAmount(booking),
                ExpiresAt = session.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
                IsPayoutToUser = true,
                RecipientName = normalizedRecipientName,
                RecipientBankAccount = normalizedRecipientBankAccount,
                Message = "Vui lòng nhập đầy đủ tên người nhận và số tài khoản trước khi xác nhận hoàn tiền."
            });
        }

        session.RecipientName = normalizedRecipientName;
        session.RecipientBankAccount = normalizedRecipientBankAccount;

        booking.Status = ResolveCompletedBookingStatus(booking, session.IsPayoutToUser);
        BookingStore.Save();
        session.IsPaid = true;
        session.PaidAt = now;
        QrSessions[sessionId] = session;

        return View("MobileScan", new MobileScanViewModel
        {
            SessionId = sessionId,
            PNR = booking.PNR,
            Email = booking.Contact,
            Route = booking.Route,
            FlightDate = booking.FlightDate,
            AmountLabel = ResolveQrAmountLabel(booking),
            Total = ResolveQrDisplayAmount(booking),
            ExpiresAt = session.ExpiresAt.ToString("dd/MM/yyyy HH:mm"),
            IsPaid = true,
            IsPayoutToUser = session.IsPayoutToUser,
            Message = session.IsPayoutToUser
                ? "Bạn đã xác nhận thông tin người nhận. Màn hình máy tính sẽ hiển thị giao dịch hoàn tiền thành công."
                : "Bạn đã đồng ý xác nhận giao dịch. Màn hình máy tính sẽ tự chuyển sang trạng thái thành công."
        });
    }

    [HttpGet("/pay/{sessionId}/status")]
    public IActionResult QrPaymentStatus(string sessionId)
    {
        return CheckStatus(sessionId);
    }

    [HttpGet("/Payment/CheckStatus")]
    public IActionResult CheckStatus(string sessionId)
    {
        if (!QrSessions.TryGetValue(sessionId, out var session))
        {
            return Json(new { success = false, status = "invalid" });
        }

        // If callback marked booking as PAID but session flag is not set yet,
        // synchronize session state so desktop polling can switch to success UI.
        if (!session.IsPaid)
        {
            var booking = FindExactBooking(session.PNR, session.Email);
            if (booking is not null && IsSessionCompletedStatus(booking.Status, session.IsPayoutToUser))
            {
                session.IsPaid = true;
                session.PaidAt ??= DateTime.Now;
                QrSessions[sessionId] = session;
            }
        }

        if (session.IsFailed)
        {
            return Json(new
            {
                success = true,
                status = "FAILED",
                message = session.FailedMessage ?? "Thanh toán thất bại. Vui lòng thử lại."
            });
        }

        if (session.ExpiresAt <= DateTime.Now && !session.IsPaid)
        {
            return Json(new { success = true, status = "expired" });
        }

        if (session.IsPaid)
        {
            return Json(new
            {
                success = true,
                status = "SUCCESS",
                paidAt = (session.PaidAt ?? DateTime.Now).ToString("dd/MM/yyyy HH:mm")
            });
        }

        return Json(new { success = true, status = "pending" });
    }

    [HttpPost]
    public IActionResult ConfirmQrPayment(string pnr, string email, bool ajax = false)
    {
        var booking = FindExactBooking(pnr, email);
        if (booking is null)
        {
            if (ajax || IsAjaxRequest())
            {
                return Json(new { success = false, message = "Không thể xác nhận thanh toán vì đơn không tồn tại." });
            }

            TempData["PaymentMessage"] = "Không thể xác nhận thanh toán vì đơn không tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        booking.Status = ResolveCompletedBookingStatus(booking, IsPayoutToUser(booking));
        BookingStore.Save();

        if (ajax || IsAjaxRequest())
        {
            return Json(new
            {
                success = true,
                message = "Giao dịch thành công",
                paidAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            });
        }

        return RedirectToAction(nameof(Success), new
        {
            pnr = booking.PNR,
            email = booking.Contact
        });
    }

    [AcceptVerbs("GET", "POST")]
    public IActionResult ConfirmPayment(
        string? sessionId,
        string? sid,
        string? pnr,
        string? email,
        string? status,
        string? vnp_ResponseCode,
        string? vnp_TransactionStatus,
        string? vnp_OrderInfo)
    {
        var response = new PaymentGatewayResponse
        {
            Status = status,
            ResponseCode = vnp_ResponseCode,
            TransactionStatus = vnp_TransactionStatus,
            OrderInfo = vnp_OrderInfo
        };

        return HandlePaymentConfirmation(sessionId ?? sid, pnr, email, response);
    }

    [AcceptVerbs("GET", "POST")]
    public IActionResult VnpayCallback(
        string? sessionId,
        string? sid,
        string? pnr,
        string? email,
        string? status,
        string? vnp_ResponseCode,
        string? vnp_TransactionStatus,
        string? vnp_OrderInfo)
    {
        var response = new PaymentGatewayResponse
        {
            Status = status,
            ResponseCode = vnp_ResponseCode,
            TransactionStatus = vnp_TransactionStatus,
            OrderInfo = vnp_OrderInfo
        };

        return HandlePaymentConfirmation(sessionId ?? sid, pnr, email, response);
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult HandlePaymentConfirmation(
        string? sessionId,
        string? pnr,
        string? email,
        PaymentGatewayResponse response)
    {
        var isSuccess = IsVnpaySuccess(response.Status, response.ResponseCode, response.TransactionStatus);
        var resolvedSessionId = ResolveSessionId(sessionId, response.OrderInfo);

        BookingRecord? booking = null;
        if (!string.IsNullOrWhiteSpace(resolvedSessionId)
            && QrSessions.TryGetValue(resolvedSessionId, out var session))
        {
            booking = FindExactBooking(session.PNR, session.Email);

            if (isSuccess)
            {
                session.IsPaid = true;
                session.PaidAt = DateTime.Now;
                QrSessions[resolvedSessionId] = session;
            }
        }

        booking ??= FindExactBooking(pnr, email);

        if (string.IsNullOrWhiteSpace(resolvedSessionId) && booking is not null)
        {
            resolvedSessionId = TryGetSessionIdByBooking(booking.PNR, booking.Contact);
        }

        if (isSuccess)
        {
            if (booking is not null)
            {
                var isPayoutToUser = !string.IsNullOrWhiteSpace(resolvedSessionId)
                    && QrSessions.TryGetValue(resolvedSessionId, out var payoutSession)
                    && payoutSession.IsPayoutToUser;

                booking.Status = ResolveCompletedBookingStatus(booking, isPayoutToUser);
                BookingStore.Save();

                if (!string.IsNullOrWhiteSpace(resolvedSessionId)
                    && QrSessions.TryGetValue(resolvedSessionId, out var successSession))
                {
                    successSession.IsPaid = true;
                    successSession.PaidAt ??= DateTime.Now;
                    QrSessions[resolvedSessionId] = successSession;
                }

                return RedirectToAction(nameof(Success), new
                {
                    pnr = booking.PNR,
                    email = booking.Contact
                });
            }

            return RedirectToAction(nameof(Success), new { pnr, email });
        }

        var failureCode = !string.IsNullOrWhiteSpace(response.ResponseCode)
            ? response.ResponseCode
            : response.TransactionStatus;
        var failureMessage = $"Thanh toán thất bại (mã: {failureCode ?? "N/A"}). Vui lòng thử lại.";

        if (!string.IsNullOrWhiteSpace(resolvedSessionId)
            && QrSessions.TryGetValue(resolvedSessionId, out var failedSession))
        {
            failedSession.IsFailed = true;
            failedSession.FailedMessage = failureMessage;
            QrSessions[resolvedSessionId] = failedSession;
        }

        return RedirectToAction(nameof(Failed), new
        {
            message = failureMessage,
            code = failureCode
        });
    }

    private static bool IsVnpaySuccess(string? status, string? responseCode, string? transactionStatus)
    {
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(responseCode, "00", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(transactionStatus)
                || string.Equals(transactionStatus, "00", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveSessionId(string? sessionId, string? orderInfo)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return sessionId;
        }

        if (string.IsNullOrWhiteSpace(orderInfo))
        {
            return null;
        }

        var sidPart = TryExtractToken(orderInfo, "sid=")
            ?? TryExtractToken(orderInfo, "sessionId=");

        return string.IsNullOrWhiteSpace(sidPart) ? null : sidPart;
    }

    private static string? TryExtractToken(string source, string token)
    {
        var idx = source.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var sidPart = source[(idx + token.Length)..].Trim();
        if (sidPart.Contains('&'))
        {
            sidPart = sidPart.Split('&')[0].Trim();
        }

        return sidPart;
    }

    private static string BuildBookingKey(string? pnr, string? email)
    {
        return $"{pnr?.Trim().ToUpperInvariant()}|{email?.Trim().ToUpperInvariant()}";
    }

    private static string? TryGetSessionIdByBooking(string? pnr, string? email)
    {
        if (string.IsNullOrWhiteSpace(pnr) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return BookingSessions.TryGetValue(BuildBookingKey(pnr, email), out var sessionId)
            ? sessionId
            : null;
    }

    private static bool IsAgreementAccepted(string? agree)
    {
        if (string.IsNullOrWhiteSpace(agree))
        {
            return false;
        }

        return string.Equals(agree, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(agree, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(agree, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static PaymentViewModel CreateBaseModel(string? pnr, string? email)
    {
        return new PaymentViewModel
        {
            PNR = pnr?.Trim(),
            Email = email?.Trim(),
            TestData = BookingStore.GetAll().Select(CloneRecord).ToList()
        };
    }

    private static IEnumerable<BookingRecord> FindMatches(string? pnr, string? email)
    {
        var query = BookingStore.GetAll();

        if (!string.IsNullOrWhiteSpace(pnr))
        {
            query = query.Where(x => x.PNR.Contains(pnr, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            query = query.Where(x => x.Contact.Contains(email, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private static BookingRecord? FindExactBooking(string? pnr, string? email)
    {
        if (string.IsNullOrWhiteSpace(pnr) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return BookingStore.GetAll().FirstOrDefault(x =>
            string.Equals(x.PNR, pnr.Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Contact, email.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyBookingToModel(PaymentViewModel model, BookingRecord booking)
    {
        const int exchangeFee = 200000;
        var isExchangePayment = IsExchangeBooking(booking);
        var isPayoutToUser = IsPayoutToUser(booking);
        var currentTotal = ParseMoney(booking.Total);
        var baseFare = ParseMoney(booking.Fare);
        var taxAmount = ParseMoney(booking.Tax);
        var totalAmount = ParseMoney(booking.Total);
        var airportFee = Math.Max(totalAmount - baseFare - taxAmount, 0);
        var combinedTaxAndAirportFee = taxAmount + airportFee;
        const int serviceFee = 300000;
        var refundableAmount = Math.Max(baseFare - combinedTaxAndAirportFee, 0);
        var refundTotal = Math.Max(refundableAmount - serviceFee, 0);

        model.Found = true;
        model.PNR = booking.PNR;
        model.Email = booking.Contact;
        model.Route = booking.Route;
        model.FlightDate = booking.FlightDate;
        model.Status = booking.Status;
        model.Fare = booking.Fare;
        model.Tax = booking.Tax;
        model.Total = ResolvePaymentAmount(booking);
        model.ExchangeTotal = booking.ExchangeTotal;
        model.PaymentDue = booking.PaymentDue;
        model.IsExchangePayment = isExchangePayment;
        model.IsPayoutToUser = isPayoutToUser;
        model.CurrentTotal = isExchangePayment ? FormatMoney(currentTotal) : null;
        model.ExchangeFee = isExchangePayment ? FormatMoney(exchangeFee) : null;
        model.RefundBaseFare = isPayoutToUser ? FormatMoney(baseFare) : null;
        model.RefundTaxAndAirportFee = isPayoutToUser ? FormatMoney(combinedTaxAndAirportFee) : null;
        model.RefundServiceFee = isPayoutToUser ? FormatMoney(serviceFee) : null;
        model.RefundTotal = isPayoutToUser ? FormatMoney(refundTotal) : null;
    }

    private static BookingRecord CloneRecord(BookingRecord value)
    {
        return new BookingRecord
        {
            PNR = value.PNR,
            Contact = value.Contact,
            Route = value.Route,
            FlightDate = value.FlightDate,
            Status = value.Status,
            Fare = value.Fare,
            Tax = value.Tax,
            Total = value.Total,
            ExchangeTotal = value.ExchangeTotal,
            PaymentDue = value.PaymentDue
        };
    }

    private static PaymentSuccessViewModel CreateSuccessModel(BookingRecord booking)
    {
        return new PaymentSuccessViewModel
        {
            PNR = booking.PNR,
            Email = booking.Contact,
            Route = booking.Route,
            FlightDate = booking.FlightDate,
            Total = ResolvePaymentAmount(booking),
            IsExchangePayment = IsExchangeBooking(booking),
            IsPayoutToUser = string.Equals(booking.Status, "REFUNDED", StringComparison.OrdinalIgnoreCase),
            PaidAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        };
    }

    private static bool IsExchangeBooking(BookingRecord booking)
    {
        return !string.IsNullOrWhiteSpace(booking.ExchangeTotal)
            && !string.IsNullOrWhiteSpace(booking.PaymentDue);
    }

    private static string ResolvePaymentAmount(BookingRecord booking)
    {
        if (!string.IsNullOrWhiteSpace(booking.PaymentDue))
        {
            return booking.PaymentDue;
        }

        return booking.Total;
    }

    private static int ParseMoney(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private static string FormatMoney(int value)
    {
        return $"{value:N0} đ".Replace(',', '.');
    }

    private static bool IsPaymentLockedStatus(string? status)
    {
        return string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "REFUNDED", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPaymentLockedMessage(string? status)
    {
        if (string.Equals(status, "REFUNDED", StringComparison.OrdinalIgnoreCase))
        {
            return "Vé này đã hoàn vé nên không thể vào lại trang thanh toán.";
        }

        return "Đơn đặt chỗ này đã thanh toán trước đó.";
    }

    private static bool CanProceedToQr(BookingRecord booking)
    {
        return !IsPaymentLockedStatus(booking.Status);
    }

    private static string BuildPaymentFlowMessage(BookingRecord booking)
    {
        if (IsPendingRefundStatus(booking.Status))
        {
            return "Yêu cầu hoàn vé đang ở trạng thái chờ hoàn vé. Bấm Tiếp Tục để tạo QR hoàn tiền cho người dùng.";
        }

        if (IsPendingExchangeStatus(booking.Status))
        {
            var signedAmount = GetSignedAmount(booking.PaymentDue);
            if (signedAmount > 0)
            {
                return "Yêu cầu đổi vé đang chờ người dùng thanh toán khi đổi vé. Vui lòng đồng ý điều khoản và bấm Tiếp Tục để quét QR.";
            }

            return "Yêu cầu đổi vé đang chờ thanh toán tiền đổi cho người dùng. Bấm Tiếp Tục để tạo QR hoàn tiền.";
        }

        return "Vé này chưa thanh toán. Vui lòng đồng ý điều khoản và bấm Tiếp Tục để quét QR.";
    }

    private static bool IsPendingRefundStatus(string? status)
    {
        return IsStatus(status, "PENDING_REFUND", "REFUND_PENDING", "WAITING_REFUND");
    }

    private static bool IsPendingExchangeStatus(string? status)
    {
        return IsStatus(status, "PENDING_EXCHANGE", "EXCHANGE_PENDING", "WAITING_EXCHANGE");
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

    private static bool IsPayoutToUser(BookingRecord booking)
    {
        if (IsPendingRefundStatus(booking.Status))
        {
            return true;
        }

        return IsPendingExchangeStatus(booking.Status)
            && GetSignedAmount(booking.PaymentDue) <= 0;
    }

    private static string ResolveQrAmountLabel(BookingRecord booking)
    {
        if (IsPendingRefundStatus(booking.Status))
        {
            return "Số tiền hoàn cho người dùng";
        }

        if (IsPendingExchangeStatus(booking.Status))
        {
            return GetSignedAmount(booking.PaymentDue) > 0
                ? "Số Tiền Cần Thanh Toán Thêm"
                : "Số tiền đổi trả cho người dùng";
        }

        return IsExchangeBooking(booking)
            ? "Số Tiền Cần Thanh Toán Thêm"
            : "Tổng Tiền";
    }

    private static string ResolveQrDisplayAmount(BookingRecord booking)
    {
        if (IsPayoutToUser(booking))
        {
            return FormatMoney(Math.Abs(GetSignedAmount(booking.PaymentDue)));
        }

        return ResolvePaymentAmount(booking);
    }

    private static string ResolveCompletedBookingStatus(BookingRecord booking, bool isPayoutToUser)
    {
        if (IsPendingRefundStatus(booking.Status))
        {
            return "REFUNDED";
        }

        if (IsPendingExchangeStatus(booking.Status))
        {
            return "EXCHANGED";
        }

        return isPayoutToUser ? "REFUNDED" : "PAID";
    }

    private static bool IsSessionCompletedStatus(string? bookingStatus, bool isPayoutToUser)
    {
        if (isPayoutToUser)
        {
            return string.Equals(bookingStatus, "REFUNDED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bookingStatus, "EXCHANGED", StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(bookingStatus, "PAID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(bookingStatus, "EXCHANGED", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildMobileScanUrl(string sessionId)
    {
        return BuildAccessibleLocalUrl($"/pay/{sessionId}");
    }

    private string BuildStatusPollUrl(string sessionId)
    {
        return $"/Payment/CheckStatus?sessionId={Uri.EscapeDataString(sessionId)}";
    }

    private string BuildAccessibleLocalUrl(string path)
    {
        var scheme = Request.Scheme;
        var host = Request.Host.Host;
        var port = Request.Host.Port;

        if (IsLocalDevelopmentHost(host))
        {
            var lanIp = GetLocalIpv4Address();
            if (!string.IsNullOrWhiteSpace(lanIp))
            {
                host = lanIp;
                scheme = "http";
                port = _mobileEndpointSettings.HttpPort;
            }
        }

        var hostWithPort = port.HasValue ? $"{host}:{port.Value}" : host;
        return $"{scheme}://{hostWithPort}{path}";
    }

    private static bool IsLocalDevelopmentHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase)
            || IsPrivateIpv4Host(host);
    }

    private static bool IsPrivateIpv4Host(string host)
    {
        if (!IPAddress.TryParse(host, out var address))
        {
            return false;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        return IsPrivateIpv4(address);
    }

    private static string BuildScannableQrDataUrl(string qrContent)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(qrData);
        var pngBytes = pngQr.GetGraphic(16, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 }, drawQuietZones: true);
        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }

    private static string? GetLocalIpv4Address()
    {
        try
        {
            var preferredInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(networkInterface =>
                    networkInterface.OperationalStatus == OperationalStatus.Up
                    && networkInterface.Supports(NetworkInterfaceComponent.IPv4)
                    && networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                    && networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Tunnel)
                .OrderByDescending(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .ThenByDescending(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                .ThenByDescending(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet);

            foreach (var networkInterface in preferredInterfaces)
            {
                var properties = networkInterface.GetIPProperties();
                foreach (var unicastAddress in properties.UnicastAddresses)
                {
                    var address = unicastAddress.Address;
                    if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    {
                        continue;
                    }

                    if (IsPrivateIpv4(address))
                    {
                        return address.ToString();
                    }
                }
            }

            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            var candidate = hostEntry.AddressList.FirstOrDefault(addr =>
                addr.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(addr));

            return candidate?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
        {
            return false;
        }

        if (bytes[0] == 10)
        {
            return true;
        }

        if (bytes[0] == 192 && bytes[1] == 168)
        {
            return true;
        }

        return bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31;
    }

    public static bool TryGetLatestActiveSessionId(out string sessionId)
    {
        sessionId = string.Empty;

        var currentLatest = LatestSessionId;
        if (string.IsNullOrWhiteSpace(currentLatest))
        {
            return false;
        }

        if (!QrSessions.TryGetValue(currentLatest, out var session))
        {
            return false;
        }

        if (session.ExpiresAt <= DateTime.Now)
        {
            return false;
        }

        sessionId = currentLatest;
        return true;
    }

    private sealed class QrPaymentSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string PNR { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool IsPayoutToUser { get; set; }
        public string? RecipientName { get; set; }
        public string? RecipientBankAccount { get; set; }
        public bool IsPaid { get; set; }
        public bool IsFailed { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? FailedMessage { get; set; }
    }
}
