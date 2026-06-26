// Admin: διαχείριση κεντρικών ρυθμίσεων/παροχών & αντιστοιχίσεων με κριτήρια
function AdminFeaturesViewModel() {
    var self = this;

    self.loading = ko.observable(false);
    self.error = ko.observable("");
    self.success = ko.observable("");

    self.features = ko.observableArray([]);
    self.criteria = ko.observableArray([]);   // επιλέξιμα (notApplicable=1)

    // Πεδία modal επεξεργασίας ρύθμισης
    self.editFeatureID = ko.observable(null);
    self.editTitle = ko.observable("");
    self.editDescription = ko.observable("");
    self.editIcon = ko.observable("");
    self.editOrder = ko.observable(0);
    self.editActive = ko.observable(true);
    self.modalError = ko.observable("");

    self.criteriaLabel = function (c) {
        return (c.code ? c.code + " — " : "") + c.title;
    };

    function flash(setter, msg) {
        setter(msg);
        setTimeout(function () { setter(""); }, 3500);
    }

    function mapFeature(f) {
        return {
            featureID: f.featureID,
            title: ko.observable(f.title),
            description: ko.observable(f.description),
            icon: ko.observable(f.icon),
            displayOrder: f.displayOrder,
            isActive: ko.observable(f.isActive),
            mappings: ko.observableArray(f.mappings || []),
            newCriteriaID: ko.observable(""),
            newDisableWhenPresent: ko.observable("false")
        };
    }

    self.fetchData = function () {
        self.loading(true);
        $.ajax({
            type: "POST", url: "/api/AdminFeaturesApi/GetData", dataType: "json",
            success: function (data) {
                self.loading(false);
                if (!data || !data.success) { self.error("Σφάλμα φόρτωσης ή μη εξουσιοδοτημένη πρόσβαση."); return; }
                self.criteria(data.criteria || []);
                self.features((data.features || []).map(mapFeature));
            },
            error: function () { self.loading(false); self.error("Σφάλμα επικοινωνίας."); }
        });
    };

    // ── Ρύθμιση (feature) ──────────────────────────────────────────
    self.openNewFeature = function () {
        self.editFeatureID(null);
        self.editTitle(""); self.editDescription(""); self.editIcon("");
        self.editOrder((self.features().length + 1) * 10);
        self.editActive(true); self.modalError("");
        $("#feature-modal").modal("show");
    };

    self.openEditFeature = function (f) {
        self.editFeatureID(f.featureID);
        self.editTitle(f.title()); self.editDescription(f.description());
        self.editIcon(f.icon()); self.editOrder(f.displayOrder);
        self.editActive(f.isActive()); self.modalError("");
        $("#feature-modal").modal("show");
    };

    self.saveFeature = function () {
        if (!self.editTitle().trim()) { self.modalError("Συμπληρώστε τίτλο."); return; }
        var payload = {
            featureID: self.editFeatureID(),
            title: self.editTitle(),
            description: self.editDescription(),
            icon: self.editIcon(),
            displayOrder: parseInt(self.editOrder(), 10) || 0,
            isActive: self.editActive()
        };
        $.ajax({
            type: "POST", url: "/api/AdminFeaturesApi/SaveFeature",
            contentType: "application/json", data: JSON.stringify(payload), dataType: "json",
            success: function (r) {
                if (r && r.success) { $("#feature-modal").modal("hide"); self.fetchData(); flash(self.success, "Αποθηκεύτηκε."); }
                else { self.modalError((r && r.message) || "Σφάλμα αποθήκευσης."); }
            },
            error: function () { self.modalError("Σφάλμα επικοινωνίας."); }
        });
    };

    self.deleteFeature = function (f) {
        if (!confirm("Διαγραφή της ρύθμισης «" + f.title() + "» και των αντιστοιχίσεών της;")) return;
        $.ajax({
            type: "POST", url: "/api/AdminFeaturesApi/DeleteFeature",
            contentType: "application/json", data: JSON.stringify({ featureID: f.featureID }), dataType: "json",
            success: function (r) { if (r && r.success) { self.fetchData(); flash(self.success, "Διαγράφηκε."); } }
        });
    };

    // ── Αντιστοιχίσεις (mappings) ──────────────────────────────────
    self.addMapping = function (f) {
        var cid = f.newCriteriaID();
        if (!cid) { flash(self.error, "Επιλέξτε κριτήριο."); return; }
        var payload = {
            featureID: f.featureID,
            criteriaID: Number(cid),
            disableWhenPresent: f.newDisableWhenPresent() === "true"
        };
        $.ajax({
            type: "POST", url: "/api/AdminFeaturesApi/SaveMapping",
            contentType: "application/json", data: JSON.stringify(payload), dataType: "json",
            success: function (r) {
                if (r && r.success) { f.newCriteriaID(""); f.newDisableWhenPresent("false"); self.fetchData(); }
                else { flash(self.error, (r && r.message) || "Σφάλμα."); }
            },
            error: function () { flash(self.error, "Σφάλμα επικοινωνίας."); }
        });
    };

    self.removeMapping = function (f, m) {
        $.ajax({
            type: "POST", url: "/api/AdminFeaturesApi/DeleteMapping",
            contentType: "application/json", data: JSON.stringify({ mapID: m.mapID }), dataType: "json",
            success: function (r) { if (r && r.success) self.fetchData(); }
        });
    };

    self.fetchData();
}
