// Admin: AI Insights — χρήση Συμβούλου, ανάλυση θεμάτων, batch έλεγχος τεκμηρίων,
// scorecard επιθεωρητών (screening — απαιτείται ανθρώπινη αξιολόγηση).
function AdminAiInsightsViewModel() {
    var self = this;

    self.loading = ko.observable(false);
    self.error = ko.observable("");
    self.days = ko.observable(30);

    self.chatStats = ko.observable(null);
    self.docStats = ko.observable(null);
    self.scorecard = ko.observableArray([]);
    self.criteriaQuality = ko.observableArray([]);
    self.recentChats = ko.observableArray([]);

    self.aiReport = ko.observable("");
    self.aiReportDate = ko.observable("");
    self.analyzing = ko.observable(false);

    // Batch
    self.batchRunning = ko.observable(false);
    self.batchStop = false;
    self.batchProcessed = ko.observable(0);
    self.batchRemaining = ko.observable(0);
    self.batchLog = ko.observableArray([]);

    // Drill-down
    self.findingsInspector = ko.observable("");
    self.findings = ko.observableArray([]);
    self.findingsLoading = ko.observable(false);

    self.fetchData = function () {
        self.loading(true);
        $.ajax({
            type: "POST", url: "/api/AdminAiInsightsApi/GetInsights", contentType: "application/json",
            data: JSON.stringify({ days: self.days() }), dataType: "json",
            success: function (r) {
                self.loading(false);
                if (!r || !r.success) { self.error("Σφάλμα φόρτωσης ή μη εξουσιοδοτημένη πρόσβαση."); return; }
                self.chatStats(r.chatStats);
                self.docStats(r.docStats);
                self.scorecard(r.scorecard || []);
                self.criteriaQuality(r.criteriaQuality || []);
                self.recentChats(r.recentChats || []);
                self.aiReport(r.cachedReport || "");
                self.aiReportDate(r.cachedReportDate || "");
                self.batchRemaining(r.docStats ? r.docStats.pendingBatch : 0);
            },
            error: function () { self.loading(false); self.error("Σφάλμα επικοινωνίας."); }
        });
    };

    self.setDays = function (d) { self.days(d); self.fetchData(); };

    // ── AI ανάλυση θεμάτων ─────────────────────────────────────────
    self.analyze = function () {
        if (self.analyzing()) return;
        self.analyzing(true);
        $.ajax({
            type: "POST", url: "/api/AdminAiInsightsApi/AnalyzeChats", contentType: "application/json",
            data: JSON.stringify({ days: self.days() }), dataType: "json",
            success: function (r) {
                self.analyzing(false);
                if (r && r.success) { self.aiReport(r.report); self.aiReportDate(r.date); }
                else alert((r && r.message) || "Σφάλμα ανάλυσης.");
            },
            error: function () { self.analyzing(false); alert("Σφάλμα επικοινωνίας."); }
        });
    };

    // ── Batch έλεγχος τεκμηρίων (επαναληπτικές παρτίδες με progress) ─
    self.startBatch = function () {
        if (self.batchRunning()) return;
        self.batchRunning(true);
        self.batchStop = false;
        self.batchProcessed(0);
        self.batchLog([]);
        self.batchStep();
    };

    self.stopBatch = function () { self.batchStop = true; };

    self.batchStep = function () {
        if (self.batchStop) { self.batchRunning(false); self.fetchData(); return; }
        $.ajax({
            type: "POST", url: "/api/AdminAiInsightsApi/BatchCheckNext", contentType: "application/json",
            data: JSON.stringify({ batchSize: 3 }), dataType: "json",
            success: function (r) {
                if (!r || !r.success) {
                    self.batchRunning(false);
                    alert((r && r.message) || "Σφάλμα batch.");
                    self.fetchData();
                    return;
                }
                self.batchProcessed(self.batchProcessed() + (r.processed || 0));
                self.batchRemaining(r.remaining || 0);
                (r.results || []).forEach(function (x) {
                    self.batchLog.unshift({
                        fileName: x.fileName,
                        verdict: x.success ? x.verdict : "σφάλμα",
                        answerVerdict: x.success ? (x.answerVerdict || "") : (x.message || "")
                    });
                });
                if (r.done) { self.batchRunning(false); self.fetchData(); }
                else self.batchStep();   // επόμενη παρτίδα
            },
            error: function () { self.batchRunning(false); alert("Σφάλμα επικοινωνίας."); }
        });
    };

    // ── Drill-down ευρημάτων επιθεωρητή ────────────────────────────
    self.showFindings = function (row) {
        self.findingsInspector(row.inspectorName);
        self.findings([]);
        self.findingsLoading(true);
        $("#findings-modal").modal("show");
        $.ajax({
            type: "POST", url: "/api/AdminAiInsightsApi/GetInspectorFindings", contentType: "application/json",
            data: JSON.stringify({ inspectorID: row.inspectorID }), dataType: "json",
            success: function (r) {
                self.findingsLoading(false);
                if (r && r.success) self.findings(r.findings || []);
            },
            error: function () { self.findingsLoading(false); }
        });
    };

    self.ratingBadge = function (r) {
        return r === "green" ? "badge bg-success" : r === "yellow" ? "badge bg-warning"
             : r === "red" ? "badge bg-danger" : "badge bg-secondary";
    };
    self.ratingLabel = function (r) {
        return r === "green" ? "Καλή εικόνα" : r === "yellow" ? "Προς παρακολούθηση"
             : r === "red" ? "Απαιτεί έλεγχο" : "Ανεπαρκή στοιχεία";
    };
    self.verdictBadge = function (v) {
        return v === "ok" ? "badge bg-success" : v === "fail" ? "badge bg-danger" : "badge bg-warning";
    };

    self.fetchData();
}
