// Ελαφρύς, self-hosted οδηγός σελίδας (highlight + popover βήματα).
// Καμία εξάρτηση πέραν jQuery (προαιρετικά). Άδεια: εσωτερική χρήση.
(function () {
    "use strict";

    // ── CSS (εισάγεται μία φορά) ───────────────────────────────────
    function injectCss() {
        if (document.getElementById("pg-style")) return;
        var css =
            "#pg-blocker{position:fixed;inset:0;z-index:20000;cursor:default;}" +
            "#pg-highlight{position:fixed;z-index:20001;border-radius:6px;border:2px solid #0acf97;" +
            "box-shadow:0 0 0 9999px rgba(0,0,0,.55);transition:all .25s ease;pointer-events:none;}" +
            "#pg-pop{position:fixed;z-index:20002;max-width:340px;background:#fff;border-radius:8px;" +
            "box-shadow:0 8px 30px rgba(0,0,0,.3);padding:14px 16px;font-size:13px;color:#313a46;}" +
            "#pg-pop h6{margin:0 0 6px;font-size:14px;font-weight:700;color:#1f4e79;}" +
            "#pg-pop .pg-body{line-height:1.45;}" +
            "#pg-pop .pg-foot{display:flex;align-items:center;justify-content:space-between;margin-top:12px;}" +
            "#pg-pop .pg-count{font-size:11px;color:#98a6ad;}" +
            "#pg-pop .pg-btns button{border:0;border-radius:5px;padding:4px 10px;margin-left:6px;font-size:12px;cursor:pointer;}" +
            "#pg-pop .pg-next{background:#0acf97;color:#fff;}" +
            "#pg-pop .pg-prev{background:#eef2f7;color:#313a46;}" +
            "#pg-pop .pg-skip{background:transparent;color:#98a6ad;}" +
            "#page-guide-btn{position:fixed;right:18px;bottom:18px;z-index:1500;width:42px;height:42px;border-radius:50%;" +
            "border:0;background:#1f4e79;color:#fff;font-size:20px;font-weight:700;cursor:pointer;box-shadow:0 4px 14px rgba(0,0,0,.25);}";
        var st = document.createElement("style");
        st.id = "pg-style";
        st.textContent = css;
        document.head.appendChild(st);
    }

    var state = { steps: [], idx: 0, active: false };

    function el(id, tag) {
        var e = document.getElementById(id);
        if (!e) { e = document.createElement(tag || "div"); e.id = id; document.body.appendChild(e); }
        return e;
    }

    function position() {
        var step = state.steps[state.idx];
        var target = document.querySelector(step.sel);
        if (!target) { next(); return; }

        var r = target.getBoundingClientRect();
        var pad = 6;
        var hl = el("pg-highlight");
        hl.style.top = (r.top - pad) + "px";
        hl.style.left = (r.left - pad) + "px";
        hl.style.width = (r.width + pad * 2) + "px";
        hl.style.height = (r.height + pad * 2) + "px";

        var pop = document.getElementById("pg-pop");
        var pw = 340, ph = pop.offsetHeight || 150;
        var vw = window.innerWidth, vh = window.innerHeight;

        var top = r.bottom + 12;
        if (top + ph > vh - 10) top = Math.max(10, r.top - ph - 12);
        var left = r.left;
        if (left + pw > vw - 10) left = vw - pw - 10;
        if (left < 10) left = 10;

        pop.style.top = top + "px";
        pop.style.left = left + "px";
    }

    function render() {
        var step = state.steps[state.idx];
        var pop = el("pg-pop");
        var isLast = state.idx === state.steps.length - 1;
        var isFirst = state.idx === 0;

        pop.innerHTML =
            "<h6>" + (step.title || "") + "</h6>" +
            "<div class='pg-body'>" + (step.text || "") + "</div>" +
            "<div class='pg-foot'>" +
            "<span class='pg-count'>" + (state.idx + 1) + " / " + state.steps.length + "</span>" +
            "<span class='pg-btns'>" +
            "<button class='pg-skip' id='pg-skip'>Κλείσιμο</button>" +
            (isFirst ? "" : "<button class='pg-prev' id='pg-prev'>Προηγούμενο</button>") +
            "<button class='pg-next' id='pg-next'>" + (isLast ? "Τέλος" : "Επόμενο") + "</button>" +
            "</span></div>";

        document.getElementById("pg-skip").onclick = stop;
        document.getElementById("pg-next").onclick = next;
        if (!isFirst) document.getElementById("pg-prev").onclick = prev;

        var target = document.querySelector(step.sel);
        if (target) target.scrollIntoView({ block: "center", behavior: "smooth" });
        setTimeout(position, 320);
    }

    function next() {
        if (state.idx >= state.steps.length - 1) { stop(); return; }
        state.idx++; render();
    }
    function prev() { if (state.idx > 0) { state.idx--; render(); } }

    function stop() {
        state.active = false;
        ["pg-blocker", "pg-highlight", "pg-pop"].forEach(function (id) {
            var e = document.getElementById(id); if (e) e.remove();
        });
        window.removeEventListener("resize", position);
        window.removeEventListener("scroll", position, true);
    }

    function start(steps) {
        injectCss();
        var avail = (steps || []).filter(function (s) { return document.querySelector(s.sel); });
        if (!avail.length) return;
        state.steps = avail; state.idx = 0; state.active = true;

        var blk = el("pg-blocker");
        blk.onclick = function () { };  // απλώς μπλοκάρει
        el("pg-highlight"); el("pg-pop");
        window.addEventListener("resize", position);
        window.addEventListener("scroll", position, true);
        render();
    }

    // Δημόσιο API: κουμπί «?» + αυτόματη εκκίνηση 1η φορά
    function register(key, waitSelector, buildSteps) {
        injectCss();
        var btn = el("page-guide-btn", "button");
        btn.textContent = "?";
        btn.title = "Οδηγός σελίδας";
        btn.onclick = function () { start(buildSteps()); };

        if (key && !localStorage.getItem(key)) {
            var tries = 0;
            var iv = setInterval(function () {
                tries++;
                if (document.querySelector(waitSelector)) {
                    clearInterval(iv);
                    localStorage.setItem(key, "1");
                    setTimeout(function () { start(buildSteps()); }, 400);
                } else if (tries > 30) {
                    clearInterval(iv);
                }
            }, 300);
        }
    }

    window.PageGuide = { start: start, register: register, stop: stop };
})();
