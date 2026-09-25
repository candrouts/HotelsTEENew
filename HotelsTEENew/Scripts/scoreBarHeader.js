// Μπάρα βαθμολογίας στο header κατά το scroll (μόνο desktop ≥ 992px).
// Η μπάρα ΔΕΝ μετακινείται στο DOM (κρατά τα KO bindings της σελίδας): με την κλάση
// body.sb-in-header γίνεται position:fixed πάνω στη λευκή λωρίδα του header, ανάμεσα
// στο ☰ και στα εικονίδια. Οι θέσεις μετρώνται από τα πραγματικά στοιχεία.
(function () {
    var ENTER = 60, LEAVE = 80;   // υστέρηση (px) — να μην τρεμοπαίζει γύρω από το όριο
    var sentinel, bar, sb;

    function measure() {
        var body = document.body;
        var nav = document.querySelector(".navbar-custom");
        var toggle = document.querySelector(".navbar-custom .button-toggle-menu");
        var icons = document.querySelector(".navbar-custom .topbar-menu");
        if (!nav || !toggle || !icons || !sb) return;

        var navH = nav.getBoundingClientRect().height || 70;
        var left = toggle.getBoundingClientRect().right + 12;
        var right = window.innerWidth - icons.getBoundingClientRect().left + 12;
        var h = sb.getBoundingClientRect().height || 50;

        body.style.setProperty("--sb-left", Math.round(left) + "px");
        body.style.setProperty("--sb-right", Math.round(right) + "px");
        body.style.setProperty("--sb-top", Math.max(0, Math.round((navH - h) / 2)) + "px");
        body.classList.toggle("sb-narrow", (window.innerWidth - left - right) < 900);
    }

    function update() {
        var body = document.body;
        if (window.innerWidth < 992) { body.classList.remove("sb-in-header"); return; }
        var top = sentinel.getBoundingClientRect().top;   // φυσική θέση της μπάρας
        var inHeader = body.classList.contains("sb-in-header");
        if (!inHeader && top < ENTER) {
            bar.style.minHeight = bar.getBoundingClientRect().height + "px";   // κρατά τη θέση (χωρίς «πήδημα»)
            measure();
            body.classList.add("sb-in-header");
        } else if (inHeader && top > LEAVE) {
            body.classList.remove("sb-in-header");
            bar.style.minHeight = "";
        }
    }

    function init() {
        bar = document.getElementById("guide-bar");
        sb = bar ? bar.querySelector(".sb-bar") : null;
        if (!bar || !sb) return;

        // col-12 + ύψος 0: σε flex .row πιάνει δική του γραμμή, ακριβώς πάνω από τη μπάρα
        sentinel = document.createElement("div");
        sentinel.className = "col-12";
        sentinel.style.cssText = "height:0;padding:0;margin:0;";
        sentinel.setAttribute("aria-hidden", "true");
        bar.parentNode.insertBefore(sentinel, bar);

        window.addEventListener("scroll", update, { passive: true });
        window.addEventListener("resize", function () { measure(); update(); });

        // Άνοιγμα/κλείσιμο πλαϊνού μενού: αλλάζει η θέση του ☰
        var toggle = document.querySelector(".navbar-custom .button-toggle-menu");
        if (toggle) toggle.addEventListener("click", function () { setTimeout(measure, 350); });
        if (window.ResizeObserver) {
            var nav = document.querySelector(".navbar-custom");
            if (nav) new ResizeObserver(measure).observe(nav);
        }

        measure();
        update();
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
