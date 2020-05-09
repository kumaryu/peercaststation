
var LogsViewModel = new function() {
  var self = this;
  self.level    = ko.observable(0);
  self.logs     = ko.observable('');
  self.logLevel = ko.computed({
    read: function() { return self.level().toString(); },
    write: function(value) {
      value = Math.floor(Number(value));
      if (!self.updating && self.level!=value) {
        PeerCastStation.setLogSettings({ level:value });
      }
      self.level(value);
    }
  });

  self.updating = false;
  self.update = function() {
    PeerCastStation.getLogSettings().then(function(result) {
      self.updating = true;
      self.logLevel(result.level);
      self.updating = false;
    });
    PeerCastStation.getLog(null, null).then(function(result) {
      if (result.lines>0) {
        self.updating = true;
        self.logs(result.log);
        self.updating = false;
      }
    });
  };

  self.clear = function() {
    PeerCastStation.clearLog().then(function() {
      self.logs('');
    });
  };

  self.bind = function(target) {
    self.update();
    ko.applyBindings(self, target);
    setInterval(self.update, 1000);
  };
};

