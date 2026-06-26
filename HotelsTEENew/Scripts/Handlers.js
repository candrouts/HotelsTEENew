

ko.bindingHandlers.iCheckCheckBox = {
    init: (el, valueAccessor) => {
        $(el).iCheck({
            checkboxClass: 'icheckbox_square-blue'
        });
        var observable = valueAccessor();
        $(el).on("ifChanged", function () {
            observable(this.checked);
        });
    },
    update: (el, valueAccessor) => {
        var val = ko.utils.unwrapObservable(valueAccessor());
        if (val) {
            $(el).iCheck('check');
        } else {
            $(el).iCheck('uncheck');
        }
    }
};


ko.bindingHandlers.iCheckRadio = {
    init: (el, valueAccessor) => {
        $(el).iCheck({
            radioClass: 'iradio_square-blue'
        });

        $(el).on("ifChanged", function () {

            var observable = valueAccessor();

            if (observable != null) {

                if (observable() != null) {
                    observable(this.checked);
                }
            }


        });
    },

    update: (el, valueAccessor) => {
        var val = ko.utils.unwrapObservable(valueAccessor());
        if (val) {
            $(el).iCheck('check');
        } else {
            $(el).iCheck('uncheck');
        }
    }
};



ko.bindingHandlers.dropzone = {
    init: (el, valueAccessor) => {

        var files = valueAccessor().files;
        //  console.log(files);
        var hotelCriteriaID = valueAccessor().hotelCriteriaID;
        var criteriaFileID = valueAccessor().criteriaFileID;

        var id = $(el).attr("id")
     
        var myDropzone = $(el).dropzone({
            //url: "/file/post",
            addRemoveLinks: false,
            maxFilesize: 500,
            maxFiles: 1000,
            uploadMultiple: true,
            acceptedFiles: "image/*,application/pdf,.doc,.docx,.xls,.xlsx,.csv,.tsv,.ppt,.pptx,.pages,.odt,.rtf,.mp4,.mkv,.mov,.heic",
            dictResponseError: 'Error uploading file!',
            init: function () {

               

                this.on("sending", function (file, xhr, data) {
                    var id = $(el).find("#criteriaFileID").val();
                    var hotelCriteriaID = $(el).find("#hotelCriteriaID").val();
                    
                    data.append("hotelCriteriaID", hotelCriteriaID);
                    data.append("criteriaFileID", id);
                    
                });
               
                this.on("maxfilesexceeded", function (file) {
                    files.remove(file);
                });
              

                this.on("complete", function (file) {
                    this.removeFile(file);
                    // uploadedFiles.push(file);

                    if (this.getUploadingFiles().length === 0 && this.getQueuedFiles().length === 0) {

                        refreshHotelCriteriaFiles(hotelCriteriaID, criteriaFileID);
                    }

                });
            }
        });


    },
    update: (el, valueAccessor) => {
    }

};



ko.bindingHandlers.bsTooltip = {
    init: function (el, valueAccessor) {
        const title = ko.unwrap(valueAccessor());
        const tip = new bootstrap.Tooltip(el, {
            title: title,
            trigger: 'hover focus',
            placement: 'top'
        });
        ko.utils.domNodeDisposal.addDisposeCallback(el, function () {
            tip.dispose();
        });
    },
    update: function (el, valueAccessor) {
        const title = ko.unwrap(valueAccessor());
        const tip = bootstrap.Tooltip.getInstance(el);
        if (tip) {

            tip.setContent({ '.tooltip-inner': title });
        } else {
            new bootstrap.Tooltip(el, {
                title: title,
                trigger: 'hover focus',
                placement: 'top'
            });
        }
    }
};