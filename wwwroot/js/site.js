document.addEventListener("DOMContentLoaded", function () {
    var yearNodes = document.querySelectorAll("[data-year]");
    var year = new Date().getFullYear().toString();

    for (var i = 0; i < yearNodes.length; i += 1) {
        yearNodes[i].textContent = year;
    }

    const hamburger = document.querySelector(".hamburger");
    const nav = document.querySelector(".main-nav");

    hamburger.addEventListener("click", function () {
        nav.classList.toggle("active");
    });
});
