// Μητρώο Επιθεωρητών (admin, role=100):
// λίστα, μαζική εισαγωγή CSV, διαχείριση περιοχών δραστηριότητας ανά επιθεωρητή
function AdminInspectorsViewModel() {
    var self = this;

    self.showLoader = function () {
        if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("show");
    };
    self.hideLoader = function () {
        setTimeout(function () {
            if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("hide");
        }, 350);
    };

    self.inspectors = ko.observableArray([]);
    self.importResult = ko.observable(null);

    // ── Φίλτρο αναζήτησης (όνομα / email / ΑΦΜ / τηλέφωνο) ─────────
    self.filterText = ko.observable("");
    self.clearFilter = function () { self.filterText(""); };

    self.filteredInspectors = ko.pureComputed(function () {
        var text = (self.filterText() || "").toLowerCase().trim();
        if (!text) return self.inspectors();

        return self.inspectors().filter(function (i) {
            var haystack = [i.lastName, i.firstName, i.email, i.taxNumber, i.phone, i.areas]
                .join(" ").toLowerCase();
            return haystack.indexOf(text) !== -1;
        });
    });

    // ── Προσκλήσεις ενεργοποίησης λογαριασμού ──────────────────────
    self.inviteBusy = ko.observable(false);

    self.sendInvitation = function (row) {
        if (!confirm("Αποστολή email πρόσκλησης ενεργοποίησης λογαριασμού στον/στην " + row.lastName + " " + row.firstName + " (" + row.email + ");")) return;
        self.inviteBusy(true);
        $.ajax({
            type: "POST", dataType: "json", contentType: "application/json",
            url: "/api/AdminInspectorsApi/SendInvitation",
            data: JSON.stringify({ inspectorID: row.id }),
            success: function (r) {
                self.inviteBusy(false);
                if (r && r.success && r.sent > 0) alert("Η πρόσκληση εστάλη στο " + row.email + ".");
                else if (r && r.success && r.skipped > 0) alert("Ο επιθεωρητής έχει ήδη λογαριασμό ή δεν έχει έγκυρο email.");
                else alert("Η αποστολή απέτυχε. Ελέγξτε το αρχείο σφαλμάτων.");
            },
            error: function () { self.inviteBusy(false); alert("Σφάλμα επικοινωνίας."); }
        });
    };

    self.inviteAll = function () {
        var pending = self.inspectors().filter(function (i) { return !i.hasAccount; }).length;
        if (pending === 0) { alert("Όλοι οι επιθεωρητές έχουν ήδη λογαριασμό."); return; }
        if (!confirm("Αποστολή πρόσκλησης ενεργοποίησης σε " + pending + " επιθεωρητές χωρίς λογαριασμό;")) return;
        self.inviteBusy(true);
        self.showLoader();
        $.ajax({
            type: "POST", dataType: "json", contentType: "application/json",
            url: "/api/AdminInspectorsApi/SendInvitation",
            data: JSON.stringify({ inspectorID: 0 }),
            success: function (r) {
                self.inviteBusy(false); self.hideLoader();
                if (r && r.success) alert("Εστάλησαν " + r.sent + " προσκλήσεις (" + r.skipped + " παραλείφθηκαν" + (r.failed ? ", " + r.failed + " απέτυχαν" : "") + ").");
                else alert("Η αποστολή απέτυχε.");
            },
            error: function () { self.inviteBusy(false); self.hideLoader(); alert("Σφάλμα επικοινωνίας."); }
        });
    };

    // ── Modal περιοχών ─────────────────────────────────────────────
    self.areasInspectorID = ko.observable(null);
    self.areasInspectorName = ko.observable("");
    self.areasPerifereies = ko.observableArray([]);

    // ── Λίστα επιθεωρητών ──────────────────────────────────────────
    self.fetchData = function () {
        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            url: "/api/AdminInspectorsApi/GetInspectors",
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    self.inspectors(data.inspectors || []);
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    // ── Εισαγωγή CSV ───────────────────────────────────────────────
    self.importCsv = function () {
        var input = document.getElementById("csv-file-input");
        if (!input.files || input.files.length === 0) {
            self.importResult({ success: false, message: "Επιλέξτε πρώτα ένα αρχείο CSV.", errors: [] });
            return;
        }

        var formData = new FormData();
        formData.append("csvFile", input.files[0]);

        self.showLoader();
        self.importResult(null);

        $.ajax({
            type: "POST",
            url: "/AdminInspectors/ImportCsv",
            data: formData,
            processData: false,
            contentType: false,
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    self.importResult({
                        success: true,
                        message: "Εισήχθησαν " + data.imported + " επιθεωρητές. Παραλείφθηκαν " + data.skipped + " (διπλοεγγραφές/σφάλματα).",
                        errors: data.errors || []
                    });
                    input.value = "";
                    self.fetchData();
                } else {
                    self.importResult({
                        success: false,
                        message: (data && data.message) || "Σφάλμα κατά την εισαγωγή.",
                        errors: []
                    });
                }
            },
            error: function () {
                self.hideLoader();
                self.importResult({ success: false, message: "Σφάλμα επικοινωνίας με τον server.", errors: [] });
            }
        });
    };

    // ── Περιοχές δραστηριότητας ────────────────────────────────────
    self.openAreas = function (inspector) {
        self.areasInspectorID(inspector.id);
        self.areasInspectorName(inspector.lastName + " " + inspector.firstName);
        self.areasPerifereies([]);

        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            contentType: "application/json",
            data: JSON.stringify({ inspectorID: inspector.id }),
            url: "/api/AdminInspectorsApi/GetInspectorAreas",
            success: function (data) {
                self.hideLoader();
                if (!data || !data.success) return;

                var mapped = (data.perifereies || []).map(function (per) {
                    var coversAll = ko.observable(per.coversAll);

                    var pes = per.pes.map(function (pe) {
                        return {
                            kalID: pe.kalID,
                            title: pe.title,
                            isChecked: ko.observable(pe.isChecked)
                        };
                    });

                    coversAll.subscribe(function (val) {
                        pes.forEach(function (pe) { pe.isChecked(val); });
                    });

                    return {
                        kalID: per.kalID,
                        title: per.title,
                        coversAll: coversAll,
                        pes: ko.observableArray(pes),
                        isExpanded: ko.observable(per.coversAll || pes.some(function (p) { return p.isChecked(); }))
                    };
                });

                self.areasPerifereies(mapped);
                $("#areas-modal").modal("show");
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.toggleExpand = function (per) {
        per.isExpanded(!per.isExpanded());
    };

    self.saveAreas = function () {
        var areas = [];
        self.areasPerifereies().forEach(function (per) {
            if (per.coversAll()) {
                areas.push({ kalID: per.kalID, levelID: 3 });
            } else {
                per.pes().forEach(function (pe) {
                    if (pe.isChecked()) areas.push({ kalID: pe.kalID, levelID: 4 });
                });
            }
        });

        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            contentType: "application/json",
            data: JSON.stringify({ inspectorID: self.areasInspectorID(), areas: areas }),
            url: "/api/AdminInspectorsApi/SaveInspectorAreas",
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    $("#areas-modal").modal("hide");
                    self.fetchData();   // ανανέωση της στήλης περιοχών
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.fetchData();
}
