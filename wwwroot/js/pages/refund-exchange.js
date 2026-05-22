const modeButtons = document.querySelectorAll(".mode-btn");
const modeInput = document.getElementById("modeInput");
const reasonSelect = document.getElementById("refundReason");
const passengerCheckbox = document.getElementById("selectedPassenger");
const submitButton = document.getElementById("submitRefund");
const refundBlock = document.getElementById("refundBlock");
const exchangeBlock = document.getElementById("exchangeBlock");
const selectedFlightKey = document.getElementById("selectedFlightKey");

const baseFareEl = document.getElementById("baseFare");
const taxAmountEl = document.getElementById("taxAmount");
const serviceFeeEl = document.getElementById("serviceFee");
const refundTotalEl = document.getElementById("refundTotal");
const currentTotalEl = document.getElementById("currentTotal");
const newFlightTotalEl = document.getElementById("newFlightTotal");
const exchangeFeeEl = document.getElementById("exchangeFee");
const exchangePayTotalEl = document.getElementById("exchangePayTotal");
const exchangeSettlementLabelEl = document.getElementById("exchangeSettlementLabel");
const reasonHintEl = document.getElementById("reasonHint");
const currentFlightDateText = document.querySelector(".flight-row .flight-meta:last-child p:last-child")?.textContent || "";

const parseAmount = (value) => {
	const digits = (value || "").replace(/[^0-9]/g, "");
	return Number.parseInt(digits || "0", 10);
};

const baseFare = parseAmount(baseFareEl?.textContent);
const taxAmount = parseAmount(taxAmountEl?.textContent);
const currentTotal = parseAmount(currentTotalEl?.textContent);
const exchangeFee = parseAmount(exchangeFeeEl?.textContent);

const parseDate = (value) => {
	if (!value) {
		return null;
	}

	if (/^\d{2}\/\d{2}\/\d{4}\s\d{2}:\d{2}$/.test(value)) {
		const [datePart, timePart] = value.split(" ");
		const [day, month, year] = datePart.split("/").map((item) => Number.parseInt(item, 10));
		const [hour, minute] = timePart.split(":").map((item) => Number.parseInt(item, 10));
		return new Date(year, month - 1, day, hour, minute, 0);
	}

	const parsed = new Date(value);
	return Number.isNaN(parsed.getTime()) ? null : parsed;
};

const currentFlightDate = parseDate(currentFlightDateText);

const formatCurrency = (value) => {
	return `${new Intl.NumberFormat("vi-VN").format(value)} đ`;
};

const reasonMessages = {
	personal_plan: "Người dùng chủ động thay đổi kế hoạch nên mức phí hoàn được áp dụng theo chính sách mặc định.",
	schedule_changed: "Hãng thay đổi lịch bay nên hệ thống giảm phí xử lý hoàn vé.",
	medical_issue: "Lý do sức khỏe được áp dụng mức phí hoàn giảm theo chính sách hỗ trợ.",
	document_issue: "Sai thông tin giấy tờ hoặc hành khách cần kiểm tra lại trước khi hoàn/đổi vé.",
	route_change: "Đổi sang chuyến bay hoặc chặng bay khác sẽ được tính như yêu cầu đổi vé.",
	other: "Lý do khác sẽ áp dụng mức phí hoàn mặc định và cần ghi chú thêm nếu có."
};

const updateReasonHint = () => {
	if (!reasonSelect || !reasonHintEl) {
		return;
	}

	reasonHintEl.textContent = reasonMessages[reasonSelect.value] || "Chọn nguyên nhân để hệ thống áp dụng mức phí hoàn phù hợp.";
};

const recalculateRefund = () => {
	if (!reasonSelect || !passengerCheckbox || !baseFareEl || !taxAmountEl || !serviceFeeEl || !refundTotalEl) {
		return;
	}

	if (!passengerCheckbox.checked) {
		baseFareEl.textContent = "0 đ";
		taxAmountEl.textContent = "- 0 đ";
		serviceFeeEl.textContent = "- 0 đ";
		refundTotalEl.textContent = "0 đ";
		return;
	}

	if (baseFare <= 0) {
		baseFareEl.textContent = "0 đ";
		taxAmountEl.textContent = "- 0 đ";
		serviceFeeEl.textContent = "- 0 đ";
		refundTotalEl.textContent = "0 đ";
		return;
	}

	let serviceFee = 300000;
	if (reasonSelect.value === "schedule_changed") {
		serviceFee = 0;
	} else if (reasonSelect.value === "medical_issue") {
		serviceFee = 150000;
	} else if (reasonSelect.value === "document_issue") {
		serviceFee = 200000;
	}

	const totalRefund = Math.max(baseFare - taxAmount - serviceFee, 0);
	baseFareEl.textContent = formatCurrency(baseFare);
	taxAmountEl.textContent = `- ${formatCurrency(taxAmount)}`;
	serviceFeeEl.textContent = `- ${formatCurrency(serviceFee)}`;
	refundTotalEl.textContent = formatCurrency(totalRefund);
};

const recalculateExchange = () => {
	if (!selectedFlightKey || !newFlightTotalEl || !exchangePayTotalEl || !exchangeSettlementLabelEl) {
		return;
	}

	const selected = selectedFlightKey.options[selectedFlightKey.selectedIndex];
	if (!selected) {
		newFlightTotalEl.textContent = formatCurrency(currentTotal);
		exchangePayTotalEl.textContent = formatCurrency(0);
		exchangeSettlementLabelEl.textContent = "Số tiền cần thanh toán thêm";
		return;
	}

	const newPrice = Number.parseInt(selected.dataset.price || "0", 10);
	const departure = parseDate(selected.dataset.departure || "");
	const now = new Date();

	if (departure && departure <= now) {
		selectedFlightKey.setCustomValidity("Chuyến bay đổi phải ở thời điểm tương lai.");
	} else if (currentFlightDate && departure && departure < currentFlightDate) {
		selectedFlightKey.setCustomValidity("Giờ chuyến bay mới phải lớn hơn hoặc bằng chuyến hiện tại.");
	} else {
		selectedFlightKey.setCustomValidity("");
	}

	const settlementAmount = newPrice + exchangeFee - currentTotal;

	newFlightTotalEl.textContent = formatCurrency(newPrice > 0 ? newPrice : currentTotal);
	if (settlementAmount > 0) {
		exchangeSettlementLabelEl.textContent = "Số tiền cần thanh toán thêm";
		exchangePayTotalEl.textContent = formatCurrency(settlementAmount);
	} else if (settlementAmount < 0) {
		exchangeSettlementLabelEl.textContent = "Số tiền hoàn lại cho người dùng";
		exchangePayTotalEl.textContent = formatCurrency(Math.abs(settlementAmount));
	} else {
		exchangeSettlementLabelEl.textContent = "Chênh lệch thanh toán";
		exchangePayTotalEl.textContent = formatCurrency(0);
	}
};

const updateModeUI = (mode) => {
	const isExchange = mode === "exchange";

	if (refundBlock) {
		refundBlock.hidden = isExchange;
	}

	if (exchangeBlock) {
		exchangeBlock.hidden = !isExchange;
	}

	if (selectedFlightKey) {
		selectedFlightKey.disabled = !isExchange;
		selectedFlightKey.required = isExchange;
	}

	if (submitButton) {
		submitButton.textContent = isExchange ? "Gửi yêu cầu đổi vé" : "Gửi yêu cầu hoàn vé";
	}
};

modeButtons.forEach((button) => {
	button.addEventListener("click", () => {
		if (button.disabled) {
			return;
		}

		modeButtons.forEach((item) => item.classList.remove("active"));
		button.classList.add("active");

		if (modeInput) {
			modeInput.value = button.dataset.mode || "refund";
		}

		updateModeUI(button.dataset.mode || "refund");
	});
});

reasonSelect?.addEventListener("change", recalculateRefund);
reasonSelect?.addEventListener("change", updateReasonHint);
passengerCheckbox?.addEventListener("change", recalculateRefund);
selectedFlightKey?.addEventListener("change", recalculateExchange);

recalculateRefund();
recalculateExchange();
updateReasonHint();
updateModeUI(modeInput?.value || "refund");
