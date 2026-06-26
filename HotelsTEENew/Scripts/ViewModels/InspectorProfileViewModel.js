function InspectorProfileViewModel() {
    var self = this;

    self.showLoader = function () {
        if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("show");
    };
    self.hideLoader = function () {
        setTimeout(function () {
            if (typeof $("#modal-loader").modal === "function") $("#modal-loader").modal("hide");
        }, 350);
    };

    self.perifereies = ko.observableArray([]);
    self.saveSuccess = ko.observable(false);
    self.saveError = ko.observable(false);

    self.fetchData = function () {
        self.showLoader();
        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/InspectorProfileApi/GetProfile',
            success: function (data) {
                self.hideLoader();
                if (!data.success) return;

                var mapped = data.perifereies.map(function (per) {
                    var coversAll = ko.observable(per.coversAll);

                    var pes = per.pes.map(function (pe) {
                        return {
                            kalID: pe.kalID,
                            title: pe.title,
                            isChecked: ko.observable(pe.isChecked),
                        };
                    });

                    // Όταν αλλάζει το "ολόκληρη περιφέρεια":
                    // ON  → τσεκάρει όλες τις ΠΕ οπτικά (disabled)
                    // OFF → ξε-τσεκάρει όλες τις ΠΕ
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

                self.perifereies(mapped);
            },
            error: function () { self.hideLoader(); }
        });
    };

    self.toggleExpand = function (per) {
        per.isExpanded(!per.isExpanded());
    };

    self.saveAreas = function () {
        self.showLoader();
        self.saveSuccess(false);
        self.saveError(false);

        var areas = [];
        self.perifereies().forEach(function (per) {
            if (per.coversAll()) {
                // Αποθηκεύουμε μόνο το row της Περιφέρειας (levelID=3)
                areas.push({ kalID: per.kalID, levelID: 3 });
            } else {
                // Αποθηκεύουμε μόνο τις επιλεγμένες ΠΕ (levelID=4)
                per.pes().forEach(function (pe) {
                    if (pe.isChecked()) {
                        areas.push({ kalID: pe.kalID, levelID: 4 });
                    }
                });
            }
        });

        $.ajax({
            type: 'POST',
            dataType: 'json',
            contentType: 'application/json',
            data: JSON.stringify(areas),
            url: '/api/InspectorProfileApi/SaveAreas',
            success: function (data) {
                self.hideLoader();
                if (data.success) {
                    self.saveSuccess(true);
                } else {
                    self.saveError(true);
                }
            },
            error: function () {
                self.hideLoader();
                self.saveError(true);
            }
        });
    };

    self.fetchData();
}
