module MatroskaContentBufferTests

open System
open System.IO
open System.Collections.Generic
open PeerCastStation.Core
open PeerCastStation.FLV.AMF
open PeerCastStation.FLV.RTMP
open PeerCastStation.MKV
open TestCommon
open Xunit

// OnContentHeader/OnContent のバイト列を収集するテスト用 Sink。
type private CollectingSink() =
    let headers  = List<byte[]>()
    let contents = List<byte[]>()
    let infos    = List<ChannelInfo>()
    member _.Headers  = headers
    member _.Contents = contents
    member _.Infos    = infos
    interface IContentSink with
        member _.OnChannelInfo(info)     = infos.Add(info)
        member _.OnChannelTrack(_)       = ()
        member _.OnContentHeader(header) = headers.Add(header.Data.ToArray())
        member _.OnContent(content)      = contents.Add(content.Data.ToArray())
        member _.OnStop(_)               = ()

let private makeChannel (peca: PeerCast) =
    DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "MKV" 500, ChannelTrack.empty)

let private videoMsg (timestamp: int64) (bytes: byte list) =
    RTMPMessage(RTMPMessageType.Video, timestamp, 0L, List.toArray bytes)

let private audioMsg (timestamp: int64) (bytes: byte list) =
    RTMPMessage(RTMPMessageType.Audio, timestamp, 0L, List.toArray bytes)

let private fourCc (s: string) =
    System.Text.Encoding.ASCII.GetBytes(s) |> Array.toList

// onMetaData(width/height/samplerate/ch)を運ぶ DataMessage を作る。
let private metaMsg () =
    let meta = AMFObject()
    meta.Add("width", 1920.0)
    meta.Add("height", 1080.0)
    meta.Add("audiosamplerate", 48000.0)
    meta.Add("audiochannels", 2.0)
    DataAMF0Message(0L, 0L, "onMetaData", [| AMFValue(meta) |])

// 映像 av01 SequenceStart(config)。0x90 = ExHeader|FrameType1|PacketType0。
let private videoConfig = videoMsg 0L ([0x90uy] @ fourCc "av01" @ [0x81uy; 0x0Cuy])
// 音声 AAC config(ASC)。0xAF = SoundFormat10|..., AACPacketType 0。
let private audioConfig = audioMsg 0L [0xAFuy; 0x00uy; 0x12uy; 0x10uy]
// 映像 av01 キーフレーム。0x93 = ExHeader|FrameType1|PacketType3(CodedFramesX, CTS無し)。
let private videoKey ts = videoMsg ts ([0x93uy] @ fourCc "av01" @ [0xDEuy; 0xADuy])
// 映像 av01 非キーフレーム。0xA3 = ExHeader|FrameType2|PacketType3。
let private videoInter ts = videoMsg ts ([0xA3uy] @ fourCc "av01" @ [0xBEuy; 0xEFuy])
// 音声 AAC raw。0xAF, AACPacketType 1。
let private audioRaw ts = audioMsg ts [0xAFuy; 0x01uy; 0xCAuy; 0xFEuy]

let private sendVideo (buf: MatroskaContentBuffer) msg = (buf :> IRTMPContentSink).OnVideo(msg)
let private sendAudio (buf: MatroskaContentBuffer) msg = (buf :> IRTMPContentSink).OnAudio(msg)
let private sendData  (buf: MatroskaContentBuffer) msg = (buf :> IRTMPContentSink).OnData(msg)

// config(映像+音声)と onMetaData が揃うと init(EBML→Tracks)を OnContentHeader する。
[<Fact>]
let ``config と metadata が揃うと init を OnContentHeader する`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendData buf (metaMsg())
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    Assert.Equal(1, sink.Headers.Count)
    let init = sink.Headers.[0]
    // 先頭は EBML ID(0x1A 0x45 0xDF 0xA3)
    Assert.Equal<byte[]>([| 0x1Auy; 0x45uy; 0xDFuy; 0xA3uy |], init.[0..3])
    // Tracks(0x16 0x54 0xAE 0x6B)を含む
    use ms = new MemoryStream(init)
    let mutable foundTracks = false
    while not foundTracks && ms.Position < ms.Length do
        let mutable h = ElementHeader.Read(ms)
        if h.ID.BinaryEquals(Elements.Tracks) then foundTracks <- true
        elif h.Size.IsUnknown then () // Segment は中身を続けて走査
        else Element.ReadBody(&h, ms) |> ignore
    Assert.True(foundTracks)
    // ChannelInfo の ContentType は MKV
    Assert.Contains(sink.Infos, fun i -> i.ContentType = "MKV")

// metadata が来ないと config が揃っても init を出さない。
[<Fact>]
let ``metadata が無いと init を出さない`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    Assert.Equal(0, sink.Headers.Count)

// init 後、最初の映像キーフレーム到達までフレームを破棄する。
[<Fact>]
let ``最初のキーフレーム前のフレームは破棄される`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendData buf (metaMsg())
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    sendVideo buf (videoInter 10L) // キーフレーム前の非キー → 破棄
    sendAudio buf (audioRaw 10L)   // 同上 → 破棄
    Assert.Equal(0, sink.Contents.Count)

// 映像キーフレームで Cluster(+Timecode)を開き、後続フレームを SimpleBlock にする。
[<Fact>]
let ``キーフレームで Cluster を開き SimpleBlock を出力する`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendData buf (metaMsg())
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    sendVideo buf (videoKey 100L) // 最初のキーフレーム → Cluster 開始(原点=100)
    sendAudio buf (audioRaw 120L) // 同 Cluster 内 → 相対 timecode=20
    Assert.Equal(2, sink.Contents.Count)

    // 1 つ目: Cluster header(unknown-size) → Timecode(=0) → SimpleBlock(track1, tc=0, key)
    use ms0 = new MemoryStream(sink.Contents.[0])
    let hCluster = ElementHeader.Read(ms0)
    Assert.True(hCluster.ID.BinaryEquals(Elements.Cluster))
    Assert.True(hCluster.Size.IsUnknown)
    let mutable hTc = ElementHeader.Read(ms0)
    Assert.True(hTc.ID.BinaryEquals(Elements.Timecode))
    let tc = Element.ReadBody(&hTc, ms0)
    Assert.Equal(0L, Element.ReadUInt(new MemoryStream(tc.Data), tc.Data.LongLength)) // 原点 rebase で 0
    let mutable hBlk = ElementHeader.Read(ms0)
    Assert.True(hBlk.ID.BinaryEquals(Elements.SimpleBlock))
    let blk = Element.ReadBody(&hBlk, ms0)
    // track1 VInt=0x81, tc=0(0x0000), keyframe=0x80, payload
    Assert.Equal<byte[]>([| 0x81uy; 0x00uy; 0x00uy; 0x80uy; 0xDEuy; 0xADuy |], blk.Data)

    // 2 つ目: 音声 SimpleBlock 単体(track2, 相対 tc=20=0x0014, 音声は常に key)
    use ms1 = new MemoryStream(sink.Contents.[1])
    let mutable hBlk2 = ElementHeader.Read(ms1)
    Assert.True(hBlk2.ID.BinaryEquals(Elements.SimpleBlock))
    let blk2 = Element.ReadBody(&hBlk2, ms1)
    Assert.Equal<byte[]>([| 0x82uy; 0x00uy; 0x14uy; 0x80uy; 0xCAuy; 0xFEuy |], blk2.Data)
