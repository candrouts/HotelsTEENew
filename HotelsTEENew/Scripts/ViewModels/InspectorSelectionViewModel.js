function InspectorSelectionViewModel() {
    var self = this;

    self.showLoader = function () {

        if (typeof $("#modal-loader").modal === "function") {
            $("#modal-loader").modal("show")
        }

    }

    self.hideLoader = function () {
        if (typeof $("#modal-loader").modal === "function") {
            $("#modal-loader").modal("hide")
        }
    }

    // Φίλτρα
    self.perifereies = ko.observableArray([]);
    self.periferiakesEnotites = ko.observableArray([]);
    self.selectedPerifereia = ko.observable("");
    self.selectedPE = ko.observable("");
    self.searchName = ko.observable("");

    // Αποτελέσματα
    self.inspectors = ko.observableArray([]);
    self.selectedInspector = ko.observable(null);

    // Αίτηση
    self.proposedDate = ko.observable("");
    self.hotelCriteriaID = ko.observable(null);
    self.hotelCriteriaStatus = ko.observable(0);

    self.submitSuccess = ko.observable(false);
    self.existingCertificate = ko.observable(null);
    self.timeline = ko.observableArray([]);

    // Απόρριψη τελικής κατάταξης
    self.rejectComment = ko.observable("");

    // Απόρριψη ανάθεσης από επιθεωρητή
    self.assignmentRejected = ko.observable(false);
    self.rejectedInspectorName = ko.observable("");
    self.assignmentRejectionNote = ko.observable("");

    // Στάδιο τρέχουσας αίτησης (για badge)
    self.currentStageLabel = ko.pureComputed(function () {
        var c = self.existingCertificate();
        if (!c) return "";
        if (c.isIssued) return "Ολοκληρώθηκε - Εκδόθηκε Βεβαίωση";
        if (c.awaitingAcceptance) return "Αναμονή Αποδοχής Τελικής Κατάταξης";
        if (c.v3Status === 1) return "Τελική Κατάταξη σε εξέλιξη";
        if (c.v2Status === 2) return "Η Αυτοψία ολοκληρώθηκε";
        if (c.v2Status === 1) return "Αυτοψία σε εξέλιξη";
        if (c.autopsyDateStatus === 2 || c.autopsyDateStatus === 3) return "Προγραμματισμένη Αυτοψία";
        return "Αναμονή Επιβεβαίωσης Ημερομηνίας";
    });

    // ── Αποδοχή τελικής κατάταξης = έκδοση βεβαίωσης ───────────────
    self.acceptFinal = function () {
        $("#accept-final-modal").modal("hide");
        self.showLoader();
        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/InspectorSelectionApi/AcceptFinal',
            success: function (data) {
                self.hideLoader();
                if (data.success) {
                    $("#accept-success-modal").modal("show");
                } else {
                    $("#submit-error-modal").modal("show");
                }
            },
            error: function () {
                self.hideLoader();
                $("#submit-error-modal").modal("show");
            }
        });
    };

    self.reloadPage = function () {
        window.location.reload();
    };

    // ── Απόρριψη τελικής κατάταξης (με σχόλιο) ─────────────────────
    self.rejectFinal = function () {
        if (!self.rejectComment().trim()) return;

        $("#reject-final-modal").modal("hide");
        self.showLoader();
        $.ajax({
            type: 'POST',
            dataType: 'json',
            contentType: 'application/json',
            data: JSON.stringify({ comment: self.rejectComment() }),
            url: '/api/InspectorSelectionApi/RejectFinal',
            success: function (data) {
                self.hideLoader();
                if (data.success) {
                    self.rejectComment("");
                    self.fetchData();   // ανανέωση κατάστασης
                } else {
                    $("#submit-error-modal").modal("show");
                }
            },
            error: function () {
                self.hideLoader();
                $("#submit-error-modal").modal("show");
            }
        });
    };

    self.selectedPerifereia.subscribe(function (val) {
        self.selectedPE("");
        self.periferiakesEnotites([]);
        self.inspectors([]);
        if (!val) return;

        $.ajax({
            type: 'POST',
            dataType: 'json',
            contentType: 'application/json',
            data: JSON.stringify({ kalID: val }),
            url: '/api/InspectorSelectionApi/GetPerifereiakesEnotites',
            success: function (data) {
                self.periferiakesEnotites(data);
            }
        });
    });

    self.fetchData = function () {
        self.showLoader();
        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/InspectorSelectionApi/GetInitialData',
            success: function (data) {
                self.hideLoader();
                self.perifereies(data.perifereies || []);
                self.hotelCriteriaID(data.hotelCriteriaID);
                self.hotelCriteriaStatus(data.hotelCriteriaStatus);

                if (data.existingCertificate != null) {
                    self.existingCertificate(data.existingCertificate);
                    self.submitSuccess(true);
                } else {
                    self.existingCertificate(null);
                    self.submitSuccess(false);
                }

                // Απόρριψη ανάθεσης από επιθεωρητή → alert + φόρμα αναζήτησης
                if (data.assignmentRejected) {
                    self.assignmentRejected(true);
                    self.rejectedInspectorName(data.rejectedInspectorName || "");
                    self.assignmentRejectionNote(data.rejectionNote || "");
                } else {
                    self.assignmentRejected(false);
                }

                if (data.timeline) {
                    self.timeline(data.timeline);
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.searchInspectors = function () {
    //    self.showLoader();
        $.ajax({
            type: 'POST',
            dataType: 'json',
            contentType: 'application/json',
            data: JSON.stringify({
                perifereaKalID: self.selectedPerifereia(),
                peKalID: self.selectedPE(),
                name: self.searchName()
            }),
            url: '/api/InspectorSelectionApi/GetInspectors',
            success: function (data) {
                self.inspectors(Array.isArray(data) ? data : []);
                self.selectedInspector(null);
     //           self.hideLoader();
            },
            error: function () {
                self.inspectors([]);
     //           self.hideLoader();
            }
        });

    };

    self.selectInspector = function (inspector) {
        self.selectedInspector(inspector);
    };

    self.submitRequest = function () {
        if (!self.selectedInspector()) {
            $("#inspector-required-modal").modal("show");
            return;
        }
        if (!self.proposedDate()) {
            $("#date-required-modal").modal("show");
            return;
        }

        self.showLoader();
        $.ajax({
            type: 'POST',
            dataType: 'json',
            contentType: 'application/json',
            data: JSON.stringify({
                hotelCriteriaID: self.hotelCriteriaID(),
                inspectorID: self.selectedInspector().id,
                proposedDate: self.proposedDate()
            }),
            url: '/api/InspectorSelectionApi/SubmitRequest',
            success: function (data) {
                self.hideLoader();
                if (data.success) {
                    self.submitSuccess(true);
                    var today = new Date();
                    var dd = String(today.getDate()).padStart(2, '0');
                    var mm = String(today.getMonth() + 1).padStart(2, '0');
                    var yyyy = today.getFullYear();
                    self.existingCertificate({
                        submissionDate: dd + '/' + mm + '/' + yyyy,
                        inspectorName: self.selectedInspector().lastName + ' ' + self.selectedInspector().firstName,
                        proposedDate: self.proposedDate()
                    });
                    $("#submit-success-modal").modal("show");
                } else {
                    $("#submit-error-modal").modal("show");
                }
            },
            error: function () {
                self.hideLoader();
                $("#submit-error-modal").modal("show");
            }
        });
    };

    self.fetchData();
}
