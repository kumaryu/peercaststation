
var tagsEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#tagsEditDialog');
    dialog.modal({show: false});
    ko.applyBindings(self, dialog.get(0));
  });

  self.model    = null;
  self.pattern  = ko.observable(null);
  self.tags     = ko.observable(null);
  self.color    = ko.observable('default');
  self.onUpdate = null;

  self.setColor = function(name) {
    return function() {
      self.color(name);
    };
  };
  self.show = function(fav, on_update) {
    self.model = fav;
    self.pattern(fav.channelName());
    self.tags(fav.tags());
    self.color(fav.color());
    self.onUpdate = on_update;
    dialog.modal('show');
  };
  self.onOK = function() {
    self.model.channelName(self.pattern());
    self.model.tags(self.tags());
    self.model.color(self.color());
    if (self.onUpdate) self.onUpdate(self.model);
    dialog.modal('hide');
  };
};

var YPChannelViewModel = function(owner, initial_value) {
  var self = this;
  self.yellowPage      = ko.observable(initial_value.yellowPage);
  self.channelId       = ko.observable(initial_value.channelId);
  self.tracker         = ko.observable(initial_value.tracker);
  self.infoName        = ko.observable(initial_value.name);
  self.infoUrl         = ko.observable(initial_value.contactUrl);
  self.infoBitrate     = ko.observable(initial_value.bitrate);
  self.infoContentType = ko.observable(initial_value.contentType);
  self.infoGenre       = ko.observable(initial_value.genre);
  self.infoDesc        = ko.observable(initial_value.description);
  self.infoComment     = ko.observable(initial_value.comment);
  self.trackName       = ko.observable(initial_value.trackTitle);
  self.trackCreator    = ko.observable(initial_value.creator);
  self.trackGenre      = ko.observable("");
  self.trackAlbum      = ko.observable(initial_value.album);
  self.trackUrl        = ko.observable(initial_value.trackUrl);
  self.uptime          = ko.observable(initial_value.uptime);
  self.listeners       = ko.observable(initial_value.listeners);
  self.relays          = ko.observable(initial_value.relays);
  self.color           = ko.observable(initial_value.listeners < -1 ? 'blue' : 'default');
  self.tags            = ko.observable(null);
  self.isPlaying       = ko.observable(false);
  self.isSelected      = ko.computed(function() {
    return owner.selectedChannel()==self;
  });
  self.isPlayable      = ko.computed(function() {
    var channel_id = self.channelId();
    if (channel_id==null || channel_id==="" || channel_id==="00000000000000000000000000000000") return false;
    return true;
  });
  self.isInfoChannel   = ko.computed(function() {
    return self.listeners()<-1;
  });
  self.isFavorite      = ko.computed(function() {
    return self.tags()!=null && self.tags()!=='';
  });
  self.attrRel = ko.computed(function() {
    return self.isFavorite() ? 'tooltip' : '';
  });
  self.streamUrl       = ko.computed(function() {
    return '/stream/' + self.channelId() + "?tip=" + self.tracker();
  });
  self.playlistUrl     = ko.computed(function() {
    return '/pls/'+self.channelId()+ "?tip=" + self.tracker();
  });
  self.uptimeReadable  = ko.computed(function() {
    var seconds = self.uptime();
    var minutes = Math.floor(seconds /   60) % 60;
    var hours   = Math.floor(seconds / 3600);
    return hours + ":" + (minutes<10 ? ("0"+minutes) : minutes);
  });

  self.onSelected = function() {
    if (self.isInfoChannel()) return;
    owner.selectedChannel(self);
  };

  self.onOpened = function() {
    if (!self.isPlayable()) return;
    window.open(self.playlistUrl());
  };

  self.toString = function() {
    return self.infoName()    + " " +
           self.infoGenre()   + " " +
           self.infoDesc()    + " " +
           self.infoComment() + " " +
           self.trackName()   + " " +
           self.trackCreator();
  };
};

var FavoriteChannelViewModel = function(fav) {
  var self = this;
  self.channelName = ko.observable(fav.channelName);
  self.tags        = ko.observable(fav.tags);
  self.color       = ko.observable(fav.color);
  self.pattern = ko.computed(function () {
    return RegExp(self.channelName(), 'i');
  });
};

var YPChannelsViewModel = function() {
  var self = this;
  self.channels = ko.observableArray();
  self.sortColumn = ko.observable({ sortBy: 'uptime', ascending: true });
  self.isLoading = ko.observable(false);
  self.searchText = ko.observable("");
  self.selectedChannel = ko.observable(null);
  self.favoriteChannels = ko.observableArray();
  self.isChannelSelected = ko.computed(function () {
    return self.selectedChannel()!=null;
  });
  self.channelPlayable = ko.computed(function () {
    var channel = self.selectedChannel();
    if (channel==null) return false;
    return channel.isPlayable();
  });
  self.channelPlaylistUrl = ko.computed(function () {
    var channel = self.selectedChannel();
    if (channel==null || !channel.isPlayable()) return "#";
    return channel.playlistUrl();
  })

  self.getMatchedFavorite = function(channel) {
    var fav_channels = self.favoriteChannels();
    for (var i in fav_channels) {
      var fav = fav_channels[i];
      if (fav.pattern().test(channel.infoName())) {
        return fav;
      }
    }
    return null;
  };
  self.favChannel = function() {
    var channel = self.selectedChannel();
    var fav = self.getMatchedFavorite(channel);
    if (fav) {
      tagsEditDialog.show(fav, function () {
        self.saveConfig();
      });
    }
    else {
      fav = new FavoriteChannelViewModel({
        channelName: channel.infoName(), tags: '', color: 'default'
      });
      tagsEditDialog.show(fav, function () {
        self.favoriteChannels.push(fav);
        self.saveConfig();
      });
    }
  };

  self.update = function() {
    self.isLoading(true);
    PeerCast.updateYPChannels(function(result) {
      if (result) {
        var new_channels = $.map(result, function(channel_info) {
          return new YPChannelViewModel(self, channel_info);
        });
        self.channels.splice.apply(self.channels, [0, self.channels().length].concat(new_channels));
        PeerCast.getChannels(function(result) {
          if (!result) return;
          var channels = self.channels();
          for (var i in channels) {
            var relaying = false;
            for (var j in result) {
              if (channels[i].channelId()===result[j].channelId) {
                relaying = true;
                break;
              }
            }
            channels[i].isPlaying(relaying);
          }
        });
      }
      self.isLoading(false);
    });
  };

  self.channelList = ko.computed(function () {
    var search = self.searchText();
    var channels = self.channels();
    var column = self.sortColumn();
    if (search!=null && search!=="") {
      var pattern = RegExp(search, "i");
      channels = channels.filter(function (x) {
        return pattern.test(x.toString());
      });
    }
    var favs = self.favoriteChannels();
    return $.map(channels.sort(function (x,y) {
      var xplayable = x.isPlayable() ? 1 : 0;
      var yplayable = y.isPlayable() ? 1 : 0;
      var xval = x[column.sortBy]();
      var yval = y[column.sortBy]();
      var cmp = (xplayable<yplayable ? -1 : (xplayable>yplayable ? 1 : 0)) * 10 + (xval<yval ? -1 : (xval>yval ? 1 : 0));
      return column.ascending ? cmp : -cmp;
    }), function(channel) {
      for (var i in favs) {
        var fav = favs[i];
        if (fav.pattern().test(channel.infoName())) {
          channel.color(fav.color());
          channel.tags(fav.tags());
          break;
        }
      }
      return channel;
    });
  });

  self.setSort = function(name) {
    return function () {
      var column = self.sortColumn();
      if (column.sortBy===name) {
        column.ascending = !column.ascending;
        self.sortColumn(column);
      }
      else {
        self.sortColumn({ sortBy: name, ascending: true });
      }
    };
  }

  self.saveConfig = function() {
    var yp_channels = {
      favorites: $.map(self.favoriteChannels(), function (fav) {
        return {
          channelName: fav.channelName(),
          tags: fav.tags(),
          color: fav.color()
        };
      })
    };
    PeerCast.setUserConfig('default', 'ypChannels', yp_channels);
  };

  self.loadConfig = function() {
    PeerCast.getUserConfig('default', 'ypChannels', function (config) {
      if (config) {
        self.favoriteChannels($.map(config.favorites, function (fav) {
          return new FavoriteChannelViewModel(fav);
        }));
      }
    });
  };

  self.isSorted = function(name) {
    return self.sortColumn().sortBy===name;
  }

  self.bind = function(target) {
    ko.applyBindings(self, target);
    self.update();
  };

  $(function () {
    self.loadConfig();
  });
};

var channelsViewModel = new YPChannelsViewModel();

