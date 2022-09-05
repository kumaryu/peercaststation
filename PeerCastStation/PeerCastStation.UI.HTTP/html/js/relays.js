
var ChannelEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#channelEditDialog');
    dialog.modal({show: false});
    dialog.on('hide', self.onHide);
    ko.applyBindings(self, dialog.get(0));
  });

  self.channelId       = ko.observable(null);
  self.infoName        = ko.observable(null);
  self.infoUrl         = ko.observable(null);
  self.infoBitrate     = ko.observable(null);
  self.infoContentType = ko.observable(null);
  self.infoMimeType    = ko.observable(null);
  self.infoGenre       = ko.observable(null);
  self.infoDesc        = ko.observable(null);
  self.infoComment     = ko.observable(null);
  self.trackName       = ko.observable(null);
  self.trackCreator    = ko.observable(null);
  self.trackGenre      = ko.observable(null);
  self.trackAlbum      = ko.observable(null);
  self.trackUrl        = ko.observable(null);
  self.source          = ko.observable(null);

  self.show = function(channel) {
    self.channelId(channel.channelId());
    self.infoName(channel.infoName());
    self.infoUrl(channel.infoUrl());
    self.infoBitrate(channel.infoBitrate());
    self.infoContentType(channel.infoContentType());
    self.infoMimeType(channel.infoMimeType());
    self.infoGenre(channel.infoGenre());
    self.infoDesc(channel.infoDesc());
    self.infoComment(channel.infoComment());
    self.trackName(channel.trackName());
    self.trackCreator(channel.trackCreator());
    self.trackGenre(channel.trackGenre());
    self.trackAlbum(channel.trackAlbum());
    self.trackUrl(channel.trackUrl());
    self.source(channel.source());
    dialog.modal('show');
  };
  self.onUpdate = function() {
    var info = {
      name:        self.infoName(),
      url:         self.infoUrl(),
      bitrate:     self.infoBitrate(),
      contentType: self.infoContentType(),
      mimeType:    self.infoMimeType(),
      genre:       self.infoGenre(),
      desc:        self.infoDesc(),
      comment:     self.infoComment()
    };
    var track = {
      name:        self.trackName(),
      creator:     self.trackCreator(),
      genre:       self.trackGenre(),
      album:       self.trackAlbum(),
      url:         self.trackUrl()
    };
    PeerCastStation.setChannelInfo(self.channelId(), info, track).then(
      function () {
        refresh();
        dialog.modal('hide');
      }
    );
  };
};

var BroadcastHistoryViewModel = function(parent, entry) {
  var self = this;
  var updateEntry = function() {
    PeerCastStation.addBroadcastHistory({
      yellowPage:  self.yellowPage(),
      networkTyep: self.networkType(),
      streamType:  self.streamType(),
      contentType: self.contentType(),
      streamUrl:   self.streamUrl(),
      bitrate:     Number(self.bitrate()),
      channelName: self.channelName(),
      genre:       self.genre(),
      description: self.description(),
      comment:     self.comment(),
      contactUrl:  self.contactUrl(),
      trackTitle:  self.trackTitle(),
      trackAlbum:  self.trackAlbum(),
      trackArtist: self.trackArtist(),
      trackGenre:  self.trackGenre(),
      trackUrl:    self.trackUrl(),
      favorite:    self.favorite()
    });
  };
  self.channelName = ko.observable(entry.channelName);
  self.networkType = ko.observable(entry.networkType || "ipv4");
  self.networkName = ko.computed(function () {
    switch (self.networkType()) {
    case 'ipv6':
      return 'IPv6';
    case 'ipv4':
    default:
      return 'IPv4';
    }
  });
  self.streamType  = ko.observable(entry.streamType);
  self.streamUrl   = ko.observable(entry.streamUrl);
  self.bitrate     = ko.observable(entry.bitrate);
  self.contentType = ko.observable(entry.contentType);
  self.yellowPage  = ko.observable(entry.yellowPage);
  self.channelName = ko.observable(entry.channelName);
  self.genre       = ko.observable(entry.genre);
  self.description = ko.observable(entry.description);
  self.comment     = ko.observable(entry.comment);
  self.contactUrl  = ko.observable(entry.contactUrl);
  self.trackTitle  = ko.observable(entry.trackTitle);
  self.trackAlbum  = ko.observable(entry.trackAlbum);
  self.trackArtist = ko.observable(entry.trackArtist);
  self.trackGenre  = ko.observable(entry.trackGenre);
  self.trackUrl    = ko.observable(entry.trackUrl);
  self.favorite    = ko.observable(entry.favorite);
  self.name = ko.observable(
      (self.networkName() || "") + " " +
      (self.channelName() || "") + " " +
      (self.genre()       || "") + " " +
      (self.description() || "") + " - " +
      (self.comment()     || "") +
      " Playing: " + (self.trackTitle() || ""));
  self.select = function () {
    parent.selectedHistory(self);
  };
  self.toggleFavorite = function (itm, e) {
    self.favorite(!self.favorite());
    updateEntry();
    e.stopPropagation();
  };
};

var LocalChannelViewModel = function(owner, initial_value) {
  var self = this;
  self.channelId = ko.observable(initial_value.channelId);
  self.name      = ko.observable(initial_value.info.name);
};

var BroadcastDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#broadcastDialog');
    dialog.modal({show: false});
    dialog.on('hide', self.onHide);
    PeerCastStation.getContentReaders().then(function(result) {
      self.contentTypes.push.apply(self.contentTypes, result);
    });
    PeerCastStation.getSourceStreams().then(function(result) {
      for (var i in result) {
        if ((result[i].type & PeerCastStation.SourceStreamType.Broadcast)!=0) {
          self.sourceStreams.push(result[i]);
        }
      }
    });
    ko.applyBindings(self, dialog.get(0));
  });

  self.networkType  = ko.observable("ipv4");
  self.sourceStream = ko.observable(null);
  self.source       = ko.observable("");
  self.yellowPage   = ko.observable(null);
  self.contentType  = ko.observable(null);
  self.infoName     = ko.observable("");
  self.infoUrl      = ko.observable("");
  self.infoBitrate  = ko.observable("");
  self.infoMimeType = ko.observable("");
  self.infoGenre    = ko.observable("");
  self.infoDesc     = ko.observable("");
  self.infoComment  = ko.observable("");
  self.trackName    = ko.observable("");
  self.trackCreator = ko.observable("");
  self.trackGenre   = ko.observable("");
  self.trackAlbum   = ko.observable("");
  self.trackUrl     = ko.observable("");
  self.localChannels = ko.observableArray();
  self.sourceChannel = ko.observable(null);
  self.sourceChannel.subscribe(function (value) {
    if (value!=null && self.sourceStream() && self.sourceStream().scheme==="loopback") {
      self.source("loopback:" + value.channelId());
    }
  });

  self.sourceStream.subscribe(function (value) {
    if (value!=null) {
      self.source(value.defaultUri);
      if (self.sourceChannel()!=null && value && value.scheme==="loopback") {
        self.source("loopback:" + self.sourceChannel().channelId());
      }
    }
  });

  self.localChannelVisibility = ko.computed(function () {
    var source_stream = self.sourceStream();
    return source_stream && source_stream.scheme==='loopback';
  });
  self.contentTypeVisibility = ko.computed(function () {
    var source_stream = self.sourceStream();
    return source_stream && source_stream.isContentReaderRequired;
  });

  self.yellowPages = ko.observableArray([
    {
      yellowPageId: null,
      name:         '掲載しない',
      uri:          null,
      protocol:     null
    }
  ]);

  self.contentTypes = ko.observableArray();
  self.networkTypes = ko.observableArray([
    { name: 'IPv4', value: 'ipv4' },
    { name: 'IPv6', value: 'ipv6' }
  ]);
  self.sourceStreams = ko.observableArray();
  self.broadcastHistory = ko.observableArray();
  self.selectedHistory = ko.observable({ name: "配信設定履歴" });
  self.selectedHistory.subscribe(function (value) {
    if (!value) return;
    if (value.networkType()) {
      self.networkType(value.networkType());
    }
    for (var i in self.sourceStreams()) {
      var item = self.sourceStreams()[i];
      if (item.name===value.streamType()) {
        self.sourceStream(item);
        break;
      }
    }
    self.source(value.streamUrl());
    var result = /loopback:([0-9a-fA-F]{32})/.exec(value.streamUrl());
    if (result!=null && result[1]) {
      var channel_id = result[1];
      for (var i in self.localChannels()) {
        var channel = self.localChannels()[i];
        if (channel.channelId()===channel_id) {
          self.sourceChannel(channel);
          break;
        }
      }
    }
    self.infoBitrate(value.bitrate());
    for (var i in self.contentTypes()) {
      var item = self.contentTypes()[i];
      if (item.name===value.contentType()) {
        self.contentType(item);
        break;
      }
    }
    for (var i in self.yellowPages()) {
      var item = self.yellowPages()[i];
      if (item.name===value.yellowPage()) {
        self.yellowPage(item);
        break;
      }
    }
    self.infoName(value.channelName());
    self.infoGenre(value.genre());
    self.infoDesc(value.description());
    self.infoComment(value.comment());
    self.infoUrl(value.contactUrl());
    self.trackName(value.trackTitle());
    self.trackAlbum(value.trackAlbum());
    self.trackCreator(value.trackArtist());
    self.trackGenre(value.trackGenre());
    self.trackUrl(value.trackUrl());
  });

  self.show = function() {
    dialog.modal('show');
    PeerCastStation.getYellowPages().then(function(result) {
      self.yellowPages(
        [
          {
            yellowPageId: null,
            name:         '掲載しない',
            uri:          null,
            protocol:     null
          }
        ].concat(result)
      );
    });
    PeerCastStation.getBroadcastHistory().then(function(result) {
      self.broadcastHistory($.map(result, function (value) { return new BroadcastHistoryViewModel(self, value); }));
    });
    PeerCastStation.getChannels().then(function(result) {
      self.localChannels($.map(result, function (value) { return new LocalChannelViewModel(self, value); }));
    })
  };
  self.onBroadcast = function() {
    var info = {
      name:        self.infoName(),
      url:         self.infoUrl(),
      bitrate:     self.infoBitrate(),
      mimeType:    self.infoMimeType(),
      genre:       self.infoGenre(),
      desc:        self.infoDesc(),
      comment:     self.infoComment()
    };
    var track = {
      name:        self.trackName(),
      creator:     self.trackCreator(),
      genre:       self.trackGenre(),
      album:       self.trackAlbum(),
      url:         self.trackUrl()
    };
    var yellowPageId  = self.yellowPage()   ? self.yellowPage().yellowPageId : null;
    var sourceStream  = self.sourceStream() ? self.sourceStream().name       : null;
    var contentReader = self.contentType()  ? self.contentType().name        : null;
    PeerCastStation.broadcastChannel(
      yellowPageId,
      self.networkType(),
      self.source(),
      contentReader,
      info,
      track,
      sourceStream
    ).then(
      function (res) {
        refresh();
        dialog.modal('hide');
      },
      function(err) {
        alert("エラー: " + err.message);
        refresh();
        dialog.modal('hide');
      }
    );
    PeerCastStation.addBroadcastHistory({
      yellowPage:  self.yellowPage() ? self.yellowPage().yellowPageId : null,
      networkType: self.networkType(),
      streamType:  sourceStream,
      contentType: contentReader,
      streamUrl:   self.source(),
      bitrate:     Number(self.infoBitrate()),
      channelName: self.infoName(),
      genre:       self.infoGenre(),
      description: self.infoDesc(),
      comment:     self.infoComment(),
      contactUrl:  self.infoUrl(),
      trackTitle:  self.trackName(),
      trackAlbum:  self.trackAlbum(),
      trackArtist: self.trackCreator(),
      trackGenre:  self.trackGenre(),
      trackUrl:    self.trackUrl()
    });
  };
};

var ChannelConnectionViewModel = function(owner, initial_value) {
  var self = this;
  self.channel = owner;
  self.connectionId     = ko.observable(initial_value.connectionId);
  self.type             = ko.observable(initial_value.type);
  self.status           = ko.observable(initial_value.status);
  self.sendRate         = ko.observable(initial_value.sendRate);
  self.recvRate         = ko.observable(initial_value.recvRate);
  self.protocolName     = ko.observable(initial_value.protocolName);
  self.localRelays      = ko.observable(initial_value.localRelays);
  self.localDirects     = ko.observable(initial_value.localDirects);
  self.contentPosition  = ko.observable(initial_value.contentPosition);
  self.agentName        = ko.observable(initial_value.agentName);
  self.remoteEndpoint   = ko.observable(initial_value.remoteEndpoint);
  self.remoteHostStatus = ko.observable(initial_value.remoteHostStatus);
  self.remoteName       = ko.observable(initial_value.remoteName);
  self.remoteSessionId  = ko.observable(initial_value.remoteSessionId);

  self.connectionName = ko.computed(function () {
    switch (UserConfig.remoteNodeName()) {
    case "sessionId":
      return self.remoteSessionId() || self.remoteName();
    case "endPoint":
      return self.remoteEndPoint();
    case "uri":
    default:
      return self.remoteName();
    }
  });

  self.connectionStatus = ko.computed(function () {
    var result = "unknown";
    switch (self.type()) {
    case "relay":
      if ($.inArray("receiving", self.remoteHostStatus())>=0) {
        if ( $.inArray("firewalled", self.remoteHostStatus())>=0 &&
            !$.inArray("local", self.remoteHostStatus())>=0) {
          if (self.localRelays()!=null && self.localRelays()>0) {
            result = "firewalledRelaying";
          }
          else {
            result = "firewalled";
          }
        }
        else if ($.inArray("relayFull", self.remoteHostStatus())>=0) {
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
      break;
    case "play":
      break;
    case "announce":
    case "source":
    default:
      if ($.inArray("root", self.remoteHostStatus())>=0)    result = "connectionToRoot";
      if ($.inArray("tracker", self.remoteHostStatus())>=0) result = "connectionToTracker";
      break;
    }
    return result;
  });

  self.connections = ko.computed(function () {
    if (self.localRelays()!=null && self.localDirects()!=null) {
      return "[" + self.localDirects() + "/" + self.localRelays() + "]";
    }
    else {
      return "";
    }
  });

  self.bitrate = ko.computed(function () {
    return Math.floor(((self.recvRate() ? self.recvRate() : 0) + (self.sendRate() ? self.sendRate() : 0))*8/1000);
  });

  self.update = function(value) {
    self.type(value.type);
    self.status(value.status);
    self.sendRate(value.sendRate);
    self.recvRate(value.recvRate);
    self.protocolName(value.protocolName);
    self.localRelays(value.localRelays);
    self.localDirects(value.localDirects);
    self.contentPosition(value.contentPosition);
    self.agentName(value.agentName);
    self.remoteEndpoint(value.remoteEndpoint);
    self.remoteHostStatus(value.remoteHostStatus);
    self.remoteName(value.remoteName);
    self.remoteSessionId(value.remoteSessionId);
    return self;
  };

  self.stop = function() {
    PeerCastStation.stopChannelConnection(self.channel.channelId(), self.connectionId()).then(function(result) {
      self.channel.connections.remove(self);
    });
  };
  self.restart = function() {
    PeerCastStation.restartChannelConnection(self.channel.channelId(), self.connectionId());
  };
};

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

  self.stop = function() {
    PeerCastStation.stopAnnounce(self.yellowPageId(), self.channel.channelId()).then(refresh);
  };
  self.reconnect = function() {
    PeerCastStation.restartAnnounce(self.yellowPageId(), self.channel.channelId()).then(refresh);
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
    return (self.infoName() || 'Unknown').replace(/((^\.)|[:\/\\~$<>?"*|])/g, '_') + ext;
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

  PeerCastStation.getChannelConnections(self.channelId()).then(function(result) {
    var connections = $.map(result, function(conn) {
      return new ChannelConnectionViewModel(self, conn);
    });
    self.connections.splice.apply(self.connections, [0, self.connections().length].concat(connections));
  });

  self.showInfo = function() {
    $('#channelInfo-'+self.channelId()).slideToggle("fast");
  };

  self.showPlayer = function() {
    window.open('player.html?channelId=' + self.channelId(), 'PeerCastStation-Play-' + self.channelId(), "");
  };
  self.popupPlayer = function() {
    window.open('player.html?channelId=' + self.channelId(), 'PeerCastStation-Play-' + self.channelId(), "popup");
  };

  var updateConnections = function() {
    PeerCastStation.getChannelConnections(self.channelId()).then(function(result) {
      var connections = self.connections();
      var new_connections = $.map(result, function(conn) {
        for (var i=0,l=connections.length; i<l; i++) {
          if (connections[i].connectionId()==conn.connectionId) {
            return connections[i].update(conn);
          }
        }
        return new ChannelConnectionViewModel(self, conn);
      });
      self.connections.splice.apply(self.connections, [0, self.connections().length].concat(new_connections));
    });
  };

  var connectionsVisible = false;
  self.showConnections = function() {
    connectionsVisible = !connectionsVisible;
    if (connectionsVisible) updateConnections();
    $('#channelConnections-'+self.channelId()).slideToggle("fast");
  };

  var createTreeNode = function(node) {
    var version = "";
    if (node.version)   version += node.version;
    if (node.versionVP) version += " VP" + node.versionVP;
    if (node.versionEX) version += " "   + node.versionEX;
    var status = "";
    if (!node.isReceiving) status = "notReceiving";
    else if (node.isFirewalled) {
      if (node.localRelays>0) {
        status = "firewalledRelaying";
      }
      else {
        status = "firewalled";
      }
    }
    else if (!node.isRelayFull) status = "relayable";
    else if (node.localRelays>0) {
      status = "relayFull";
    }
    else {
      status = "notRelayable";
    }
    var connections = "[" + node.localDirects + "/" + node.localRelays + "]";
    var connection_name = node.address + ":" + node.port;
    switch (UserConfig.remoteNodeName()) {
    case "sessionId":
      connection_name = node.sessionId || connection_name;
      break;
    case "endPoint":
    case "uri":
    default:
      connection_name = node.address + ":" + node.port;
      break;
    }
    return {
      connectionStatus: status,
      remoteName:   node.address + ":" + node.port,
      connectionName: connection_name,
      connections:  connections,
      agentVersion: version,
      children: $.map(node.children, createTreeNode)
    };
  };

  var updateRelayTree = function() {
    PeerCastStation.getChannelRelayTree(self.channelId()).then(function(result) {
      var nodes = $.map(result, createTreeNode);
      self.nodes.splice.apply(self.nodes, [0, self.nodes().length].concat(nodes));
    });
  }

  var updateYellowPages = function(new_value) {
    var yellowpages = self.yellowPages();
    var new_yps = $.map(new_value, function(yp) {
      for (var i=0,l=yellowpages.length; i<l; i++) {
        if (yellowpages[i].name()==yp.name && yellowpages[i].protocol()==yp.protocol ) {
          return yellowpages[i].update(yp);
        }
      }
      return new ChannelYellowPageViewModel(self, yp);
    });
    self.yellowPages.splice.apply(self.yellowPages, [0, self.yellowPages().length].concat(new_yps));
  }

  var relayTreeVisible = false;
  self.showRelayTree = function() {
    relayTreeVisible = !relayTreeVisible;
    $('#channelRelayTree-'+self.channelId()).slideToggle("fast");
    if (relayTreeVisible) updateRelayTree();
  };

  self.editChannelInfo = function() {
    ChannelEditDialog.show(self);
  };

  self.stop = function() {
    PeerCastStation.stopChannel(self.channelId()).then(refresh);
  };

  self.bump = function() {
    PeerCastStation.bumpChannel(self.channelId());
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
    updateYellowPages(c.yellowPages);
    if (connectionsVisible) updateConnections();
    if (relayTreeVisible) updateRelayTree();
    return self;
  };
};

var channelsViewModel = new function() {
  var self = this;
  self.isFirewalled = ko.observable(null);
  self.channels = ko.observableArray();
  self.authToken = ko.observable(null);
  self.playPageUrls = ko.observableArray();

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
    PeerCastStation.getListeners().then(function(result) {
      var urls = [];
      for (var i=0; i<result.length; i++) {
        var port = result[i];
        if ((port.globalAccepts & PeerCastStation.OutputStreamType.Play)===0) continue;
        var addresses = port.globalAddresses;
        if (addresses.length===0) continue;
        for (var j=0; j<addresses.length; j++) {
          var host = addresses[j] + ":" + port.port;
          var family = "";
          if (/:/.exec(addresses[j])) {
            host = "[" + addresses[j] + "]:" + port.port;
            family = "(IPv6)";
          }
          var url = "http://" + host + "/html/play.html";
          if (port.globalAuthorizationRequired) {
            url = url + "?auth=" + port.authToken;
          }
          urls.push({
            url: url,
            family: family
          });
        }
      }
      self.playPageUrls(urls);
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

