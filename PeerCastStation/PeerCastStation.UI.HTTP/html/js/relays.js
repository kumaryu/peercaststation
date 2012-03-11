
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

var ChannelOutputViewModel = function(owner, initial_value) {
  var self = this;
  self.channel = owner;
  self.outputId = ko.observable(initial_value.outputId);
  self.name     = ko.observable(initial_value.name);
  self.type     = ko.observable(initial_value.type);
  self.typeName = ko.computed(function() {
    var type = self.type();
    if ((type & PeerCast.OutputStreamType.Relay)!=0)     return "Relay";
    if ((type & PeerCast.OutputStreamType.Play)!=0)      return "Play";
    if ((type & PeerCast.OutputStreamType.Interface)!=0) return "Interface";
    return "Other";
  });

  self.update = function(value) {
    self.name(value.name);
    self.type(value.type);
    return self;
  };

  self.stop = function() {
    PeerCast.stopChannelOutput(self.channel.channelId(), self.outputId(), function(result, error) {
      if (!error) {
        self.channel.outputs.remove(self);
      }
    });
  };
};

var ChannelViewModel = function(initial_value) {
  var self = this;
  self.channelId       = ko.observable(initial_value.channelId);
  self.streamUrl       = ko.computed(function() { return '/stream/'+self.channelId;});
  self.playlistUrl     = ko.computed(function() { return '/pls/'+self.channelId;});
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
  self.outputs         = ko.observableArray();
  self.uptimeReadable  = ko.computed(function() {
    var seconds = self.uptime();
    var minutes = Math.floor(seconds /  60) % 60;
    var hours   = Math.floor(seconds / 360);
    return hours + ":" + (minutes<10 ? ("0"+minutes) : minutes);
  });
  self.isFirewalled = ko.computed(function() {
    return channelsViewModel.isFirewalled();
  });

  PeerCast.getChannelOutputs(self.channelId(), function(result) {
    if (result) {
      var outputs = $.map(result, function(output) {
        return new ChannelOutputViewModel(self, output);
      });
      self.outputs.splice.apply(self.outputs, [0, self.outputs().length].concat(outputs));
    }
  });

  self.showInfo = function() {
    $('#channelInfo-'+self.channelId()).slideToggle("fast");
  };

  self.outputsVisible = false;
  self.showOutputs = function() {
    self.outputsVisible = !self.outputsVisible;
    $('#channelOutputs-'+self.channelId()).slideToggle("fast");
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
    if (self.outputsVisible) {
      PeerCast.getChannelOutputs(self.channelId(), function(result) {
        if (result) {
          var outputs = self.outputs();
          var new_outputs = $.map(result, function(output_info) {
            for (var i=0,l=outputs.length; i<l; i++) {
              if (outputs[i].outputId()==output_info.outputId) {
                return outputs[i].update(output_info);
              }
            }
            return new ChannelOutputViewModel(self, output_info);
          });
          self.outputs.splice.apply(self.outputs, [0, self.outputs().length].concat(new_outputs));
        }
      });
    }
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
};
$(document).ready(function() {
  $('#channels').tooltip({selector: "a[rel=tooltip]"});
  ko.applyBindings(channelsViewModel, $('#channels').get(0));
  channelsViewModel.update();
  setInterval(refresh, 1000);
});

function refresh()
{
  channelsViewModel.update();
}

function broadcastChannel()
{
  BroadcastDialog.show();
}

