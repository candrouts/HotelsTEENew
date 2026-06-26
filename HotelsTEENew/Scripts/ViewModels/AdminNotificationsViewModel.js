// Διαχείριση Ειδοποιήσεων (admin, role=100): πρότυπα ανά γεγονός + ιστορικό αποστολών
function AdminNotificationsViewModel() {
    var self = this;

    self.showLoader = function () {
        if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("show");
    };
    self.hideLoader = function () {
        setTimeout(function () {
            if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("hide");
        }, 300);
    };

    self.events = ko.observableArray([]);
    self.log = ko.observableArray([]);

    // ── Πρότυπα ────────────────────────────────────────────────────
    self.fetchEvents = function () {
        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            url: "/api/AdminNotificationsApi/GetEvents",
            success: function (data) {
                self.hideLoader();
                if (!data || !data.success) return;

                var mapped = (data.events || []).map(function (e) {
                    var ev = {
                        eventKey: e.eventKey,
                        title: e.title,
                        description: e.description,
                        tokens: e.tokens || [],
                        isActive: ko.observable(e.isActive),
                        recipientType: ko.observable(String(e.recipientType)),
                        customEmail: ko.observable(e.customEmail || ""),
                        subject: ko.observable(e.subject || ""),
                        body: ko.observable(e.body || "")
                    };
                    // Εισαγωγή token στο τέλος του κειμένου
                    ev.insertToken = function (token) {
                        ev.body(ev.body() + " " + token);
                    };
                    return ev;
                });

                self.events(mapped);
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.saveTemplate = function (ev) {
        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            contentType: "application/json",
            data: JSON.stringify({
                eventKey: ev.eventKey,
                isActive: ev.isActive(),
                recipientType: parseInt(ev.recipientType()),
                customEmail: ev.customEmail(),
                subject: ev.subject(),
                body: ev.body()
            }),
            url: "/api/AdminNotificationsApi/SaveTemplate",
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    $("#notif-saved-toast").fadeIn(150).delay(1200).fadeOut(300);
                } else {
                    alert("Σφάλμα αποθήκευσης. Ελέγξτε ότι ο παραλήπτης 'Custom' έχει email.");
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    // ── Ιστορικό ───────────────────────────────────────────────────
    self.loadLog = function () {
        $.ajax({
            type: "POST",
            dataType: "json",
            url: "/api/AdminNotificationsApi/GetLog",
            success: function (data) {
                if (!data || !data.success) return;
                var rows = (data.log || []).map(function (r) {
                    r.sentDate = formatDate(r.sentDateTime);
                    return r;
                });
                self.log(rows);
            }
        });
    };

    function formatDate(d) {
        if (!d) return "";
        var dt = new Date(d);
        if (isNaN(dt.getTime())) return d;
        var pad = function (n) { return n < 10 ? "0" + n : n; };
        return pad(dt.getDate()) + "/" + pad(dt.getMonth() + 1) + "/" + dt.getFullYear() +
            " " + pad(dt.getHours()) + ":" + pad(dt.getMinutes());
    }

    self.fetchEvents();
}
