
var UpdateViewModel = new function() {
  var self = this;
  self.versions = ko.observable();
  self.link = ko.observable();
  self.refresh = function() {
    PeerCast.getNewVersions(function(results) {
      if (!results) return;
      var descs = "";
      var link  = results[0].link;
      for (var i in results) {
        var ver = results[i];
        descs += ver.description;
      }
      self.versions(descs);
      self.link(link);
    });
  };

  self.bind = function (target) {
    ko.applyBindings(self, target);
    self.refresh();
  }
};
