
var UIViewModel = new function() {
  var self = this;
  self.alerts = ko.observableArray([]);
  self.refresh = function() {
    PeerCast.getNotificationMessages(function(results) {
      if (results) {
        self.alerts.push.apply(self.alerts, $.map(results, function (data) {
          var alert = "";
          switch (data.type) {
          case "info":    alert = "alert-info"; break;
          case "warning": alert = "alert-danger"; break;
          case "error":   alert = "alert-error"; break;
          }
          var closed = false;
          return {
            title:   data.title,
            message: data.message,
            type:    data.type,
            clicked: function () {
              if (closed) return;
              switch (data.class) {
              case "newversion":
                window.open("update.html", "_blank");
                break;
              }
            },
            close: function () {
              closed = true;
            },
            alert: alert
          };
        }));
      }
    });
  };

  self.bind = function (target) {
    ko.applyBindings(self, target);
    setInterval(self.refresh, 1000);
  }
};
