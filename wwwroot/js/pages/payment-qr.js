(() => {
    const qrImage = document.getElementById("fakeQrImage");
    const qrSection = document.getElementById("qrSection");
    const successMessage = document.getElementById("successMessage");
    const errorMessage = document.getElementById("errorMessage");
    const errorText = document.getElementById("errorText");
    const config = window.paymentQrConfig || {};
    const sessionId = config.sessionId || "";

    const resolveStatusPollUrl = () => {
        if (config.statusPollUrl) {
            return config.statusPollUrl;
        }

        if (!sessionId) {
            return "";
        }

        return `/Payment/CheckStatus?sessionId=${encodeURIComponent(sessionId)}`;
    };

    const statusPollUrl = resolveStatusPollUrl();

    if (!qrImage || !qrSection || !successMessage || !errorMessage || !errorText || !statusPollUrl) {
        return;
    }

    let pollTimer = null;

    const handlePaymentResult = (status, paidAt, message) => {
        const normalizedStatus = String(status || "").toUpperCase();

        if (normalizedStatus === "SUCCESS" || normalizedStatus === "PAID") {
            qrSection.style.display = "none";
            errorMessage.hidden = true;
            successMessage.hidden = false;
            return;
        }

        if (normalizedStatus === "FAILED" || normalizedStatus === "FAIL") {
            qrSection.style.display = "none";
            successMessage.hidden = true;
            errorMessage.hidden = false;
            errorText.textContent = message || "Thanh toán thất bại. Vui lòng thử lại.";
        }
    };

    const showExpiredState = () => {
        const note = document.createElement("p");
        note.className = "scan-note";
        note.textContent = "Mã QR đã hết hạn, vui lòng quay lại để tạo mã mới.";
        qrSection.appendChild(note);
    };

    const stopPolling = () => {
        if (pollTimer) {
            window.clearInterval(pollTimer);
            pollTimer = null;
        }
    };

    const pollStatus = async () => {
        try {
            const response = await fetch(statusPollUrl, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (!response.ok) {
                return;
            }

            const result = await response.json();
            if (!result?.success) {
                return;
            }

            const status = String(result.status || "").toUpperCase();

            if (status === "SUCCESS" || status === "PAID") {
                qrImage.classList.add("scanned");
                stopPolling();
                handlePaymentResult("SUCCESS", result.paidAt || "");
                return;
            }

            if (status === "FAILED" || status === "FAIL") {
                stopPolling();
                handlePaymentResult("FAILED", "", result.message || "Thanh toán thất bại. Vui lòng thử lại.");
                return;
            }

            if (status === "EXPIRED") {
                stopPolling();
                showExpiredState();
            }
        } catch (error) {
            // Keep retrying but leave a browser-side hint for debugging.
            console.warn("Payment status polling failed", error);
        }
    };

    pollStatus();
    pollTimer = window.setInterval(pollStatus, 2000);
})();
