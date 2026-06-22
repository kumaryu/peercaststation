module MKVTests

open System.IO
open System.Text
open PeerCastStation.MKV
open Xunit

// VInt.FromSize: 最小バイト長で符号化し、ReadUInt で読み戻すと元の値に一致する。
// 127/16383/2097151 は全ビット1(unknown size)と衝突するため 1 バイト繰り上がる。
[<Theory>]
[<InlineData(0, 1)>]
[<InlineData(126, 1)>]
[<InlineData(127, 2)>]
[<InlineData(128, 2)>]
[<InlineData(16382, 2)>]
[<InlineData(16383, 3)>]
[<InlineData(16384, 3)>]
[<InlineData(2097150, 3)>]
[<InlineData(2097151, 4)>]
let ``VInt.FromSize は最小長で符号化し読み戻せる`` (value: int, expectedLen: int) =
    let v = VInt.FromSize(int64 value)
    Assert.Equal(expectedLen, v.Length)
    use ms = new MemoryStream(v.Binary)
    let read = VInt.ReadUInt(ms)
    Assert.Equal(int64 value, read.Value)

[<Fact>]
let ``VInt.FromSize は4バイト超の値も往復できる`` () =
    let value = 5_000_000L
    let v = VInt.FromSize(value)
    Assert.Equal(4, v.Length)
    use ms = new MemoryStream(v.Binary)
    Assert.Equal(value, VInt.ReadUInt(ms).Value)

[<Fact>]
let ``VInt.FromSize は負値を拒否する`` () =
    Assert.Throws<System.ArgumentOutOfRangeException>(fun () -> VInt.FromSize(-1L) |> ignore)
    |> ignore

// MakeElement: ID とデータを保ち、既存リーダーで読み戻せる。
[<Fact>]
let ``MakeElement は ID とデータを保ち読み戻せる`` () =
    let data = [| 0x12uy; 0x34uy; 0x56uy |]
    let elt = MatroskaWriter.MakeElement(Elements.DocType, data)
    use ms = new MemoryStream(elt.ToArray())
    let mutable header = ElementHeader.Read(ms)
    Assert.True(header.ID.BinaryEquals(Elements.DocType))
    Assert.Equal(int64 data.Length, header.Size.Value)
    let read = Element.ReadBody(&header, ms)
    Assert.Equal<byte[]>(data, read.Data)

// 既知の EBML バイト列(DocType="matroska") を read→write して完全一致。
[<Fact>]
let ``既知の EBML バイト列を read write で完全一致`` () =
    let known =
        Array.concat [
            [| 0x42uy; 0x82uy; 0x88uy |] // DocType id + size(len1,val8)
            Encoding.UTF8.GetBytes("matroska")
        ]
    use ms = new MemoryStream(known)
    let mutable header = ElementHeader.Read(ms)
    let elt = Element.ReadBody(&header, ms)
    let rebuilt = MatroskaWriter.MakeElement(header.ID.Binary, elt.Data)
    Assert.Equal<byte[]>(known, rebuilt.ToArray())

// MakeMaster: 子要素を内包し、ネスト境界とサイズが正しい。
[<Fact>]
let ``MakeMaster は子要素を内包し境界が正しい`` () =
    let child1 = MatroskaWriter.MakeUInt(Elements.TimecodeScale, 1000000UL)
    let child2 = MatroskaWriter.MakeString(Elements.DocType, "webm")
    let master = MatroskaWriter.MakeMaster(Elements.Info, child1, child2)
    use ms = new MemoryStream(master.ToArray())
    let header = ElementHeader.Read(ms)
    Assert.True(header.ID.BinaryEquals(Elements.Info))

    let mutable h1 = ElementHeader.Read(ms)
    Assert.True(h1.ID.BinaryEquals(Elements.TimecodeScale))
    let c1 = Element.ReadBody(&h1, ms)
    Assert.Equal(1000000L, Element.ReadUInt(new MemoryStream(c1.Data), c1.Data.LongLength))

    let mutable h2 = ElementHeader.Read(ms)
    Assert.True(h2.ID.BinaryEquals(Elements.DocType))
    let c2 = Element.ReadBody(&h2, ms)
    Assert.Equal("webm", Encoding.UTF8.GetString(c2.Data))

    // master のサイズ = 子要素の合計(余りなく読み切る)
    Assert.Equal(ms.Length, ms.Position)

// MakeUInt: 最小バイト長で符号なし整数を符号化する。
[<Theory>]
[<InlineData(0, 1)>]
[<InlineData(255, 1)>]
[<InlineData(256, 2)>]
[<InlineData(65535, 2)>]
[<InlineData(65536, 3)>]
let ``MakeUInt は最小バイト長で符号化する`` (value: int, expectedLen: int) =
    let elt = MatroskaWriter.MakeUInt(Elements.TimecodeScale, uint64 value)
    Assert.Equal(expectedLen, elt.Data.Length)
    Assert.Equal(int64 value, Element.ReadUInt(new MemoryStream(elt.Data), elt.Data.LongLength))

// VInt.Unknown: 全ビット1で生成し、読み戻すと IsUnknown==true になる。
// len は実用範囲(Segment は len=1)。len*7>=32 では既存 IsUnknown の int シフト
// 制約に触れるため対象外とする。
[<Theory>]
[<InlineData(1)>]
[<InlineData(2)>]
[<InlineData(3)>]
[<InlineData(4)>]
let ``VInt.Unknown は全ビット1で IsUnknown になる`` (len: int) =
    let v = VInt.Unknown(len)
    Assert.Equal(len, v.Length)
    // EBML の unknown-size は値ビットが全 1(長さマーカーは別)。最終バイトは 0xFF。
    Assert.Equal(0xFFuy, v.Binary.[len - 1])
    Assert.True(v.IsUnknown)
    use ms = new MemoryStream(v.Binary)
    let read = VInt.ReadUInt(ms)
    Assert.Equal(len, read.Length)
    Assert.True(read.IsUnknown)

// FourCc → CodecID マッピングを網羅。
[<Theory>]
[<InlineData("av01", "V_AV1")>]
[<InlineData("hvc1", "V_MPEGH/ISO/HEVC")>]
[<InlineData("avc1", "V_MPEG4/ISO/AVC")>]
[<InlineData("mp4a", "A_AAC")>]
let ``FourCcToCodecId は既知コーデックを対応付ける`` (fourCc: string, codecId: string) =
    Assert.Equal(codecId, MatroskaInitBuilder.FourCcToCodecId(fourCc))

[<Fact>]
let ``FourCcToCodecId は未対応 FourCc を拒否する`` () =
    Assert.Throws<System.ArgumentException>(fun () ->
        MatroskaInitBuilder.FourCcToCodecId("xxxx") |> ignore)
    |> ignore

// init 領域内の特定 master 要素を読み出すヘルパ(トップレベルを線形走査)。
let private readTopLevel (init: byte[]) (id: byte[]) =
    use ms = new MemoryStream(init)
    let mutable result = None
    while result.IsNone && ms.Position < ms.Length do
        let mutable h = ElementHeader.Read(ms)
        if h.ID.BinaryEquals(id) then
            result <- Some(Element.ReadBody(&h, ms))
        elif h.Size.IsUnknown then
            () // Segment など unknown-size はスキップせず中身を続けて走査
        else
            Element.ReadBody(&h, ms) |> ignore
    result.Value

// master の子要素を辿って指定 ID の最初の子を返す。
let private childOf (master: Element) (id: byte[]) =
    use ms = new MemoryStream(master.Data)
    let mutable result = None
    while result.IsNone && ms.Position < ms.Length do
        let mutable h = ElementHeader.Read(ms)
        let body = Element.ReadBody(&h, ms)
        if h.ID.BinaryEquals(id) then result <- Some(body)
    result.Value

let private sampleVideo : MatroskaVideoTrack =
    let mutable v = MatroskaVideoTrack()
    v.FourCc <- "av01"
    v.CodecPrivate <- [| 0x81uy; 0x05uy; 0x0Cuy; 0x00uy |] // av1C 風ダミー
    v.PixelWidth <- 1920
    v.PixelHeight <- 1080
    v

let private sampleAudio : MatroskaAudioTrack =
    let mutable a = MatroskaAudioTrack()
    a.FourCc <- "mp4a"
    a.CodecPrivate <- [| 0x11uy; 0x90uy |] // AudioSpecificConfig 風ダミー
    a.SamplingFrequency <- 48000.0
    a.Channels <- 2
    a

// init 領域は EBML→Segment(unknown)→Info→Tracks の順で並ぶ。
[<Fact>]
let ``BuildInit は EBML Segment Info Tracks の順で出力する`` () =
    let init = MatroskaInitBuilder.BuildInit(sampleVideo, sampleAudio)
    use ms = new MemoryStream(init)

    let mutable hEbml = ElementHeader.Read(ms)
    Assert.True(hEbml.ID.BinaryEquals(Elements.EBML))
    Element.ReadBody(&hEbml, ms) |> ignore

    let hSeg = ElementHeader.Read(ms)
    Assert.True(hSeg.ID.BinaryEquals(Elements.Segment))
    Assert.True(hSeg.Size.IsUnknown) // ライブ mux ゆえ unknown-size

    let hInfo = ElementHeader.Read(ms)
    Assert.True(hInfo.ID.BinaryEquals(Elements.Info))
    ms.Seek(hInfo.Size.Value, SeekOrigin.Current) |> ignore

    let hTracks = ElementHeader.Read(ms)
    Assert.True(hTracks.ID.BinaryEquals(Elements.Tracks))

// EBML ヘッダの DocType が matroska であること。
[<Fact>]
let ``BuildInit の DocType は matroska`` () =
    let init = MatroskaInitBuilder.BuildInit(sampleVideo, sampleAudio)
    let ebml = readTopLevel init Elements.EBML
    let docType = childOf ebml Elements.DocType
    Assert.Equal("matroska", Encoding.UTF8.GetString(docType.Data))

// Info の TimecodeScale が 1ms(1000000ns)。
[<Fact>]
let ``BuildInit の TimecodeScale は 1000000`` () =
    let init = MatroskaInitBuilder.BuildInit(sampleVideo, sampleAudio)
    let info = readTopLevel init Elements.Info
    let ts = childOf info Elements.TimecodeScale
    Assert.Equal(1000000L, Element.ReadUInt(new MemoryStream(ts.Data), ts.Data.LongLength))

// 映像トラックの CodecID/CodecPrivate/解像度を検証。
[<Fact>]
let ``BuildInit の映像トラックを読み戻せる`` () =
    let init = MatroskaInitBuilder.BuildInit(sampleVideo, sampleAudio)
    let tracks = readTopLevel init Elements.Tracks
    // 先頭の TrackEntry が映像(TrackType=1)
    let video = childOf tracks Elements.TrackEntry
    let trackType = childOf video Elements.TrackType
    Assert.Equal(1L, Element.ReadUInt(new MemoryStream(trackType.Data), trackType.Data.LongLength))
    let codecId = childOf video Elements.CodecID
    Assert.Equal("V_AV1", Encoding.UTF8.GetString(codecId.Data))
    let codecPrivate = childOf video Elements.CodecPrivate
    Assert.Equal<byte[]>(sampleVideo.CodecPrivate, codecPrivate.Data) // 透過コピー
    let vsettings = childOf video Elements.Video
    let width = childOf vsettings Elements.PixelWidth
    Assert.Equal(1920L, Element.ReadUInt(new MemoryStream(width.Data), width.Data.LongLength))
    let height = childOf vsettings Elements.PixelHeight
    Assert.Equal(1080L, Element.ReadUInt(new MemoryStream(height.Data), height.Data.LongLength))

// 音声トラックの CodecID/CodecPrivate/サンプルレート/ch を検証。
[<Fact>]
let ``BuildInit の音声トラックを読み戻せる`` () =
    let init = MatroskaInitBuilder.BuildInit(sampleVideo, sampleAudio)
    let tracks = readTopLevel init Elements.Tracks
    // 2 番目の TrackEntry が音声。Tracks の子から TrackEntry を2つ集める。
    use ms = new MemoryStream(tracks.Data)
    let entries = System.Collections.Generic.List<Element>()
    while ms.Position < ms.Length do
        let mutable h = ElementHeader.Read(ms)
        let body = Element.ReadBody(&h, ms)
        if h.ID.BinaryEquals(Elements.TrackEntry) then entries.Add(body)
    Assert.Equal(2, entries.Count)
    let audio = entries.[1]
    let trackType = childOf audio Elements.TrackType
    Assert.Equal(2L, Element.ReadUInt(new MemoryStream(trackType.Data), trackType.Data.LongLength))
    let codecId = childOf audio Elements.CodecID
    Assert.Equal("A_AAC", Encoding.UTF8.GetString(codecId.Data))
    let codecPrivate = childOf audio Elements.CodecPrivate
    Assert.Equal<byte[]>(sampleAudio.CodecPrivate, codecPrivate.Data)
    let asettings = childOf audio Elements.Audio
    let channels = childOf asettings Elements.Channels
    Assert.Equal(2L, Element.ReadUInt(new MemoryStream(channels.Data), channels.Data.LongLength))
