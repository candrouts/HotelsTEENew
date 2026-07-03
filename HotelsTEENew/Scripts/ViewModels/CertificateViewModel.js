

function CertificateViewModel() {
    var self = this;

    self.error = ko.observable(false);
    self.errorMessage = ko.observable("");

    self.success = ko.observable(false);
    self.successMessage = ko.observable("");

    self.showLoader = function () {

        $("#modal-loader").modal("show")
    }

    self.hideLoader = function () {
        $("#modal-loader").modal("hide")
    }

    var suc = $("#success").val();
    if (suc != null && suc.length > 0) {

        self.successMessage(suc);
        self.success(true);
    }

    var err = $("#failure").val();
    if (err != null && err.length > 0) {

        self.errorMessage(err);
        self.error(true);
    }

    self.inpsectorID = ko.observable(0);
    self.isAdmin = ko.observable(false);
    self.certificates = ko.observableArray([]);
    self.otable = null;

    // ── Modal απόρριψης ανάθεσης από επιθεωρητή ────────────────────
    self.rejectAssignComment = ko.observable("");
    self.rejectAssignCert = ko.observable(null);
    self.rejectAssignHotel = ko.observable("");

    self.confirmRejectAssignment = function () {
        var c = self.rejectAssignCert();
        if (!c || !self.rejectAssignComment().trim()) return;

        $("#reject-assignment-modal").modal("hide");
        self.showLoader();
        $.ajax({
            type: "POST",
            contentType: "application/json",
            url: "/api/CertificateApi/RejectAssignment",
            data: JSON.stringify({ certificateID: c.certificateID, comment: self.rejectAssignComment() }),
            success: function (response) {
                self.hideLoader();
                if (response && response.success) {
                    self.successMessage("Η ανάθεση απορρίφθηκε. Ο ξενοδόχος θα ειδοποιηθεί να επιλέξει νέο επιθεωρητή.");
                    self.success(true);
                    self.error(false);
                    self.fetchData();   // ανανέωση λίστας (το certificate φεύγει)
                } else {
                    self.errorMessage((response && response.message) || "Σφάλμα απόρριψης.");
                    self.error(true);
                }
            },
            error: function () {
                self.hideLoader();
                self.errorMessage("Σφάλμα επικοινωνίας με τον server.");
                self.error(true);
            }
        });
    };

    // ── Modal σχολίων απόρριψης ξενοδόχου ──────────────────────────
    self.rejectionModalTitle = ko.observable("");
    self.rejectionModalText = ko.observable("");

    self.showRejection = function (c) {
        self.rejectionModalTitle(c.hotelTitle || ("#" + c.certificateID));
        self.rejectionModalText(c.rejectionNote || "");
        $("#rejection-note-modal").modal("show");
    };

    // ── Φίλτρα αναζήτησης ──────────────────────────────────────────
    self.filterText = ko.observable("");
    self.filterStage = ko.observable("");   // "", new, autopsy-due, autopsy, final, completed
    self.filterMedal = ko.observable("");   // "" ή medalID
    self.medals = ko.observableArray([]);

    // Admin φίλτρα: κατηγορία αστέρων / Περιφέρεια / Περιφερειακή Ενότητα
    self.filterCategory = ko.observable("");
    self.filterRegion = ko.observable("");   // periphereiaTitle
    self.filterPE = ko.observable("");        // peripheryTitle

    var distinctSorted = function (selector, predicate) {
        var set = {};
        self.certificates().forEach(function (c) {
            if (predicate && !predicate(c)) return;
            var v = selector(c);
            if (v) set[v] = true;
        });
        return Object.keys(set).sort(function (a, b) { return a.localeCompare(b, "el"); });
    };
    self.categoryOptions = ko.pureComputed(function () { return distinctSorted(function (c) { return c.category; }); });
    self.regionOptions = ko.pureComputed(function () { return distinctSorted(function (c) { return c.periphereiaTitle; }); });
    self.peOptions = ko.pureComputed(function () {
        var region = self.filterRegion();
        return distinctSorted(function (c) { return c.peripheryTitle; },
            function (c) { return !region || c.periphereiaTitle === region; });
    });

    // ── Focus σε μία αίτηση (deep-link από dashboard: ?certId=123) ──
    self.focusCertId = ko.observable("");
    (function () {
        var m = /[?&]certId=(\d+)/.exec(window.location.search);
        if (m) self.focusCertId(m[1]);
    })();
    self.focusActive = ko.pureComputed(function () { return self.focusCertId().length > 0; });
    self.focusHotel = ko.pureComputed(function () {
        var id = self.focusCertId();
        var c = self.certificates().filter(function (x) { return String(x.certificateID) === id; })[0];
        return c ? (c.hotelTitle || ("#" + id)) : ("#" + id);
    });
    self.clearFocus = function () { self.focusCertId(""); };

    self.clearFilters = function () {
        self.filterText("");
        self.filterStage("");
        self.filterMedal("");
        self.filterCategory("");
        self.filterRegion("");
        self.filterPE("");
        self.focusCertId("");
    };



    // -----------------------------------------------------------------
    // Mapping: κάθε certificate αποκτά inline-edit state για την ημ/νία
    // -----------------------------------------------------------------
    self.mapCertificate = function (c) {
        c.autopsyDateTime = ko.observable(c.autopsyDateTime);
        c.isEditingDate = ko.observable(false);
        c.editDateValue = ko.observable("");   // τιμή στο input κατά το edit
        c.dateSaveError = ko.observable("");

        // Κατάσταση ημ/νίας αυτοψίας: 1=εκκρεμεί, 2=αποδοχή, 3=οριστική με αλλαγή
        c.autopsyDateStatus = ko.observable(c.autopsyDateStatus);

        c.isDatePending = ko.pureComputed(function () {
            return c.autopsyDateStatus() === 1;
        });
        c.isDateFinal = ko.pureComputed(function () {
            return c.autopsyDateStatus() === 2 || c.autopsyDateStatus() === 3;
        });

        // ── Workflow state (από τον server) ────────────────────────
        // v2Status/v3Status: null=δεν ξεκίνησε, 1=draft, 2=υποβλήθηκε
        // canDoAutopsy, canDoFinal, isNew, isAutopsyDue: boolean flags

        // Στάδιο ροής για badge + φιλτράρισμα
        c.stage = ko.pureComputed(function () {
            if (c.isIssued) return "completed";                 // εκδόθηκε βεβαίωση
            if (c.v3Status === 2) return "awaiting-acceptance"; // αναμονή αποδοχής ξενοδόχου
            if (c.v3Status === 1) return "final";
            if (c.canDoFinal) return "final";
            if (c.v2Status === 1) return "autopsy";
            if (c.canDoAutopsy) return "autopsy-due";
            // Επιβεβαιωμένη ημ/νία (αποδοχή/αλλαγή) αλλά δεν έφτασε ακόμη η μέρα
            if (c.autopsyDateStatus() === 2 || c.autopsyDateStatus() === 3) return "scheduled";
            return "new";
        });

        c.stageLabel = ko.pureComputed(function () {
            switch (c.stage()) {
                case "completed":           return "Ολοκληρώθηκε - Βεβαίωση";
                case "awaiting-acceptance": return "Αναμονή Αποδοχής Ξενοδόχου";
                case "final":               return "Προς Τελική Κατάταξη";
                case "autopsy":             return "Αυτοψία σε εξέλιξη";
                case "autopsy-due":         return "Προς Αυτοψία";
                case "scheduled":           return "Προγραμματισμένη Αυτοψία";
                default:                    return "Νέα Ανάθεση";
            }
        });

        c.stageBadgeClass = ko.pureComputed(function () {
            switch (c.stage()) {
                case "completed":           return "badge badge-success-lighten";
                case "awaiting-acceptance": return "badge badge-info-lighten";
                case "final":               return "badge badge-info-lighten";
                case "autopsy":             return "badge badge-warning-lighten";
                case "autopsy-due":         return "badge badge-danger-lighten";
                case "scheduled":           return "badge badge-info-lighten";
                default:                    return "badge badge-primary-lighten";
            }
        });

        // Εκκρεμεί διόρθωση μετά από απόρριψη ξενοδόχου
        c.hasRejection = ko.pureComputed(function () {
            return !c.isIssued && c.v3Status === 1 && !!c.rejectionNote;
        });

        // Απόρριψη ανάθεσης: επιτρέπεται μόνο πριν ξεκινήσει η αυτοψία (χωρίς v2)
        c.canRejectAssignment = ko.pureComputed(function () {
            return !c.isIssued && (c.v2Status === null || c.v2Status === undefined);
        });

        c.openRejectAssignment = function () {
            self.rejectAssignComment("");
            self.rejectAssignCert(c);
            self.rejectAssignHotel(c.hotelTitle || ("#" + c.certificateID));
            $("#reject-assignment-modal").modal("show");
        };

        // Αποδοχή της προτεινόμενης ημ/νίας
        c.acceptDate = function () {
            self.showLoader();
            $.ajax({
                type: "POST",
                contentType: "application/json",
                url: "/api/CertificateApi/AcceptAutopsyDate",
                data: JSON.stringify({ certificateID: c.certificateID }),
                success: function (response) {
                    self.hideLoader();
                    if (response && response.success) {
                        c.autopsyDateStatus(2);
                        self.successMessage("Η ημερομηνία αυτοψίας έγινε αποδεκτή.");
                        self.success(true);
                        self.error(false);
                    } else {
                        self.errorMessage(response.message || "Σφάλμα αποδοχής.");
                        self.error(true);
                    }
                },
                error: function () {
                    self.hideLoader();
                    self.errorMessage("Σφάλμα επικοινωνίας με τον server.");
                    self.error(true);
                }
            });
        };

        // Ξεκινά inline edit: γεμίζει το input με την τρέχουσα τιμή
        c.startEditDate = function () {
            c.editDateValue(c.autopsyDateTime() || "");
            c.dateSaveError("");
            c.isEditingDate(true);

            setTimeout(function () {
                var input = document.getElementById(c.certificateID);
                if (input) {
                    input.focus();
                    input.select(); 
                }
            }, 0);
        };

        // Ακυρώνει χωρίς αποθήκευση
        c.cancelEditDate = function () {
            c.isEditingDate(false);
            c.dateSaveError("");
        };

        // Αποθηκεύει μέσω AJAX
        c.saveDate = function () {
            var newDate = c.editDateValue();

            // Βασικό validation: dd/MM/yyyy
            var datePattern = /^\d{2}\/\d{2}\/\d{4}$/;
            if (!datePattern.test(newDate)) {
                c.dateSaveError("Μορφή: ηη/ΜΜ/εεεε");
                return;
            }

            self.showLoader();

            $.ajax({
                type: "POST",
                contentType: "application/json",
                url: "/api/CertificateApi/UpdateAutopsyDate",
                data: JSON.stringify({
                    certificateID: c.certificateID,
                    autopsyDateTime: newDate
                }),
                success: function (response) {
                    self.hideLoader();
                    if (response && response.success) {

                        c.autopsyDateTime(newDate);
                        c.autopsyDateStatus(3);   // αλλαγή από επιθεωρητή = οριστική
                        c.isEditingDate(false);
                        c.dateSaveError("");

                        self.successMessage("Η ημερομηνία αυτοψίας ενημερώθηκε και οριστικοποιήθηκε.");
                        self.success(true);
                        self.error(false);
                    } else {
                        c.dateSaveError(response.message || "Σφάλμα αποθήκευσης.");
                    }
                },
                error: function () {
                    self.hideLoader();
                    c.dateSaveError("Σφάλμα επικοινωνίας με τον server.");
                }
            });
        };

        return c;
    };


    // -----------------------------------------------------------------
    // Φιλτραρισμένη λίστα
    // -----------------------------------------------------------------
    self.filteredCertificates = ko.pureComputed(function () {
        // Focus deep-link: εμφάνισε μόνο τη συγκεκριμένη αίτηση
        if (self.focusActive()) {
            var fid = self.focusCertId();
            return self.certificates().filter(function (c) { return String(c.certificateID) === fid; });
        }

        var text = (self.filterText() || "").toLowerCase().trim();
        var stage = self.filterStage();
        var medal = self.filterMedal();
        var cat = self.filterCategory();
        var region = self.filterRegion();
        var pe = self.filterPE();

        return self.certificates().filter(function (c) {
            if (stage && c.stage() !== stage) return false;
            if (medal && String(c.medalID) !== String(medal)) return false;
            if (cat && c.category !== cat) return false;
            if (region && c.periphereiaTitle !== region) return false;
            if (pe && c.peripheryTitle !== pe) return false;

            if (text) {
                var haystack = [
                    c.hotelTitle, c.exploitingCompanyName, c.taxNumber,
                    c.municipalityTitle, c.address, c.email,
                    "#" + c.certificateID
                ].join(" ").toLowerCase();

                if (haystack.indexOf(text) === -1) return false;
            }
            return true;
        });
    });

    // Counters για τα φίλτρα
    self.countByStage = function (stage) {
        return ko.pureComputed(function () {
            return self.certificates().filter(function (c) { return c.stage() === stage; }).length;
        });
    };
    self.countNew = self.countByStage("new");
    self.countAutopsyDue = self.countByStage("autopsy-due");

    // -----------------------------------------------------------------
    // Paging (client-side, πάνω στη φιλτραρισμένη λίστα)
    // -----------------------------------------------------------------
    self.pageSize = ko.observable(10);
    self.currentPage = ko.observable(1);

    self.totalPages = ko.pureComputed(function () {
        return Math.max(1, Math.ceil(self.filteredCertificates().length / self.pageSize()));
    });

    // Αν αλλάξουν φίλτρα ή pageSize, επιστροφή στην 1η σελίδα
    self.filterText.subscribe(function () { self.currentPage(1); });
    self.filterStage.subscribe(function () { self.currentPage(1); });
    self.filterMedal.subscribe(function () { self.currentPage(1); });
    self.filterCategory.subscribe(function () { self.currentPage(1); });
    self.filterRegion.subscribe(function () { self.filterPE(""); self.currentPage(1); });
    self.filterPE.subscribe(function () { self.currentPage(1); });
    self.pageSize.subscribe(function () { self.currentPage(1); });

    self.pagedCertificates = ko.pureComputed(function () {
        var page = Math.min(self.currentPage(), self.totalPages());
        var size = self.pageSize();
        var start = (page - 1) * size;
        return self.filteredCertificates().slice(start, start + size);
    });

    // Αριθμοί σελίδων για τα κουμπιά (max 5 ορατές)
    self.pageNumbers = ko.pureComputed(function () {
        var total = self.totalPages();
        var current = Math.min(self.currentPage(), total);
        var start = Math.max(1, current - 2);
        var end = Math.min(total, start + 4);
        start = Math.max(1, end - 4);

        var pages = [];
        for (var i = start; i <= end; i++) pages.push(i);
        return pages;
    });

    self.goToPage = function (page) {
        if (page >= 1 && page <= self.totalPages()) self.currentPage(page);
    };
    self.prevPage = function () { self.goToPage(self.currentPage() - 1); };
    self.nextPage = function () { self.goToPage(self.currentPage() + 1); };

    // "Εμφάνιση 1-10 από 35"
    self.pagingInfo = ko.pureComputed(function () {
        var total = self.filteredCertificates().length;
        if (total === 0) return "";
        var page = Math.min(self.currentPage(), self.totalPages());
        var start = (page - 1) * self.pageSize() + 1;
        var end = Math.min(page * self.pageSize(), total);
        return "Εμφάνιση " + start + "-" + end + " από " + total;
    });

    // -----------------------------------------------------------------
    // fetchData
    // -----------------------------------------------------------------

    self.fetchData = function () {

        

        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/CertificateApi/GetAllCertificates',
            success: function (data) {

                if (self.otable != null) {
                    self.otable.destroy();
                    $('#certificates tbody').empty();
                    self.otable = null;
                }


                if (data != null) {
                    self.inpsectorID(data.user.tee_inspectorID);
                    self.isAdmin(data.user.role === 100);
                    self.medals(data.medals || []);
                    self.certificates((data.certificates || []).map(self.mapCertificate));
                }


            }
        });
    }

   


    self.fetchData();
}



function ViewCertificateViewModel() {
    var self = this;

    self.error = ko.observable(false);
    self.errorMessage = ko.observable("");

    self.success = ko.observable(false);
    self.successMessage = ko.observable("");

    self.showLoader = function () {

        // Static overlay (αν υπάρχει στη σελίδα) — αλλιώς το modal
        if ($("#page-loader").length) {
            $("#page-loader").css("display", "flex");
            return;
        }

        if (typeof $("#modal-loader").modal === "function") {
            $("#modal-loader").modal("show")
        }

    }

    self.hideLoader = function () {
        $("#page-loader").hide();

        if (typeof $("#modal-loader").modal === "function") {
            $("#modal-loader").modal("hide")
        }
    }

    var suc = $("#success").val();
    if (suc != null && suc.length > 0) {

        self.successMessage(suc);
        self.success(true);
    }

    var err = $("#failure").val();
    if (err != null && err.length > 0) {

        self.errorMessage(err);
        self.error(true);
    }

    self.certificateID = ko.observable(null);
    var id = $("#model").val();
    self.certificateID(parseInt(id));

    // mode: 1 = προβολή αυτοαξιολόγησης (read-only), 2 = αυτοψία (v2), 3 = τελική κατάταξη (v3)
    self.mode = ko.observable(2);
    var m = parseInt($("#mode").val());
    if (m === 1 || m === 2 || m === 3) self.mode(m);

    // read-only (mode=1 ή ξενοδόχος που βλέπει τον κύκλο του)
    self.readOnly = ko.observable($("#ro").val() === "1" || self.mode() === 1);

    self.hotelID = ko.observable("");
    self.exploitingCompanyID = ko.observable("");
    self.category = ko.observable("");
    self.hotelTitle = ko.observable("");
    self.hotelType = ko.observable("");
    self.totalRooms = ko.observable(0);
    self.totalBeds = ko.observable(0);

    self.peaNotApplicable = ko.observable(false);
    self.medals = ko.observableArray([]);

    self.usePillarThresholds = ko.observable(false);
    self.medalThresholds = ko.observableArray([]);

    self.categories = ko.observableArray([]);
    self.chart = null;

    self.enableAll = ko.observable(true);
    self.hotelCriteriaID = ko.observable(null);
    self.samplingText = ko.observable("");

    // για το modal στον έλεγχο των κριτηρίων
    self.requiredModalMessage = ko.observable("");
    self.requiredModalItems = ko.observableArray([]);

    self.hotelCriteriaID = ko.observable(null);

    // ── Κεντρικές ρυθμίσεις/παροχές καταλύματος (κάρτα κορυφής) ──────
    self.features = ko.observableArray([]);
    self.featureMaps = [];
    self.criteriaIndex = {};
    self.featuresCollapsed = ko.observable(false);
    self.hasFeatures = ko.pureComputed(function () { return self.features().length > 0; });
    self.allFeaturesAnswered = ko.pureComputed(function () {
        return self.features().every(function (f) { return f.answered(); });
    });
    self.disabledByFeaturesCount = ko.observable(0);

    self.setFeature = function (f, value) {
        if (self.readOnly() || !self.enableAll()) return;   // μόνο κατά την ενεργή αυτοψία
        f.hasFeature(value);
        f.answered(true);
        self.saveFeatureAnswer(f);
        self.applyFeatureRules();
    };

    self.saveFeatureAnswer = function (f) {
        if (!self.hotelCriteriaID()) return;
        $.ajax({
            type: "POST", url: "/api/CertificateApi/SaveFeatureAnswer",
            contentType: "application/json",
            data: JSON.stringify({
                hotelCriteriaID: self.hotelCriteriaID(),
                featureID: f.featureID,
                hasFeature: f.hasFeature()
            }),
            dataType: "json"
        });
    };

    self.applyFeatureRules = function () {
        var byCrit = {};
        (self.featureMaps || []).forEach(function (m) {
            (byCrit[m.criteriaID] = byCrit[m.criteriaID] || []).push(m);
        });
        var featById = {};
        self.features().forEach(function (f) { featById[f.featureID] = f; });

        var disabledCount = 0;
        Object.keys(byCrit).forEach(function (cid) {
            var crit = self.criteriaIndex[cid];
            if (!crit) return;
            crit.featureMapped(true);

            var disabled = false, reasons = [];
            byCrit[cid].forEach(function (m) {
                var f = featById[m.featureID];
                if (!f || !f.answered()) return;
                var present = f.hasFeature() === true;
                var thisDisable = m.disableWhenPresent ? present : !present;
                if (thisDisable) { disabled = true; reasons.push(f.title); }
            });

            crit.featureDisabled(disabled);
            crit.isApplicable(!disabled);
            if (disabled) {
                disabledCount++;
                crit.featureReason("Δεν ισχύει — " + reasons.join(", "));
                crit.isChecked(false);
                crit.isNotChecked(false);
                crit.value(null);
            } else {
                crit.featureReason("");
            }
        });
        self.disabledByFeaturesCount(disabledCount);
    };

    // ── AI Προ-έλεγχος Τεκμηρίων ─────────────────────────────────────
    self.aiEnabled = ko.observable(false);
    self.aiFileIndex = {};   // fileID -> uploaded file object

    self.initAiFile = function (f) {
        f.aiVerdict = ko.observable(null);        // ok | warn | fail | null (είδος τεκμηρίου)
        f.aiAnswerVerdict = ko.observable(null);  // supported | unclear | contradicts | na (κάλυψη απάντησης)
        f.aiSummary = ko.observable("");
        f.aiChecking = ko.observable(false);
        self.aiFileIndex[f.id()] = f;
    };

    self.aiBadgeClass = function (v) {
        return v === "ok" ? "badge bg-success" : v === "fail" ? "badge bg-danger" : "badge bg-warning";
    };
    self.aiBadgeLabel = function (v) {
        return v === "ok" ? "AI Είδος: Κατάλληλο" : v === "fail" ? "AI Είδος: Ακατάλληλο" : "AI Είδος: Με επιφύλαξη";
    };
    self.aiAnswerBadgeClass = function (v) {
        return v === "supported" ? "badge bg-success"
             : v === "contradicts" ? "badge bg-danger"
             : "badge bg-warning";
    };
    self.aiAnswerBadgeLabel = function (v) {
        return v === "supported" ? "AI Απάντηση: Υποστηρίζεται"
             : v === "contradicts" ? "AI Απάντηση: Αντικρούεται"
             : "AI Απάντηση: Ασαφές";
    };

    self.fetchAiChecks = function () {
        if (!self.hotelCriteriaID()) return;
        $.ajax({
            type: "POST", url: "/api/AiApi/GetDocumentChecks", contentType: "application/json",
            data: JSON.stringify({ hotelCriteriaID: self.hotelCriteriaID() }), dataType: "json",
            success: function (r) {
                if (!r || !r.success) return;
                self.aiEnabled(r.aiEnabled === true);
                (r.checks || []).forEach(function (c) {
                    var f = self.aiFileIndex[c.fileID];
                    if (f) { f.aiVerdict(c.verdict); f.aiAnswerVerdict(c.answerVerdict || null); f.aiSummary(c.summary || ""); }
                });
            }
        });
    };

    self.runAiCheck = function (f) {
        if (f.aiChecking()) return;
        f.aiChecking(true);
        $.ajax({
            type: "POST", url: "/api/AiApi/CheckDocument", contentType: "application/json",
            data: JSON.stringify({ fileID: f.id() }), dataType: "json",
            success: function (r) {
                f.aiChecking(false);
                if (r && r.success) { f.aiVerdict(r.verdict); f.aiAnswerVerdict(r.answerVerdict || null); f.aiSummary(r.summary || ""); }
                else { f.aiVerdict("warn"); f.aiAnswerVerdict(null); f.aiSummary((r && r.message) || "Σφάλμα ελέγχου."); }
            },
            error: function () {
                f.aiChecking(false);
                f.aiVerdict("warn"); f.aiAnswerVerdict(null); f.aiSummary("Σφάλμα επικοινωνίας.");
            }
        });
    };

    self.fetchData = function () {

        //setTimeout(function () { self.showLoader(); }, 10);
        self.showLoader();

        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/CertificateApi/GetCertificate/' + self.certificateID() + '/' + self.mode(),
            success: function (data) {

                if (!data || !data.hotelDetails) {
                    self.hideLoader();
                    return;
                }

                self.hotelID(data.hotelDetails.hotelID);
                self.exploitingCompanyID(data.hotelDetails.exploitingCompanyID);


                // Read-only: mode=1, ξενοδόχος (ro=1),
                // ή όταν η τρέχουσα έκδοση έχει ήδη υποβληθεί οριστικά
                if (self.readOnly()) {
                    self.enableAll(false);
                }
                if (data.hotelCriteria != null && data.hotelCriteria.version == self.mode() && data.hotelCriteria.status == 2) {
                    self.enableAll(false);
                }
                if (data.hotelCriteria != null) {
                    self.hotelCriteriaID(data.hotelCriteria.id);
                }

                // Κεντρικές ρυθμίσεις/παροχές: κάρτα + αντιστοιχίσεις + απαντήσεις έκδοσης
                self.criteriaIndex = {};
                self.featureMaps = data.featureMaps || [];
                var answersMap = {};
                (data.featureAnswers || []).forEach(function (a) { answersMap[a.featureID] = a.hasFeature; });
                self.features((data.features || []).map(function (f) {
                    var answered = answersMap.hasOwnProperty(f.featureID);
                    return {
                        featureID: f.featureID,
                        title: f.title,
                        description: f.description,
                        icon: f.icon,
                        answered: ko.observable(answered),
                        hasFeature: ko.observable(answered ? !!answersMap[f.featureID] : null)
                    };
                }));
                self.featuresCollapsed(self.features().length > 0 && self.allFeaturesAnswered());

                self.medals(data.medals);
                self.usePillarThresholds(data.usePillarThresholds === true);
                self.medalThresholds(data.medalThresholds || []);

                for (var i = 0; i < data.categories.length; i++) {
                    var category = data.categories[i];

                    var newCategory = {};

                    newCategory.id = ko.observable(category.id);
                    newCategory.order = ko.observable(category.order);
                    newCategory.title = ko.observable(category.title);
                    newCategory.totalUnits = ko.observable(category.totalUnits);
                    //newCategory.maxGrade = ko.observable(category.maxGrade);

                    newCategory.categories = ko.observableArray([]);

                    if (category.categories.length > 0) {
                        for (var j = 0; j < category.categories.length; j++) {
                            var subCategory = category.categories[j];

                            var newSubCategory = {};

                            newSubCategory.id = ko.observable(subCategory.id);
                            newSubCategory.order = ko.observable(subCategory.order);
                            newSubCategory.title = ko.observable(subCategory.title);
                            newSubCategory.description = ko.observable(subCategory.description);
                            newSubCategory.examples = ko.observable(subCategory.examples);
                            //  newSubCategory.maxGrade = ko.observable(subCategory.maxGrade);
                            newSubCategory.maxCriteria = ko.observable(subCategory.criteria.length);
                            newSubCategory.percentSelectCriteria = ko.observable(0);
                            newSubCategory.criteria = ko.observableArray([]);

                            if (subCategory.criteria != null && subCategory.criteria.length > 0) {

                                for (var h = 0; h < subCategory.criteria.length; h++) {
                                    var criteria = subCategory.criteria[h];

                                    var newCriteria = {};

                                    newCriteria.id = ko.observable(criteria.id);
                                    newCriteria.title = ko.observable(criteria.title);
                                    newCriteria.description = ko.observable(criteria.description || '');
                                    newCriteria.categoryID = ko.observable(criteria.categoryID);
                                    newCriteria.order = ko.observable(criteria.order);
                                    newCriteria.code = ko.observable(criteria.code);
                                    newCriteria.criteriaType = ko.observable(criteria.criteriaType);
                                    newCriteria.maxGrade = ko.observable(criteria.maxGrade);
                                    newCriteria.weight = ko.observable(criteria.weight);

                                    newCriteria.gradesList = ko.observable(criteria.gradesList);
                                    newCriteria.selectList = ko.observable(criteria.selectList || '[]');
                                    newCriteria.notes1 = ko.observable(criteria.notes1);
                                    newCriteria.notes2 = ko.observable(criteria.notes2);
                                    newCriteria.needsFiles = ko.observable(criteria.needsFiles);
                                    newCriteria.notApplication = ko.observable(false);
                                    newCriteria.isRequired = ko.observable(criteria.isRequired);

                                    newCriteria.options = ko.observableArray([]);
                                    //newCriteria.value = ko.observable();
                                    // newCriteria.expandPEA = ko.observable(true);

                                    if (criteria.isChecked == null) {
                                        criteria.isChecked = false;
                                    }

                                    if (criteria.isNotChecked == null) {
                                        criteria.isNotChecked = false;
                                    }

                                    newCriteria.value = ko.observable(criteria.value);
                                    newCriteria.isChecked = ko.observable(criteria.isChecked);
                                    newCriteria.isNotChecked = ko.observable(criteria.isNotChecked);
                                    newCriteria.notApplicable = ko.observable(criteria.notApplicable)
                                    newCriteria.isApplicable = ko.observable(true);

                                    // Έλεγχος μέσω κεντρικών ρυθμίσεων (κάρτα κορυφής)
                                    newCriteria.featureMapped = ko.observable(false);
                                    newCriteria.featureDisabled = ko.observable(false);
                                    newCriteria.featureReason = ko.observable("");



                                    // Έλεγχος για ην υποχρεωτικότητα των κριτηρίων

                                    newCriteria.isYesNoType = ko.pureComputed(function () {
                                        return this.criteriaType() == 1 || this.criteriaType() == 3;
                                    }, newCriteria);

                                    newCriteria.isAnswered = ko.pureComputed(function () {
                                        // if (!this.expandPEA()) return true;        // δεν μετράει
                                        if (!this.isApplicable()) return true;     // αν δεν εφαρμόζεται, θεωρείται done

                                        if (this.isYesNoType()) {
                                            return this.isChecked() === true || this.isNotChecked() === true;
                                        }
                                        if (this.criteriaType() === 2) {
                                            var v = this.value();
                                            return v !== null && v !== undefined && v !== '';
                                        }
                                        return true;
                                    }, newCriteria);

                                    newCriteria.isDisabled = ko.pureComputed(function () {
                                        return !this.isApplicable();
                                    }, newCriteria);

                                    newCriteria.isHeritage = ko.pureComputed(function () {
                                        return this.criteriaType() === 3 && this.isNotChecked();
                                    }, newCriteria);

                                    newCriteria.canShowNotApplicableSwitch = ko.pureComputed(function () {
                                        var beds = parseInt(self.totalBeds() || 0, 10);
                                        var code = this.code();
                                        return this.notApplicable() === true;
                                        /*&& !(beds >= 100 && (code === "ΔΑ_ΣΑ_1" || code === "ΔΑ_ΣΑ_2"));*/
                                    }, newCriteria);

                                    //if (newCriteria.code().startsWith('_ΔΕ_') == true) {
                                    //    newCriteria.expandPEA(false);
                                    //    newCriteria.isApplicable(false)
                                    //}

                                    //if (self.peaNotApplicable() == true && newCriteria.code().startsWith('_ΔΕ_') == true) {
                                    //    newCriteria.expandPEA(true);
                                    //}


                                    newCriteria.enabled = ko.observable(true);

                                    newCriteria.isExpanded = ko.observable(false);

                                    newCriteria.displayedDescription = ko.pureComputed(function () {
                                        var s = this.description();
                                        return this.isExpanded() ? s : (s.length > 150 ? s.substring(0, 150) : s);
                                    }, newCriteria);


                                    newCriteria.canToggle = ko.pureComputed(function () {
                                        return this.description().length > 150;
                                    }, newCriteria);


                                    newCriteria.toggleExpand = function (vm, e) {
                                        vm.isExpanded(!vm.isExpanded());
                                        if (e) { e.preventDefault(); e.stopPropagation(); }
                                        return false;
                                    };



                                    if (newCriteria.criteriaType() === 2) {

                                        const list = JSON.parse(newCriteria.selectList());
                                        list.forEach(o => o.value = Number(o.value));
                                        newCriteria.options(list);

                                        // αν required, αφαίρώ το 0 από τις επιλογές
                                        if (newCriteria.isRequired && newCriteria.isRequired()) {
                                            newCriteria.options(list.filter(o => o.value !== 0));
                                        } else {
                                            newCriteria.options(list);
                                        }

                                        newCriteria.isAnsweredNonZero = ko.pureComputed(function () {

                                            var v = this.value();

                                            // Αν δεν έχει επιλεγεί τίποτα
                                            if (v === null || v === undefined || v === '') return false;

                                            var num = Number(v);
                                            if (isNaN(num)) return false;

                                            // Δεν επιτρέπεται 0
                                            return num !== 0;

                                        }, newCriteria);
                                    }

                                    // Αυτό είναι ο βασικός κανόνας για τα υποχρεωτικά:
                                    newCriteria.isValidRequired = ko.pureComputed(function () {
                                        if (!this.isRequired()) return true;
                                        // if (!this.expandPEA()) return true;
                                        if (!this.isApplicable()) return true;

                                        if (this.isYesNoType()) {
                                            return this.isChecked() === true;  // υποχρεωτικό => πρέπει να είναι ΝΑΙ
                                        }
                                        if (this.criteriaType() === 2) {
                                            return this.isAnsweredNonZero();  // required list => επιλεγμένο ΚΑΙ όχι 0
                                        }
                                        return true;
                                    }, newCriteria);

                                    newCriteria.isApplicable.subscribe(function (newValue) {

                                        if (newValue == true) {
                                            this.enabled(true);
                                        }
                                        else if (newValue == false) {

                                            this.enabled(false);
                                            this.value(null);
                                            this.isChecked(false);
                                            this.isNotChecked(false);
                                        }
                                    }, newCriteria);

                                    //newCriteria.isApplicable.subscribe(function (newValue) {

                                    //    if (newValue == true) {


                                    //        this.enabled(true);
                                    //        //if (this.code() == 'ΑΠ_ΒΣ_1') {
                                    //        //    for (var i = 0; i < self.categories().length; i++) {
                                    //        //        var categories = self.categories()[i];

                                    //        //        if (categories.id() == 2) {

                                    //        //            for (var h = 0; h < categories.categories().length; h++) {
                                    //        //                var subCategory = categories.categories()[h];

                                    //        //                if (subCategory.id() == 11) {

                                    //        //                    for (var j = 0; j < subCategory.criteria().length; j++) {
                                    //        //                        var criteria = subCategory.criteria()[j];
                                    //        //                        if (criteria.code().startsWith('_ΔΕ_') == true) {

                                    //        //                            criteria.expandPEA(false);
                                    //        //                            criteria.isApplicable(false);
                                    //        //                        }
                                    //        //                    }
                                    //        //                    break;
                                    //        //                }
                                    //        //            }
                                    //        //            break;
                                    //        //        }

                                    //        //    }
                                    //        //}
                                    //    }
                                    //    else if (newValue == false) {

                                    //        this.enabled(false);
                                    //        this.value(null);
                                    //        this.isChecked(false);
                                    //        this.isNotChecked(false);
                                    //        //if (this.code() == 'ΑΠ_ΒΣ_1') {
                                    //        //    for (var i = 0; i < self.categories().length; i++) {
                                    //        //        var categories = self.categories()[i];

                                    //        //        if (categories.id() == 2) {

                                    //        //            for (var h = 0; h < categories.categories().length; h++) {
                                    //        //                var subCategory = categories.categories()[h];

                                    //        //                if (subCategory.id() == 11) {

                                    //        //                    for (var j = 0; j < subCategory.criteria().length; j++) {
                                    //        //                        var criteria = subCategory.criteria()[j];
                                    //        //                        if (criteria.code().startsWith('_ΔΕ_') == true) {

                                    //        //                            criteria.expandPEA(true);
                                    //        //                            criteria.isApplicable(true);
                                    //        //                        }
                                    //        //                    }
                                    //        //                    break;
                                    //        //                }
                                    //        //            }
                                    //        //            break;
                                    //        //        }

                                    //        //    }
                                    //        //}
                                    //    }
                                    //}, newCriteria);


                                    newCriteria.files = ko.observableArray([]);
                                    if (newCriteria.needsFiles() == true) {
                                        for (var q = 0; q < criteria.files.length; q++) {
                                            var file = criteria.files[q];

                                            var newFile = {};

                                            newFile.id = ko.observable(file.id);
                                            newFile.title = ko.observable(file.title);
                                            newFile.description = ko.observable(file.description);
                                            newFile.files = ko.observableArray([]);
                                            newFile.uploadedFiles = ko.observableArray([]);

                                            if (file.files != null && file.files.length > 0) {
                                                for (var f = 0; f < file.files.length; f++) {
                                                    var fi = file.files[f];

                                                    var newFileFile = {};
                                                    newFileFile.fileName = ko.observable(fi.fileName);
                                                    newFileFile.id = ko.observable(fi.id);
                                                    self.initAiFile(newFileFile);

                                                    newFile.uploadedFiles.push(newFileFile);


                                                }
                                            }
                                            newCriteria.files.push(newFile);
                                        }
                                    }

                                    newCriteria.isApplicable(criteria.isApplicable);

                                    if (self.totalBeds() >= 100 && (newCriteria.code() === "ΔΑ_ΣΑ_1" || newCriteria.code() === "ΔΑ_ΣΑ_2")) {
                                        newCriteria.isRequired(true);
                                    }

                                    if (newCriteria.code() == 'ΑΔ_ΠΔ_5' && self.hotelType() != 'ΠΑΡΑΔΟΣΙΑΚΟ ΞΕΝΟΔΟΧΕΙΟ') {
                                        newCriteria.isApplicable(false);
                                    }

                                    //if (newCriteria.code() == 'ΑΠ_ΒΣ_1' && newCriteria.isApplicable() == false) {
                                    //    self.peaNotApplicable(true);
                                    //}

                                    newSubCategory.criteria.push(newCriteria);
                                    self.criteriaIndex[newCriteria.id()] = newCriteria;

                                }
                            }


                            newSubCategory.totalPoints = ko.computed(function () {

                                var points = 0;

                                if (this.criteria().length > 0) {
                                    for (var i = 0; i < this.criteria().length; i++) {
                                        var criteria = this.criteria()[i];

                                        //if (self.totalRooms() > 40 && ( criteria.code() == "ΔΥ_WM_3" || criteria.code() == "ΔΥ_WM_4" || criteria.code() == "ΔΥ_WM_5")) {
                                        //    criteria.weight(2);
                                        //}

                                        if (criteria.criteriaType() == 1 || criteria.criteriaType() == 3) {
                                            if (criteria.isChecked() == true) {
                                                points += criteria.weight() * criteria.maxGrade();
                                            }
                                        }
                                        else if (criteria.criteriaType() == 2) {
                                            if (criteria.value() != null) {
                                                points += criteria.weight() * parseFloat(criteria.value());
                                            }
                                        }

                                    }
                                }

                                return points;

                            }, newSubCategory);


                            newSubCategory.maxGrade = ko.computed(function () {

                                var points = 0;

                                if (this.criteria().length > 0) {
                                    for (var i = 0; i < this.criteria().length; i++) {
                                        var criteria = this.criteria()[i];

                                        //if (self.totalRooms() > 40 && (criteria.code() == "ΔΥ_WM_3" || criteria.code() == "ΔΥ_WM_4" || criteria.code() == "ΔΥ_WM_5")) {
                                        //    criteria.weight(2);
                                        //}

                                        /* if (!criteria.expandPEA()) continue;*/

                                        if (criteria.criteriaType() == 1) {
                                            if (criteria.isApplicable() == true) {
                                                points += criteria.weight() * criteria.maxGrade();

                                            }
                                        }
                                        else if (criteria.criteriaType() == 3) {
                                            if (criteria.isChecked() == true) {
                                                points += criteria.weight() * criteria.maxGrade();

                                            }
                                        }
                                        else if (criteria.criteriaType() == 2) {
                                            if (criteria.isApplicable() == true) {
                                                points += criteria.weight() * criteria.maxGrade();
                                            }
                                        }

                                    }
                                }

                                return points;

                            }, newSubCategory);


                            newSubCategory.totalTrackable = ko.pureComputed(function () {
                                var n = 0;
                                for (var i = 0; i < this.criteria().length; i++) {
                                    var c = this.criteria()[i];
                                    n++;
                                }
                                return n;
                            }, newSubCategory);


                            newSubCategory.completedCriteria = ko.pureComputed(function () {
                                var done = 0;
                                for (var i = 0; i < this.criteria().length; i++) {
                                    var c = this.criteria()[i];
                                    /* if (!c.expandPEA()) continue;*/

                                    if (!c.isApplicable()) {
                                        done++;
                                        continue;
                                    }

                                    if (c.criteriaType() === 1 || c.criteriaType() == 3) {
                                        if (c.isChecked() === true || c.isNotChecked() === true) done++;
                                    } else if (c.criteriaType() === 2) {
                                        var v = c.value();
                                        if (v !== null && v !== undefined && v !== '') done++;
                                    }
                                }
                                return done;
                            }, newSubCategory);


                            newSubCategory.percentSelectCriteria = ko.pureComputed(function () {
                                var total = this.totalTrackable();
                                if (!total) return 0;
                                return (this.completedCriteria() / total) * 100;
                            }, newSubCategory);

                            newSubCategory.remainingCount = ko.pureComputed(function () {
                                var total = this.totalTrackable();
                                var done = this.completedCriteria();
                                var rem = total - done;
                                return rem > 0 ? rem : 0;
                            }, newSubCategory);

                            newSubCategory.remainingText = ko.pureComputed(function () {
                                var r = this.remainingCount();
                                return r === 0 ? 'Ολοκληρωμένο'
                                    : r === 1 ? 'Απομένει 1 κριτήριο'
                                        : 'Απομένουν ' + r + ' κριτήρια';
                            }, newSubCategory);


                            newCategory.categories.push(newSubCategory);

                        }
                    }

                    newCategory.maxGrade = ko.computed(function () {

                        var points = 0;

                        if (this.categories().length > 0) {
                            for (var i = 0; i < this.categories().length; i++) {
                                var category = this.categories()[i];
                                points += category.maxGrade();
                            }
                        }
                        return points;

                    }, newCategory);


                    // Συγκεντρωτική βαθμολογία πυλώνα ΧΩΡΙΣ αναγωγή (π.χ. 61.50 / 105)
                    newCategory.rawPoints = ko.computed(function () {

                        var points = 0;

                        if (this.categories().length > 0) {
                            for (var i = 0; i < this.categories().length; i++) {
                                var category = this.categories()[i];
                                points += category.totalPoints();
                            }
                        }

                        return points.toFixed(2);

                    }, newCategory);

                    // Βαθμολογία πυλώνα ΜΕ αναγωγή στις μονάδες του (totalUnits)
                    newCategory.totalPoints = ko.computed(function () {

                        var points = 0;

                        if (this.categories().length > 0) {
                            for (var i = 0; i < this.categories().length; i++) {
                                var category = this.categories()[i];
                                points += category.totalPoints();
                            }
                            points = ((points / this.maxGrade()) * this.totalUnits()).toFixed(2);
                        }

                        return points;

                    }, newCategory);


                    self.categories.push(newCategory);
                }



                self.getInvalidRequiredCriteria = function () {
                    var invalid = [];

                    self.categories().forEach(function (cat) {
                        cat.categories().forEach(function (sub) {
                            sub.criteria().forEach(function (c) {

                                var required = (typeof c.isRequired === "function") ? c.isRequired() : !!c.isRequired;
                                if (!required) return;

                                var validReq = (typeof c.isValidRequired === "function") ? c.isValidRequired() : true;

                                if (!validReq) invalid.push(c);
                            });
                        });
                    });

                    console.log("invalid", invalid.length, invalid);
                    return invalid;
                };


                self.validateRequiredBeforeSubmit = function () {
                    var invalid = self.getInvalidRequiredCriteria();
                    if (invalid.length === 0) return true;

                    self.requiredModalMessage("Υπάρχουν " + invalid.length + " υποχρεωτικά κριτήρια που δεν έχουν απαντηθεί.");

                    var items = invalid.slice(0, 5).map(function (x) {
                        return x.code() + ": " + x.title();
                    });

                    if (invalid.length > 5) items.push("...");

                    self.requiredModalItems(items);


                    //var el = document.getElementById('required-alert-modal-save');
                    //var modal = bootstrap.Modal.getOrCreateInstance(el);
                    //modal.show();
                    $("#required-alert-modal-save").modal("show");

                    return false;
                };



                // Εφαρμογή κανόνων κεντρικών ρυθμίσεων στα κριτήρια (live disable)
                self.applyFeatureRules();

                // AI: φόρτωση αποτελεσμάτων προ-ελέγχου τεκμηρίων
                self.fetchAiChecks();

                self.hideLoader();
                //  window.jQuery.FileUpload.init();

                // scroll tab-content


                $(document).on('shown.bs.tab.noScrollCtl shown.bs.tab', '[data-bs-toggle="tab"], [data-bs-toggle="pill"]', function (e) {
                    //  skip στα  no-scroll-tabs 
                    if ($(e.target).closest('.no-scroll-tabs').length) return;

                    //var sel = $(e.target).attr('data-bs-target') || $(e.target).attr('href');
                    //if (!sel) return;

                    //var $pane = $(sel);
                    //var $scroller = $pane.closest('.tab-content.criteria-scroll');
                    //if (!$scroller.length) $scroller = $pane.find('.tab-content.criteria-scroll');
                    //if (!$scroller.length) $scroller = $pane; 


                    //$scroller.scrollTop({ top: 0, behavior: 'smooth' });
                    window.scrollTo({ top: 75, behavior: 'smooth' });
                });



            },
            error: function () {
                self.hideLoader();
            }
        });



    }

    self.saveDataTemp = function () {

        if (self.readOnly() || !self.enableAll()) return;   // read-only / υποβεβλημένη έκδοση

        self.showLoader();
        var obj = self.setObjectToSave(1);

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: obj,
            url: '/api/CertificateApi/SaveCriteria',
            success: function (data) {
                if (data.success == true) {
                    self.hideLoader();
                    $("#success-alert-modal-save").modal("show");
                }
                else {
                    self.hideLoader();
                    $("#danger-alert-modal").modal("show");
                }
            }
        });
    }


    self.saveData = function () {
        if (!self.validateRequiredBeforeSubmit()) {
            return;
        }

        $("#info-header-modal").modal("show");
    }

    self.saveDataSubmit = function () {

        if (self.readOnly() || !self.enableAll()) return;   // read-only / υποβεβλημένη έκδοση

        $("#info-header-modal").modal("hide");
        self.showLoader();
        var obj = self.setObjectToSave(2);

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: obj,
            url: '/api/CertificateApi/SaveCriteria',
            success: function (data) {
                if (data.success == true) {
                    self.hideLoader();
                    $("#success-alert-modal").modal("show");
                    self.enableAll(false);
                }
                else {
                    self.hideLoader();
                    $("#danger-alert-modal").modal("show");
                }
            }
        });
    }



    self.setObjectToSave = function (temp) {

        var obj = {};

        obj.hotelID = self.hotelID();
        obj.exploitingCompanyID = self.exploitingCompanyID();
        obj.certificateID = self.certificateID();
        obj.version = self.mode();   // 2 = αυτοψία, 3 = τελική κατάταξη
        obj.status = temp;
        obj.criteria = [];


        for (var i = 0; i < self.categories().length; i++) {
            var category = self.categories()[i];

            for (var j = 0; j < category.categories().length; j++) {
                var subCategory = category.categories()[j];

                for (var z = 0; z < subCategory.criteria().length; z++) {
                    var criteria = subCategory.criteria()[z];

                    var newCriteria = {};

                    newCriteria.criteriaID = criteria.id();
                    newCriteria.isApplicable = criteria.isApplicable();
                    newCriteria.isChecked = criteria.isChecked();
                    newCriteria.isNotChecked = criteria.isNotChecked();
                    newCriteria.value = criteria.value();

                    obj.criteria.push(newCriteria);
                }
            }
        }

        return obj;
    }



    self.totalScore = ko.computed(function () {

        var total = 0;

        for (var i = 0; i < self.categories().length; i++) {
            var category = self.categories()[i];

            if (!isNaN(category.totalPoints())) {
                total = total + parseFloat(category.totalPoints());
            }


        }

        total = total.toFixed(2);

        return total;

    }, self)


    self.progressPct = ko.pureComputed(function () {
        return (Math.min(95, self.totalScore()) / 95 * 100).toFixed(2) + '%';
    });

    // ── Per-pillar gating (ίδια λογική με τα Κριτήρια) ─────────────
    self.pillarRequired = function (medalID, category) {
        var t = self.medalThresholds().find(function (z) {
            return z.medalID === medalID && z.categoryID === category.id();
        });
        if (!t) return 0;
        var max = parseFloat(category.totalUnits()) || 0;
        return t.isPercent ? (t.minValue / 100 * max) : t.minValue;
    };
    self.pillarMeets = function (medalID, category) {
        return parseFloat(category.totalPoints()) >= self.pillarRequired(medalID, category);
    };

    self.byTotalTier = ko.pureComputed(function () {
        var x = parseFloat(self.totalScore());
        var ms = self.medals();
        if (!ms.length) return null;
        var cand = ms.filter(function (m) { return x >= m.min; }).sort(function (a, b) { return b.min - a.min; });
        return cand.length ? cand[0] : null;
    });

    self.tier = ko.pureComputed(function () {
        var byTotal = self.byTotalTier();
        if (!self.usePillarThresholds() || !byTotal) return byTotal;

        var x = parseFloat(self.totalScore());
        var cand = self.medals().filter(function (m) { return x >= m.min; }).sort(function (a, b) { return b.min - a.min; });

        for (var i = 0; i < cand.length; i++) {
            var m = cand[i];
            var ths = self.medalThresholds().filter(function (z) { return z.medalID === m.id; });
            var ok = true;
            for (var j = 0; j < ths.length; j++) {
                var cat = self.categories().find(function (c) { return c.id() === ths[j].categoryID; });
                if (cat && !self.pillarMeets(m.id, cat)) { ok = false; break; }
            }
            if (ok) return m;
        }
        return byTotal;
    });

    self.isDemoted = ko.pureComputed(function () {
        var b = self.byTotalTier(), t = self.tier();
        return self.usePillarThresholds() && b && t && b.id !== t.id;
    });

    self.blockedPillarNames = ko.pureComputed(function () {
        if (!self.isDemoted()) return [];
        var b = self.byTotalTier();
        var ths = self.medalThresholds().filter(function (z) { return z.medalID === b.id; });
        var names = [];
        for (var j = 0; j < ths.length; j++) {
            var cat = self.categories().find(function (c) { return c.id() === ths[j].categoryID; });
            if (cat && !self.pillarMeets(b.id, cat)) names.push(cat.title());
        }
        return names;
    });

    self.gatingMessage = ko.pureComputed(function () {
        if (!self.isDemoted()) return "";
        var names = self.blockedPillarNames();
        var label = names.length === 1
            ? "υπολείπεται ο πυλώνας «" + names[0] + "»"
            : "υπολείπονται οι πυλώνες: " + names.map(function (n) { return "«" + n + "»"; }).join(", ");
        return "Βάσει συνόλου: " + self.byTotalTier().title + " — " + label;
    });

    self.tierLabel = ko.pureComputed(function () { if (self.tier() != null) { return self.tier().title; } else { return null; } });

    self.nextTierDeltaText = ko.pureComputed(function () {
        const x = parseFloat(self.totalScore()), t = self.byTotalTier();
        if (t != null) {
            if (t.title === "Platinum") return "Μέγιστη βαθμίδα";
            const next = self.medals()[self.medals().findIndex(v => v.title === t.title) + 1];
            if (!next) return "Μέγιστη βαθμίδα";
            var diff = (next.min - x).toFixed(2);
            return `λείπουν ${diff} βαθμοί για ${next.title}`;
        }
        else {
            return null;
        }
    });

    self.nextTierBadgeClass = ko.pureComputed(function () {
        const x = parseFloat(self.totalScore()), t = self.byTotalTier();
        if (t != null) {
            if (t.title === "Platinum") return "badge ok";
            const next = self.medals()[self.medals().findIndex(v => v.title === t.title) + 1];
            if (!next) return "badge ok";
            return "badge " + ((next.min - x) <= 5 ? "warn" : "");
        }
        else {
            return "";
        }
    });


    self.fetchHotelCriteriaFiles = function (hotelCriteriaID, criteriaFileID) {

        var obj = {};
        obj.hotelCriteriaID = hotelCriteriaID;
        obj.criteriaFileID = criteriaFileID;

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: obj,
            url: '/api/CriteriaApi/GetCriteriaFIles',
            success: function (data) {


                if (data.hotelCriteria_criteriaFiles.length > 0) {

                    for (var i = 0; i < self.categories().length; i++) {
                        var category = self.categories()[i];

                        for (var j = 0; j < category.categories().length; j++) {
                            var subCategory = category.categories()[j];

                            for (var z = 0; z < subCategory.criteria().length; z++) {
                                var criteria = subCategory.criteria()[z];

                                if (criteria.needsFiles() == true && criteria.files().length > 0) {
                                    for (var b = 0; b < criteria.files().length; b++) {
                                        var f = criteria.files()[b];

                                        if (f.id() == criteriaFileID) {
                                            f.uploadedFiles([]);
                                            if (data.hotelCriteria_criteriaFiles != null && data.hotelCriteria_criteriaFiles.length > 0) {
                                                for (var t = 0; t < data.hotelCriteria_criteriaFiles.length; t++) {
                                                    var fi = data.hotelCriteria_criteriaFiles[t];

                                                    var newFileFile = {};
                                                    newFileFile.fileName = ko.observable(fi.fileName);
                                                    newFileFile.id = ko.observable(fi.id);
                                                    self.initAiFile(newFileFile);

                                                    f.uploadedFiles.push(newFileFile);


                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }


                }


                //self.hideLoader();
            }
        });

    }


    self.deleteFile = function () {

        // self.showLoader();
        var file = this;

        var obj = {};

        var id = file.id();

        obj.id = file.id();
        // obj.fileName = file.fileName();
        // obj.criteriaFileID = file.criteriaFileID();

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: obj,
            url: '/api/CriteriaApi/DeleteFile',
            success: function (data) {

                if (data.success == true) {
                    for (var i = 0; i < self.categories().length; i++) {
                        var category = self.categories()[i];

                        for (var j = 0; j < category.categories().length; j++) {
                            var subCategory = category.categories()[j];

                            for (var z = 0; z < subCategory.criteria().length; z++) {
                                var criteria = subCategory.criteria()[z];

                                if (criteria.needsFiles() == true && criteria.files().length > 0) {
                                    for (var b = 0; b < criteria.files().length; b++) {
                                        var f = criteria.files()[b];

                                        if (f.uploadedFiles().length > 0) {
                                            for (var u = 0; u < f.uploadedFiles().length; u++) {
                                                var upFile = f.uploadedFiles()[u];

                                                if (upFile.id() == id) {
                                                    f.uploadedFiles.remove(upFile);
                                                }

                                            }
                                        }

                                    }
                                }
                            }
                        }
                    }
                }

                // self.hideLoader();

            }
        });


    }

    self.fetchData();
}
