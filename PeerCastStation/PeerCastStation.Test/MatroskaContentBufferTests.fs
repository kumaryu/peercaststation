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
    let contFlags = List<PCPChanPacketContinuation>()
    let infos    = List<ChannelInfo>()
    member _.Headers  = headers
    member _.Contents = contents
    member _.ContFlags = contFlags
    member _.Infos    = infos
    interface IContentSink with
        member _.OnChannelInfo(info)     = infos.Add(info)
        member _.OnChannelTrack(_)       = ()
        member _.OnContentHeader(header) = headers.Add(header.Data.ToArray())
        member _.OnContent(content)      = contents.Add(content.Data.ToArray()); contFlags.Add(content.ContFlag)
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

// 符号付き 24bit big-endian の CompositionTime(CTS)を 3 バイトにする。
let private cts24 (v: int) = [ byte ((v >>> 16) &&& 0xFF); byte ((v >>> 8) &&& 0xFF); byte (v &&& 0xFF) ]

// Enhanced RTMP の avc1。avc1/hvc1 だけが CTS をワイヤに乗せる。
// 映像 avc1 SequenceStart(config)。0x90 = ExHeader|FrameType1|PacketType0。
let private videoConfigAvc = videoMsg 0L ([0x90uy] @ fourCc "avc1" @ [0x01uy; 0x42uy])
// avc1 キーフレーム CodedFrames(PacketType1)。0x91 = ExHeader|FrameType1|PacketType1。CTS あり。
let private videoKeyAvc ts cts = videoMsg ts ([0x91uy] @ fourCc "avc1" @ cts24 cts @ [0xDEuy; 0xADuy])
// avc1 非キーフレーム CodedFrames。0xA1 = ExHeader|FrameType2|PacketType1。CTS あり。
let private videoInterAvc ts cts = videoMsg ts ([0xA1uy] @ fourCc "avc1" @ cts24 cts @ [0xBEuy; 0xEFuy])

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

// §A: avc1/hvc1 は CTS が乗る。SimpleBlock の timecode は DTS ではなく
//     PTS(=DTS+CTS)を原点 rebase した値になる。
[<Fact>]
let ``avc1 では CTS を加えた PTS で SimpleBlock の timecode を決める`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendData buf (metaMsg())
    sendVideo buf videoConfigAvc
    sendAudio buf audioConfig
    // キーフレーム DTS=100, CTS=10 → PTS=110。これが出力原点。Cluster timecode=0。
    sendVideo buf (videoKeyAvc 100L 10)
    // 非キー DTS=120, CTS=30 → PTS=150 → rebased=40=0x0028。
    // CTS を無視して DTS だけだと 120-100=20=0x14 になる(=回帰検出)。
    sendVideo buf (videoInterAvc 120L 30)
    Assert.Equal(2, sink.Contents.Count)

    // 1 つ目: Cluster(unknown-size) → Timecode(=0, 原点 rebase) → キーフレーム SimpleBlock(相対 0)
    use ms0 = new MemoryStream(sink.Contents.[0])
    let hCluster = ElementHeader.Read(ms0)
    Assert.True(hCluster.ID.BinaryEquals(Elements.Cluster))
    let mutable hTc = ElementHeader.Read(ms0)
    Assert.True(hTc.ID.BinaryEquals(Elements.Timecode))
    let tc = Element.ReadBody(&hTc, ms0)
    Assert.Equal(0L, Element.ReadUInt(new MemoryStream(tc.Data), tc.Data.LongLength))

    // 2 つ目: 非キー SimpleBlock(track1, 相対 tc=40=0x0028, 非キーなので flag=0x00)
    use ms1 = new MemoryStream(sink.Contents.[1])
    let mutable hBlk = ElementHeader.Read(ms1)
    Assert.True(hBlk.ID.BinaryEquals(Elements.SimpleBlock))
    let blk = Element.ReadBody(&hBlk, ms1)
    Assert.Equal<byte[]>([| 0x81uy; 0x00uy; 0x28uy; 0x00uy; 0xBEuy; 0xEFuy |], blk.Data)

// §B-1: onMetaData が来なくても、config が揃って一定数フレームが流れたら
//       CodecPrivate だけで init をフォールバック送出する。
[<Fact>]
let ``metadata が来なくても一定フレーム後に config だけで init を出す`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    // metadata は送らない。config だけ揃える。
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    // しきい値(MetadataWaitFrames=60)未満では init は出ない。
    for i in 1..59 do
        sendVideo buf (videoKey (int64 i))
    Assert.Equal(0, sink.Headers.Count)
    // 60 フレーム目でフォールバック init が出る。
    sendVideo buf (videoKey 60L)
    Assert.Equal(1, sink.Headers.Count)

// 後発参加者のリシンク: Cluster を開く映像キーフレームのパケットだけ ContFlag=None、
// 中間フレームは InterFrame、音声は AudioFrame。これにより Core の GetFirstContent が
// 後発参加者を必ず Cluster 境界(キーフレーム)から開始させ、孤児 SimpleBlock を防ぐ。
[<Fact>]
let ``ContFlag はキーフレームのみ None、中間は InterFrame、音声は AudioFrame`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendData buf (metaMsg())
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    sendVideo buf (videoKey 0L)     // キーフレーム → Cluster 開始 → None
    sendAudio buf (audioRaw 10L)    // 音声 → AudioFrame
    sendVideo buf (videoInter 20L)  // 中間フレーム → InterFrame
    sendVideo buf (videoKey 100L)   // 次のキーフレーム → 新 Cluster → None
    Assert.Equal<PCPChanPacketContinuation[]>(
        [| PCPChanPacketContinuation.None
           PCPChanPacketContinuation.AudioFrame
           PCPChanPacketContinuation.InterFrame
           PCPChanPacketContinuation.None |],
        sink.ContFlags.ToArray())

// §E: 相対 timecode が int16(±約32.7秒)を超えたら飽和クランプする(GOP 過大時の安全網)。
[<Fact>]
let ``int16 を超える相対 timecode は飽和クランプされる`` () =
    use peca = new PeerCast()
    let sink = CollectingSink()
    let buf = MatroskaContentBuffer(makeChannel peca, sink)
    sendData buf (metaMsg())
    sendVideo buf videoConfig
    sendAudio buf audioConfig
    sendVideo buf (videoKey 0L)      // Cluster 開始、原点=0
    sendAudio buf (audioRaw 40000L)  // 相対 40000ms > 32767 → 0x7FFF にクランプ
    Assert.Equal(2, sink.Contents.Count)
    use ms1 = new MemoryStream(sink.Contents.[1])
    let mutable hBlk = ElementHeader.Read(ms1)
    Assert.True(hBlk.ID.BinaryEquals(Elements.SimpleBlock))
    let blk = Element.ReadBody(&hBlk, ms1)
    Assert.Equal<byte[]>([| 0x82uy; 0x7Fuy; 0xFFuy; 0x80uy; 0xCAuy; 0xFEuy |], blk.Data)
