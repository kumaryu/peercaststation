
var tagsEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#tagsEditDialog');
    dialog.modal({show: false});
    ko.applyBindings(self, dialog.get(0));
  });

  self.pattern = ko.observable(null);
  self.tags    = ko.observable(null);
  self.color   = ko.observable(null);

  self.show = function(channel) {
    if (channel!=null) {
      //self.pattern(channel.infoName());
    }
    dialog.modal('show');
  };
  self.onAdd = function() {
    dialog.modal('hide');
  };
};

var YPChannelViewModel = function(initial_value) {
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
  self.isPlaying       = ko.observable(false);
  self.isPlayable      = ko.computed(function() {
    var channel_id = self.channelId();
    if (channel_id==null || channel_id==="" || channel_id==="00000000000000000000000000000000") return false;
    return true;
  });
  self.isInfoChannel   = ko.computed(function() {
    return self.listeners()<-1;
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

  self.toString = function() {
    return self.infoName()    + " " +
           self.infoGenre()   + " " +
           self.infoDesc()    + " " +
           self.infoComment() + " " +
           self.trackName()   + " " +
           self.trackCreator();
  };
};

var YPChannelsViewModel = function() {
  var self = this;
  self.channels = ko.observableArray();
  self.sortColumn = ko.observable({ sortBy: 'uptime', ascending: true });
  self.isLoading = ko.observable(false);
  self.searchText = ko.observable("");

  self.update = function() {
    self.isLoading(true);
    PeerCast.updateYPChannels(function(result) {
      if (result) {
        var new_channels = $.map(result, function(channel_info) {
          return new YPChannelViewModel(channel_info);
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
    return channels.sort(function (x,y) {
      var xplayable = x.isPlayable() ? 1 : 0;
      var yplayable = y.isPlayable() ? 1 : 0;
      var xval = x[column.sortBy]();
      var yval = y[column.sortBy]();
      var cmp = (xplayable<yplayable ? -1 : (xplayable>yplayable ? 1 : 0)) * 10 + (xval<yval ? -1 : (xval>yval ? 1 : 0));
      return column.ascending ? cmp : -cmp;
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

  self.getUserConfig = function() {
    PeerCast.getUserConfig('default', 'ypChannels', function (result) {
      if (result) {
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
};

var channelsViewModel = new YPChannelsViewModel();

