// Κοινό ημερολόγιο (FullCalendar v6): αυτοψίες (read-only) + προσωπικές σημειώσεις (to-do).
// Χρησιμοποιείται και στο home dashboard (compact) και στη σελίδα /Calendar (πλήρες).
(function () {
    var calendars = [];               // όλες οι ενεργές instances (για refetch μετά από αλλαγή)
    var modalReady = false;
    var COLORS = ["#727cf5", "#39afd1", "#1D9E75", "#ffbc00", "#fa5c7c", "#6f42c1"];

    function pad(n) { return (n < 10 ? "0" : "") + n; }
    function todayStr() { var d = new Date(); return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()); }

    // ── Modal σημείωσης (γίνεται inject μία φορά στο body) ───────────────
    function ensureModal() {
        if (modalReady) return;
        var swatches = COLORS.map(function (c) {
            return '<span class="cal-swatch" data-color="' + c + '" style="display:inline-block;width:26px;height:26px;border-radius:50%;margin-right:8px;cursor:pointer;background:' + c + ';border:3px solid transparent;"></span>';
        }).join("");

        var html =
        '<div class="modal fade" id="calNoteModal" tabindex="-1">' +
          '<div class="modal-dialog modal-dialog-centered">' +
            '<div class="modal-content">' +
              '<div class="modal-header py-2">' +
                '<h5 class="modal-title" id="calNoteModalTitle">Νέα σημείωση</h5>' +
                '<button type="button" class="btn-close" data-bs-dismiss="modal"></button>' +
              '</div>' +
              '<div class="modal-body">' +
                '<input type="hidden" id="calNoteId" />' +
                '<div class="mb-3">' +
                  '<label class="form-label">Τίτλος</label>' +
                  '<input type="text" class="form-control" id="calNoteTitle" maxlength="250" placeholder="π.χ. Τηλεφώνημα στο ξενοδοχείο" />' +
                '</div>' +
                '<div class="mb-3">' +
                  '<label class="form-label">Ημερομηνία</label>' +
                  '<input type="date" class="form-control" id="calNoteDate" />' +
                '</div>' +
                '<div class="mb-3">' +
                  '<label class="form-label d-block">Χρώμα</label>' +
                  '<div id="calNoteColors">' + swatches + '</div>' +
                '</div>' +
                '<div class="form-check" id="calNoteDoneWrap" style="display:none;">' +
                  '<input class="form-check-input" type="checkbox" id="calNoteDone" />' +
                  '<label class="form-check-label" for="calNoteDone">Ολοκληρώθηκε</label>' +
                '</div>' +
                '<div class="text-danger mt-2" id="calNoteErr" style="display:none;"></div>' +
              '</div>' +
              '<div class="modal-footer py-2">' +
                '<button type="button" class="btn btn-sm btn-outline-danger me-auto" id="calNoteDelete" style="display:none;"><i class="mdi mdi-trash-can-outline me-1"></i>Διαγραφή</button>' +
                '<button type="button" class="btn btn-sm btn-light" data-bs-dismiss="modal">Άκυρο</button>' +
                '<button type="button" class="btn btn-sm btn-success" id="calNoteSave">Αποθήκευση</button>' +
              '</div>' +
            '</div>' +
          '</div>' +
        '</div>';

        $("body").append(html);
        modalReady = true;

        // Επιλογή χρώματος
        $(document).on("click", "#calNoteColors .cal-swatch", function () {
            $("#calNoteColors .cal-swatch").css("border-color", "transparent");
            $(this).css("border-color", "#343a40");
            $("#calNoteColors").data("color", $(this).data("color"));
        });

        $("#calNoteSave").on("click", saveNote);
        $("#calNoteDelete").on("click", deleteNote);
    }

    function selectColor(c) {
        $("#calNoteColors .cal-swatch").css("border-color", "transparent");
        var $sw = $('#calNoteColors .cal-swatch[data-color="' + c + '"]');
        if ($sw.length) $sw.css("border-color", "#343a40");
        else $("#calNoteColors .cal-swatch").first().css("border-color", "#343a40");
        $("#calNoteColors").data("color", $sw.length ? c : COLORS[0]);
    }

    function openModal(ev, dateStr) {
        ensureModal();
        $("#calNoteErr").hide().text("");
        if (ev) {
            $("#calNoteModalTitle").text("Επεξεργασία σημείωσης");
            $("#calNoteId").val(String(ev.id).replace("note-", ""));
            $("#calNoteTitle").val(ev.title);
            $("#calNoteDate").val(ev.startStr ? ev.startStr.substring(0, 10) : todayStr());
            selectColor(ev.backgroundColor || COLORS[0]);
            $("#calNoteDoneWrap").show();
            $("#calNoteDone").prop("checked", !!ev.extendedProps.isDone);
            $("#calNoteDelete").show();
        } else {
            $("#calNoteModalTitle").text("Νέα σημείωση");
            $("#calNoteId").val("");
            $("#calNoteTitle").val("");
            $("#calNoteDate").val(dateStr || todayStr());
            selectColor(COLORS[0]);
            $("#calNoteDoneWrap").hide();
            $("#calNoteDone").prop("checked", false);
            $("#calNoteDelete").hide();
        }
        new bootstrap.Modal(document.getElementById("calNoteModal")).show();
    }

    var todoEls = [];   // containers TODO που ανανεώνονται σε κάθε αλλαγή
    function refetchAll() {
        calendars.forEach(function (c) { c.refetchEvents(); });
        todoEls.forEach(function (id) { renderTodo(id); });
        // ανανέωση «Εκκρεμείς Ενέργειες» (καμπανάκι + κάρτα) live
        if (typeof loadPendingActions === "function") loadPendingActions();
    }

    // ── TODO λίστα σημειώσεων (date - τίτλος, checkbox ολοκλήρωσης) ───────
    function fmtDate(iso) {
        if (!iso) return "";
        var p = iso.substring(0, 10).split("-");
        return p.length === 3 ? (p[2] + "/" + p[1]) : iso;
    }

    function renderTodo(elId) {
        var host = document.getElementById(elId);
        if (!host) return;
        $.ajax({
            type: "POST", url: "/api/HomeApi/GetCalendarEvents", dataType: "json",
            success: function (data) {
                var notes = ((data && data.events) || []).filter(function (e) { return e.type === "note"; });
                // εκκρεμείς πρώτα (κατά ημερομηνία), ολοκληρωμένες στο τέλος
                notes.sort(function (a, b) {
                    if (a.isDone !== b.isDone) return a.isDone ? 1 : -1;
                    return (a.start || "").localeCompare(b.start || "");
                });

                var emptyEl = document.getElementById(elId + "-empty");
                if (notes.length === 0) {
                    host.innerHTML = "";
                    if (emptyEl) emptyEl.style.display = "";
                    return;
                }
                if (emptyEl) emptyEl.style.display = "none";

                host.innerHTML = notes.map(function (n) {
                    var noteId = String(n.id).replace("note-", "");
                    var done = !!n.isDone;
                    return '<div class="d-flex align-items-start gap-2 py-2 border-bottom todo-row" data-id="' + noteId + '">' +
                        '<input type="checkbox" class="form-check-input mt-1 todo-check" ' + (done ? "checked" : "") + ' style="cursor:pointer;">' +
                        '<span class="badge" style="background:' + (n.color || "#727cf5") + ';">' + fmtDate(n.start) + '</span>' +
                        '<span class="flex-grow-1 todo-title" style="' + (done ? "text-decoration:line-through;opacity:.55;" : "") + '">' + n.title + '</span>' +
                        '</div>';
                }).join("");
            }
        });
    }

    // Toggle ολοκλήρωσης από το checkbox
    $(document).on("change", ".todo-check", function () {
        var $row = $(this).closest(".todo-row");
        var id = $row.data("id");
        var isDone = $(this).is(":checked");
        // φέρε τα τρέχοντα στοιχεία της σημείωσης από το event feed και ενημέρωσε
        $.ajax({
            type: "POST", url: "/api/HomeApi/GetCalendarEvents", dataType: "json",
            success: function (data) {
                var ev = ((data && data.events) || []).filter(function (e) { return e.id === "note-" + id; })[0];
                if (!ev) { refetchAll(); return; }
                $.ajax({
                    type: "POST", url: "/api/HomeApi/SaveCalendarNote", contentType: "application/json",
                    data: JSON.stringify({
                        noteID: Number(id), noteDate: ev.start.substring(0, 10),
                        title: ev.title, color: ev.color, isDone: isDone
                    }),
                    dataType: "json",
                    success: function () { refetchAll(); }
                });
            }
        });
    });

    function saveNote() {
        var title = ($("#calNoteTitle").val() || "").trim();
        var date = $("#calNoteDate").val();
        if (!title || !date) { $("#calNoteErr").text("Συμπληρώστε τίτλο και ημερομηνία.").show(); return; }

        var idVal = $("#calNoteId").val();
        var payload = {
            noteID: idVal ? Number(idVal) : null,
            noteDate: date,
            title: title,
            color: $("#calNoteColors").data("color") || COLORS[0],
            isDone: $("#calNoteDone").is(":checked")
        };

        $("#calNoteSave").prop("disabled", true);
        $.ajax({
            type: "POST", url: "/api/HomeApi/SaveCalendarNote",
            contentType: "application/json", data: JSON.stringify(payload), dataType: "json",
            success: function (r) {
                $("#calNoteSave").prop("disabled", false);
                if (r && r.success) {
                    bootstrap.Modal.getInstance(document.getElementById("calNoteModal")).hide();
                    refetchAll();
                } else { $("#calNoteErr").text((r && r.message) || "Σφάλμα αποθήκευσης.").show(); }
            },
            error: function () { $("#calNoteSave").prop("disabled", false); $("#calNoteErr").text("Σφάλμα αποθήκευσης.").show(); }
        });
    }

    function deleteNote() {
        var idVal = $("#calNoteId").val();
        if (!idVal) return;
        if (!confirm("Διαγραφή της σημείωσης;")) return;
        $.ajax({
            type: "POST", url: "/api/HomeApi/DeleteCalendarNote",
            contentType: "application/json", data: JSON.stringify({ noteID: Number(idVal) }), dataType: "json",
            success: function (r) {
                bootstrap.Modal.getInstance(document.getElementById("calNoteModal")).hide();
                refetchAll();
            }
        });
    }

    // ── Δημιουργία calendar ──────────────────────────────────────────────
    function init(elId, options) {
        var el = document.getElementById(elId);
        if (!el || typeof FullCalendar === "undefined") return null;
        options = options || {};

        var cal = new FullCalendar.Calendar(el, {
            initialView: "dayGridMonth",
            firstDay: 1,
            height: options.height || "auto",
            headerToolbar: {
                left: "prev,next today",
                center: "title",
                right: options.compact ? "" : "addNote"
            },
            customButtons: {
                addNote: { text: "+ Σημείωση", click: function () { openModal(null, todayStr()); } }
            },
            buttonText: { today: "Σήμερα" },
            editable: true,
            dayMaxEvents: options.compact ? 2 : true,
            dayHeaderContent: function (a) { return a.date.toLocaleDateString("el-GR", { weekday: "short" }); },
            datesSet: function (info) {
                var d = info.view.currentStart;
                var t = d.toLocaleDateString("el-GR", { month: "long", year: "numeric" });
                t = t.charAt(0).toUpperCase() + t.slice(1);
                el.querySelectorAll(".fc-toolbar-title").forEach(function (x) { x.textContent = t; });
            },
            events: function (info, success, failure) {
                $.ajax({
                    type: "POST", url: "/api/HomeApi/GetCalendarEvents", dataType: "json",
                    success: function (data) { success((data && data.events) || []); },
                    error: function () { failure(); }
                });
            },
            eventClick: function (info) {
                info.jsEvent.preventDefault();
                var ev = info.event;
                if (ev.extendedProps.type === "note") openModal(ev);
                else if (ev.url) window.location.href = ev.url;
            },
            dateClick: function (info) { openModal(null, info.dateStr); },
            eventDrop: function (info) {
                if (info.event.extendedProps.type !== "note") { info.revert(); return; }
                $.ajax({
                    type: "POST", url: "/api/HomeApi/SaveCalendarNote", contentType: "application/json",
                    data: JSON.stringify({
                        noteID: Number(String(info.event.id).replace("note-", "")),
                        noteDate: info.event.startStr.substring(0, 10),
                        title: info.event.title,
                        color: info.event.backgroundColor,
                        isDone: !!info.event.extendedProps.isDone
                    }),
                    dataType: "json",
                    success: function (r) { if (!r || !r.success) info.revert(); },
                    error: function () { info.revert(); }
                });
            },
            eventDidMount: function (info) {
                if (info.event.extendedProps.isDone) {
                    info.el.style.opacity = "0.55";
                    var t = info.el.querySelector(".fc-event-title");
                    if (t) t.style.textDecoration = "line-through";
                }
            }
        });

        cal.render();
        calendars.push(cal);
        return cal;
    }

    function initTodo(elId) {
        ensureModal();
        if (todoEls.indexOf(elId) === -1) todoEls.push(elId);
        renderTodo(elId);
    }

    window.HotelsTeeCalendar = {
        init: init,
        initTodo: initTodo,
        openNew: function () { openModal(null, todayStr()); }
    };

    // Auto-init βάσει marker elements (το _Layout φορτώνει τα scripts μετά το body)
    $(function () {
        if (document.getElementById("calendar-full")) init("calendar-full", { compact: false });
        if (document.getElementById("calendar-home")) init("calendar-home", { compact: true, height: 520 });
        if (document.getElementById("insp-todo")) initTodo("insp-todo");
    });
})();
