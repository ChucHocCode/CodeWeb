// hoan-doi.js
// Xử lý sự kiện cho các button Hoàn/Dổi vé

document.addEventListener('DOMContentLoaded', function () {
  // Hoàn vé
  const btnHoan = document.querySelector('button:not(.ghost):not([data-doi])');
  if (btnHoan) {
    btnHoan.addEventListener('click', function () {
      alert('Đã gửi yêu cầu hoàn vé!');
    });
  }

  // Đổi vé
  const btnDoi = document.querySelector('button[data-doi]');
  if (btnDoi) {
    btnDoi.addEventListener('click', function () {
      alert('Đã gửi yêu cầu đổi vé!');
    });
  }

  // Các nút hủy/từ chối
  document.querySelectorAll('button.ghost').forEach(function (btn) {
    btn.addEventListener('click', function () {
      alert('Đã hủy thao tác!');
    });
  });
});
