// AI Σύμβουλος Βιωσιμότητας — global floating chat widget (όλες οι σελίδες).
// Εμφανίζεται μόνο όταν το AI είναι ενεργό και ο χρήστης είναι ξενοδόχος (role=1).
// Το ιστορικό της συνομιλίας διατηρείται μεταξύ σελίδων (sessionStorage).
(function () {
    "use strict";

    var HISTORY_KEY = "aiChatHistory";
    var busy = false;

    var SUGGESTIONS = [
        "Τι μου λείπει για το επόμενο μετάλλιο;",
        "Ποια εύκολα κριτήρια δεν έχω απαντήσει;",
        "Ποιοι επιθεωρητές καλύπτουν την περιοχή μου;"
    ];

    var GREETING = "Γεια σας! Είμαι ο AI Σύμβουλος Βιωσιμότητας. Γνωρίζω τα κριτήρια και την τρέχουσα αξιολόγησή σας — ρωτήστε με ό,τι θέλετε!";

    function loadHistory() {
        try { return JSON.parse(sessionStorage.getItem(HISTORY_KEY)) || []; }
        catch (e) { return []; }
    }
    function saveHistory(h) {
        try { sessionStorage.setItem(HISTORY_KEY, JSON.stringify(h.slice(-30))); } catch (e) { }
    }

    function build() {
        var fab = document.createElement("button");
        fab.type = "button";
        fab.id = "ai-chat-fab";
        fab.title = "AI Σύμβουλος Βιωσιμότητας";
        fab.style.cssText = "position:fixed;bottom:24px;right:24px;z-index:2500;width:56px;height:56px;border-radius:50%;border:none;background:#39afd1;color:#fff;box-shadow:0 4px 12px rgba(0,0,0,0.25);font-size:26px;";
        fab.innerHTML = "<i class='mdi mdi-robot-outline'></i>";
        document.body.appendChild(fab);

        var panel = document.createElement("div");
        panel.id = "ai-chat-panel";
        panel.style.cssText = "position:fixed;bottom:92px;right:24px;z-index:2500;width:390px;max-width:92vw;height:520px;max-height:70vh;background:#fff;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,0.3);display:none;flex-direction:column;overflow:hidden;";
        panel.innerHTML =
            "<div style='background:#39afd1;color:#fff;padding:10px 14px;display:flex;justify-content:space-between;align-items:center;'>" +
            "<strong><i class='mdi mdi-robot-outline me-1'></i>AI Σύμβουλος Βιωσιμότητας</strong>" +
            "<span><a href='javascript:void(0)' id='ai-chat-reset' style='color:#fff;margin-right:12px;font-size:12px;' title='Νέα συζήτηση'><i class='mdi mdi-refresh'></i></a>" +
            "<a href='javascript:void(0)' id='ai-chat-close' style='color:#fff;'><i class='mdi mdi-close'></i></a></span></div>" +
            "<div id='ai-chat-body' style='flex:1 1 auto;min-height:0;overflow-y:auto;padding:12px;background:#f5f7fa;'></div>" +
            "<div id='ai-chat-suggestions' style='padding:6px 10px;background:#f5f7fa;'></div>" +
            "<div style='padding:10px;border-top:1px solid #e3e6ea;display:flex;gap:6px;'>" +
            "<input type='text' id='ai-chat-input' class='form-control form-control-sm' placeholder='Ρωτήστε τον Σύμβουλο...' />" +
            "<button class='btn btn-sm btn-info' id='ai-chat-send'><i class='mdi mdi-send'></i></button></div>" +
            "<div style='padding:3px 10px 6px;background:#fff;font-size:10px;color:#98a6ad;text-align:center;'>Οι συνομιλίες καταγράφονται για τη βελτίωση της υπηρεσίας.</div>";
        document.body.appendChild(panel);

        fab.onclick = toggle;
        document.getElementById("ai-chat-close").onclick = toggle;
        document.getElementById("ai-chat-reset").onclick = function () {
            saveHistory([]); renderMessages([]); renderSuggestions([]);
        };
        document.getElementById("ai-chat-send").onclick = send;
        document.getElementById("ai-chat-input").addEventListener("keypress", function (e) {
            if (e.keyCode === 13) { send(); e.preventDefault(); }
        });
    }

    function toggle() {
        var panel = document.getElementById("ai-chat-panel");
        var open = panel.style.display === "flex";
        panel.style.display = open ? "none" : "flex";
        if (!open) {
            var h = loadHistory();
            renderMessages(h);
            renderSuggestions(h);
            setTimeout(function () { document.getElementById("ai-chat-input").focus(); }, 150);
        }
    }

    function bubble(role, text) {
        var wrap = document.createElement("div");
        wrap.className = "mb-2";
        if (role === "user") wrap.style.textAlign = "right";
        var b = document.createElement("div");
        b.style.cssText = "display:inline-block;max-width:85%;padding:8px 12px;border-radius:12px;text-align:left;white-space:pre-wrap;font-size:13px;" +
            (role === "user" ? "background:#39afd1;color:#fff;" : "background:#fff;color:#343a40;border:1px solid #e3e6ea;");
        b.textContent = text;
        wrap.appendChild(b);
        return wrap;
    }

    function renderMessages(history) {
        var body = document.getElementById("ai-chat-body");
        body.innerHTML = "";
        body.appendChild(bubble("assistant", GREETING));
        history.forEach(function (m) { body.appendChild(bubble(m.role, m.content)); });
        body.scrollTop = body.scrollHeight;
    }

    function renderSuggestions(history) {
        var box = document.getElementById("ai-chat-suggestions");
        box.innerHTML = "";
        if (history.length > 0) return;
        SUGGESTIONS.forEach(function (s) {
            var btn = document.createElement("button");
            btn.type = "button";
            btn.className = "btn btn-sm btn-outline-info mb-1 me-1";
            btn.style.fontSize = "11px";
            btn.textContent = s;
            btn.onclick = function () {
                document.getElementById("ai-chat-input").value = s;
                send();
            };
            box.appendChild(btn);
        });
    }

    function setBusy(on) {
        busy = on;
        document.getElementById("ai-chat-input").disabled = on;
        document.getElementById("ai-chat-send").disabled = on;
        var body = document.getElementById("ai-chat-body");
        var sp = document.getElementById("ai-chat-spinner");
        if (on && !sp) {
            sp = document.createElement("div");
            sp.id = "ai-chat-spinner";
            sp.className = "mb-2";
            sp.innerHTML = "<div style='display:inline-block;padding:8px 12px;border-radius:12px;background:#fff;border:1px solid #e3e6ea;'><span class='spinner-border spinner-border-sm text-info'></span></div>";
            body.appendChild(sp);
            body.scrollTop = body.scrollHeight;
        } else if (!on && sp) {
            sp.remove();
        }
    }

    function send() {
        if (busy) return;
        var input = document.getElementById("ai-chat-input");
        var text = (input.value || "").trim();
        if (!text) return;
        input.value = "";

        var history = loadHistory();
        history.push({ role: "user", content: text });
        saveHistory(history);
        renderMessages(history);
        renderSuggestions(history);
        setBusy(true);

        $.ajax({
            type: "POST", url: "/api/AiApi/Advise", contentType: "application/json",
            data: JSON.stringify({ messages: history }), dataType: "json",
            success: function (r) {
                setBusy(false);
                var h = loadHistory();
                h.push({ role: "assistant", content: (r && r.success) ? r.reply : ((r && r.message) || "Κάτι πήγε στραβά — δοκιμάστε ξανά.") });
                saveHistory(h);
                renderMessages(h);
            },
            error: function () {
                setBusy(false);
                var h = loadHistory();
                h.push({ role: "assistant", content: "Πρόβλημα επικοινωνίας — δοκιμάστε ξανά." });
                saveHistory(h);
                renderMessages(h);
            }
        });
    }

    // Εκκίνηση: μόνο αν το AI είναι ενεργό και ο χρήστης ξενοδόχος
    $(function () {
        $.ajax({
            type: "POST", url: "/api/AiApi/ChatAvailable", dataType: "json",
            success: function (r) { if (r && r.enabled === true) build(); }
        });
    });
})();
