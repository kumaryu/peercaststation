
var tagsEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#tagsEditDialog');
    dialog.modal({show: false});
    ko.applyBindings(self, dialog.get(0));
  });

  self.name     = ko.observable(null);
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
    self.name(fav.name);
    self.pattern(fav.pattern);
    self.tags(fav.tags);
    self.color(fav.color);
    self.onUpdate = on_update;
    dialog.modal('show');
  };
  self.onOK = function() {
    if (self.onUpdate) {
      self.onUpdate({
        type: 'favorite',
        name: self.name(),
        pattern: self.pattern(),
        tags: self.tags(),
        color: self.color()
      });
    }
    dialog.modal('hide');
  };
};

var filtersEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#filtersEditDialog');
    dialog.modal({show: false});
    ko.applyBindings(self, dialog.get(0));
  });

  var FilterViewModel = function (filter) {
    var self = this;
    self.type    = ko.observable(filter.type);
    self.name    = ko.observable(filter.name);
    self.pattern = ko.observable(filter.pattern);
    self.tags    = ko.observable(filter.tags);
    self.color   = ko.observable(filter.color);
    self.model = function () {
      return {
        type:    self.type(),
        name:    self.name(),
        pattern: self.pattern(),
        tags:    self.tags(),
        color:   self.color()
      };
    }
  };

  self.types = [
    { name:'フィルタ', value:'filter' },
    { name:'お気に入り', value:'favorite' },
  ];

  self.filters = ko.observableArray();
  self.selectedFilter = ko.observable(null);
  self.type = ko.computed({
    read: function () {
      var filter = self.selectedFilter();
      if (!filter) return null;
      return filter.type();
    },
    write: function (value) {
      var filter = self.selectedFilter();
      if (!filter) return;
      filter.type(value);
    }
  });
  self.name = ko.computed({
    read: function () {
      var filter = self.selectedFilter();
      if (!filter) return null;
      return filter.name();
    },
    write: function (value) {
      var filter = self.selectedFilter();
      if (!filter) return;
      filter.name(value);
    }
  });
  self.pattern = ko.computed({
    read: function () {
      var filter = self.selectedFilter();
      if (!filter) return null;
      return filter.pattern();
    },
    write: function (value) {
      var filter = self.selectedFilter();
      if (!filter) return;
      filter.pattern(value);
    }
  });
  self.tags = ko.computed({
    read: function () {
      var filter = self.selectedFilter();
      if (!filter) return null;
      return filter.tags();
    },
    write: function (value) {
      var filter = self.selectedFilter();
      if (!filter) return;
      filter.tags(value);
    }
  });
  self.color = ko.computed({
    read: function () {
      var filter = self.selectedFilter();
      if (!filter) return null;
      return filter.color();
    },
    write: function (value) {
      var filter = self.selectedFilter();
      if (!filter) return;
      filter.color(value);
    }
  });
  self.onUpdate = null;

  self.add = function () {
    self.filters.push(new FilterViewModel({
      type: 'filter',
      name: '新しいフィルタ',
      pattern: '',
      tags: '',
      color: 'default'
    }));
  };

  self.remove = function () {
    self.filters.remove(self.selectedFilter());
    self.selectedFilter(null);
  };

  self.setColor = function(name) {
    return function() {
      self.color(name);
    };
  };
  self.show = function(filters, on_update) {
    self.filters($.map(filters, function (filter) {
      return new FilterViewModel({
        type:    filter.type,
        name:    filter.name,
        pattern: filter.pattern,
        tags:    filter.tags,
        color:   filter.color
      });
    }));
    self.onUpdate = on_update;
    dialog.modal('show');
  };
  self.onOK = function() {
    if (self.onUpdate) {
      self.onUpdate($.map(self.filters(), function (filter) { return filter.model(); }));
    }
    dialog.modal('hide');
  };
};

var YPChannelViewModel = function(owner, initial_value, new_channel) {
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
  self.isNewChannel    = ko.observable(new_channel);
  self.isFavorite      = ko.observable(false);
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
  self.channelIcon = ko.computed(function () {
    if (self.isFavorite())    return 'icon-heart';
    if (self.isPlaying())     return 'icon-bullhorn';
    if (self.isInfoChannel()) return 'icon-info-sign';
    if (!self.isPlayable())   return 'icon-ban-circle';
    if (self.isNewChannel())  return 'icon-flag';
    return 'icon-white';
  });
  self.attrRel = ko.computed(function() {
    return (self.tags()!=null && self.tags()!=='') ? 'tooltip' : '';
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

var AllFilter = function(owner) {
  var self = this;
  self.name = ko.observable('すべて');
  self.test = function (channel) {
    return true;
  };
  self.isSelected = ko.computed(function () {
    return owner.selectedFilter()==self;
  });
  self.select = function () {
    owner.selectedFilter(self);
  };
  self.macthedCount = ko.computed(function () {
    return owner.channels().length;
  })
};

var FavoriteFilter = function(owner) {
  var self = this;
  self.name = ko.observable('お気に入り');
  self.test = function (channel) {
    return owner.getMatchedFavorite(channel)!=null;
  };
  self.isSelected = ko.computed(function () {
    return owner.selectedFilter()==self;
  });
  self.select = function () {
    owner.selectedFilter(self);
  };
  self.macthedCount = ko.computed(function () {
    return $.grep(owner.channels(), function (channel) { return self.test(channel); }).length;
  })
};

var CustomFilterViewModel = function(owner, filter) {
  var self = this;
  self.type     = ko.observable(filter.type);
  self.name     = ko.observable(filter.name);
  var regexp = RegExp(filter.pattern, 'i');
  self.pattern  = ko.observable(filter.pattern);
  self.pattern.subscribe(function (new_value) {
    regexp = RegExp(new_value, 'i');
  });
  var tags = ko.observable(filter.tags);
  self.tags     = ko.computed({
    read: function () {
      if (self.type()==='favorite') return tags();
      else return null;
    },
    write: function (value) {
      tags(value);
    }
  });
  self.color    = ko.observable(filter.color);
  self.model    = ko.computed(function () {
    return {
      type: self.type(),
      name: self.name(),
      pattern: self.pattern(),
      tags: tags(),
      color: self.color()
    };
  });

  self.test = function (channel) {
    switch (self.type()) {
    case 'favorite':
      return regexp.test(channel.infoName());
    case 'filter':
      return regexp.test(channel.toString());
    default:
      return false;
    }
  };
  self.isSelected = ko.computed(function () {
    return owner.selectedFilter()==self;
  });
  self.macthedCount = ko.computed(function () {
    return $.grep(owner.channels(), function (channel) { return self.test(channel); }).length;
  })
  self.select = function () {
    owner.selectedFilter(self);
  };
}

var YPChannelsViewModel = function() {
  var self = this;
  self.channels = ko.observableArray();
  self.sortColumn = ko.observable({ sortBy: 'uptime', ascending: true });
  self.sortColumn.subscribe(function () {
    self.saveLocalConfig();
  });
  self.isLoading = ko.observable(false);
  self.searchText = ko.observable("");
  self.selectedChannel  = ko.observable(null);
  self.selectedFilter   = ko.observable(null);
  self.defaultFilters = [new AllFilter(self), new FavoriteFilter(self)];

  var custom_filters = ko.observable([]);
  self.customFilters = ko.computed({
    read: function () { return custom_filters(); },
    write: function (value) {
      custom_filters(value.sort(function (x,y) {
        var xname = x.name();
        var yname = y.name();
        var xtype = x.type();
        var ytype = y.type();
        return xtype===ytype ? (xname===yname ? 0 : xname<yname ? -1 : 1) : xtype<ytype ? -1 : 1;
      }));
    }
  });
  self.selectedFilter(self.defaultFilters[0]);
  self.filters = ko.computed(function () {
    return self.defaultFilters.concat(
      $.grep(self.customFilters(), function (filter) { return filter.type()==='filter'; })
    );
  });
  self.favoriteChannels = ko.computed(function () {
    return $.grep(self.customFilters(), function (filter) { return filter.type()==='favorite'; });
  });
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
      if (fav.test(channel)) {
        return fav;
      }
    }
    return null;
  };
  self.favChannel = function() {
    var channel = self.selectedChannel();
    var fav = self.getMatchedFavorite(channel);
    if (fav) {
      tagsEditDialog.show(fav.model(), function (favorite) {
        fav.name(favorite.name);
        fav.pattern(favorite.pattern);
        fav.tags(favorite.tags);
        fav.color(favorite.color);
        self.saveConfig();
      });
    }
    else {
      fav = {
        type:    'favorite',
        name:    channel.infoName(),
        pattern: channel.infoName(),
        tags:    '',
        color:   'default'
      };
      tagsEditDialog.show(fav, function (favorite) {
        var filters = self.customFilters();
        filters.push(new CustomFilterViewModel(self, favorite));
        self.customFilters(filters);
        self.saveConfig();
      });
    }
  };

  self.editFilters = function() {
    filtersEditDialog.show($.map(self.customFilters(), function (filter) { return filter.model(); }), function (filters) {
      self.customFilters($.map(filters, function (filter) {
        return new CustomFilterViewModel(self, filter);
      }));
      self.saveConfig();
    });
  };

  var lastChannels = null;
  var getLastChannels = function () {
    if (lastChannels!=null) return lastChannels;
    var doc = window.localStorage['lastChannels'];
    if (!doc) return null;
    lastChannels = JSON.parse(doc);
    return lastChannels;
  };

  var setLastChannels = function (value) {
    window.localStorage['lastChannels'] = JSON.stringify(value);
    lastChannels = value;
  };

  self.update = function() {
    self.isLoading(true);
    PeerCast.updateYPChannels(function(result) {
      if (result) {
        var old_channels = getLastChannels();
        var is_new_channel = function (channel_info) {
          if (old_channels==null) return false;
          for (var i in old_channels) {
            if (old_channels[i]===channel_info.name) {
              return false;
            }
          }
          return true;
        }
        var new_channels = $.map(result, function(channel_info) {
          return new YPChannelViewModel(self, channel_info, is_new_channel(channel_info));
        });
        setLastChannels($.map(result, function(channel_info) { return channel_info.name; }));
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
    var filter = self.selectedFilter();
    if (search!=null && search!=="") {
      var pattern = RegExp(search, "i");
      channels = $.grep(channels, function (x) {
        return pattern.test(x.toString());
      });
    }
    channels = $.grep(channels, function (x) {
      return filter.test(x);
    });
    var favs = self.customFilters().sort(function (x,y) {
      var xval = x.type();
      var yval = y.type();
      return xval===yval ? 0 : xval<yval ? -1 : 1;
    });
    return $.map(channels.sort(function (x,y) {
      var xplayable = x.isPlayable() ? 1 : 0;
      var yplayable = y.isPlayable() ? 1 : 0;
      var xval = x[column.sortBy]();
      var yval = y[column.sortBy]();
      var cmp = (xplayable<yplayable ? -1 : (xplayable>yplayable ? 1 : 0)) * 10 + (xval<yval ? -1 : (xval>yval ? 1 : 0));
      return column.ascending ? cmp : -cmp;
    }), function(channel) {
      var favorite = false;
      for (var i in favs) {
        var fav = favs[i];
        if (fav.test(channel)) {
          favorite = fav.type()==='favorite';
          channel.color(fav.color());
          channel.tags(fav.tags());
          break;
        }
      }
      channel.isFavorite(favorite);
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

  self.columnVisibilities = {
    name:      ko.observable(true),
    genre:     ko.observable(true),
    desc:      ko.observable(true),
    bitrate:   ko.observable(true),
    uptime:    ko.observable(true),
    listeners: ko.observable(true),
    type:      ko.observable(true),
    yp:        ko.observable(true)
  };
  for (var i in self.columnVisibilities) {
    self.columnVisibilities[i].subscribe(function () {
      self.saveLocalConfig();
    });
  }
  self.toggleColumnVisibility = function(name) {
    return function (data, e) {
      self.columnVisibilities[name](!self.columnVisibilities[name]());
      e.stopPropagation();
    };
  }

  self.saveConfig = function() {
    var yp_channels = {
      filters: $.map(self.customFilters(), function (filter) { return filter.model(); })
    };
    PeerCast.setUserConfig('default', 'ypChannels', yp_channels);
  };

  self.loadConfig = function() {
    PeerCast.getUserConfig('default', 'ypChannels', function (config) {
      if (config) {
        self.customFilters($.map(config.filters, function (filter) {
          return new CustomFilterViewModel(self, filter);
        }));
      }
    });
  };

  self.saveLocalConfig = function () {
    window.localStorage['ypChannels'] = JSON.stringify({
      'default': {
        sortColumn: self.sortColumn(),
        columnVisibilities: {
          name:      self.columnVisibilities.name(),
          genre:     self.columnVisibilities.genre(),
          desc:      self.columnVisibilities.desc(),
          bitrate:   self.columnVisibilities.bitrate(),
          uptime:    self.columnVisibilities.uptime(),
          listeners: self.columnVisibilities.listeners(),
          type:      self.columnVisibilities.type(),
          yp:        self.columnVisibilities.yp()
        }
      }
    });
  };

  self.loadLocalConfig = function () {
    var config = window.localStorage['ypChannels'];
    if (!config) return;
    var user = JSON.parse(config);
    if (!user) return;
    var doc = user['default'];
    if (!doc) return;
    if (doc.sortColumn) {
      self.sortColumn(doc.sortColumn);
    }
    if (doc.columnVisibilities) {
      var set_visibility = function (dst, value) {
        if (value===true || value===false) dst(value);
      };
      set_visibility(self.columnVisibilities.name,      doc.columnVisibilities.name);
      set_visibility(self.columnVisibilities.genre,     doc.columnVisibilities.genre);
      set_visibility(self.columnVisibilities.desc,      doc.columnVisibilities.desc);
      set_visibility(self.columnVisibilities.bitrate,   doc.columnVisibilities.bitrate);
      set_visibility(self.columnVisibilities.uptime,    doc.columnVisibilities.uptime);
      set_visibility(self.columnVisibilities.listeners, doc.columnVisibilities.listeners);
      set_visibility(self.columnVisibilities.type,      doc.columnVisibilities.type);
      set_visibility(self.columnVisibilities.yp,        doc.columnVisibilities.yp);
    }
  };

  self.isSorted = function(name) {
    return self.sortColumn().sortBy===name;
  };

  self.bind = function(target) {
    ko.applyBindings(self, target);
    self.update();
  };

  $(function () {
    self.loadLocalConfig();
    self.loadConfig();
  });
};

var channelsViewModel = new YPChannelsViewModel();

