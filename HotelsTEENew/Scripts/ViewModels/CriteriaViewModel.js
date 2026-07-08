function CriteriaViewModel() {
    var self = this;

    self.error = ko.observable(false);
    self.errorMessage = ko.observable("");

    self.success = ko.observable(false);
    self.successMessage = ko.observable("");

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

    // Full-page loader με σχετικό κείμενο (αποθήκευση/υποβολή κριτηρίων)
    self.showPageLoader = function (msg) {
        if (msg) $("#page-loader-text").text(msg);
        $("#page-loader").css("display", "flex");
    }
    self.hidePageLoader = function () {
        $("#page-loader").hide();
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

    self.hotelID = ko.observable("");
    self.exploitingCompanyID = ko.observable("");
    self.category = ko.observable("");

    // Η αυτοαξιολόγηση έχει υποβληθεί (status=2) και η αίτηση τρέχει στον επιθεωρητή:
    // στη σελίδα "Υποβολή Κριτηρίων" δεν δείχνουμε κριτήρια, μόνο ενημερωτικό μήνυμα.
    self.applicationInProgress = ko.observable(false);
    self.hotelTitle = ko.observable("");
    self.hotelType = ko.observable("");
    self.totalRooms = ko.observable(0);
    self.totalBeds = ko.observable(0);

    self.peaNotApplicable = ko.observable(false);
    self.medals = ko.observableArray([]);

    // Per-pillar βάσεις μεταλλίων + διακόπτης (gating)
    self.usePillarThresholds = ko.observable(false);
    self.medalThresholds = ko.observableArray([]);

    self.categories = ko.observableArray([]);
    self.chart = null;

    self.enableAll = ko.observable(true);
    self.hotelCriteriaID = ko.observable(null);
    self.samplingText = ko.observable("");

    // ── Κεντρικές ρυθμίσεις/παροχές καταλύματος (κάρτα κορυφής) ──────
    self.features = ko.observableArray([]);   // για την κάρτα: {featureID,title,description,icon,answered,hasFeature}
    self.featureMaps = [];                     // [{featureID,criteriaID,disableWhenPresent}]
    self.criteriaIndex = {};                   // criteriaID -> newCriteria (για live disable)
    self.featuresCollapsed = ko.observable(false);
    self.hasFeatures = ko.pureComputed(function () { return self.features().length > 0; });
    self.allFeaturesAnswered = ko.pureComputed(function () {
        return self.features().every(function (f) { return f.answered(); });
    });
    self.disabledByFeaturesCount = ko.observable(0);

    // για το modal στον έλεγχο των κριτηρίων
    self.requiredModalMessage = ko.observable("");
    self.requiredModalItems = ko.observableArray([]);

    // Δεν υπάρχει ενεργή αυτοαξιολόγηση — εμφανίζουμε επιβεβαίωση πριν δημιουργήσουμε νέα
    self.needsNewAssessment = ko.observable(false);

    // ── AI Σύμβουλος Βιωσιμότητας (chat) ─────────────────────────────
    self.aiEnabled = ko.observable(false);
    self.aiChatOpen = ko.observable(false);
    self.aiChatMessages = ko.observableArray([]);   // { role: 'user'|'assistant', content }
    self.aiChatInput = ko.observable("");
    self.aiChatBusy = ko.observable(false);
    self.aiSuggestions = ko.observableArray([
        "Τι μου λείπει για το επόμενο μετάλλιο;",
        "Ποια εύκολα κριτήρια δεν έχω απαντήσει;",
        "Τι τεκμήρια χρειάζονται τα υποχρεωτικά κριτήρια;"
    ]);

    self.toggleAiChat = function () {
        self.aiChatOpen(!self.aiChatOpen());
        if (self.aiChatOpen() && self.aiChatMessages().length === 0) {
            self.aiChatMessages.push({
                role: "assistant",
                content: "Γεια σας! Είμαι ο AI Σύμβουλος Βιωσιμότητας. Γνωρίζω τα κριτήρια και την τρέχουσα αξιολόγησή σας — ρωτήστε με ό,τι θέλετε!"
            });
        }
        if (self.aiChatOpen()) setTimeout(function () { $("#ai-chat-input").focus(); }, 200);
    };

    self.aiScrollDown = function () {
        setTimeout(function () {
            var el = document.getElementById("ai-chat-body");
            if (el) el.scrollTop = el.scrollHeight;
        }, 50);
    };

    self.sendAiSuggestion = function (s) { self.aiChatInput(s); self.sendAiMessage(); };

    self.sendAiMessage = function () {
        var text = (self.aiChatInput() || "").trim();
        if (!text || self.aiChatBusy()) return;

        self.aiChatMessages.push({ role: "user", content: text });
        self.aiChatInput("");
        self.aiChatBusy(true);
        self.aiScrollDown();

        // Στέλνουμε το ιστορικό ΧΩΡΙΣ το εναρκτήριο χαιρετιστήριο μήνυμα
        var payload = self.aiChatMessages().slice(1).map(function (m) {
            return { role: m.role, content: m.content };
        });

        $.ajax({
            type: "POST", url: "/api/AiApi/Advise", contentType: "application/json",
            data: JSON.stringify({ messages: payload }), dataType: "json",
            success: function (r) {
                self.aiChatBusy(false);
                if (r && r.success) self.aiChatMessages.push({ role: "assistant", content: r.reply });
                else self.aiChatMessages.push({ role: "assistant", content: (r && r.message) || "Κάτι πήγε στραβά — δοκιμάστε ξανά." });
                self.aiScrollDown();
            },
            error: function () {
                self.aiChatBusy(false);
                self.aiChatMessages.push({ role: "assistant", content: "Πρόβλημα επικοινωνίας — δοκιμάστε ξανά." });
                self.aiScrollDown();
            }
        });
    };

    self.onAiChatEnter = function (d, e) {
        if (e.keyCode === 13 && !e.shiftKey) { self.sendAiMessage(); return false; }
        return true;
    };

    // ── Σημασιολογική αναζήτηση κριτηρίων (#6) ───────────────────────
    self.aiSearchQuery = ko.observable("");
    self.aiSearchBusy = ko.observable(false);
    self.aiSearchResults = ko.observableArray([]);
    self.aiSearchNoResults = ko.observable(false);

    self.runAiSearch = function () {
        var q = (self.aiSearchQuery() || "").trim();
        if (!q || self.aiSearchBusy()) return;
        self.aiSearchBusy(true);
        self.aiSearchNoResults(false);

        $.ajax({
            type: "POST", url: "/api/AiApi/SearchCriteria", contentType: "application/json",
            data: JSON.stringify({ query: q }), dataType: "json",
            success: function (r) {
                self.aiSearchBusy(false);
                if (r && r.success) {
                    self.aiSearchResults(r.results || []);
                    self.aiSearchNoResults((r.results || []).length === 0);
                } else {
                    self.aiSearchResults([]);
                    self.aiSearchNoResults(true);
                }
            },
            error: function () { self.aiSearchBusy(false); self.aiSearchNoResults(true); }
        });
    };

    self.clearAiSearch = function () {
        self.aiSearchQuery("");
        self.aiSearchResults([]);
        self.aiSearchNoResults(false);
    };

    self.onAiSearchEnter = function (d, e) {
        if (e.keyCode === 13) { self.runAiSearch(); return false; }
        return true;
    };

    // Μετάβαση σε κριτήριο: πυλώνας (tab) → υποπυλώνας (pill) → scroll + highlight.
    // Χρήση πραγματικών click() ώστε να ενεργοποιηθούν αξιόπιστα τα data-bs-toggle handlers.
    self.gotoCriterion = function (r) {
        // Κλείσιμο του dropdown αποτελεσμάτων (κρατάμε το query για επανάληψη)
        self.aiSearchResults([]);
        self.aiSearchNoResults(false);
        if (r.pillarID) {
            var pillarLink = document.querySelector('a[href="#tab-' + r.pillarID + '"]');
            if (pillarLink && !pillarLink.classList.contains("active")) pillarLink.click();
        }
        setTimeout(function () {
            if (r.subID) {
                var subLink = document.querySelector('a[href="#subTabPane-' + r.subID + '"]');
                if (subLink && !subLink.classList.contains("active")) subLink.click();
            }
            setTimeout(function () {
                var card = document.getElementById("crit-card-" + r.id);
                if (card) {
                    card.scrollIntoView({ behavior: "smooth", block: "center" });
                    card.style.transition = "box-shadow 0.4s";
                    card.style.boxShadow = "0 0 0 3px #39afd1";
                    setTimeout(function () { card.style.boxShadow = ""; }, 2200);
                }
            }, 300);
        }, 250);
    };

    self.startAssessment = function () {
        self.needsNewAssessment(false);
        self.fetchData(true);   // δημιουργία μετά από ρητή επιβεβαίωση (panel)
    };

    // ── Λογική κεντρικών ρυθμίσεων ─────────────────────────────────
    // Ο χρήστης απαντά Ναι/Όχι ανά παροχή. Τα αντιστοιχισμένα κριτήρια
    // ελέγχονται 100% από εδώ (ο χειροκίνητος διακόπτης τους κρύβεται).
    self.setFeature = function (f, value) {
        f.hasFeature(value);
        f.answered(true);
        self.saveFeatureAnswer(f);
        self.applyFeatureRules();
    };

    self.saveFeatureAnswer = function (f) {
        if (!self.hotelCriteriaID()) return;
        $.ajax({
            type: "POST", url: "/api/CriteriaApi/SaveFeatureAnswer",
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
        // Ομαδοποίηση αντιστοιχίσεων ανά κριτήριο
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

            crit.featureMapped(true);   // ελέγχεται από την κάρτα → κρύβουμε χειροκίνητο διακόπτη

            var disabled = false;
            var reasons = [];
            byCrit[cid].forEach(function (m) {
                var f = featById[m.featureID];
                if (!f || !f.answered()) return;          // αγνόησε μη απαντημένες παροχές
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

    self.fetchData = function (create) {

        //setTimeout(function () { self.showLoader(); }, 10);
        self.showLoader();

        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/CriteriaApi/GetAllCriteria' + (create ? '?create=true' : ''),
            success: function (data) {

                // Καμία ενεργή αυτοαξιολόγηση: εμφάνισε panel επιβεβαίωσης (όχι αυτόματη δημιουργία)
                if (data.needsNewAssessment) {
                    self.hideLoader();
                    self.needsNewAssessment(true);
                    return;
                }

                self.hotelID(data.hotelDetails.hotelID);
                self.exploitingCompanyID(data.hotelDetails.exploitingCompanyID);

                //self.category(data.hotelDetails.category);
                //self.hotelTitle(data.hotelDetails.hotelTitle);
                //self.hotelType(data.hotelDetails.hotelType);
                //self.totalRooms(data.hotelDetails.totalRooms);
                //self.totalBeds(data.hotelDetails.totalBeds);

                //// Επιλογή κειμένου Δειγματοληψίας
                //var beds = parseInt(self.totalBeds() || 0, 10);
                //if (beds < 51) {
                //    self.samplingText("Μικρό ξενοδοχείο (<51 κλίνες): Ο αξιολογητής καλείται να ελέγξει το 15% του συνόλου των δωματίων.");
                //} else if (beds >= 51 && beds <= 200) {
                //    self.samplingText("Μεσαίου μεγέθους ξενοδοχείο (51–200 κλίνες): Ο αξιολογητής καλείται να ελέγξει το 10% του συνόλου των δωματίων.");
                //} else if (beds > 200) {
                //    self.samplingText("Μεγάλο ξενοδοχείο (>200 κλίνες): Ο αξιολογητής καλείται να ελέγξει το 5% του συνόλου των δωματίων.");
                //} else {
                //    self.samplingText("Δεν έχουν οριστεί κλίνες για το κατάλυμα.");
                //}

                if (data.hotelCriteria != null && data.hotelCriteria.status == 2) {
                    self.enableAll(false);
                }

                // Ενεργή αίτηση σε εξέλιξη: v1, υποβληθείσα (status=2), με επιλεγμένο
                // επιθεωρητή (certificateID != null) και μη ολοκληρωμένη (isFinished=false).
                // Μόνο τότε κρύβουμε τα κριτήρια και δείχνουμε το ενημερωτικό μήνυμα.
                if (data.hotelCriteria != null
                    && data.hotelCriteria.version == 1
                    && data.hotelCriteria.status == 2
                    && data.hotelCriteria.certificateID != null
                    && data.hotelCriteria.isFinished == false) {
                    self.applicationInProgress(true);
                    // Καταστέλλουμε την αυτόματη ξενάγηση (τα κριτήρια είναι κρυμμένα εδώ)
                    try { localStorage.setItem("guide_seen_criteria", "1"); } catch (e) { }
                }
                if (data.hotelCriteria != null) {
                    self.hotelCriteriaID(data.hotelCriteria.id);
                }

                // AI Σύμβουλος: διαθεσιμότητα
                self.aiEnabled(data.aiEnabled === true);

                // Κεντρικές ρυθμίσεις/παροχές: κάρτα + αντιστοιχίσεις + απαντήσεις κύκλου
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
                // Αν έχουν απαντηθεί όλες, ξεκίνα συμπτυγμένη η κάρτα
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

                

            }
        });
      


    }

    self.saveDataTemp = function () {

        self.showPageLoader("Αποθήκευση κριτηρίων...");
        var obj = self.setObjectToSave(1);

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: obj,
            url: '/api/CriteriaApi/SaveCriteria',
            success: function (data) {
                self.hidePageLoader();
                if (data.success == true) {
                    $("#success-alert-modal-save").modal("show");
                }
                else {
                    $("#danger-alert-modal").modal("show");
                }
            },
            error: function () {
                self.hidePageLoader();
                $("#danger-alert-modal").modal("show");
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

        $("#info-header-modal").modal("hide");
        self.showPageLoader("Υποβολή αυτοαξιολόγησης...");
        var obj = self.setObjectToSave(2);

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: obj,
            url: '/api/CriteriaApi/SaveCriteria',
            success: function (data) {
                self.hidePageLoader();
                if (data.success == true) {
                    $("#success-alert-modal").modal("show");
                    self.enableAll(false);
                }
                else {
                    $("#danger-alert-modal").modal("show");
                }
            },
            error: function () {
                self.hidePageLoader();
                $("#danger-alert-modal").modal("show");
            }
        });
    }



    self.setObjectToSave = function (temp) {

        var obj = {};

        obj.hotelID = self.hotelID();
        obj.exploitingCompanyID = self.exploitingCompanyID();
        obj.version = 1;
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

    // ── Per-pillar gating helpers ──────────────────────────────────
    // Απαιτούμενη βάση (αναγμένη) για (μετάλιο, πυλώνας)
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

    // Μετάλιο βάσει ΜΟΝΟ της συνολικής βαθμολογίας
    self.byTotalTier = ko.pureComputed(function () {
        var x = parseFloat(self.totalScore());
        var ms = self.medals();
        if (!ms.length) return null;
        var cand = ms.filter(function (m) { return x >= m.min; }).sort(function (a, b) { return b.min - a.min; });
        return cand.length ? cand[0] : null;
    });

    // Πραγματικό (gated) μετάλιο — υποβιβασμός αν υπολείπεται πυλώνας
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

    // Όλοι οι πυλώνες που υπολείπονται της βάσης του μεταλλίου-βάσει-συνόλου
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

    self.tierLabel = ko.pureComputed(function () { if (self.tier() != null) { return self.tier().title; } else { return null;  } });

    // Βασίζεται στο μετάλιο-βάσει-συνόλου (η μπάρα δείχνει τη συνολική βαθμολογία)
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
