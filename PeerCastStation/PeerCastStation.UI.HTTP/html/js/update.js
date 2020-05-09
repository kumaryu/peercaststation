
var UpdateViewModel = new function() {
  var self = this;
  self.currentVersion = ko.observable();
  self.versions = ko.observable();
  self.refresh = function() {
    PeerCastStation.getVersionInfo().then(function (result) {
      self.currentVersion(result.agentName);
    });
    PeerCastStation.getNewVersions().then(function (results) {
      if (!results) return;
      var descs = "";
      for (var i in results) {
        var ver = results[i];
        descs += ver.description;
      }
      self.versions(descs);
    });
  };
  self.updateStatus   = ko.observable('ready');
  self.updateProgress = ko.observable();
  self.checkUpdated = function () {
    var check_func = function () {
      PeerCastStation.getVersionInfo().then(
        function (result) {
          if (self.currentVersion() !== result.agentName) {
            self.updateStatus('completed');
          }
          else {
            window.setTimeout(check_func, 1000);
          }
        },
        function (err) {
          window.setTimeout(check_func, 1000);
        }
      );
    };
    window.setTimeout(check_func, 1000);
  };

  self.updating = function (result) {
    if (!result) {
      window.setTimeout(function () { PeerCastStation.getUpdateStatus().then(self.updating); }, 1000);
      return;
    }
    self.updateStatus(result.status);
    self.updateProgress(Math.floor(result.progress * 100.0));
    switch (result.status) {
    case "progress":
      window.setTimeout(function () { PeerCastStation.getUpdateStatus().then(self.updating); }, 1000);
      break;
    case "succeeded":
      if (self.currentVersion()!==result.agentName) {
        self.updateStatus('completed');
      }
      else {
        window.setTimeout(function () { PeerCastStation.getUpdateStatus().then(self.updating); }, 1000);
      }
      break;
    }
  };

  self.doUpdate = function () {
    switch (self.updateStatus()) {
    case 'ready':
      PeerCastStation.updateAndRestart().then(self.updating);
      break;
    case 'succeeded':
    case 'progress':
      break;
    case 'completed':
    case 'failed':
      window.close();
      break;
    }
  };

  self.bind = function (target) {
    ko.applyBindings(self, target);
    self.refresh();
  }
};

