// Διαχείριση ελάχιστων βάσεων μεταλλίων ανά πυλώνα (admin, role=100)
function AdminMedalsViewModel() {
    var self = this;

    self.showLoader = function () {
        if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("show");
    };
    self.hideLoader = function () {
        setTimeout(function () {
            if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("hide");
        }, 300);
    };

    self.usePillarThresholds = ko.observable(false);
    self.medals = ko.observableArray([]);     // {id, title} — στήλες μήτρας
    self.medalsEdit = ko.observableArray([]); // {id, title(obs), min(obs), max(obs)} — όρια συνόλου
    self.pillars = ko.observableArray([]);    // {id, title, totalUnits, cells:[...]}

    self.fetchData = function () {
        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            url: "/api/AdminMedalsApi/GetMatrix",
            success: function (data) {
                self.hideLoader();
                if (!data || !data.success) return;

                self.usePillarThresholds(data.usePillarThresholds);

                // Όλα τα μετάλια — επεξεργάσιμα όρια συνόλου
                self.medalsEdit((data.medals || []).map(function (m) {
                    return {
                        id: m.id,
                        title: ko.observable(m.title),
                        min: ko.observable(m.min),
                        max: ko.observable(m.max)
                    };
                }));

                // Στήλες μήτρας: μετάλια με κατώφλι > 0 (αγνοούμε το "Αταξινόμητο")
                var medalCols = (data.medals || []).filter(function (m) { return m.min > 0; });
                self.medals(medalCols);

                var thresholds = data.thresholds || [];
                var find = function (medalID, categoryID) {
                    return thresholds.find(function (t) { return t.medalID === medalID && t.categoryID === categoryID; });
                };

                var pillars = (data.pillars || []).map(function (p) {
                    var cells = medalCols.map(function (m) {
                        var existing = find(m.id, p.id);
                        var value = ko.observable(existing ? existing.minValue : "");
                        var isPercent = ko.observable(existing ? String(existing.isPercent) : "true");

                        var cell = {
                            medalID: m.id,
                            categoryID: p.id,
                            value: value,
                            isPercent: isPercent
                        };

                        // Ένδειξη: αν % → δείξε αντίστοιχα μόρια, και αντίστροφα
                        cell.requiredText = ko.pureComputed(function () {
                            var v = parseFloat(value());
                            if (isNaN(v) || v <= 0) return "";
                            if (isPercent() === "true" || isPercent() === true) {
                                return "= " + (v / 100 * p.totalUnits).toFixed(1) + " μόρ.";
                            }
                            if (p.totalUnits > 0) {
                                return "= " + (v / p.totalUnits * 100).toFixed(0) + "%";
                            }
                            return "";
                        });

                        return cell;
                    });

                    return { id: p.id, title: p.title, totalUnits: p.totalUnits, cells: cells };
                });

                self.pillars(pillars);
            },
            error: function () { self.hideLoader(); }
        });
    };

    // Αποθήκευση ορίων συνολικής βαθμολογίας
    self.saveMedals = function () {
        var medals = self.medalsEdit().map(function (m) {
            return { id: m.id, title: m.title(), min: parseFloat(m.min()) || 0, max: parseFloat(m.max()) || 0 };
        });

        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            contentType: "application/json",
            data: JSON.stringify(medals),
            url: "/api/AdminMedalsApi/SaveMedals",
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    $("#medals-saved-toast").fadeIn(150).delay(1200).fadeOut(300);
                    self.fetchData();   // ανανέωση (επηρεάζει & τις στήλες μήτρας)
                } else {
                    alert("Σφάλμα αποθήκευσης ορίων.");
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.save = function () {
        var cells = [];
        self.pillars().forEach(function (p) {
            p.cells.forEach(function (c) {
                var v = parseFloat(c.value());
                if (!isNaN(v) && v > 0) {
                    cells.push({
                        medalID: c.medalID,
                        categoryID: c.categoryID,
                        minValue: v,
                        isPercent: (c.isPercent() === "true" || c.isPercent() === true)
                    });
                }
            });
        });

        self.showLoader();
        $.ajax({
            type: "POST",
            dataType: "json",
            contentType: "application/json",
            data: JSON.stringify({ usePillarThresholds: self.usePillarThresholds(), cells: cells }),
            url: "/api/AdminMedalsApi/SaveMatrix",
            success: function (data) {
                self.hideLoader();
                if (data && data.success) {
                    $("#medals-saved-toast").fadeIn(150).delay(1200).fadeOut(300);
                } else {
                    alert("Σφάλμα αποθήκευσης.");
                }
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.fetchData();
}
