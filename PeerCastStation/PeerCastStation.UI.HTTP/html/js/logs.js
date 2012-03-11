
var LogsViewModel = new function() {
  var self = this;
  self.level    = ko.observable(0);
  self.logs     = ko.observable('');
  self.lines    = 0;
  self.logLevel = ko.computed({
    read: function() { return self.level().toString(); },
    write: function(value) {
      value = Math.floor(Number(value));
      if (!self.updating && self.level!=value) {
        PeerCast.setLogSettings({ level:value });
      }
      self.level(value);
    }
  });

  self.updating = false;
  self.update = function() {
    PeerCast.getLogSettings(function(result) {
      if (result) {
        self.updating = true;
        self.logLevel(result.level);
        self.updating = false;
      }
    });
    PeerCast.getLog(self.lines, null, function(result) {
      if (result && result.lines>0) {
        self.updating = true;
        if (self.lines>0) {
          self.logs(self.logs() + "\n" + result.log);
        }
        else {
          self.logs(result.log);
        }
        self.lines += result.lines;
        self.updating = false;
      }
    });
  };

  self.clear = function() {
    PeerCast.clearLog(function() {
      self.lines = 0;
      self.logs('');
    });
  };
};

$(document).ready(function() {
  LogsViewModel.update();
  setInterval(function() { LogsViewModel.update(); }, 1000);
  ko.applyBindings(LogsViewModel, $('#logs').get(0));
});

