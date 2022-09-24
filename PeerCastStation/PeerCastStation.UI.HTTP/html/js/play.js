
var ChannelYellowPageViewModel = function(owner, initial_value) {
  var self = this;
  self.channel      = owner;
  self.yellowPageId = ko.observable(initial_value.yellowPageId);
  self.name         = ko.observable(initial_value.name);
  self.protocol     = ko.observable(initial_value.protocol);
  self.uri          = ko.observable(initial_value.uri);
  self.status       = ko.observable(initial_value.status);

  self.update = function(value) {
    self.yellowPageId(value.yellowPageId);
    self.name(value.name);
    self.protocol(value.protocol);
    self.uri(value.uri);
    self.status(value.status);
    return self;
  };
};

var ChannelViewModel = function(owner, initial_value) {
  var self = this;
  self.channelId       = ko.observable(initial_value.channelId);
  self.infoName        = ko.observable(initial_value.info.name);
  self.infoUrl         = ko.observable(initial_value.info.url);
  self.infoBitrate     = ko.observable(initial_value.info.bitrate);
  self.infoContentType = ko.observable(initial_value.info.contentType);
  self.infoMimeType    = ko.observable(initial_value.info.mimeType);
  self.infoGenre       = ko.observable(initial_value.info.genre);
  self.infoDesc        = ko.observable(initial_value.info.desc);
  self.infoComment     = ko.observable(initial_value.info.comment);
  self.trackName       = ko.observable(initial_value.track.name);
  self.trackCreator    = ko.observable(initial_value.track.creator);
  self.trackGenre      = ko.observable(initial_value.track.genre);
  self.trackAlbum      = ko.observable(initial_value.track.album);
  self.trackUrl        = ko.observable(initial_value.track.url);
  self.source          = ko.observable(initial_value.status.source);
  self.network         = ko.observable(initial_value.status.network);
  self.networkType = ko.computed(function () {
    switch (self.network()) {
    case 'ipv6':
      return 'IPv6';
    case 'ipv4':
    default:
      return 'IPv4';
    }
  });
  self.status          = ko.observable(initial_value.status.status);
  self.uptime          = ko.observable(initial_value.status.uptime);
  self.totalDirects    = ko.observable(initial_value.status.totalDirects);
  self.totalRelays     = ko.observable(initial_value.status.totalRelays);
  self.localDirects    = ko.observable(initial_value.status.localDirects);
  self.localRelays     = ko.observable(initial_value.status.localRelays);
  self.isBroadcasting  = ko.observable(initial_value.status.isBroadcasting);
  self.isRelayFull     = ko.observable(initial_value.status.isRelayFull);
  self.isDirectFull    = ko.observable(initial_value.status.isDirectFull);
  self.isReceiving     = ko.observable(initial_value.status.isReceiving);
  self.streamUrl = ko.computed(function () {
    var url = '/stream/' + self.channelId();
    var auth_token = owner.authToken();
    return auth_token ? url + '?auth=' + auth_token : url;
  });
  self.playlistFilename = ko.computed(function () {
    var ext = "";
    var protocol = UserConfig.defaultPlayProtocol()[self.infoContentType()] || 'Unknown';
    switch (protocol) {
    case 'Unknown':
      break;
    case 'MSWMSP':
      ext = ".asx";
      break;
    case 'HTTP':
      ext = ".m3u";
      break;
    case 'RTMP':
      ext = ".m3u";
      break;
    case 'HLS':
      ext = ".m3u8";
      break;
    }
    return self.infoName().replace(/((^\.)|[:\/\\~$<>?"*|])/g, '_') + ext;
  });
  self.playlistUrl = ko.computed(function () {
    var ext = "";
    var parameters = [];
    var protocol = UserConfig.defaultPlayProtocol()[self.infoContentType()] || 'Unknown';
    switch (protocol) {
    case 'Unknown':
      break;
    case 'MSWMSP':
      parameters.push("fmt=asx");
      ext = ".asx";
      break;
    case 'HTTP':
      parameters.push("scheme=http");
      ext = ".m3u";
      break;
    case 'RTMP':
      parameters.push("scheme=rtmp");
      ext = ".m3u";
      break;
    case 'HLS':
      parameters.push("fmt=m3u8");
      ext = ".m3u8";
      break;
    }
    var auth_token = owner.authToken();
    if (auth_token) parameters.push('auth=' + auth_token);
    var query = parameters.Length === 0 ? "" : ("?" + parameters.join('&'));
    return '/pls/' + self.channelId() + ext + query;
  });
  self.connections     = ko.observableArray();
  self.nodes           = ko.observableArray();
  self.yellowPages     = ko.observableArray($.map(initial_value.yellowPages, function(yp) {
    return new ChannelYellowPageViewModel(self, yp);
  }));
  self.uptimeReadable  = ko.computed(function() {
    var seconds = self.uptime();
    var minutes = Math.floor(seconds /   60) % 60;
    var hours   = Math.floor(seconds / 3600);
    return hours + ":" + (minutes<10 ? ("0"+minutes) : minutes);
  });
  self.isFirewalled = ko.computed(function() {
    return channelsViewModel.isFirewalled();
  });

  self.connectionStatus = ko.computed(function () {
    var result = "";
    if (self.isReceiving()) {
      if (self.isFirewalled()) {
        if (self.localRelays()!=null && self.localRelays()>0) {
          result = "firewalledRelaying";
        }
        else {
          result = "firewalled";
        }
      }
      else if (self.isRelayFull()) {
        if (self.localRelays()!=null && self.localRelays()>0) {
          result = "relayFull";
        }
        else {
          result = "notRelayable";
        }
      }
      else {
        result = "relayable";
      }
    }
    else {
      result = "notReceiving";
    }
    return result;
  });

  self.isSourceTracker = ko.computed(function() {
    for (var i in self.connections()) {
      var conn = self.connections()[i];
      if ($.inArray("tracker", conn.remoteHostStatus())>=0) return true;
    }
    return false;
  });

  self.showInfo = function() {
    $('#channelInfo-'+self.channelId()).slideToggle("fast");
  };

  self.play = function () {
    var player = UserConfig.defaultPlayer()[self.infoContentType()] || 'Unknown';
    switch (player) {
    case 'playlist':
      var a = document.createElement('a');
      a.setAttribute("href", self.playlistUrl());
      a.setAttribute("download", self.playlistFilename());
      a.click();
      break;
    case 'html':
      self.showPlayer();
      break;
    case 'html-popup':
    case 'Unknown':
    default:
      self.popupPlayer();
      break;
    }
  };
  self.showPlayer = function() {
    window.open('player.html?channelId=' + self.channelId(), 'PeerCastStation-Play-' + self.channelId(), "");
  };
  self.popupPlayer = function() {
    window.open('player.html?channelId=' + self.channelId(), 'PeerCastStation-Play-' + self.channelId(), "popup");
  };

  self.update = function(c) {
    self.infoName(c.info.name);
    self.infoUrl(c.info.url);
    self.infoBitrate(c.info.bitrate);
    self.infoContentType(c.info.contentType);
    self.infoMimeType(c.info.mimeType);
    self.infoGenre(c.info.genre);
    self.infoDesc(c.info.desc);
    self.infoComment(c.info.comment);
    self.trackName(c.track.name);
    self.trackCreator(c.track.creator);
    self.trackGenre(c.track.genre);
    self.trackAlbum(c.track.album);
    self.trackUrl(c.track.url);
    self.source(c.status.source);
    self.status(c.status.status);
    self.uptime(c.status.uptime);
    self.totalDirects(c.status.totalDirects);
    self.totalRelays(c.status.totalRelays);
    self.localDirects(c.status.localDirects);
    self.localRelays(c.status.localRelays);
    self.isBroadcasting(c.status.isBroadcasting);
    self.isRelayFull(c.status.isRelayFull);
    self.isDirectFull(c.status.isDirectFull);
    self.isReceiving(c.status.isReceiving);
    return self;
  };
};

var channelsViewModel = new function() {
  var self = this;
  self.currentChannel = ko.observable(null);
  self.isFirewalled = ko.observable(null);
  self.channels = ko.observableArray();
  self.authToken = ko.observable(null);

  self.update = function() {
    PeerCastStation.getStatus().then(function(result) {
      self.updateStatus(result);
    });
    PeerCastStation.getChannels().then(function(result) {
      self.updateChannels(result);
    });
    PeerCastStation.getAuthToken().then(function(result) {
      self.authToken(result);
    });
  };

  self.updateStatus = function(status) {
    self.isFirewalled(status.isFirewalled);
  };

  self.updateChannels = function(channel_infos) {
    var new_channels = $.map(channel_infos, function(channel_info) {
      var channels = self.channels();
      for (var i=0,l=channels.length; i<l; i++) {
        if (channels[i].channelId()==channel_info.channelId) {
          return channels[i].update(channel_info);
        }
      }
      return new ChannelViewModel(self, channel_info);
    });
    self.channels.splice.apply(self.channels, [0, self.channels().length].concat(new_channels));
  };

  self.bind = function(target) {
    ko.applyBindings(channelsViewModel, target);
    channelsViewModel.update();
    setInterval(channelsViewModel.update, 1000);
  };
};

function refresh()
{
  channelsViewModel.update();
}
