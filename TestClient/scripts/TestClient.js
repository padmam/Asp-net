var total = 0,
    delays = 0,
    speed = 10;

var requests = ["simple/time", "simple/birthday?name=John&age=25", "simple/exception?msg=some exception text", "list/sum?values=1,2,3,4,5", "list/add?values=1.03,2.17&units=Dollars", "list/text?values=first,second,third", "list/any?values=one,2,-3.4&desc=mixed parameters", "image/diagram"];

$(document).ready(function () {
    $("#speed").val(speed);
    $("#speed").change(function () {
        speed = $(this).val();
    });
    $("#reset").click(function () {
        total = 0;
        delays = 0;
    });
    $("#submit").click(function () {
        sendRequest();
    });

    $("#tabs").tabs();
    $("button").button();

    setTimer(1000/speed);

    $("c").each(function (i) {
        $(this).replaceWith("<span class='code'>" + $(this).html() + "</span>");
    });

    $("#requests").autocomplete({
        source: requests
    });

    $("#requests").keypress(function (e) {
        if (e.which == 13) {
            sendRequest();
        }
    });
});

function sendRequest() {
    /*
    We are doing a little hack here, because in order to render dynamic images
    into our HTML page we need to place them inside image tag...
    */
    var value = $.trim($("#requests").val().toLowerCase());
    if (value.length < 1)
        return;
    if (value == "image/diagram") {
        $("#result").html("<img src='data/image/diagram'>");
        return;
    }
    // End of the hack (special case).
    $.ajax({
        url: "data/" + value,
        success: function (data) {
            $("#result").html(data);
        },
        error: function (jqXHR, textStatus, errorThrown) {
            // the following is also a hack, because for the demo we used date/*/*
            // as a filter, and thus we do not handle multi-segment requests here,
            // which is just a limitation for the demo.
            $("#result").html("<span style='color:Red;'>Requests must be in the form of segment1/segment2</span>");
        }
    });
}

function setTimer(delay) {
    if (delay < 1) {
        delay = 1;
    }
    setTimeout(function () {
        processTimer();
    }, delay);
}

function processTimer() {
    var d1 = new Date();
    var start = d1.getTime();

    // We append a dummy parameter -current time to guarantee the request isn't cached;
    $.get("data/simple/time?dummy=" + start, function (data) {
        var d2 = new Date();
        var end = d2.getTime();
        var delay = end - start;
        delays += delay;
        total++;
        $("#time").html(data);
        $("#calls").html(total);
        $("#average").html(round(delays / total));
        setTimer((1000 / speed) - delay - 1); // loop the request;
    });
}

function round(num) {
    return Math.round(num * Math.pow(10, 2)) / Math.pow(10, 2);
}