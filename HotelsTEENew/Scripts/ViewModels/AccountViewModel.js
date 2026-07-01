function AccountLoginViewModel() {
    var self = this;

    self.error = ko.observable(false);
    self.errorMessage = ko.observable("");

    self.success = ko.observable(false);
    self.successMessage = ko.observable("");

    self.showLoader = function () {
        $('#load_screen').show(0)
    }

    self.hideLoader = function () {
        $('#load_screen').hide(0)
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

    self.UserName = ko.observable("");
    self.Password = ko.observable("");

    self.UserNameState = ko.observable(false);
    self.PasswordState = ko.observable(false);
    self.rememberme = ko.observable(false);

    // Κατάσταση φόρτωσης (spinner + απενεργοποίηση κουμπιού κατά την προσπάθεια)
    self.isLoading = ko.observable(false);

    // Καθάρισμα σφάλματος μόλις ο χρήστης πληκτρολογεί ξανά
    var clearError = function () { if (self.error()) { self.error(false); self.errorMessage(""); } return true; };
    self.UserName.subscribe(clearError);
    self.Password.subscribe(clearError);

    self.submitLogin = function () {
        if (self.isLoading()) return;   // αποφυγή διπλής υποβολής

        self.error(false);
        self.errorMessage("");

        var uname = (self.UserName() || "").trim();
        var pass = self.Password() || "";

        if (uname.length === 0 || pass.length === 0) {
            self.errorMessage("Συμπληρώστε όνομα χρήστη και κωδικό.");
            self.error(true);
            return;
        }

        self.isLoading(true);

        $.ajax({
            type: 'POST',
            dataType: 'json',
            data: { UserName: uname, Password: pass, RememberMe: self.rememberme() },
            url: '/api/AccountApi/',
            success: function (data) {
                if (data && data.success === true) {
                    document.location = "/Home";   // παραμένει το loading μέχρι το redirect
                    return;
                }

                self.isLoading(false);
                if (data && data.responseText === "locked") {
                    self.errorMessage("Ο λογαριασμός κλειδώθηκε προσωρινά λόγω πολλών αποτυχημένων προσπαθειών. Δοκιμάστε ξανά αργότερα ή επαναφέρετε τον κωδικό σας.");
                } else {
                    self.errorMessage("Λανθασμένο όνομα χρήστη ή κωδικός.");
                }
                self.error(true);
            },
            error: function () {
                self.isLoading(false);
                self.errorMessage("Πρόβλημα επικοινωνίας με τον server. Προσπαθήστε ξανά.");
                self.error(true);
            }
        });
    }


    self.onEnter = function (d, e) {
        if (e.keyCode == 13) {
            self.submitLogin();
            return false;
        }
        return true;
    }

}
