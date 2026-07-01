// Admin: Διαχείριση Πυλώνων / Υποπυλώνων / Κριτηρίων
function AdminCriteriaViewModel() {
    var self = this;

    self.loading = ko.observable(false);
    self.error = ko.observable("");
    self.success = ko.observable("");
    self.pillars = ko.observableArray([]);

    function flash(setter, msg) { setter(msg); setTimeout(function () { setter(""); }, 3500); }
    function numOrNull(v) { if (v === "" || v === null || v === undefined) return null; var n = parseFloat(v); return isNaN(n) ? null : n; }

    function mapPillar(p) { p.expanded = ko.observable(true); (p.subPillars || []).forEach(mapSub); return p; }
    function mapSub(s) { s.expanded = ko.observable(false); return s; }

    self.fetchData = function () {
        self.loading(true);
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/GetTree", dataType: "json",
            success: function (data) {
                self.loading(false);
                if (!data || !data.success) { self.error("Σφάλμα φόρτωσης ή μη εξουσιοδοτημένη πρόσβαση."); return; }
                self.pillars((data.pillars || []).map(mapPillar));
            },
            error: function () { self.loading(false); self.error("Σφάλμα επικοινωνίας."); }
        });
    };

    // ═══════════ Κατηγορία (Πυλώνας/Υποπυλώνας) modal ═══════════
    self.cmId = ko.observable(null);
    self.cmParentID = ko.observable(null);
    self.cmIsSub = ko.observable(false);
    self.cmContext = ko.observable("");
    self.cmTitle = ko.observable("");
    self.cmDescription = ko.observable("");
    self.cmExamples = ko.observable("");
    self.cmOrder = ko.observable(0);
    self.cmTotalUnits = ko.observable("");
    self.cmMaxGrade = ko.observable("");
    self.cmActive = ko.observable(true);
    self.cmError = ko.observable("");

    function openCat(isSub, parentID, context, cat) {
        self.cmIsSub(isSub); self.cmParentID(parentID); self.cmContext(context); self.cmError("");
        if (cat) {
            self.cmId(cat.id); self.cmTitle(cat.title); self.cmDescription(cat.description || "");
            self.cmExamples(cat.examples || ""); self.cmOrder(cat.order);
            self.cmTotalUnits(cat.totalUnits == null ? "" : cat.totalUnits);
            self.cmMaxGrade(cat.maxGrade == null ? "" : cat.maxGrade);
            self.cmActive(cat.isActive);
        } else {
            self.cmId(null); self.cmTitle(""); self.cmDescription(""); self.cmExamples("");
            self.cmOrder(0); self.cmTotalUnits(""); self.cmMaxGrade(""); self.cmActive(true);
        }
        $("#cat-modal").modal("show");
    }

    self.openNewPillar = function () { openCat(false, null, "Νέος Πυλώνας", null); };
    self.openEditPillar = function (p) { openCat(false, null, "Επεξεργασία Πυλώνα", p); };
    self.openNewSubPillar = function (p) { openCat(true, p.id, "Νέος Υποπυλώνας στον: " + p.title, null); };
    self.openEditSubPillar = function (s) { openCat(true, s.parentID, "Επεξεργασία Υποπυλώνα", s); };

    self.saveCategory = function () {
        if (!self.cmTitle().trim()) { self.cmError("Συμπληρώστε τίτλο."); return; }
        var payload = {
            id: self.cmId(), parentID: self.cmIsSub() ? self.cmParentID() : null,
            title: self.cmTitle(), description: self.cmDescription(), examples: self.cmExamples(),
            order: parseInt(self.cmOrder(), 10) || 0,
            totalUnits: numOrNull(self.cmTotalUnits()), maxGrade: numOrNull(self.cmMaxGrade()),
            isActive: self.cmActive()
        };
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/SaveCategory", contentType: "application/json",
            data: JSON.stringify(payload), dataType: "json",
            success: function (r) {
                if (r && r.success) { $("#cat-modal").modal("hide"); self.fetchData(); flash(self.success, "Αποθηκεύτηκε."); }
                else self.cmError((r && r.message) || "Σφάλμα.");
            },
            error: function () { self.cmError("Σφάλμα επικοινωνίας."); }
        });
    };

    self.toggleCategory = function (cat) {
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/ToggleCategory", contentType: "application/json",
            data: JSON.stringify({ id: cat.id, isActive: !cat.isActive }), dataType: "json",
            success: function (r) { if (r && r.success) self.fetchData(); }
        });
    };

    self.deleteCategory = function (cat) {
        if (!confirm("Διαγραφή «" + cat.title + "»;")) return;
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/DeleteCategory", contentType: "application/json",
            data: JSON.stringify({ id: cat.id }), dataType: "json",
            success: function (r) {
                if (r && r.success) { self.fetchData(); flash(self.success, "Διαγράφηκε."); }
                else flash(self.error, (r && r.message) || "Δεν διαγράφηκε.");
            }
        });
    };

    // ═══════════ Κριτήριο modal ═══════════
    self.crId = ko.observable(null);
    self.crCategoryID = ko.observable(null);
    self.crContext = ko.observable("");
    self.crCode = ko.observable("");
    self.crTitle = ko.observable("");
    self.crDescription = ko.observable("");
    self.crOrder = ko.observable(0);
    self.crWeight = ko.observable(1);
    self.crMaxGrade = ko.observable("");
    self.crType = ko.observable("1");
    self.crGradesList = ko.observable("");
    self.crGradesOptions = ko.observable("");
    self.crSelectList = ko.observable("");
    self.crNotes1 = ko.observable("");
    self.crNotes2 = ko.observable("");
    self.crNeedsFiles = ko.observable(false);
    self.crNotApplicable = ko.observable(false);
    self.crIsRequired = ko.observable(false);
    self.crDateFrom = ko.observable("");
    self.crDateTo = ko.observable("");
    self.crError = ko.observable("");

    function todayStr() { var d = new Date(); function p(n) { return (n < 10 ? "0" : "") + n; } return d.getFullYear() + "-" + p(d.getMonth() + 1) + "-" + p(d.getDate()); }

    self.openNewCriterion = function (sub) {
        self.crId(null); self.crCategoryID(sub.id); self.crContext("Νέο κριτήριο στον: " + sub.title); self.crError("");
        self.crCode(""); self.crTitle(""); self.crDescription(""); self.crOrder(0);
        self.crWeight(1); self.crMaxGrade("5"); self.crType("1");
        self.crGradesList(""); self.crGradesOptions(""); self.crSelectList("");
        self.crNotes1(""); self.crNotes2("");
        self.crNeedsFiles(false); self.crNotApplicable(false); self.crIsRequired(false);
        self.crDateFrom(todayStr()); self.crDateTo("2099-12-31");
        $("#crit-modal").modal("show");
    };

    self.openEditCriterion = function (c) {
        self.crId(c.id); self.crCategoryID(c.categoryID); self.crContext("Επεξεργασία: " + c.code); self.crError("");
        self.crCode(c.code || ""); self.crTitle(c.title || ""); self.crDescription(c.description || "");
        self.crOrder(c.order); self.crWeight(c.weight); self.crMaxGrade(c.maxGrade == null ? "" : c.maxGrade);
        self.crType(String(c.criteriaType));
        self.crGradesList(c.gradesList || ""); self.crGradesOptions(c.gradesOptions || ""); self.crSelectList(c.selectList || "");
        self.crNotes1(c.notes1 || ""); self.crNotes2(c.notes2 || "");
        self.crNeedsFiles(!!c.needsFiles); self.crNotApplicable(!!c.notApplicable); self.crIsRequired(!!c.isRequired);
        self.crDateFrom(c.dateFrom || todayStr()); self.crDateTo(c.dateTo || "2099-12-31");
        $("#crit-modal").modal("show");
    };

    self.saveCriterion = function () {
        if (!self.crCode().trim() || !self.crTitle().trim()) { self.crError("Συμπληρώστε κωδικό και τίτλο."); return; }
        var payload = {
            id: self.crId(), categoryID: self.crCategoryID(),
            code: self.crCode(), title: self.crTitle(), description: self.crDescription(),
            order: parseInt(self.crOrder(), 10) || 0, weight: parseInt(self.crWeight(), 10) || 0,
            maxGrade: numOrNull(self.crMaxGrade()), criteriaType: parseInt(self.crType(), 10) || 1,
            gradesList: self.crGradesList(), gradesOptions: self.crGradesOptions(), selectList: self.crSelectList(),
            notes1: self.crNotes1(), notes2: self.crNotes2(),
            needsFiles: self.crNeedsFiles(), notApplicable: self.crNotApplicable(), isRequired: self.crIsRequired(),
            dateFrom: self.crDateFrom(), dateTo: self.crDateTo()
        };
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/SaveCriterion", contentType: "application/json",
            data: JSON.stringify(payload), dataType: "json",
            success: function (r) {
                if (r && r.success) { $("#crit-modal").modal("hide"); self.fetchData(); flash(self.success, "Αποθηκεύτηκε."); }
                else self.crError((r && r.message) || "Σφάλμα.");
            },
            error: function () { self.crError("Σφάλμα επικοινωνίας."); }
        });
    };

    self.setCriterionActive = function (c, active) {
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/SetCriterionActive", contentType: "application/json",
            data: JSON.stringify({ id: c.id, active: active }), dataType: "json",
            success: function (r) { if (r && r.success) self.fetchData(); }
        });
    };
    self.retireCriterion = function (c) { if (confirm("Απόσυρση του κριτηρίου «" + c.code + "»; (δεν θα εμφανίζεται σε νέες αξιολογήσεις)")) self.setCriterionActive(c, false); };
    self.reactivateCriterion = function (c) { self.setCriterionActive(c, true); };

    self.deleteCriterion = function (c) {
        if (!confirm("Οριστική διαγραφή του κριτηρίου «" + c.code + "»;")) return;
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/DeleteCriterion", contentType: "application/json",
            data: JSON.stringify({ id: c.id }), dataType: "json",
            success: function (r) {
                if (r && r.success) { self.fetchData(); flash(self.success, "Διαγράφηκε."); }
                else flash(self.error, (r && r.message) || "Δεν διαγράφηκε.");
            }
        });
    };

    // ═══════════ Τεκμήρια κριτηρίου modal ═══════════
    self.filesCritId = ko.observable(null);
    self.filesContext = ko.observable("");
    self.critFiles = ko.observableArray([]);
    self.showFileForm = ko.observable(false);
    self.fId = ko.observable(null);
    self.fTitle = ko.observable("");
    self.fDescription = ko.observable("");
    self.fActive = ko.observable(true);
    self.fError = ko.observable("");

    self.openFiles = function (c) {
        self.filesCritId(c.id);
        self.filesContext(c.code + " — " + c.title);
        self.showFileForm(false); self.fError("");
        self.loadFiles();
        $("#files-modal").modal("show");
    };

    self.loadFiles = function () {
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/GetCriterionFiles", contentType: "application/json",
            data: JSON.stringify({ criteriaID: self.filesCritId() }), dataType: "json",
            success: function (r) { if (r && r.success) self.critFiles(r.files || []); }
        });
    };

    self.newFile = function () {
        self.fId(null); self.fTitle(""); self.fDescription(""); self.fActive(true); self.fError("");
        self.showFileForm(true);
    };
    self.editFile = function (f) {
        self.fId(f.id); self.fTitle(f.title || ""); self.fDescription(f.description || ""); self.fActive(f.isActive); self.fError("");
        self.showFileForm(true);
    };
    self.cancelFileForm = function () { self.showFileForm(false); };

    self.saveFile = function () {
        if (!self.fTitle().trim()) { self.fError("Συμπληρώστε τίτλο."); return; }
        var payload = { id: self.fId(), criteriaID: self.filesCritId(), title: self.fTitle(), description: self.fDescription(), isActive: self.fActive() };
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/SaveCriterionFile", contentType: "application/json",
            data: JSON.stringify(payload), dataType: "json",
            success: function (r) {
                if (r && r.success) { self.showFileForm(false); self.loadFiles(); self.fetchData(); }
                else self.fError((r && r.message) || "Σφάλμα.");
            },
            error: function () { self.fError("Σφάλμα επικοινωνίας."); }
        });
    };

    self.toggleFile = function (f) {
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/ToggleCriterionFile", contentType: "application/json",
            data: JSON.stringify({ id: f.id, isActive: !f.isActive }), dataType: "json",
            success: function (r) { if (r && r.success) self.loadFiles(); }
        });
    };

    self.deleteFile = function (f) {
        if (!confirm("Διαγραφή του τεκμηρίου «" + f.title + "»;")) return;
        $.ajax({
            type: "POST", url: "/api/AdminCriteriaApi/DeleteCriterionFile", contentType: "application/json",
            data: JSON.stringify({ id: f.id }), dataType: "json",
            success: function (r) {
                if (r && r.success) { self.loadFiles(); self.fetchData(); }
                else flash(self.error, (r && r.message) || "Δεν διαγράφηκε.");
            }
        });
    };

    self.typeLabel = function (t) {
        return t === 1 ? "Ναι/Όχι (υποχρεωτικό)" : t === 2 ? "Αριθμητικό/Επιλογή" : t === 3 ? "Ναι/Όχι (προαιρετικό)" : "—";
    };

    self.fetchData();
}
