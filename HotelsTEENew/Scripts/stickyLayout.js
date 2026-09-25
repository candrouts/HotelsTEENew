// Sticky διάταξη σελίδων αξιολόγησης (Criteria / ViewCertificate):
// μπάρα βαθμολογίας -> πυλώνες -> υποπυλώνες, με θέσεις που υπολογίζονται από τα
// πραγματικά ύψη (όχι σταθερά px), ώστε οι υποπυλώνες να μένουν πάντα κάτω από τους
// πυλώνες. Κατά το scroll η μπάρα γίνεται πιο συμπαγής (body.crit-compact).
(function () {
    function px(v) { return Math.round(v) + "px"; }

    function update() {
        var bar = document.getElementById("guide-bar");
        if (!bar) return;
        var root = document.documentElement;
        var topbar = document.querySelector(".navbar-custom");
        var pills = document.getElementById("guide-pillars");

        var barTop = topbar ? topbar.getBoundingClientRect().height : 70;
        var pillsTop = barTop + bar.getBoundingClientRect().height;
        var subTop = pillsTop + (pills ? pills.getBoundingClientRect().height : 0) + 6;

        root.style.setProperty("--sticky-bar-top", px(barTop));
        root.style.setProperty("--sticky-pills-top", px(pillsTop));
        root.style.setProperty("--sticky-sub-top", px(subTop));
    }

    // Υστέρηση (hysteresis) ώστε να μην «τρεμοπαίζει» γύρω από το όριο
    function onScroll() {
        var y = window.scrollY || window.pageYOffset;
        var compact = document.body.classList.contains("crit-compact");
        if (!compact && y > 120) { document.body.classList.add("crit-compact"); update(); }
        else if (compact && y < 40) { document.body.classList.remove("crit-compact"); update(); }
    }

    function init() {
        var bar = document.getElementById("guide-bar");
        if (!bar) return;
        update();
        window.addEventListener("resize", update);
        window.addEventListener("scroll", onScroll, { passive: true });
        if (window.ResizeObserver) {
            var ro = new ResizeObserver(update);
            ro.observe(bar);
            var pills = document.getElementById("guide-pillars");
            if (pills) ro.observe(pills);
        }
        onScroll();
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
