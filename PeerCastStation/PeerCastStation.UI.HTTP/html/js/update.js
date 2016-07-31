
var UpdateViewModel = new function() {
  var EnclosureViewModel = function (data) {
    self.title = "ZIP";
    switch (data) {
    case "archive":
      self.title = "ZIP";
      break;
    case "installer":
      self.title = "インストーラ";
      break;
    }
    self.url = data.url;
  };
  var self = this;
  self.versions = ko.observable();
  self.enclosures = ko.observableArray();
  self.refresh = function() {
    PeerCast.getNewVersions(function(results) {
      if (!results) return;
      var descs = "";
      for (var i in results) {
        var ver = results[i];
        descs += ver.description;
      }
      self.versions(descs);
      for (var i in results[0].enclosures) {
        var title = "ZIP";
        switch (results[0].enclosures[i].installerType) {
        case "archive":
          title = "ZIPをダウンロード";
          break;
        case "installer":
          title = "インストーラをダウンロード";
          break;
        case "serviceinstaller":
          title = "サービス版インストーラをダウンロード";
          break;
        }
        self.enclosures.push({
          title: title,
          url:   results[0].enclosures[i].url
        });
      }
    });
  };

  self.bind = function (target) {
    ko.applyBindings(self, target);
    self.refresh();
  }
};
