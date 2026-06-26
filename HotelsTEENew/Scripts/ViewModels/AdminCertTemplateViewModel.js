// Διαχείριση προτύπου εγγράφου Βεβαίωσης (admin, role=100)
function AdminCertTemplateViewModel() {
    var self = this;

    self.showLoader = function () {
        if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("show");
    };
    self.hideLoader = function () {
        setTimeout(function () {
            if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("hide");
        }, 300);
    };

    self.body = ko.observable("");
    self.tokens = ko.observableArray([]);

    self.fetchData = function () {
        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            url: "/api/AdminCertTemplateApi/GetTemplate",
            success: function (data) {
                self.hideLoader();
                if (!data || !data.success) return;
                self.body(data.body || "");
                self.tokens(data.tokens || []);
            },
            error: function () { self.hideLoader(); }
        });
    };

    // Εισαγωγή token στη θέση του δρομέα
    self.insertToken = function (token) {
        var ta = document.getElementById("cert-template-body");
        if (!ta) { self.body(self.body() + " " + token); return; }
        var start = ta.selectionStart, end = ta.selectionEnd;
        var v = self.body();
        self.body(v.substring(0, start) + token + v.substring(end));
        setTimeout(function () { ta.focus(); ta.selectionStart = ta.selectionEnd = start + token.length; }, 0);
    };

    // Προεπισκόπηση με ενδεικτικές τιμές
    self.preview = function () {
        var samples = {
            "{certNumber}": "001050", "{hotelType}": "ΞΕΝΟΔΟΧΕΙΟ", "{hotelName}": "ΚΑΜΕΛΙΑ",
            "{category}": "4 αστέρων", "{address}": "ΣΠΕΤΣΕΣ 180 50 ΔΗΜΟΣ ΣΠΕΤΣΩΝ",
            "{company}": "ΜΠΟΥΛΑΜΑΤΣΗ Π. ΔΗΜΗΤΡΙΟΣ", "{taxNumber}": "123456789",
            "{medal}": "GOLD", "{score}": "72.40", "{inspectorName}": "Ιωάννης Παπαδόπουλος",
            "{issueDate}": "03/06/2026", "{validUntil}": "02/06/2029",
            "{today}": "03/06/2026", "{place}": "Αθήνα,"
        };
        var html = self.body();
        for (var k in samples) html = html.split(k).join(samples[k]);

        var w = window.open("", "_blank");
        w.document.open();
        w.document.write("<!DOCTYPE html><html lang='el'><head><meta charset='utf-8'><title>Προεπισκόπηση</title></head><body>" + html + "</body></html>");
        w.document.close();
    };

    self.save = function () {
        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            contentType: "application/json",
            data: JSON.stringify({ body: self.body() }),
            url: "/api/AdminCertTemplateApi/SaveTemplate",
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    $("#cert-saved-toast").fadeIn(150).delay(1200).fadeOut(300);
                } else {
                    alert("Σφάλμα αποθήκευσης.");
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.fetchData();
}
