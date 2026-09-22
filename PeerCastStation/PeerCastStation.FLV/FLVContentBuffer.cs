using System;
using System.IO;
using PeerCastStation.Core;
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  internal class FLVContentBuffer
    : IRTMPContentSink
  {
    public Channel       TargetChannel { get; private set; }
    public IContentSink  ContentSink   { get; private set; }
    public long          Position      { get { return position; } }
    private long         position        = 0;
    private int          streamIndex     = -1;
    private DateTime     streamOrigin;
    private long         timestampOrigin = 0;
    private DataMessage? metadata        = null;
    private RTMPMessage? audioHeader     = null;
    private RTMPMessage? videoHeader     = null;
    private RTMPMessage? videoMetadata   = null;
    private MemoryStream bodyBuffer      = new MemoryStream();

    public FLVContentBuffer(
      Channel target_channel,
      IContentSink content_sink)
    {
      this.TargetChannel = target_channel;
      this.ContentSink   = new BufferedContentSink(content_sink);
    }

    private void SetDataFrame(DataMessage msg)
    {
      var name = (string?)msg.Arguments[0] ?? "";
      var data_msg = new DataAMF0Message(msg.Timestamp, 0, name, new AMF.AMFValue[] { msg.Arguments[1] });
      OnData(data_msg);
    }

    private void ClearDataFrame(DataMessage msg)
    {
      var name = (string?)msg.Arguments[0];
      switch (name) {
      case "onMetaData":
        metadata = null;
        break;
      }
    }

    private void OnMetaData(DataMessage msg)
    {
      this.metadata = msg;
      var info = new AtomCollection();
      info.SetChanInfoType("FLV");
      info.SetChanInfoStreamType("video/x-flv");
      info.SetChanInfoStreamExt(".flv");
      if (metadata.Arguments[0].Type==AMF.AMFValueType.ECMAArray || metadata.Arguments[0].Type==AMF.AMFValueType.Object){
        var bitrate = 0.0;
        var val = metadata.Arguments[0]["maxBitrate"];
        if (!AMF.AMFValue.IsNull(val)) {
          double maxBitrate;
          string maxBitrateStr = System.Text.RegularExpressions.Regex.Replace((string?)val ?? "", @"([\d]+)k", "$1");
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

    public void OnFLVHeader(FLVFileHeader header)
    {
      var info = new AtomCollection();
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

    private enum VideoMessageType
    {
      Content,
      SequenceStart,
      VideoMetadata,
    }

    public void OnVideo(RTMPMessage msg)
    {
      switch (GetVideoMessageType(msg)) {
      case VideoMessageType.SequenceStart:
        videoHeader = msg;
        OnHeaderChanged(msg);
        break;
      case VideoMessageType.VideoMetadata:
        videoMetadata = msg;
        OnHeaderChanged(msg);
        break;
      }
      OnContentChanged(msg);
    }
    private VideoMessageType GetVideoMessageType(RTMPMessage msg)
    {
      if (msg.MessageType!=RTMPMessageType.Video) {
        throw new ArgumentException("Not a video message", nameof(msg));
      }
      if (msg.Body.Length>3 &&
          (msg.Body[0]==0x17 && msg.Body[1]==0x00 && msg.Body[2]==0x00 && msg.Body[3]==0x00)) { // AVC sequence header
        return VideoMessageType.SequenceStart;
      }
      else if (IsExVideoTagHeader(msg)) {
        var type = GetExVideoTagHeaderPacketType(msg);
        return type switch {
          ExVideoPacketType.SequenceStart or ExVideoPacketType.MPEG2TSSequenceStart => VideoMessageType.SequenceStart,
          ExVideoPacketType.Metadata => VideoMessageType.VideoMetadata,
          _ => VideoMessageType.Content,
        };
      }
      else {
        return VideoMessageType.Content;
      }
    }


    private enum AudioMessageType
    {
      Content,
      SequenceStart,
    }

    public void OnAudio(RTMPMessage msg)
    {
      switch (GetAudioMessageType(msg)) {
      case AudioMessageType.SequenceStart:
        audioHeader = msg;
        OnHeaderChanged(msg);
        break;
      }
      OnContentChanged(msg);
    }

    private AudioMessageType GetAudioMessageType(RTMPMessage msg)
    {
      if (msg.MessageType!=RTMPMessageType.Audio) {
        throw new ArgumentException("Not an audio message", nameof(msg));
      }
      if (msg.Body.Length>1 && (msg.Body[0]==0xAF && msg.Body[1]==0x00)) { // AAC sequence header
        return AudioMessageType.SequenceStart;
      }
      else if (IsExAudioTagHeader(msg)) {
        var type = GetExAudioTagHeaderPacketType(msg);
        return type switch {
          ExAudioPacketType.SequenceStart => AudioMessageType.SequenceStart,
          _ => AudioMessageType.Content,
        };
      }
      else {
        return AudioMessageType.Content;
      }
    }

    private enum ExVideoPacketType {
      SequenceStart = 0,
      CodedFrames = 1,
      SequenceEnd = 2,
      CodedFramesX = 3,
      Metadata = 4,
      MPEG2TSSequenceStart = 5,
      Multitrack = 6,
      ModEx = 7,
    }

    private enum AVMultitrackType {
      OneTrack = 0,
      ManyTracks = 1,
      ManyTracksManyCodecs = 2,
    }

    private bool IsExVideoTagHeader(RTMPMessage msg)
    {
      return
         msg.MessageType==RTMPMessageType.Video &&
         msg.Body.Length>1 &&
        (msg.Body[0]>>4 & 0b1000)!=0x00;
    }

    private ExVideoPacketType GetExVideoTagHeaderPacketType(RTMPMessage msg)
    {
      if (!IsExVideoTagHeader(msg)) {
        throw new ArgumentException("Not an ExVideo tag header", nameof(msg));
      }
      using (var reader = new RTMPBinaryReader(msg.Body)) {
        var type = (ExVideoPacketType)(reader.ReadByte() & 0b1111);
        while (type==ExVideoPacketType.ModEx){
          int modex_datasize = reader.ReadByte() + 1;
          if (modex_datasize==256) {
            modex_datasize = reader.ReadUInt16() + 1;
          }
          // ModExData そのものはここでは使わないので読み飛ばす
          reader.ReadBytes(modex_datasize);
          // 真の ExVidoePacketType を取得する
          // ただしまだ ModExData の可能性もあるのでその時はループする
          type = (ExVideoPacketType)(reader.ReadByte() & 0b1111);
        }

        // Multitrack の時も真の ExVideoPacketType を取得する必要がある
        if (type==ExVideoPacketType.Multitrack) {
          var type_byte = reader.ReadByte();
          var multitrack_type = (AVMultitrackType)((type_byte & 0b11110000) >> 4);
          type = (ExVideoPacketType)(type_byte & 0b1111);
        }
        return type;
      }
    }

    private enum ExAudioPacketType {
      SequenceStart = 0,
      CodedFrames = 1,
      SequenceEnd = 2,
      MultichannelConfig = 4,
      Multitrack = 5,
      ModEx = 7,
    }

    private bool IsExAudioTagHeader(RTMPMessage msg)
    {
      return
         msg.MessageType==RTMPMessageType.Audio &&
         msg.Body.Length>1 &&
        ((msg.Body[0] & 0xF0)>>4)==9;
    }

    private ExAudioPacketType GetExAudioTagHeaderPacketType(RTMPMessage msg)
    {
      if (!IsExAudioTagHeader(msg)) {
        throw new ArgumentException("Not an ExAudio tag header", nameof(msg));
      }
      using (var reader = new RTMPBinaryReader(msg.Body)) {
        var type = (ExAudioPacketType)(reader.ReadByte() & 0b1111);
        while (type==ExAudioPacketType.ModEx){
          int modex_datasize = reader.ReadByte() + 1;
          if (modex_datasize==256) {
            modex_datasize = reader.ReadUInt16() + 1;
          }
          // ModExData そのものはここでは使わないので読み飛ばす
          reader.ReadBytes(modex_datasize);
          // 真の ExAudioPacketType を取得する
          // ただしまだ ModExData の可能性もあるのでその時はループする
          type = (ExAudioPacketType)(reader.ReadByte() & 0b1111);
        }

        // Multitrack の時も真の ExAudioPacketType を取得する必要がある
        if (type==ExAudioPacketType.Multitrack) {
          var type_byte = reader.ReadByte();
          var multitrack_type = (AVMultitrackType)((type_byte & 0b11110000) >> 4);
          type = (ExAudioPacketType)(type_byte & 0b1111);
        }
        return type;
      }
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
        if (metadata!=null)      WriteMessage(s, metadata,    0xFFFFFFFF);
        if (audioHeader!=null)   WriteMessage(s, audioHeader, 0xFFFFFFFF);
        if (videoHeader!=null)   WriteMessage(s, videoHeader, 0xFFFFFFFF);
        if (videoMetadata!=null) WriteMessage(s, videoMetadata, 0xFFFFFFFF);
      }
      streamIndex     = TargetChannel.GenerateStreamID();
      streamOrigin    = DateTime.Now;
      timestampOrigin = msg.Timestamp;
      var bytes = s.ToArray();
      ContentSink.OnContentHeader(new Content(streamIndex, TimeSpan.Zero, position, bytes, PCPChanPacketContinuation.None));
      position += bytes.Length;
    }

    private void OnContentChanged(RTMPMessage content)
    {
      if (streamIndex<0) {
        OnHeaderChanged(content);
        return;
      }
      WriteMessage(bodyBuffer, content, timestampOrigin);
      if (bodyBuffer.Length>0) {
        ContentSink.OnContent(new Content(streamIndex, DateTime.Now-streamOrigin, position, bodyBuffer.ToArray(), PCPChanPacketContinuation.None));
        position += bodyBuffer.Length;
        bodyBuffer.SetLength(0);
      }
    }

    private void OnChannelInfoChanged(AtomCollection info)
    {
      ContentSink.OnChannelInfo(new ChannelInfo(info));
    }

    private void OnChannelTrackChanged(AtomCollection info)
    {
      ContentSink.OnChannelTrack(new ChannelTrack(info));
    }
  }

}
