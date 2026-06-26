// Dashboard αρχικής σελίδας Επιθεωρητή (role=10)
function loadInspectorDashboard() {

    var labels = ["Νέες", "Προγραμματισμένες", "Προς Αυτοψία", "Αυτοψία σε εξέλιξη", "Προς Τελική", "Αναμονή Αποδοχής"];
    var colors = ["#727cf5", "#1abc9c", "#fa5c7c", "#ffbc00", "#39afd1", "#6f42c1"];

    $.ajax({
        type: "POST",
        dataType: "json",
        url: "/api/HomeApi/GetInspectorDashboard",
        success: function (data) {
            if (!data || !data.success) return;

            // ── KPI ────────────────────────────────────────────────
            $("#insp-kpi-active").text(data.totalActive);
            $("#insp-kpi-autopsydue").text(data.countAutopsyDue);
            $("#insp-kpi-awaiting").text(data.countAwaitingAcceptance);
            $("#insp-kpi-completed").text(data.countCompleted);

            // ── RadialBars ─────────────────────────────────────────
            var counts = [data.countNew, data.countScheduled, data.countAutopsyDue, data.countAutopsyInProgress, data.countFinal, data.countAwaitingAcceptance];
            var total = data.totalActive;

            if (total <= 0) {
                $("#insp-radial").hide();
                $("#insp-radial-empty").show();
            } else if (typeof ApexCharts !== "undefined") {
                var series = counts.map(function (c) { return Math.round(c / total * 100); });
                var options = {
                    chart: { type: "radialBar", height: 280, fontFamily: "inherit" },
                    series: series,
                    labels: labels,
                    colors: colors,
                    plotOptions: {
                        radialBar: {
                            hollow: { size: "38%" },
                            dataLabels: {
                                name: { fontSize: "12px" },
                                value: { fontSize: "14px", formatter: function (v) { return v + "%"; } },
                                total: { show: true, label: "Ενεργές", fontSize: "12px", formatter: function () { return String(total); } }
                            }
                        }
                    },
                    legend: { show: false },
                    stroke: { lineCap: "round" }
                };
                new ApexCharts(document.querySelector("#insp-radial"), options).render();

                // Υπόμνημα με απόλυτους αριθμούς
                var lg = labels.map(function (l, i) {
                    return '<div class="col-6"><span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:' + colors[i] + ';"></span> ' +
                        '<small class="text-muted">' + l + '</small> <strong>' + counts[i] + '</strong></div>';
                }).join("");
                $("#insp-radial-legend").html(lg);
            }

            // ── Προσεχείς Αυτοψίες ─────────────────────────────────
            var up = data.upcoming || [];
            if (up.length === 0) {
                $("#insp-upcoming-empty").show();
            } else {
                var rows = up.map(function (u) {
                    var color, label, href;
                    if (u.pendingConfirm) {
                        // Πρώτα αποδοχή/αλλαγή προτεινόμενης ημ/νίας — όχι έναρξη αυτοψίας
                        color = "#98a6ad";
                        label = '<span class="text-muted"><i class="mdi mdi-calendar-question me-1"></i>Επιβεβαίωση · ' + u.autopsyDate + '</span>';
                        href = "/Certificate?certId=" + u.certificateID;
                    } else if (u.daysUntil <= 0) {
                        // Έφτασε η ημ/νία → επιτρέπεται η έναρξη αυτοψίας
                        if (u.overdue) { color = "#fa5c7c"; label = '<span class="text-danger fw-bold">Εκπρόθεσμη</span>'; }
                        else { color = "#ffbc00"; label = '<span class="text-warning fw-bold">Σήμερα</span>'; }
                        href = "/Certificate/ViewCertificate/" + u.certificateID + "?mode=2";
                    } else {
                        // Προγραμματισμένη μελλοντική → δεν ξεκινά ακόμη η αυτοψία, μόνο προβολή
                        color = "#adb5bd";
                        label = (u.daysUntil === 1)
                            ? '<span class="text-muted">Αύριο · ' + u.autopsyDate + '</span>'
                            : '<span class="text-muted">+' + u.daysUntil + ' ημ. · ' + u.autopsyDate + '</span>';
                        href = "/Certificate?certId=" + u.certificateID;
                    }

                    return '<a href="' + href + '" ' +
                        'class="d-flex align-items-center gap-2 py-2 border-bottom text-body" style="text-decoration:none;">' +
                        '<span style="display:inline-block;width:4px;align-self:stretch;border-radius:2px;background:' + color + ';"></span>' +
                        '<span class="flex-grow-1"><strong>' + (u.hotelTitle || "") + '</strong>' +
                        (u.area ? '<br><small class="text-muted">' + u.area + '</small>' : '') + '</span>' +
                        '<span class="text-end" style="font-size:12px;">' + label + '</span></a>';
                }).join("");
                $("#insp-upcoming").html(rows);
            }
        }
    });
}

$(function () {
    if (document.getElementById("inspector-dashboard")) {
        loadInspectorDashboard();
    }
});
