using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  internal class FLVContentBuffer
  {
    public Channel       TargetChannel { get; private set; }
    public long          Position      { get { return position; } }
    private long         position        = 0;
    private int          streamIndex     = -1;
    private DateTime     streamOrigin;
    private long         timestampOrigin = 0;
    private DataMessage  metadata        = null;
    private RTMPMessage  audioHeader     = null;
    private RTMPMessage  videoHeader     = null;
    private MemoryStream bodyBuffer      = new MemoryStream();
    private System.Diagnostics.Stopwatch flushTimer = new System.Diagnostics.Stopwatch();
    private ParsedContent contents = new ParsedContent();

    public FLVContentBuffer(Channel target_channel)
    {
      this.TargetChannel = target_channel;
      this.flushTimer.Start();
    }

    private void SetDataFrame(DataMessage msg)
    {
      var name = (string)msg.Arguments[0];
      var data_msg = new DataAMF0Message(msg.Timestamp, 0, name, new AMF.AMFValue[] { msg.Arguments[1] });
      OnData(data_msg);
    }

    private void ClearDataFrame(DataMessage msg)
    {
      var name = (string)msg.Arguments[0];
      switch (name) {
      case "onMetaData":
        metadata = null;
        break;
      }
    }

    private void OnMetaData(DataMessage msg)
    {
      this.metadata = msg;
      var info = new AtomCollection(TargetChannel.ChannelInfo.Extra);
      info.SetChanInfoType("FLV");
      info.SetChanInfoStreamType("video/x-flv");
      info.SetChanInfoStreamExt(".flv");
      if (metadata.Arguments[0].Type==AMF.AMFValueType.ECMAArray || metadata.Arguments[0].Type==AMF.AMFValueType.Object){
        var bitrate = 0.0;
        var val = metadata.Arguments[0]["maxBitrate"];
        if (!AMF.AMFValue.IsNull(val)) {
          double maxBitrate;
          string maxBitrateStr = System.Text.RegularExpressions.Regex.Replace((string)val, @"([\d]+)k", "$1");
          if (double.TryParse(maxBitrateStr, out maxBitrate)) {
            bitrate += maxBitrate;
          }
        }
        else if (!AMF.AMFValue.IsNull(val = metadata.Arguments[0]["videodatarate"])) {
          bitrate += (double)val;
        }
        if (!AMF.AMFValue.IsNull(val = metadata.Arguments[0]["audiodatarate"])) {
          bitrate += (double)val;
        }
        info.SetChanInfoBitrate((int)bitrate);
      }
      OnChannelInfoChanged(info);
      OnHeaderChanged(msg);
      OnContentChanged(msg);
    }

    public void OnStart()
    {
      var info = new AtomCollection(TargetChannel.ChannelInfo.Extra);
      info.SetChanInfoType("FLV");
      info.SetChanInfoStreamType("video/x-flv");
      info.SetChanInfoStreamExt(".flv");
      OnChannelInfoChanged(info);
    }

    public void OnData(DataMessage msg)
    {
      switch (msg.PropertyName) {
      case "@setDataFrame":
        SetDataFrame(msg);
        break;

      case "@clearDataFrame":
        ClearDataFrame(msg);
        break;

      case "onMetaData":
        OnMetaData(msg);
        break;

      default:
        OnContentChanged(msg);
        break;
      }
    }

    public void OnVideo(RTMPMessage msg)
    {
      if (IsAVCHeader(msg)) {
        videoHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    public void OnAudio(RTMPMessage msg)
    {
      if (IsAACHeader(msg)) {
        audioHeader = msg;
        OnHeaderChanged(msg);
      }
      OnContentChanged(msg);
    }

    private bool IsAVCHeader(RTMPMessage msg)
    {
      return
         msg.MessageType==RTMPMessageType.Video &&
         msg.Body.Length>3 &&
        (msg.Body[0]==0x17 &&
         msg.Body[1]==0x00 &&
         msg.Body[2]==0x00 &&
         msg.Body[3]==0x00);
    }

    private bool IsAACHeader(RTMPMessage msg)
    {
      return
         msg.MessageType==RTMPMessageType.Audio &&
         msg.Body.Length>1 &&
        (msg.Body[0]==0xAF &&
         msg.Body[1]==0x00);
    }

    private void WriteMessage(Stream stream, RTMPMessage msg, long time_origin)
    {
      var timestamp = Math.Max(0, msg.Timestamp-time_origin);
      using (var writer=new RTMPBinaryWriter(stream, true)) {
        writer.Write((byte)msg.MessageType);
        writer.WriteUInt24(msg.Body.Length);
        writer.WriteUInt24((int)timestamp & 0xFFFFFF);
        writer.Write((byte)((timestamp>>24) & 0xFF));
        writer.WriteUInt24(0);
        writer.Write(msg.Body, 0, msg.Body.Length);
        writer.Write(msg.Body.Length+11);
      }
    }

    private void OnHeaderChanged(RTMPMessage msg)
    {
      FlushContents();
      var s = new MemoryStream();
      using (s) {
        using (var writer=new RTMPBinaryWriter(s, true)) {
          writer.Write((byte)'F');
          writer.Write((byte)'L');
          writer.Write((byte)'V');
          writer.Write((byte)1);
          writer.Write((byte)5);
          writer.WriteUInt32(9);
          writer.WriteUInt32(0);
        }
        if (metadata!=null)    WriteMessage(s, metadata,    0xFFFFFFFF);
        if (audioHeader!=null) WriteMessage(s, audioHeader, 0xFFFFFFFF);
        if (videoHeader!=null) WriteMessage(s, videoHeader, 0xFFFFFFFF);
      }
      streamIndex     = TargetChannel.GenerateStreamID();
      streamOrigin    = DateTime.Now;
      timestampOrigin = msg.Timestamp;
      var bytes = s.ToArray();
      contents.ContentHeader = new Content(streamIndex, TimeSpan.Zero, position, bytes);
      position += bytes.Length;
    }

    private void OnContentChanged(RTMPMessage content)
    {
      if (streamIndex<0) OnHeaderChanged(content);
      WriteMessage(bodyBuffer, content, timestampOrigin);
      if (bodyBuffer.Length>=7500 ||
          flushTimer.ElapsedMilliseconds>=100) {
        FlushContents();
      }
    }

    private void FlushContents()
    {
      if (bodyBuffer.Length>0) {
        if (contents.Contents==null) {
          contents.Contents = new List<Content>();
        }
        contents.Contents.Add(new Content(streamIndex, DateTime.Now-streamOrigin, position, bodyBuffer.ToArray()));
        position += bodyBuffer.Length;
        bodyBuffer.SetLength(0);
      }
      flushTimer.Reset();
      flushTimer.Start();
    }

    private void OnChannelInfoChanged(AtomCollection info)
    {
      contents.ChannelInfo = new ChannelInfo(info);
    }

    private void OnChannelTrackChanged(AtomCollection info)
    {
      contents.ChannelTrack = new ChannelTrack(info);
    }

    public ParsedContent GetContents()
    {
      var res = contents;
      contents = new ParsedContent();
      return res;
    }

  }

}
