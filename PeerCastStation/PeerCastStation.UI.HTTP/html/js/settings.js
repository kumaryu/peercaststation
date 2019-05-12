
var UserConfig = new function () {
  var self = this;
  var loading = false;
  self.remoteNodeName = ko.observable("sessionId");
  self.defaultPlayProtocol = ko.observable({});

  self.loadConfig = function() {
    PeerCast.getUserConfig('default', 'ui', function (config) {
      if (!config) return;
      loading = true;
      if (config.remoteNodeName) self.remoteNodeName(config.remoteNodeName);
      loading = false;
    });
    PeerCast.getUserConfig('default', 'defaultPlayProtocol', function (value) {
      if (!value) return;
      loading = true;
      self.defaultPlayProtocol(value);
      loading = false;
    });
  };

  self.saveConfig = function() {
    if (loading) return;
    var ui = {
      remoteNodeName: self.remoteNodeName()
    };
    PeerCast.setUserConfig('default', 'ui', ui);
    PeerCast.setUserConfig('default', 'defaultPlayProtocol', self.defaultPlayProtocol());
  };

  $(function () {
    self.loadConfig();
  });
}

var YellowPageEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#yellowPageEditDialog');
    dialog.modal({show: false});
    dialog.on('hide', self.onHide);
    ko.applyBindings(self, dialog.get(0));
    PeerCast.getYellowPageProtocols(function(result) {
      if (result) {
        self.yellowPageProtocols.splice.apply(
          self.yellowPageProtocols,
          [0, self.yellowPageProtocols().length].concat(result));
      }
    });
  });
  self.yellowPageProtocols = ko.observableArray();
  self.name        = ko.observable("");
  self.protocol    = ko.observable("");
  self.announceUri = ko.observable("");
  self.channelsUri = ko.observable("");
  self.onOK        = null;

  self.show = function(ok) {
    self.onOK = ok;
    dialog.modal('show');
  };

  self.onUpdate = function() {
    self.onOK(self);
    dialog.modal('hide');
  };

  self.clear = function() {
    self.name("");
    self.protocol("");
    self.announceUri("");
    self.channelsUri("");
  };
};

var ListenerEditDialog = new function() {
  var self = this;
  var dialog = null;
  $(document).ready(function() {
    dialog = $('#listenerEditDialog');
    dialog.modal({show: false});
    dialog.on('hide', self.onHide);
    ko.applyBindings(self, dialog.get(0));
  });
  self.address            = ko.observable('0.0.0.0');
  self.port               = ko.observable(7144);
  self.localAccepts       = ko.observable(15);
  self.localAuthRequired  = ko.observable(false);
  self.globalAccepts      = ko.observable(PeerCast.OutputStreamType.Relay | PeerCast.OutputStreamType.Metadata);
  self.globalAuthRequired = ko.observable(true);
  self.onOK = null;

  self.lanPlayAccept = ko.computed({
    read: function() { return (self.localAccepts() & PeerCast.OutputStreamType.Play)!=0; },
    write: function(value) {
      if (value) self.localAccepts(self.localAccepts() | PeerCast.OutputStreamType.Play);
      else       self.localAccepts(self.localAccepts() & ~PeerCast.OutputStreamType.Play);
    }
  });

  self.lanRelayAccept = ko.computed({
    read: function() { return (self.localAccepts() & PeerCast.OutputStreamType.Relay)!=0; },
    write: function(value) {
      if (value) self.localAccepts(self.localAccepts() | PeerCast.OutputStreamType.Relay);
      else       self.localAccepts(self.localAccepts() & ~PeerCast.OutputStreamType.Relay);
    }
  });

  self.lanInterfaceAccept = ko.computed({
    read: function() { return (self.localAccepts() & PeerCast.OutputStreamType.Interface)!=0; },
    write: function(value) {
      if (value) self.localAccepts(self.localAccepts() | PeerCast.OutputStreamType.Interface);
      else       self.localAccepts(self.localAccepts() & ~PeerCast.OutputStreamType.Interface);
    }
  });

  self.wanPlayAccept = ko.computed({
    read: function() { return (self.globalAccepts() & PeerCast.OutputStreamType.Play)!=0; },
    write: function(value) {
      if (value) self.globalAccepts(self.globalAccepts() | PeerCast.OutputStreamType.Play);
      else       self.globalAccepts(self.globalAccepts() & ~PeerCast.OutputStreamType.Play);
    }
  });

  self.wanRelayAccept = ko.computed({
    read: function() { return (self.globalAccepts() & PeerCast.OutputStreamType.Relay)!=0; },
    write: function(value) {
      if (value) self.globalAccepts(self.globalAccepts() | PeerCast.OutputStreamType.Relay);
      else       self.globalAccepts(self.globalAccepts() & ~PeerCast.OutputStreamType.Relay);
    }
  });

  self.wanInterfaceAccept = ko.computed({
    read: function() { return (self.globalAccepts() & PeerCast.OutputStreamType.Interface)!=0; },
    write: function(value) {
      if (value) self.globalAccepts(self.globalAccepts() | PeerCast.OutputStreamType.Interface);
      else       self.globalAccepts(self.globalAccepts() & ~PeerCast.OutputStreamType.Interface);
    }
  });

  self.show = function(ok) {
    self.onOK = ok;
    dialog.modal('show');
  };

  self.onUpdate = function() {
    self.onOK(self);
    dialog.modal('hide');
  };
};

var ListenerViewModel = function(value) {
  var self = this;
  self.id                     = ko.observable(value.listenerId);
  self.address                = ko.observable(value.address);
  self.port                   = ko.observable(value.port);
  self.localAccepts           = ko.observable(value.localAccepts);
  self.globalAccepts          = ko.observable(value.globalAccepts);
  self.localAuthRequired      = ko.observable(value.localAuthorizationRequired);
  self.globalAuthRequired     = ko.observable(value.globalAuthorizationRequired);
  self.authenticationId       = ko.observable(value.authenticationId);
  self.authenticationPassword = ko.observable(value.authenticationPassword);
  self.authToken              = ko.observable(value.authToken);
  self.isOpened               = ko.observable(value.isOpened);
  self.authenticationInfoVisibility = ko.observable(false);
  self.authUrl = ko.computed(function () {
    var addr = self.address();
    var ipv6 = !!addr.match(/\:/);
    if (addr=='0.0.0.0' || addr=='0::0' || addr=='::') {
      return "http://" + window.location.hostname + ":" + self.port() + "/?auth=" + self.authToken();
    } else if (ipv6) {
      return "http://[" + addr + "]:" + self.port() + "/?auth=" + self.authToken();
    } else {
      return "http://" + addr + ":" + self.port() + "/?auth=" + self.authToken();
    }
  });
  self.portStatus = ko.computed(function() {
    switch (self.isOpened()) {
    case true:  return "開放";
    case false: return "未開放";
    default:    return "";
    }
  });
  self.checked                = ko.observable(false);
  self.setAccepts = function() {
    PeerCast.setListenerAccepts(self.id(), self.localAccepts(), self.globalAccepts());
  };
  self.setAuthorizationRequired = function() {
    PeerCast.setListenerAuthorizationRequired(self.id(), self.localAuthRequired(), self.globalAuthRequired());
  };
  self.localAuthRequired.subscribe(function (value) {
    self.setAuthorizationRequired();
  });
  self.globalAuthRequired.subscribe(function (value) {
    self.setAuthorizationRequired();
  });

  self.showAuthenticationInfo = function() {
    self.authenticationInfoVisibility(true);
  };

  self.update = function(data) {
    self.id(data.listenerId);
    self.address(data.address);
    self.port(data.port);
    self.isOpened(data.isOpened);
    self.localAccepts(data.localAccepts);
    self.globalAccepts(data.globalAccepts);
    self.localAuthRequired(data.localAuthorizationRequired);
    self.globalAuthRequired(data.globalAuthorizationRequired);
    self.authenticationId(data.authenticationId);
    self.authenticationPassword(data.authenticationPassword);
  };

  self.addressLabel = ko.computed(function() {
    var addr = self.address();
    if (addr==='0.0.0.0')                  return 'IPv4 Any';
    else if (addr==='0::0' || addr==='::') return 'IPv6 Any';
    else                                   return addr;
  });

  self.lanPlayAccept = ko.computed({
    read: function() { return (self.localAccepts() & PeerCast.OutputStreamType.Play)!=0; },
    write: function(value) {
      if (value) self.localAccepts(self.localAccepts() | PeerCast.OutputStreamType.Play);
      else       self.localAccepts(self.localAccepts() & ~PeerCast.OutputStreamType.Play);
      self.setAccepts();
    }
  });

  self.lanRelayAccept = ko.computed({
    read: function() { return (self.localAccepts() & PeerCast.OutputStreamType.Relay)!=0; },
    write: function(value) {
      if (value) self.localAccepts(self.localAccepts() | PeerCast.OutputStreamType.Relay);
      else       self.localAccepts(self.localAccepts() & ~PeerCast.OutputStreamType.Relay);
      self.setAccepts();
    }
  });

  self.lanInterfaceAccept = ko.computed({
    read: function() { return (self.localAccepts() & PeerCast.OutputStreamType.Interface)!=0; },
    write: function(value) {
      if (value) self.localAccepts(self.localAccepts() | PeerCast.OutputStreamType.Interface);
      else       self.localAccepts(self.localAccepts() & ~PeerCast.OutputStreamType.Interface);
      self.setAccepts();
    }
  });

  self.wanPlayAccept = ko.computed({
    read: function() { return (self.globalAccepts() & PeerCast.OutputStreamType.Play)!=0; },
    write: function(value) {
      if (value) self.globalAccepts(self.globalAccepts() | PeerCast.OutputStreamType.Play);
      else       self.globalAccepts(self.globalAccepts() & ~PeerCast.OutputStreamType.Play);
      self.setAccepts();
    }
  });

  self.wanRelayAccept = ko.computed({
    read: function() { return (self.globalAccepts() & PeerCast.OutputStreamType.Relay)!=0; },
    write: function(value) {
      if (value) self.globalAccepts(self.globalAccepts() | PeerCast.OutputStreamType.Relay);
      else       self.globalAccepts(self.globalAccepts() & ~PeerCast.OutputStreamType.Relay);
      self.setAccepts();
    }
  });

  self.wanInterfaceAccept = ko.computed({
    read: function() { return (self.globalAccepts() & PeerCast.OutputStreamType.Interface)!=0; },
    write: function(value) {
      if (value) self.globalAccepts(self.globalAccepts() | PeerCast.OutputStreamType.Interface);
      else       self.globalAccepts(self.globalAccepts() & ~PeerCast.OutputStreamType.Interface);
      self.setAccepts();
    }
  });

  self.resetAuthenticationKey = function() {
    PeerCast.resetListenerAuthenticationKey(self.id(), function (data) {
      self.update(data);
    });
  };
};

var YellowPageViewModel = function(value) {
  var self = this;
  self.id          = ko.observable(value.yellowPageId);
  self.name        = ko.observable(value.name);
  self.announceUri = ko.observable(value.announceUri);
  self.channelsUri = ko.observable(value.channelsUri);
  self.protocol    = ko.observable(value.protocol);
  self.checked     = ko.observable(false);
};

var SettingsViewModel = new function() {
  var self = this;
  var updating = false;

  self.maxRelays                     = ko.observable(null);
  self.maxDirects                    = ko.observable(null);
  self.maxRelaysPerBroadcastChannel  = ko.observable(null);
  self.maxRelaysPerRelayChannel      = ko.observable(null);
  self.maxDirectsPerBroadcastChannel = ko.observable(null);
  self.maxDirectsPerRelayChannel     = ko.observable(null);
  self.maxUpstreamRate           = ko.observable(null);
  self.maxUpstreamRateIPv6       = ko.observable(null);
  self.maxUpstreamRatePerBroadcastChannel = ko.observable(null);
  self.maxUpstreamRatePerRelayChannel     = ko.observable(null);
  self.checkBandwidthStatus      = ko.observable("");
  self.checkPortsStatus          = ko.observable("");
  self.externalIPAddresses       = ko.observable("");
  self.inactiveChannelLimit      = ko.observable(null);
  self.channelCleanupMode        = ko.observable(null);
  self.portMapperEnabled         = ko.observable(null);
  self.listeners                 = ko.observableArray();
  self.yellowPages               = ko.observableArray();
  self.remoteNodeName            = UserConfig.remoteNodeName;
  self.defaultPlayProtocolFLV    = ko.computed({
    read: function() { return UserConfig.defaultPlayProtocol()['FLV'] || 'Unknown'; },
    write: function(value) {
      var obj = UserConfig.defaultPlayProtocol();
      obj['FLV'] = value;
      UserConfig.defaultPlayProtocol(obj);
    }
  });

  $.each([
    self.maxRelays,
    self.maxDirects,
    self.maxRelaysPerBroadcastChannel,
    self.maxRelaysPerRelayChannel,
    self.maxDirectsPerBroadcastChannel,
    self.maxDirectsPerRelayChannel,
    self.maxUpstreamRate,
    self.maxUpstreamRateIPv6,
    self.maxUpstreamRatePerBroadcastChannel,
    self.maxUpstreamRatePerRelayChannel,
    self.inactiveChannelLimit,
    self.channelCleanupMode,
    self.portMapperEnabled
  ], function (i, o) {
    o.subscribe(function (new_value) { if (!updating) self.submit(); });
  });
  self.submit = function() {
    var settings = {
      maxRelays:                          self.maxRelays()!=null                          ? Number(self.maxRelays()) : null,
      maxDirects:                         self.maxDirects()!=null                         ? Number(self.maxDirects()) : null,
      maxRelaysPerBroadcastChannel:       self.maxRelaysPerBroadcastChannel()!=null       ? Number(self.maxRelaysPerBroadcastChannel()) : null,
      maxRelaysPerRelayChannel:           self.maxRelaysPerRelayChannel()!=null           ? Number(self.maxRelaysPerRelayChannel()) : null,
      maxDirectsPerBroadcastChannel:      self.maxDirectsPerBroadcastChannel()!=null      ? Number(self.maxDirectsPerBroadcastChannel()) : null,
      maxDirectsPerRelayChannel:          self.maxDirectsPerRelayChannel()!=null          ? Number(self.maxDirectsPerRelayChannel()) : null,
      maxUpstreamRate:                    self.maxUpstreamRate()!=null                    ? Number(self.maxUpstreamRate()) : null,
      maxUpstreamRateIPv6:                self.maxUpstreamRateIPv6()!=null                ? Number(self.maxUpstreamRateIPv6()) : null,
      maxUpstreamRatePerBroadcastChannel: self.maxUpstreamRatePerBroadcastChannel()!=null ? Number(self.maxUpstreamRatePerBroadcastChannel()) : null,
      maxUpstreamRatePerRelayChannel:     self.maxUpstreamRatePerRelayChannel()!=null     ? Number(self.maxUpstreamRatePerRelayChannel()) : null,
      channelCleaner: {
        inactiveLimit: self.inactiveChannelLimit()!=null ? Number(self.inactiveChannelLimit())*60000 : null,
        mode:          self.channelCleanupMode()!=null   ? Number(self.channelCleanupMode()) : null
      },
      portMapper: {
        enabled:       self.portMapperEnabled()
      }
    };
    PeerCast.setSettings(settings);
  };
  $.each([
    UserConfig.defaultPlayProtocol,
    self.remoteNodeName
  ], function (i, o) {
    o.subscribe(function (new_value) { if (!updating) self.submitUserConfig(); });
  });
  self.submitUserConfig = function() {
    UserConfig.saveConfig();
  };

  self.addYellowPage = function() {
    YellowPageEditDialog.clear();
    YellowPageEditDialog.show(function onOK(yp) {
      var announce_uri = yp.announceUri();
      if (announce_uri==null || announce_uri==="") {
        announce_uri = null;
      }
      else if (!announce_uri.match(/^\w+:\/\//)) {
        announce_uri = yp.protocol() + '://' + yp.announceUri();
      }
      var channels_uri = yp.channelsUri();
      if (channels_uri==null || channels_uri==="") {
        channels_uri = null;
      }
      PeerCast.addYellowPage(yp.protocol(), yp.name(), announce_uri, channels_uri, function(res, err) {
        if (err) {
          alert("YPの追加に失敗しました: " + err.message);
          YellowPageEditDialog.show(onOK);
          return;
        }
        self.update();
      });
    });
  }

  self.removeYellowPages = function() {
    var removed = self.yellowPages.remove(function(yp) { return yp.checked(); });
    $.each(removed, function(i, yp) {
      PeerCast.removeYellowPage(yp.id(), function() { self.update(); });
    });
  }

  self.editYellowPage = function() {
    var checkedItems = self.yellowPages().filter(function (yp) { return yp.checked(); });

    if (checkedItems.length == 0) {
      alert("編集するYPを1つ選択してください。");
      return;
    }
    if (checkedItems.length > 1) {
      alert("選択するYPは1つにしてください。");
      return;
    }

    var target = checkedItems[0];
    YellowPageEditDialog.name(target.name());
    YellowPageEditDialog.announceUri(target.announceUri());
    YellowPageEditDialog.channelsUri(target.channelsUri());
    YellowPageEditDialog.protocol(target.protocol());

    YellowPageEditDialog.show(function onOK(yp) {
      var announce_uri = yp.announceUri();
      if (announce_uri==null || announce_uri==="") {
        announce_uri = null;
      }
      else if (!announce_uri.match(/^\w+:\/\//)) {
        announce_uri = yp.protocol() + '://' + yp.announceUri();
      }
      var channels_uri = yp.channelsUri();
      if (channels_uri==null || channels_uri==="") {
        channels_uri = null;
      }
      PeerCast.addYellowPage(yp.protocol(), yp.name(), announce_uri, channels_uri, function(res, err) {
        if (err) {
          alert("YPの追加に失敗しました: " + err.message);
          YellowPageEditDialog.show(onOK);
          return;
        }
        PeerCast.removeYellowPage(target.id(), function() {
          self.update();
        });
      });
    });
  }

  self.addListener = function() {
    ListenerEditDialog.show(function(listener) {
      PeerCast.addListener(
          listener.address(),
          Number(listener.port()),
          listener.localAccepts(),
          listener.globalAccepts(),
          listener.localAuthRequired(),
          listener.globalAuthRequired(),
          function() {
        self.update();
      });
    });
  };

  self.removeListener = function() {
    var removed = self.listeners.remove(function(listener) { return listener.checked(); });
    $.each(removed, function(i, listener) {
      PeerCast.removeListener(listener.id(), function() { self.update(); });
    });
  };

  self.resetListenerAuthenticationKey = function() {
    $.each(self.listeners(), function(i, listener) {
      if (listener.checked()) {
        listener.resetAuthenticationKey();
      }
    });
  };

  self.checkBandwidth = function() {
    self.checkBandwidthStatus("計測中");
    PeerCast.checkBandwidth('ipv4', function (result) {
      if (result) {
        var rate = Math.floor(result * 0.8 / 100) * 100;
        self.maxUpstreamRate(rate);
        self.checkBandwidthStatus("帯域測定完了: " + result + "kbps, 設定推奨値: " + rate + "kbps");
      }
      else {
        self.checkBandwidthStatus("帯域測定失敗。接続できませんでした");
      }
    });
  };

  self.checkBandwidthIPv6 = function() {
    self.checkBandwidthStatus("計測中");
    PeerCast.checkBandwidth('ipv6', function (result) {
      if (result) {
        var rate = Math.floor(result * 0.8 / 100) * 100;
        self.maxUpstreamRateIPv6(rate);
        self.checkBandwidthStatus("帯域測定完了: " + result + "kbps, 設定推奨値: " + rate + "kbps");
      }
      else {
        self.checkBandwidthStatus("帯域測定失敗。接続できませんでした");
      }
    });
  };

  self.checkPorts = function() {
    self.checkPortsStatus("確認中");
    PeerCast.checkPorts(function (result) {
      if (result) {
        if (result.length>0) {
          var listeners = self.listeners();
          for (var i in listeners) {
            var port = listeners[i].port();
            var opened = false;
            for (var j in result) {
              if (result[j]===port) {
                opened = true;
                break;
              }
            }
            listeners[i].isOpened(opened);
          }
          self.checkPortsStatus("開放されています");
        }
        else {
          self.checkPortsStatus("開放されてません");
        }
      }
      else {
        self.checkPortsStatus("ポート開放確認失敗。接続できませんでした");
      }
    });
  };

  self.update = function() {
    PeerCast.getSettings(function(result) {
      if (result) {
        updating = true;
        self.maxRelays(result.maxRelays);
        self.maxDirects(result.maxDirects);
        self.maxRelaysPerBroadcastChannel(result.maxRelaysPerBroadcastChannel);
        self.maxRelaysPerRelayChannel(result.maxRelaysPerRelayChannel);
        self.maxDirectsPerBroadcastChannel(result.maxDirectsPerBroadcastChannel);
        self.maxDirectsPerRelayChannel(result.maxDirectsPerRelayChannel);
        self.maxUpstreamRate(result.maxUpstreamRate);
        self.maxUpstreamRateIPv6(result.maxUpstreamRateIPv6);
        self.maxUpstreamRatePerBroadcastChannel(result.maxUpstreamRatePerBroadcastChannel);
        self.maxUpstreamRatePerRelayChannel(result.maxUpstreamRatePerRelayChannel);
        if (result.channelCleaner) {
          self.inactiveChannelLimit(result.channelCleaner.inactiveLimit/60000);
          self.channelCleanupMode(result.channelCleaner.mode);
        }
        if (result.portMapper) {
          self.portMapperEnabled(result.portMapper.enabled);
        }
        updating = false;
      }
    });
    PeerCast.getListeners(function(result) {
      if (result) {
        updating = true;
        var new_listeners = $.map(result, function(listener) {
          return new ListenerViewModel(listener);
        });
        self.listeners.splice.apply(
          self.listeners,
          [0, self.listeners().length].concat(new_listeners));
        updating = false;
      }
    });
    PeerCast.getYellowPages(function(result) {
      if (result) {
        updating = true;
        var new_yps = $.map(result, function(yp) {
          return new YellowPageViewModel(yp);
        });
        self.yellowPages.splice.apply(
          self.yellowPages,
          [0, self.yellowPages().length].concat(new_yps));
        updating = false;
      }
    });
    PeerCast.getExternalIPAddresses(function(result) {
      if (result) {
        self.externalIPAddresses(result.join(", "));
      }
    });
  };

  self.bind = function(target) {
    self.update();
    self.checkPorts();
    updating = true;
    ko.applyBindings(self, target);
    updating = false;
  };
};

