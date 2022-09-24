
var UIViewModel = new function() {
  var self = this;
  self.alerts = ko.observableArray([]);
  self.newVersionAvailable = ko.observable(false);
  self.refresh = function () {
    PeerCastStation.getNotificationMessages().then(function(results) {
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
    });
    PeerCastStation.getNewVersions().then(function(results) {
      self.newVersionAvailable(results.length>0);
    });
  };

  self.bind = function (target) {
    ko.applyBindings(self, target);
  }
  $(function() { setInterval(self.refresh, 1000); });
};

var UserConfig = new function () {
  var self = this;
  var loading = false;
  self.remoteNodeName = ko.observable("sessionId");
  self.defaultPlayProtocol = ko.observable({});
  self.defaultPlayer = ko.observable({});
  self.ypChannels = ko.observable({});

  var loadingTask = null;
  self.loadConfigInternal = function() {
    loadingTask = Promise.all([
      PeerCastStation.getUserConfig('default', 'ui').then(function (value) {
        if (!value || !value.remoteNodeName) return;
        loading = true;
        self.remoteNodeName(value.remoteNodeName);
        loading = false;
      }),
      PeerCastStation.getUserConfig('default', 'defaultPlayProtocol').then(function (value) {
        if (!value) return;
        loading = true;
        self.defaultPlayProtocol(value);
        loading = false;
      }),
      PeerCastStation.getUserConfig('default', 'defaultPlayer').then(function (value) {
        if (!value) return;
        loading = true;
        self.defaultPlayer(value);
        loading = false;
      }),
      PeerCastStation.getUserConfig('default', 'ypChannels').then(function (value) {
        if (!value) return;
        loading = true;
        self.ypChannels(value);
        loading = false;
      })
    ]);
  }

  self.loadConfig = function() {
    return loadingTask;
  };

  self.saveConfig = function() {
    if (loading) return;
    var ui = {
      remoteNodeName: self.remoteNodeName()
    };
    return Promise.all([
      PeerCastStation.setUserConfig('default', 'ui', ui),
      PeerCastStation.setUserConfig('default', 'defaultPlayProtocol', self.defaultPlayProtocol()),
      PeerCastStation.setUserConfig('default', 'defaultPlayer', self.defaultPlayer()),
      PeerCastStation.setUserConfig('default', 'ypChannels', self.ypChannels())
    ]);
  };

  self.exportBlob = function() {
    var defaultUser = {
      ui: { remoteNodeName: self.remoteNodeName() },
      defaultPlayProtocol: self.defaultPlayProtocol(),
      defaultPlayer: self.defaultPlayer(),
      ypChannels: self.ypChannels()
    };
    return new Blob([JSON.stringify({ default: defaultUser }, null, 2)], { "type" : "application/json" });
  };

  self.importBlob = function(blob) {
    console.log(blob.type);
    switch (blob.type) {
    case null:
    case "application/json":
        var reader = new FileReader();
        reader.onload = function (event) {
          var doc = JSON.parse(event.target.result);
          if (!doc) return;
          if (!doc.default) return;
          if (doc.default.ui) {
            if (doc.default.ui.remoteNodeName) {
              self.remoteNodeName(doc.default.ui.remoteNodeName);
            }
          }
          if (doc.default.defaultPlayProtocol) {
            self.defaultPlayProtocol(doc.default.defaultPlayProtocol);
          }
          if (doc.default.defaultPlayer) {
            self.defaultPlayer(doc.default.defaultPlayer);
          }
          if (doc.default.ypChannels) {
            self.ypChannels(doc.default.ypChannels);
          }
          self.saveConfig();
        };
        reader.readAsText(blob);
        break;
    }
  };

  $(function () {
    self.loadConfigInternal();
  });
}

