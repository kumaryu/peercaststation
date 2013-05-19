
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
    PeerCast.setChannelInfo(
        self.channelId(),
        info,
        track,
        function() {
          refresh();
          dialog.modal('hide');
        });
  };
};

var BroadcastDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#broadcastDialog');
    dialog.modal({show: false});
    dialog.on('hide', self.onHide);
    ko.applyBindings(self, dialog.get(0));
  });

  self.source          = ko.observable("");
  self.yellowPage      = ko.observable(null);
  self.contentType     = ko.observable(null);
  self.infoName        = ko.observable("");
  self.infoUrl         = ko.observable("");
  self.infoBitrate     = ko.observable("");
  self.infoMimeType    = ko.observable("");
  self.infoGenre       = ko.observable("");
  self.infoDesc        = ko.observable("");
  self.infoComment     = ko.observable("");
  self.trackName       = ko.observable("");
  self.trackCreator    = ko.observable("");
  self.trackGenre      = ko.observable("");
  self.trackAlbum      = ko.observable("");
  self.trackUrl        = ko.observable("");

  self.yellowPages     = ko.observableArray([
    {
      yellowPageId: null,
      name:         '掲載しない',
      uri:          null,
      protocol:     null
    }
  ]);
  self.contentTypes    = ko.observableArray();
  PeerCast.getYellowPages(function(result, error) {
    if (!error) {
      self.yellowPages.push.apply(self.yellowPages, result);
    }
  });
  PeerCast.getContentReaders(function(result, error) {
    if (!error) {
      self.contentTypes.push.apply(self.contentTypes, result);
    }
  });

  self.show = function() {
    dialog.modal('show');
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
    var yellowPageId  = self.yellowPage()  ? self.yellowPage().yellowPageId : null;
    var contentReader = self.contentType() ? self.contentType().name        : null;
    PeerCast.broadcastChannel(
        yellowPageId,
        self.source(),
        contentReader,
        info,
        track,
        function() {
          refresh();
          dialog.modal('hide');
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

  self.note = ko.computed(function () {
    var result = "　";
    switch (self.type()) {
    case "relay":
      if ($.inArray("receiving", self.remoteHostStatus())>=0) {
        if ( $.inArray("firewalled", self.remoteHostStatus())>=0 &&
            !$.inArray("local", self.remoteHostStatus())>=0) {
          result = "×";
        }
        else if ($.inArray("relayFull", self.remoteHostStatus())>=0) {
          if (self.localRelays() && self.localRelays()>0) {
            result = "○";
          }
          else {
            result = "△";
          }
        }
        else {
          result = "◎";
        }
      }
      else {
        result = "■";
      }
      break;
    case "play":
      break;
    case "announce":
    case "source":
    default:
      if ($.inArray("root", self.remoteHostStatus())>=0)    result = "Ｒ";
      if ($.inArray("tracker", self.remoteHostStatus())>=0) result = "Ｔ";
      break;
    }
    return result;
  });

  self.connections = ko.computed(function () {
    if (self.localRelays() && self.localDirects()) {
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
    return self;
  };

  self.stop = function() {
    PeerCast.stopChannelConnection(self.channel.channelId(), self.connectionId(), function(result, error) {
      if (!error && result) {
        self.channel.connections.remove(self);
      }
    });
  };
  self.restart = function() {
    PeerCast.restartChannelConnection(self.channel.channelId(), self.connectionId());
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
    PeerCast.stopAnnounce(self.yellowPageId(), self.channel.channelId(), refresh);
  };
  self.reconnect = function() {
    PeerCast.restartAnnounce(self.yellowPageId(), self.channel.channelId(), refresh);
  };
};

var ChannelViewModel = function(initial_value) {
  var self = this;
  self.channelId       = ko.observable(initial_value.channelId);
  self.streamUrl       = ko.computed(function() { return '/stream/'+self.channelId();});
  self.playlistUrl     = ko.computed(function() { return '/pls/'+self.channelId();});
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
  self.status          = ko.observable(initial_value.status.status);
  self.uptime          = ko.observable(initial_value.status.uptime);
  self.totalDirects    = ko.observable(initial_value.status.totalDirects);
  self.totalRelays     = ko.observable(initial_value.status.totalRelays);
  self.localDirects    = ko.observable(initial_value.status.localDirects);
  self.localRelays     = ko.observable(initial_value.status.localRelays);
  self.isBroadcasting  = ko.observable(initial_value.status.isBroadcasting);
  self.isRelayFull     = ko.observable(initial_value.status.isRelayFull);
  self.isDirectFull    = ko.observable(initial_value.status.isDirectFull);
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

  PeerCast.getChannelConnections(self.channelId(), function(result) {
    if (result) {
      var connections = $.map(result, function(conn) {
        return new ChannelConnectionViewModel(self, conn);
      });
      self.connections.splice.apply(self.connections, [0, self.connections().length].concat(connections));
    }
  });

  self.showInfo = function() {
    $('#channelInfo-'+self.channelId()).slideToggle("fast");
  };

  var updateConnections = function() {
    PeerCast.getChannelConnections(self.channelId(), function(result) {
      if (result) {
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
      }
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
    var title =
      node.address + ":" + node.port +
      " (" + node.localDirects + "/" + node.localRelays + ") " +
      (node.isFirewalled ? "0" : "") +
      (node.isRelayFull ? "-" : "") +
      (node.isReceiving ? "" : "B") + " " +
      version;
    return {
      title:         title,
      sessionId:     node.sessionId,
      address:       node.address,
      port:          node.port,
      isFirewalled:  node.isFirewalled,
      localRelays:   node.localRelays,
      localDirects:  node.localDirects,
      isTracker:     node.isTracker,
      isRelayFull:   node.isRelayFull,
      isDirectFull:  node.isDirectFull,
      isReceiving:   node.isReceiving,
      isControlFull: node.isControlFull,
      version:       node.version,
      versionVP:     node.versionVP,
      versionEX:     node.versionEX,
      children: $.map(node.children, createTreeNode)
    };
  };

  var updateRelayTree = function() {
    PeerCast.getChannelRelayTree(self.channelId(), function(result) {
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
    PeerCast.stopChannel(self.channelId(), refresh);
  };

  self.bump = function() {
    PeerCast.bumpChannel(self.channelId());
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
    updateYellowPages(c.yellowPages);
    if (connectionsVisible) updateConnections();
    if (relayTreeVisible) updateRelayTree();
    return self;
  };
};

var channelsViewModel = new function() {
  var self = this;
  self.currentChannel = ko.observable(null);
  self.isFirewalled = ko.observable(null);
  self.channels = ko.observableArray();

  self.update = function() {
    PeerCast.getStatus(function(result) {
      if (result) self.updateStatus(result);
    });
    PeerCast.getChannels(function(result) {
      if (result) self.updateChannels(result);
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
      return new ChannelViewModel(channel_info);
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

