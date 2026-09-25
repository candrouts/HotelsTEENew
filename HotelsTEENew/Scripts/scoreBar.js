// Συμπαγής μπάρα βαθμολογίας (σε δοκιμή παράλληλα με την υφιστάμενη).
// Προσθέτει στο viewmodel μόνο παράγωγα δεδομένα (sb*) πάνω στα υπάρχοντα
// computeds: medals, tier, byTotalTier, isDemoted, gatingMessage, totalScore.
function attachScoreBar(self) {
    var MAX = 95;

    function medalKey(title) {
        var t = (title || "").toLowerCase();
        if (t.indexOf("bronze") >= 0 || t.indexOf("χάλκ") >= 0) return "bronze";
        if (t.indexOf("silver") >= 0 || t.indexOf("αργυρ") >= 0 || t.indexOf("ασημ") >= 0) return "silver";
        if (t.indexOf("gold") >= 0 || t.indexOf("χρυσ") >= 0) return "gold";
        if (t.indexOf("platinum") >= 0 || t.indexOf("πλατιν") >= 0) return "plat";
        return "none";
    }
    var ICONS = { none: "–", bronze: "🥉", silver: "🥈", gold: "🥇", plat: "💎" };
    function pct(v) { return (Math.max(0, Math.min(MAX, v)) / MAX * 100).toFixed(2) + "%"; }

    function sortedMedals() {
        return (self.medals() || []).slice().sort(function (a, b) { return a.min - b.min; });
    }

    // Ζώνες της μπάρας από τα μετάλλια της βάσης (ό,τι ορίσει ο admin)
    self.sbZones = ko.pureComputed(function () {
        var ms = sortedMedals(), cur = self.tier();
        return ms.map(function (m, i) {
            var to = i < ms.length - 1 ? ms[i + 1].min : MAX;
            var key = medalKey(m.title);
            return {
                title: m.title,
                showName: key !== "none",
                key: key,
                left: pct(m.min),
                width: ((Math.min(to, MAX) - Math.min(m.min, MAX)) / MAX * 100).toFixed(2) + "%",
                mid: pct((m.min + Math.min(to, MAX)) / 2),
                isFirst: i === 0,
                isCurrent: !!cur && cur.id === m.id
            };
        });
    });

    self.sbMedalKey = ko.pureComputed(function () { var t = self.tier(); return medalKey(t ? t.title : ""); });
    self.sbMedalIcon = ko.pureComputed(function () { return ICONS[self.sbMedalKey()]; });
    self.sbMedalName = ko.pureComputed(function () { var t = self.tier(); return t ? t.title : "Αταξινόμητο"; });

    self.sbPinLeft = ko.pureComputed(function () {
        return pct(parseFloat(self.totalScore()) || 0);
    });
    // Κοντά στα άκρα το bubble «κουμπώνει» στην αρχή/τέλος της μπάρας (το βελάκι δείχνει πάντα το σκορ)
    self.sbPinAlign = ko.pureComputed(function () {
        var v = parseFloat(self.totalScore()) || 0;
        return v < 4 ? "start" : (v > MAX - 4 ? "end" : "mid");
    });

    // «Επόμενο βήμα»: υποβιβασμός έχει προτεραιότητα
    self.sbNextState = ko.pureComputed(function () {
        if (self.isDemoted()) return "warn";
        var ms = sortedMedals(), t = self.byTotalTier();
        var idx = t ? ms.findIndex(function (m) { return m.id === t.id; }) : -1;
        return idx === ms.length - 1 ? "top" : "info";
    });

    self.sbNextHtml = ko.pureComputed(function () {
        var state = self.sbNextState();
        if (state === "warn") return "Κλειδωμένο σε <b>" + self.sbMedalName() + "</b>";
        if (state === "top") return "Ανώτατη βαθμίδα";
        var ms = sortedMedals(), t = self.byTotalTier(), x = parseFloat(self.totalScore()) || 0;
        var idx = t ? ms.findIndex(function (m) { return m.id === t.id; }) : -1;
        var next = ms[idx + 1];
        if (!next) return "";
        return "<b>" + Math.max(0, next.min - x).toFixed(2) + "</b> για " + next.title;
    });

    self.sbNextDetail = ko.pureComputed(function () {
        var state = self.sbNextState();
        if (state === "warn") return self.gatingMessage();
        if (state === "top") return "Το κατάλυμα βρίσκεται στην ανώτατη βαθμίδα.";
        var ms = sortedMedals(), t = self.byTotalTier(), x = parseFloat(self.totalScore()) || 0;
        var idx = t ? ms.findIndex(function (m) { return m.id === t.id; }) : -1;
        var next = ms[idx + 1];
        if (!next) return "";
        return "Χρειάζονται ακόμη " + Math.max(0, next.min - x).toFixed(2) + " βαθμοί για " + next.title +
               " (" + next.min + "/95), εφόσον καλύπτονται και οι ελάχιστες βάσεις των πυλώνων.";
    });
}
