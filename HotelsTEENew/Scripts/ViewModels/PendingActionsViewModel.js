// Εκκρεμείς Ενέργειες: γεμίζει το καμπανάκι 🔔 του topbar (σε όλες τις σελίδες)
// και την κάρτα "Εκκρεμείς Ενέργειες" στην αρχική σελίδα (αν υπάρχει).
function loadPendingActions() {

    var colorIcon = function (a) {
        return '<i class="' + a.icon + ' text-' + a.colorClass + ' me-1"></i>';
    };

    $.ajax({
        type: "POST",
        dataType: "json",
        url: "/api/PendingActionsApi/GetPendingActions",
        success: function (data) {
            if (!data || !data.success) return;

            var actions = data.actions || [];

            // ── Καμπανάκι topbar ───────────────────────────────────
            var $badge = $("#pending-bell-badge");
            if (actions.length > 0) {
                $badge.text(actions.length).show();
            } else {
                $badge.hide();
            }

            var bellHtml;
            if (actions.length > 0) {
                bellHtml = actions.map(function (a) {
                    return '<a href="' + a.link + '" class="dropdown-item notify-item py-2 border-bottom">' +
                        '<div class="d-flex">' +
                        '<div class="me-2 mt-1">' + colorIcon(a) + '</div>' +
                        '<div>' +
                        '<strong class="d-block">' + a.title + '</strong>' +
                        '<small class="text-muted">' + a.description + '</small>' +
                        '</div></div></a>';
                }).join("");
            } else {
                bellHtml = '<div class="text-center text-muted p-3">' +
                    '<i class="mdi mdi-check-circle-outline font-18 d-block"></i>' +
                    'Καμία εκκρεμής ενέργεια</div>';
            }
            $("#pending-bell-list").html(bellHtml);

            // ── Κάρτα αρχικής σελίδας ──────────────────────────────
            var $card = $("#pending-actions-card");
            if ($card.length) {
                if (actions.length > 0) {
                    var cardHtml = actions.map(function (a) {
                        return '<a href="' + a.link + '" class="list-group-item list-group-item-action d-flex align-items-center">' +
                            '<span class="me-2 font-18">' + colorIcon(a) + '</span>' +
                            '<span class="flex-grow-1">' +
                            '<strong class="d-block">' + a.title + '</strong>' +
                            '<small class="text-muted">' + a.description + '</small>' +
                            '</span>' +
                            '<i class="uil uil-angle-right-b text-muted"></i>' +
                            '</a>';
                    }).join("");

                    $("#pending-actions-list").html(cardHtml);
                    $("#pending-actions-count").text(actions.length);
                    $card.show();
                } else {
                    $card.hide();
                }
            }
        }
    });
}

$(function () {
    if ($("#pending-bell").length) {
        loadPendingActions();
    }
});
