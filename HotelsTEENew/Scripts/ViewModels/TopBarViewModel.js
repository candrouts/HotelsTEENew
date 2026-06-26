
function TopBarViewModel() {
    var self = this;

    self.error = ko.observable(false);
    self.errorMessage = ko.observable("");

    self.success = ko.observable(false);
    self.successMessage = ko.observable("");

    self.hotelDetails = ko.observable();

    self.category = ko.observable("");
    self.hotelTitle = ko.observable("");
    self.hotelType = ko.observable("");
    self.totalRooms = ko.observable(0);
    self.totalBeds = ko.observable(0);
    self.samplingText = ko.observable("");

    self.certificateID = ko.observable(null);
    var id = $("#model").val();
    if (id > 0) {
        self.certificateID(parseInt(id));
    } else {
        self.certificateID(0);
    }


    self.fetchData = function () {
        $.ajax({
            type: 'POST',
            dataType: 'json',
            url: '/api/TopBarApi/GetHotelDetails/' + self.certificateID(), 
            success: function (data) {


                if (data != null) {
                    self.category(data.category);
                    self.hotelTitle(data.hotelTitle);
                    self.hotelType(data.hotelType);
                    self.totalRooms(data.totalRooms);
                    self.totalBeds(data.totalBeds);

                    // Επιλογή κειμένου Δειγματοληψίας
                    var beds = parseInt(self.totalBeds() || 0, 10);
                    if (beds < 51) {
                        self.samplingText("Μικρό ξενοδοχείο (<51 κλίνες): Ο αξιολογητής καλείται να ελέγξει το 15% του συνόλου των δωματίων.");
                    } else if (beds >= 51 && beds <= 200) {
                        self.samplingText("Μεσαίου μεγέθους ξενοδοχείο (51–200 κλίνες): Ο αξιολογητής καλείται να ελέγξει το 10% του συνόλου των δωματίων.");
                    } else if (beds > 200) {
                        self.samplingText("Μεγάλο ξενοδοχείο (>200 κλίνες): Ο αξιολογητής καλείται να ελέγξει το 5% του συνόλου των δωματίων.");
                    } else {
                        self.samplingText("Δεν έχουν οριστεί κλίνες για το κατάλυμα.");
                    }
                }

            }
        });
    }



    self.fetchData();
}