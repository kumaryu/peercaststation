
var FileStreamViewModel = new function() {
  var self = this;

  self.bind = function(target) {
    ko.applyBindings(self, target);
  };

  self.init = function(channelId) {
    $("#uploadForm").css("display", "none");
    $("#streamURL").css("display", "block");
    if(!channelId){
      return;
    }
    PeerCastStation.getChannelStatus(channelId).then(function(result){
      $("#sURL").val("/stream/"+channelId+".flv");
      if(result.isBroadcasting){
        $("#uploadForm").css("display", "block");
        $("#streamURL").css("display", "none");
      }
    }).catch(function () {});
  };

  self.upload = function() {
    const file = document.querySelector('#uploadImage').files[0];
    const reader = new FileReader();
    const idstr = new Date().getTime();
    const splitSize = 60*1024;
    reader.onload = (event) => {
      var splitDataObjList = [];
      var sequence = 0;
      var base64Text = event.currentTarget.result;
      var total = Math.ceil(base64Text.length/splitSize);
      for (var start = 0; start < base64Text.length; start += splitSize) {
        var end = start + splitSize > base64Text.length ? base64Text.length : start + splitSize;
        var base64Subtxt = base64Text.substring(start, end);
        var obj = {type:"base64image", id:idstr, size:total, index:sequence++, data:base64Subtxt};
        splitDataObjList.push(obj);
      }
      self.bulkUpload(splitDataObjList);
    }
    reader.readAsDataURL(file);
  };

  self.bulkUpload = function(splitDataObjList) {
    if(splitDataObjList.length==0){
      $("#uploadStatus").parent().css("display", "none");
      $("#uploadStatus").css("width", "0%");
      return;
    }else{
      $("#uploadStatus").parent().css("display", "block");
    }
    var dataObj = splitDataObjList.shift();
    var base64Message = JSON.stringify(dataObj);
    PeerCastStation.sendBase64Message(base64Message).then(function() {
      var progress = Math.ceil(parseInt(dataObj.index)/parseInt(dataObj.size)*100);
      $("#uploadStatus").css("width", progress+"%");
      setTimeout(function(){
        self.bulkUpload(splitDataObjList);
      },50);
    });
  }

};





//bilibili flv.js sample

var checkBoxFields = ['isLive', 'withCredentials', 'hasAudio', 'hasVideo'];
var streamURL, mediaSourceURL;
var scriptDataCache = [];

function flv_load() {
    console.log('isSupported: ' + flvjs.isSupported());
    if (mediaSourceURL.className === '') {
        var url = document.getElementById('msURL').value;

        var xhr = new XMLHttpRequest();
        xhr.open('GET', url, true);
        xhr.onload = function (e) {
            var mediaDataSource = JSON.parse(xhr.response);
            flv_load_mds(mediaDataSource);
        }
        xhr.send();
    } else {
        var i;
        var mediaDataSource = {
            type: 'flv'
        };
        for (i = 0; i < checkBoxFields.length; i++) {
            var field = checkBoxFields[i];
            /** @type {HTMLInputElement} */
            var checkbox = document.getElementById(field);
            mediaDataSource[field] = checkbox.checked;
        }
        mediaDataSource['url'] = document.getElementById('sURL').value;
        console.log('MediaDataSource', mediaDataSource);
        flv_load_mds(mediaDataSource);
    }
}

function flv_load_mds(mediaDataSource) {
    var element = document.getElementsByName('videoElement')[0];
    if (typeof player !== "undefined") {
        if (player != null) {
            player.unload();
            player.detachMediaElement();
            player.destroy();
            player = null;
        }
    }
    player = flvjs.createPlayer(mediaDataSource, {
        enableWorker: false,
        lazyLoadMaxDuration: 3 * 60,
        seekType: 'range',
    });
    player.attachMediaElement(element);
    player.load();

    player._mediaElement.addEventListener('loadedmetadata', function(){
      player._transmuxer.on('scriptdata_arrived', flv_script);
      player.play();
    });
}

function flv_script(e) {
  //e.base64image={type:"base64image", id:12345678, size:2, index:0, data:"data:image/png;base64, ..."}
  var obj = JSON.parse(e.base64image);
  if(obj.index==0){
    scriptDataCache = [];
  }
  scriptDataCache.push(obj.data);
  //receive data complete
  if(scriptDataCache.length==obj.size && obj.size-1==obj.index){
    var img = $("<img>");
    img.attr("height", "320");
    img.attr("src", scriptDataCache.join(""));
    $("#imgContainer").prepend(img);
    $("#imgContainer").find("img:gt(9)").remove();
  }
}

function flv_start() {
    player.play();
}

function flv_pause() {
    player.pause();
}

function flv_destroy() {
    player.pause();
    player.unload();
    player.detachMediaElement();
    player.destroy();
    player = null;
}

function flv_seekto() {
    var input = document.getElementsByName('seekpoint')[0];
    player.currentTime = parseFloat(input.value);
}

function switch_url() {
    streamURL.className = '';
    mediaSourceURL.className = 'hidden';
    saveSettings();
}

function switch_mds() {
    streamURL.className = 'hidden';
    mediaSourceURL.className = '';
    saveSettings();
}

function ls_get(key, def) {
    try {
        var ret = localStorage.getItem('flvjs_demo.' + key);
        if (ret === null) {
            ret = def;
        }
        return ret;
    } catch (e) {}
    return def;
}

function ls_set(key, value) {
    try {
        localStorage.setItem('flvjs_demo.' + key, value);
    } catch (e) {}
}

function saveSettings() {
    if (mediaSourceURL.className === '') {
        ls_set('inputMode', 'MediaDataSource');
    } else {
        ls_set('inputMode', 'StreamURL');
    }
    var i;
    for (i = 0; i < checkBoxFields.length; i++) {
        var field = checkBoxFields[i];
        /** @type {HTMLInputElement} */
        var checkbox = document.getElementById(field);
        ls_set(field, checkbox.checked ? '1' : '0');
    }
    var msURL = document.getElementById('msURL');
    var sURL = document.getElementById('sURL');
    ls_set('msURL', msURL.value);
    ls_set('sURL', sURL.value);
    console.log('save');
}

function loadSettings() {
    var i;
    for (i = 0; i < checkBoxFields.length; i++) {
        var field = checkBoxFields[i];
        /** @type {HTMLInputElement} */
        var checkbox = document.getElementById(field);
        var c = ls_get(field, checkbox.checked ? '1' : '0');
        checkbox.checked = c === '1' ? true : false;
    }

    var msURL = document.getElementById('msURL');
    var sURL = document.getElementById('sURL');
    msURL.value = ls_get('msURL', msURL.value);
    sURL.value = ls_get('sURL', sURL.value);
    if (ls_get('inputMode', 'StreamURL') === 'StreamURL') {
        switch_url();
    } else {
        switch_mds();
    }
}

function showVersion() {
    var version = flvjs.version;
    document.title = document.title + " (v" + version + ")";
}

document.addEventListener('DOMContentLoaded', function () {
    var logcatbox = document.getElementsByName('logcatbox')[0];
    flvjs.LoggingControl.addLogListener(function(type, str) {
        logcatbox.value = logcatbox.value + str + '\n';
        logcatbox.scrollTop = logcatbox.scrollHeight;
    });
    streamURL = document.getElementById('streamURL');
    mediaSourceURL = document.getElementById('mediaSourceURL');
    //loadSettings();
    showVersion();
    var iid = setInterval(function(){
      if($("#sURL").val()!=""){
        clearInterval(iid);
        flv_load();
      }
    }, 1000);
});