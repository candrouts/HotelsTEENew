// Dashboard αρχικής σελίδας ξενοδόχου:
// 2 ApexCharts "Bar with Markers" (Βαθμολογία μου vs Μ.Ο. υπολοίπων)
// Chart 1: κύριες κατηγορίες (πάντα γεμάτο)
// Chart 2: υποκατηγορίες της επιλεγμένης κατηγορίας
function HomeDashboard() {
    var self = this;

    var COLOR_MINE = "#0acf97";   // πράσινο - δική μου βαθμολογία
    var COLOR_AVG = "#fa5c7c";    // κόκκινο - Μ.Ο. υπολοίπων

    self.categories = [];
    self.subCategories = [];
    self.selectedCategoryID = null;
    self.isAdmin = false;   // role=100: charts μόνο με Μ.Ο. ξενοδοχείων

    // Admin "view as hotel": από query string (?hotelID=..&exploitingCompanyID=..)
    var qs = new URLSearchParams(window.location.search);
    self.targetHotelID = qs.get("hotelID") || null;
    self.targetCompanyID = qs.get("exploitingCompanyID") || null;

    self.catChart = null;
    self.subChart = null;

    // ── Loader helpers ─────────────────────────────────────────────
    self.showLoader = function () {
        if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("show");
    };
    self.hideLoader = function () {
        setTimeout(function () {
            if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("hide");
        }, 350);
    };

    // ── Δημιουργία options για bar-with-markers ────────────────────
    // withRaw = true (chart κατηγοριών): δείχνει "αρχική → αναγωγή"
    // Admin: η μπάρα είναι ο Μ.Ο. των (φιλτραρισμένων) ξενοδοχείων, χωρίς marker
    self.buildOptions = function (items, onSelect, withRaw) {
        var admin = self.isAdmin;

        var data = items.map(function (it) {
            var point = {
                x: it.title,
                y: admin ? it.avgPoints : it.myPoints,
                raw: admin ? it.avgRawPoints : it.myRawPoints,   // αρχική βαθμολογία (πριν την αναγωγή)
                avgRaw: it.avgRawPoints
            };
            if (!admin) {
                point.goals = [{
                    name: "Μ.Ο. υπολοίπων",
                    value: it.avgPoints,
                    strokeWidth: 10,
                    strokeHeight: 0,
                    strokeLineCap: "round",
                    strokeColor: COLOR_AVG
                }];
            }
            return point;
        });

        var seriesName = admin ? "Μ.Ο. ξενοδοχείων" : "Η βαθμολογία μου";

        var opts = {
            series: [{ name: seriesName, data: data }],
            chart: {
                height: Math.max(280, items.length * 56 + 90),
                type: "bar",
                toolbar: { show: false },
                events: {}
            },
            plotOptions: { bar: { horizontal: true, barHeight: "55%" } },
            colors: [COLOR_MINE],
            dataLabels: {
                enabled: true,
                formatter: function (v, opts) {
                    if (withRaw) {
                        try {
                            var d = opts.w.config.series[opts.seriesIndex].data[opts.dataPointIndex];
                            return d.raw + " → " + v;
                        } catch (e) { }
                    }
                    return v;
                }
            },
            legend: {
                show: true,
                showForSingleSeries: true,
                customLegendItems: admin ? [seriesName] : ["Η βαθμολογία μου", "Μ.Ο. υπολοίπων"],
                markers: { fillColors: admin ? [COLOR_MINE] : [COLOR_MINE, COLOR_AVG] }
            },
            tooltip: {
                shared: false,
                y: {
                    formatter: function (val, ctx) {
                        try {
                            var d = ctx.w.config.series[ctx.seriesIndex].data[ctx.dataPointIndex];
                            var mine = withRaw ? d.raw + " → " + val : "" + val;
                            if (d.goals && d.goals.length) {
                                var avg = withRaw ? d.avgRaw + " → " + d.goals[0].value : "" + d.goals[0].value;
                                return mine + "  (Μ.Ο.: " + avg + ")";
                            }
                            return mine;
                        } catch (e) { }
                        return val;
                    }
                }
            },
            xaxis: { axisBorder: { show: false } }
        };

        if (onSelect) {
            opts.chart.events.dataPointSelection = function (event, ctx, cfg) {
                onSelect(cfg.dataPointIndex);
            };
            opts.states = { active: { filter: { type: "darken", value: 0.85 } } };
        }

        return opts;
    };

    // ── Render chart κατηγοριών ────────────────────────────────────
    self.renderCategoryChart = function () {
        var el = document.querySelector("#dash-cat-chart");
        if (!el) return;

        if (self.catChart) { self.catChart.destroy(); self.catChart = null; }

        var opts = self.buildOptions(self.categories, function (index) {
            var cat = self.categories[index];
            if (cat) self.renderSubChart(cat);
        }, true);   // withRaw: εμφάνιση "αρχική → αναγωγή"

        self.catChart = new ApexCharts(el, opts);
        self.catChart.render();
    };

    // ── Render chart υποκατηγοριών ─────────────────────────────────
    self.renderSubChart = function (category) {
        self.selectedCategoryID = category ? category.id : null;

        var el = document.querySelector("#dash-sub-chart");
        if (!el) return;

        if (self.subChart) { self.subChart.destroy(); self.subChart = null; }

        if (!category) {
            $("#dash-sub-title").text("Υποκατηγορίες");
            $("#dash-sub-placeholder").show();
            return;
        }

        $("#dash-sub-placeholder").hide();
        $("#dash-sub-title").text(category.title);

        var subs = self.subCategories.filter(function (s) { return s.parentID === category.id; });

        self.subChart = new ApexCharts(el, self.buildOptions(subs, null));
        self.subChart.render();
    };

    // ── Πλήθος ανά μετάλλιο (admin): toggle Καταλύματα / Βεβαιώσεις ──
    self.medalMode = "hotels";   // "hotels" (distinct, τρέχουσα βαθμίδα) | "certs" (όλες οι βεβαιώσεις)
    self.medalData = null;

    self.medalColorOf = function (title) {
        switch ((title || "").toLowerCase()) {
            case "platinum": return "#5f9ea0";
            case "gold": return "#e0a800";
            case "silver": return "#9aa0a6";
            case "bronze": return "#cd7f32";
            default: return "#98a6ad"; // Αταξινόμητο
        }
    };

    self.renderMedalCards = function () {
        var host = document.getElementById("admin-medal-cards");
        if (!host || !self.medalData) return;
        var data = self.medalData;
        var byHotels = self.medalMode === "hotels";
        var total = byHotels ? data.totalHotels : data.totalCertificates;

        var header = '<div class="col-12 d-flex justify-content-between align-items-center mb-1">' +
            '<small class="text-muted">' +
            (byHotels ? 'Καταλύματα ανά τρέχουσα βαθμίδα' : 'Σύνολο εκδομένων βεβαιώσεων') +
            ' · Σύνολο: <strong>' + total + '</strong></small>' +
            '<div class="btn-group btn-group-sm">' +
            '<button type="button" class="btn medal-toggle-btn ' + (byHotels ? 'btn-primary' : 'btn-outline-primary') + '" data-mode="hotels">Καταλύματα</button>' +
            '<button type="button" class="btn medal-toggle-btn ' + (!byHotels ? 'btn-primary' : 'btn-outline-primary') + '" data-mode="certs">Βεβαιώσεις</button>' +
            '</div></div>';

        var cards = (data.medals || []).map(function (m) {
            var c = self.medalColorOf(m.title);
            var val = byHotels ? m.hotelCount : m.certCount;
            return '<div class="col"><div class="card mb-2"><div class="card-body text-center py-2">' +
                '<i class="uil uil-award" style="font-size:24px;color:' + c + ';"></i>' +
                '<h3 class="my-1" style="color:' + c + ';">' + val + '</h3>' +
                '<span class="text-muted" style="font-size:12px;">' + m.title + '</span>' +
                '</div></div></div>';
        }).join("");

        $(host).html(header + cards).show();
    };

    self.loadMedalCounts = function () {
        if (!document.getElementById("admin-medal-cards")) return;
        $.ajax({
            type: "POST", url: "/api/HomeApi/GetMedalCounts", dataType: "json",
            success: function (data) {
                if (!data || !data.success) return;
                self.medalData = data;
                self.renderMedalCards();
            }
        });
    };

    // Εναλλαγή μέτρησης (Καταλύματα / Βεβαιώσεις)
    $(document).off("click.medalToggle").on("click.medalToggle", ".medal-toggle-btn", function () {
        self.medalMode = $(this).data("mode");
        self.renderMedalCards();
    });

    // ── Ιστορικό βεβαιώσεων (μόνο ξενοδόχος) ───────────────────────
    self.fetchHistory = function () {
        $.ajax({
            type: "POST",
            dataType: "json",
            url: "/api/HomeApi/GetCertificateHistory",
            success: function (data) {
                if (!data || !data.success) return;

                // Κουμπί νέας αξιολόγησης: μόνο όταν ΔΕΝ υπάρχει ενεργός κύκλος
                // και υπάρχει τουλάχιστον μία ολοκληρωμένη βεβαίωση
                $("#dash-start-new").toggle(!data.hasActiveCycle && data.history.length > 0);

                if (!data.history || data.history.length === 0) {
                    $("#dash-history").hide();
                    return;
                }

                var rows = data.history.map(function (h) {
                    var viewBtn = function (mode, label, cls, icon) {
                        return '<a class="btn btn-sm btn-outline-' + cls + ' py-0 me-1" href="/Certificate/ViewCertificate/' +
                            h.certificateID + '?mode=' + mode + '"><i class="uil ' + icon + '"></i> ' + label + '</a>';
                    };

                    var actions = "";
                    if (h.hasV1) actions += viewBtn(1, "Αυτοαξιολόγηση", "secondary", "uil-eye");
                    if (h.hasV2) actions += viewBtn(2, "Αυτοψία", "warning", "uil-clipboard-notes");
                    if (h.hasV3) actions += viewBtn(3, "Τελική", "success", "uil-award");
                    if (h.hasFile) actions += '<a class="btn btn-sm btn-outline-dark py-0 me-1" target="_blank" href="/CertificateDoc/View/' +
                        h.certificateID + '"><i class="uil-file-check-alt"></i> Βεβαίωση</a>';

                    var validBadge = h.isValid
                        ? '<span class="badge badge-success-lighten ms-1">Σε ισχύ</span>'
                        : '<span class="badge badge-danger-lighten ms-1">Έληξε</span>';

                    return "<tr>" +
                        "<td><strong>#" + h.certificateID + "</strong></td>" +
                        "<td>" + h.issueDate + "</td>" +
                        "<td>" + h.validUntil + validBadge + "</td>" +
                        "<td><strong>" + h.totalPoints + "</strong></td>" +
                        "<td>" + (h.medalTitle || "—") + "</td>" +
                        "<td>" + actions + "</td>" +
                        "</tr>";
                });

                $("#dash-history-rows").html(rows.join(""));
                $("#dash-history").show();
            }
        });
    };

    // Η «Έναρξη Νέας Αξιολόγησης» είναι πλέον link προς /Criteria, όπου ο χρήστης
    // επιβεβαιώνει (panel) πριν δημιουργηθεί η νέα αυτοαξιολόγηση.

    // ── Φόρτωση δεδομένων ──────────────────────────────────────────
    self.fetchData = function (isFirstLoad) {
        self.showLoader();

        var bedsVal = $("#dash-filter-beds").val();
        var bedsFrom = null, bedsTo = null;
        if (bedsVal) {
            var parts = bedsVal.split("-");
            bedsFrom = parseInt(parts[0]) || null;
            bedsTo = parts[1] ? (parseInt(parts[1]) || null) : null;
        }

        // Στάδιο: null στην 1η φόρτωση → ο server διαλέγει το πιο ώριμο διαθέσιμο
        var stageVal = isFirstLoad ? null : (parseInt($("#dash-filter-stage").val()) || null);

        $.ajax({
            type: "POST",
            contentType: "application/json",
            dataType: "json",
            url: "/api/HomeApi/GetDashboard",
            data: JSON.stringify({
                hotelCategory: $("#dash-filter-category").val() || null,
                periphereiaID: $("#dash-filter-periphery").val() || null,
                peripheryID: $("#dash-filter-pe").val() || null,
                bedsFrom: bedsFrom,
                bedsTo: bedsTo,
                version: stageVal,
                targetHotelID: self.targetHotelID,
                targetCompanyID: self.targetCompanyID
            }),
            success: function (data) {
                self.hideLoader();

                if (!data || (!data.isHotelier && !data.isAdmin)) {
                    // ούτε ξενοδόχος ούτε admin: μένει το static περιεχόμενο
                    $("#hotelier-dashboard").hide();
                    $("#home-hero").show();
                    return;
                }

                // viewAsHotel: ο admin βλέπει τα charts όπως ο ξενοδόχος (μπάρα + marker)
                self.isAdmin = data.isAdmin === true && data.viewAsHotel !== true;

                $("#home-hero").hide();
                $("#hotelier-dashboard").show();

                // Ιστορικό βεβαιώσεων: μόνο για τον ίδιο τον ξενοδόχο (στην 1η φόρτωση)
                if (data.isHotelier && isFirstLoad) {
                    self.fetchHistory();
                }

                if (data.viewAsHotel === true) {
                    $("#dash-page-title").html(
                        'Βαθμολογία: <span class="text-primary">' + (data.targetHotelTitle || "") + '</span> ' +
                        '<a href="/Certificate" class="btn btn-sm btn-light ms-2"><i class="uil-arrow-left"></i> Πίσω στις Αιτήσεις</a>'
                    );
                    $("#dash-charts-title").text("Βαθμολογία Ξενοδοχείου");
                } else if (self.isAdmin) {
                    $("#dash-page-title").text("Στατιστικά Βαθμολογιών Ξενοδοχείων");
                    $("#dash-charts-title").text("Μ.Ο. Βαθμολογιών Ξενοδοχείων");
                    if (isFirstLoad) self.loadMedalCounts();
                }

                if (!data.hasSubmitted) {
                    // Admin (view as hotel): ουδέτερο λεκτικό, χωρίς link προς τα Κριτήρια
                    if (data.viewAsHotel === true) {
                        $("#dash-no-submission-text").text("Το ξενοδοχείο δεν έχει υποβάλει οριστικά την αυτοαξιολόγησή του — δεν υπάρχουν δεδομένα για συγκριτικά γραφήματα.");
                        $("#dash-no-submission-link").hide();
                    }
                    $("#dash-no-submission").show();
                    $("#dash-content").hide();
                    return;
                }
                $("#dash-no-submission").hide();
                $("#dash-content").show();

                self.categories = data.categories || [];
                self.subCategories = data.subCategories || [];

                $("#dash-others-count").text(data.othersCount);

                // Συγχρονισμός dropdown σταδίου με το στάδιο που εφάρμοσε ο server
                $("#dash-filter-stage").val(String(data.stage));
                $("#dash-stage-label").text(data.stage === 3 ? "Τελική Κατάταξη" : "Αυτοαξιολόγηση");

                // Η "Τελική Κατάταξη" ενεργή μόνο αν υπάρχει δική του v3
                $("#dash-filter-stage option[value='3']").prop("disabled", !data.hasFinal);
                $("#dash-no-final-hint").toggleClass("d-none", data.hasFinal);

                // Γέμισμα φίλτρων μόνο την πρώτη φορά (κρατάμε τις επιλογές)
                if (isFirstLoad) {
                    self.fillSelect("#dash-filter-category", data.hotelCategories);
                    self.fillSelect("#dash-filter-periphery", data.peripheries);
                }
                // ΠΕ: ανανεώνονται όταν αλλάζει Περιφέρεια
                self.fillSelect("#dash-filter-pe", data.periferiakesEnotites, $("#dash-filter-pe").val());

                self.renderCategoryChart();

                // Διατήρηση επιλεγμένης κατηγορίας μετά από αλλαγή φίλτρων
                var keep = null;
                if (self.selectedCategoryID != null) {
                    keep = self.categories.find(function (c) { return c.id === self.selectedCategoryID; });
                }
                self.renderSubChart(keep || null);
            },
            error: function () {
                self.hideLoader();
            }
        });
    };

    self.fillSelect = function (selector, options, keepValue) {
        var $sel = $(selector);
        var current = keepValue !== undefined ? keepValue : $sel.val();
        $sel.find("option:not(:first)").remove();
        (options || []).forEach(function (o) {
            $sel.append($("<option>").val(o.value).text(o.title));
        });
        if (current) $sel.val(current);
        if ($sel.val() == null) $sel.val("");
    };

    // ── Events φίλτρων ─────────────────────────────────────────────
    $("#dash-filter-stage, #dash-filter-category, #dash-filter-pe, #dash-filter-beds").on("change", function () {
        self.fetchData(false);
    });
    $("#dash-filter-periphery").on("change", function () {
        $("#dash-filter-pe").val("");
        self.fetchData(false);
    });
    $("#dash-filter-clear").on("click", function () {
        $("#dash-filter-category, #dash-filter-periphery, #dash-filter-pe, #dash-filter-beds").val("");
        self.fetchData(false);
    });

    self.fetchData(true);
}

// Εκκίνηση μόνο στη σελίδα Home
$(function () {
    if (document.getElementById("hotelier-dashboard")) {
        new HomeDashboard();
    }
});
