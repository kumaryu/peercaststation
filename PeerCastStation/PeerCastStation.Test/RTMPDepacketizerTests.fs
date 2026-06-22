module RTMPDepacketizerTests

open System
open PeerCastStation.FLV.RTMP
open Xunit

// バイト列から映像 RTMPMessage を作る。
let private videoMsg (timestamp: int64) (bytes: byte list) =
    RTMPMessage(RTMPMessageType.Video, timestamp, 0L, List.toArray bytes)

// バイト列から音声 RTMPMessage を作る。
let private audioMsg (timestamp: int64) (bytes: byte list) =
    RTMPMessage(RTMPMessageType.Audio, timestamp, 0L, List.toArray bytes)

// FourCC 文字列をバイト列にする。
let private fourCc (s: string) =
    System.Text.Encoding.ASCII.GetBytes(s) |> Array.toList

// Payload を byte[] として取り出す。
let private payloadBytes (frame: DepacketizedFrame) =
    frame.Payload.ToArray()

// --- 旧 FLV 映像(AVC) ---

[<Fact>]
let ``旧FLV AVC config を SequenceHeader/key として正規化する`` () =
    // 0x17 = (FrameType 1 keyframe)<<4 | (CodecID 7 AVC), AVCPacketType 0, CTS 0, 本体
    let msg = videoMsg 0L [0x17uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy; 0x64uy]
    let ok, frame = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.True(ok)
    Assert.Equal(DepacketizedTrackType.Video, frame.TrackType)
    Assert.Equal("avc1", frame.FourCc)
    Assert.Equal(DepacketizedFrameKind.SequenceHeader, frame.Kind)
    Assert.True(frame.IsKeyFrame)
    Assert.Equal<byte[]>([| 0x01uy; 0x64uy |], payloadBytes frame)

[<Fact>]
let ``旧FLV AVC NALU の CTS 正値を復元しCodedFrame にする`` () =
    // 0x27 = (FrameType 2 interframe)<<4 | 7, AVCPacketType 1, CTS = 0x000102 = 258
    let msg = videoMsg 1000L [0x27uy; 0x01uy; 0x00uy; 0x01uy; 0x02uy; 0xAAuy; 0xBBuy]
    let ok, frame = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.True(ok)
    Assert.Equal(DepacketizedFrameKind.CodedFrame, frame.Kind)
    Assert.False(frame.IsKeyFrame)
    Assert.Equal(258, frame.CompositionTimeOffset)
    Assert.Equal(1000L, frame.Timestamp)
    Assert.Equal<byte[]>([| 0xAAuy; 0xBBuy |], payloadBytes frame)

[<Fact>]
let ``旧FLV AVC NALU の CTS 負値を符号復元する`` () =
    // CTS = 0xFFFFFE = -2
    let msg = videoMsg 0L [0x27uy; 0x01uy; 0xFFuy; 0xFFuy; 0xFEuy; 0xAAuy]
    let ok, frame = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.True(ok)
    Assert.Equal(-2, frame.CompositionTimeOffset)

// --- 旧 FLV 音声(AAC) ---

[<Fact>]
let ``旧FLV AAC config を mp4a/SequenceHeader にする`` () =
    // 0xAF = (SoundFormat 10 AAC)<<4 | ..., AACPacketType 0 (ASC)
    let msg = audioMsg 0L [0xAFuy; 0x00uy; 0x12uy; 0x10uy]
    let ok, frame = ERTMPDepacketizer.TryParseAudio(msg)
    Assert.True(ok)
    Assert.Equal(DepacketizedTrackType.Audio, frame.TrackType)
    Assert.Equal("mp4a", frame.FourCc)
    Assert.Equal(DepacketizedFrameKind.SequenceHeader, frame.Kind)
    Assert.Equal<byte[]>([| 0x12uy; 0x10uy |], payloadBytes frame)

[<Fact>]
let ``旧FLV AAC raw を CodedFrame にする`` () =
    let msg = audioMsg 0L [0xAFuy; 0x01uy; 0xDEuy; 0xADuy]
    let ok, frame = ERTMPDepacketizer.TryParseAudio(msg)
    Assert.True(ok)
    Assert.Equal(DepacketizedFrameKind.CodedFrame, frame.Kind)
    Assert.Equal<byte[]>([| 0xDEuy; 0xADuy |], payloadBytes frame)

// --- Enhanced RTMP 映像 ---

[<Fact>]
let ``Enhanced SequenceStart(av01) を FourCC/SequenceHeader/key にする`` () =
    // 0x80 (ExHeader) | (FrameType 1)<<4 | (PacketType 0 SequenceStart) = 0x90
    let msg = videoMsg 0L ([0x90uy] @ fourCc "av01" @ [0x81uy; 0x0Cuy])
    let ok, frame = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.True(ok)
    Assert.Equal("av01", frame.FourCc)
    Assert.Equal(DepacketizedFrameKind.SequenceHeader, frame.Kind)
    Assert.True(frame.IsKeyFrame)
    Assert.Equal<byte[]>([| 0x81uy; 0x0Cuy |], payloadBytes frame)

[<Fact>]
let ``Enhanced CodedFrames(hvc1) は3byteCTSを読みPayloadは8byte目から`` () =
    // 0x80 | (FrameType 2)<<4 | (PacketType 1 CodedFrames) = 0xA1, CTS = 0x000005 = 5
    let msg = videoMsg 500L ([0xA1uy] @ fourCc "hvc1" @ [0x00uy; 0x00uy; 0x05uy; 0x11uy; 0x22uy])
    let ok, frame = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.True(ok)
    Assert.Equal("hvc1", frame.FourCc)
    Assert.Equal(DepacketizedFrameKind.CodedFrame, frame.Kind)
    Assert.False(frame.IsKeyFrame)
    Assert.Equal(5, frame.CompositionTimeOffset)
    Assert.Equal<byte[]>([| 0x11uy; 0x22uy |], payloadBytes frame)

[<Fact>]
let ``Enhanced CodedFramesX は CTS=0 でPayloadは5byte目から`` () =
    // 0x80 | (FrameType 1)<<4 | (PacketType 3 CodedFramesX) = 0x93
    let msg = videoMsg 0L ([0x93uy] @ fourCc "av01" @ [0x33uy; 0x44uy; 0x55uy])
    let ok, frame = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.True(ok)
    Assert.Equal(DepacketizedFrameKind.CodedFrame, frame.Kind)
    Assert.True(frame.IsKeyFrame)
    Assert.Equal(0, frame.CompositionTimeOffset)
    Assert.Equal<byte[]>([| 0x33uy; 0x44uy; 0x55uy |], payloadBytes frame)

// --- 異常系 ---

[<Fact>]
let ``空Bodyの映像はfalseを返す`` () =
    let msg = videoMsg 0L []
    let ok, _ = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.False(ok)

[<Fact>]
let ``Enhancedで FourCC に満たない短いBodyはfalseを返す`` () =
    // ExHeader だが byte1-4 の FourCC が揃わない。
    let msg = videoMsg 0L [0x90uy; 0x61uy; 0x76uy]
    let ok, _ = ERTMPDepacketizer.TryParseVideo(msg)
    Assert.False(ok)

[<Fact>]
let ``音声でないメッセージを TryParseAudio はfalseにする`` () =
    let msg = videoMsg 0L [0xAFuy; 0x00uy]
    let ok, _ = ERTMPDepacketizer.TryParseAudio(msg)
    Assert.False(ok)
