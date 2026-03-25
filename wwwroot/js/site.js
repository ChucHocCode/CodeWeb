document.addEventListener("DOMContentLoaded", function () {
  var yearNodes = document.querySelectorAll("[data-year]");
  var year = new Date().getFullYear().toString();

  for (var i = 0; i < yearNodes.length; i += 1) {
    yearNodes[i].textContent = year;
  }
});
