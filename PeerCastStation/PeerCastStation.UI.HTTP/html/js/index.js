
var PeercastViewModel = new function() {
  var self = this;
  self.apiVersion           = ko.observable(null);
  self.agentName            = ko.observable(null);
  self.isFirewalled         = ko.observable(null);
  self.uptime               = ko.observable(null);
  self.globalRelayEndPoint  = ko.observable(null);
  self.globalDirectEndPoint = ko.observable(null);
  self.localRelayEndPoint   = ko.observable(null);
  self.localDirectEndPoint  = ko.observable(null);

  self.firewalledStatus = ko.computed(function() {
    var firewalled = self.isFirewalled();
    if (firewalled==true) {
      return 'OK';
    }
    else if (firewalled==false) {
      return 'NG';
    }
    else {
      return '不明';
    }
  });

  self.uptimeReadable  = ko.computed(function() {
    var seconds = self.uptime();
    var minutes = Math.floor(seconds /  60) % 60;
    var hours   = Math.floor(seconds / 360);
    return hours + ":" + (minutes<10 ? ("0"+minutes) : minutes);
  });

  self.globalRelayAddress = ko.computed(function() {
    var endpoint = self.globalRelayEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.globalDirectAddress = ko.computed(function() {
    var endpoint = self.globalDirectEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.localRelayAddress = ko.computed(function() {
    var endpoint = self.localRelayEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });

  self.localDirectAddress = ko.computed(function() {
    var endpoint = self.localDirectEndPoint();
    return endpoint ? (endpoint[0]+':'+endpoint[1]) : '無し';
  });


  self.update = function() {
    PeerCast.getVersionInfo(function(result) {
      if (result) {
        self.apiVersion(result.apiVersion);
        self.agentName(result.agentName);
      }
    });
    PeerCast.getStatus(function(result) {
      if (result) {
        self.isFirewalled(result.isFirewalled);
        self.uptime(result.uptime);
        self.globalRelayEndPoint(result.globalRelayEndPoint);
        self.globalDirectEndPoint(result.globalDirectEndPoint);
        self.localRelayEndPoint(result.localRelayEndPoint);
        self.localDirectEndPoint(result.localDirectEndPoint);
      }
    });
  };
};

$(document).ready(function() {
  PeercastViewModel.update();
  ko.applyBindings(PeercastViewModel, $('#status').get(0));
});

