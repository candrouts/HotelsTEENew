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

    self.submitLogin = function () {
        self.showLoader();

        var flag = true;
        var model = {};
        model.UserName = self.UserName();
        model.Password = self.Password();
        model.RememberMe = self.rememberme();


        if (self.UserName().length == 0) {
            self.UserNameState(true);
            flag = false;
        }
        if (self.Password().length == 0) {
            self.PasswordState(true)
            flag = false;
        }

        if (flag) {
            $.ajax({
                type: 'POST',
                dataType: 'json',
                data: model,
                url: '/api/AccountApi/',
                success: function (data) {

                    var d = JSON.stringify(data);

                    if (d.indexOf("true") > -1) {
                        var url = "/Home";
                        document.location = url;
                    }
                    else {

                        self.errorMessage("The username or password are incorrect")
                        self.error(true)
                        self.hideLoader();
                    }

                    //self.hideLoader();
                    //alert(d);
                }
            });
        }
        else {
            self.errorMessage("There are some errors")
            self.error(true)
            self.hideLoader();
        }


    }


    self.onEnter = function (d, e) {
        if (e.keyCode == 13) {
            self.submitLogin();
        }
        else {
            return true;
        }
    }

}
