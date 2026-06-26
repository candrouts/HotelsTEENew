

function MenuViewModel() {
    var self = this;

    self.error = ko.observable(false);
    self.errorMessage = ko.observable("");

    self.success = ko.observable(false);
    self.successMessage = ko.observable("");

    

    self.role = ko.observable(null);
    //self.exploitingCompanyID = ko.observable("");
    

    self.fetchData = function () {     
        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/MenuApi/GetMenu',
            success: function (data) {

              
                if (data != null) {
                    self.role(data.role);
                }

            }
        });
    }

   

    self.fetchData();
}
