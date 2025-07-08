$(function () {
    var timerInterval, startTime;

    function parseUtc(utcString) {
        return new Date(utcString.replace(' ', 'T') + 'Z');
    }

    function startStopwatch(fromTimestamp) {
        stopStopwatch();
        if (fromTimestamp) {
            startTime = parseUtc(fromTimestamp).getTime();
        } else {
            startTime = Date.now();
        }
        updateStopwatch();
        timerInterval = setInterval(updateStopwatch, 1000);
    }

    function updateStopwatch() {
        var diff = Math.floor((Date.now() - startTime) / 1000);
        var minutes = String(Math.floor(Math.abs(diff) / 60)).padStart(2, '0');
        var seconds = String(Math.abs(diff) % 60).padStart(2, '0');
        $('#stopwatch-time').text((diff < 0 ? '-' : '') + minutes + ':' + seconds);
    }

    function stopStopwatch() {
        clearInterval(timerInterval);
    }

    function showProcessing(fromTimestamp) {
        startStopwatch(fromTimestamp);
        $('#processingPanel').show();
    }
    function hideProcessing() {
        stopStopwatch();
        $('#processingPanel').hide();
    }

    function hideStatus() {
        $('#statusMessage').hide().removeClass('alert-success alert-danger');
    }

    var rowColors = {
        Success: '#dff0d8',
        Error: '#f2dede',
        Processing: '#f5f5f5'
    };

    function createJobRow(status, source, dest, startTime, endTime, errorMsg) {
        var $tr = $('<tr>').css('background-color', rowColors[status] || '');
        $tr.append($('<td>').text(status));
        $tr.append($('<td>').text(source));
        $tr.append($('<td>').text(dest));
        $tr.append($('<td>').text(new Date(startTime).toLocaleString()));
        $tr.append($('<td>').text(endTime ? new Date(endTime).toLocaleString() : ''));
        $tr.append($('<td>').text(errorMsg || ''));
        return $tr;
    }

    function addJobRow(status, source, dest, startTime, endTime, errorMsg) {
        $('#tbJobs').prepend(createJobRow(status, source, dest, startTime, endTime, errorMsg));
    }

    function loadHistory() {
        $.getJSON('/api/alyas/catalogrefreshapi/GetCatalogRefreshHistory')
            .done(function (data) {
                $('#tbJobs').empty();
                if (data && data.length) {
                    data.forEach(function (rec) {
                        $('#tbJobs').append(createJobRow(
                            rec.Status, rec.Source, rec.Destination,
                            rec.StartTime, rec.EndTime, rec.ErrorMessage
                        ));
                    });
                    var latest = data[0];
                    if (latest.Status === 'Processing') {
                        showProcessing(latest.StartTime);
                    } else {
                        hideProcessing();
                    }
                } else {
                    hideProcessing();
                }
            })
            .fail(function () {
                $('#statusMessage')
                    .addClass('alert alert-danger')
                    .text('Error loading history')
                    .show();
            });
    }

    $('#selDestination').multiselect({
        includeSelectAllOption: true,
        nonSelectedText: '-- select destination --',
        allSelectedText: 'All selected',
        buttonWidth: '100%',
        enableFiltering: false
    });

    function getSelectedDestinations() {
        return $('#selDestination').val() || [];
    }

    function validateButton() {
        var src = $('#selSource').val();
        var dsts = getSelectedDestinations();
        $('#btnRefresh').prop('disabled', !(src && dsts.length));
    }

    $('#selSource').on('change', validateButton);
    $('#selDestination').on('change', validateButton);
    validateButton();

    $('#btnRefresh').click(function () {
        hideStatus();
        showProcessing();
        $('#btnRefresh').prop('disabled', true);

        var payload = {
            Source: $('#selSource').val(),
            Destinations: getSelectedDestinations()
        };

        $.ajax({
            url: '/api/alyas/catalogrefreshapi/catalogrefresh',
            method: 'POST',
            data: JSON.stringify(payload),
            contentType: 'application/json'
        })
            .done(function (data) {
                hideProcessing();
                $('#tbJobs').empty();
                data.forEach(function (rec) {
                    $('#tbJobs').append(createJobRow(
                        rec.Status, rec.Source, rec.Destination,
                        rec.StartTime, rec.EndTime, rec.ErrorMessage
                    ));
                });

                var processing = data.find(r => r.Status === 'Processing');
                if (processing) showProcessing(processing.StartTime);

                validateButton();

                var latest = data[0];
                if (latest.Status !== 'Processing') {
                    var cls = latest.Status === 'Success'
                        ? 'alert alert-success'
                        : 'alert alert-danger';
                    $('#statusMessage')
                        .removeClass()
                        .addClass(cls)
                        .text(latest.Status + (latest.ErrorMessage ? ': ' + latest.ErrorMessage : ''))
                        .show();
                }
            })
            .fail(function () {
                hideProcessing();
                $('#statusMessage')
                    .removeClass()
                    .addClass('alert alert-danger')
                    .text('Error invoking refresh')
                    .show();
                validateButton();
            });
    });

    loadHistory();
});
