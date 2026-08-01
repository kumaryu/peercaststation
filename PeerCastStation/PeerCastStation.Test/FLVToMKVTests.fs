// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
module FLVToMKVTests

open Xunit
open System
open System.IO
open PeerCastStation.Core
open PeerCastStation.FLV
open PeerCastStation.FLV.AMF
open PeerCastStation.FLV.RTMP
open FLVTestHelpers

// ---- helpers ----

let private countOf (haystack:byte[]) (needle:byte[]) =
    if needle.Length=0 then 0
    else
        let mutable count = 0
        for i in 0..(haystack.Length - needle.Length) do
            let mutable j = 0
            while j<needle.Length && haystack.[i+j]=needle.[j] do j <- j+1
            if j=needle.Length then count <- count+1
        count

let private startsWith (haystack:byte[]) (needle:byte[]) =
    haystack.Length>=needle.Length &&
    Array.forall2 (=) (Array.sub haystack 0 needle.Length) needle

// ---- EBMLWriter primitive unit tests ----

[<Fact>]
let ``EncodeVInt は最短長でマーカービットを付与する`` () =
    Assert.Equal<byte[]>([| 0x81uy |], EBMLWriter.EncodeVInt(1UL))
    // 126 は1バイトに収まる(1バイトの全ビット1=0xFFはunknownに予約)
    Assert.Equal<byte[]>([| 0xFEuy |], EBMLWriter.EncodeVInt(126UL))
    // 127 は1バイトのunknownと衝突するため2バイトに繰り上がる
    Assert.Equal<byte[]>([| 0x40uy; 0x7Fuy |], EBMLWriter.EncodeVInt(127UL))
    Assert.Equal<byte[]>([| 0x40uy; 0xC8uy |], EBMLWriter.EncodeVInt(200UL))

[<Fact>]
let ``EncodeUnknownVInt は全データビットが1になる`` () =
    Assert.Equal<byte[]>([| 0xFFuy |], EBMLWriter.EncodeUnknownVInt(1))
    let u8 = EBMLWriter.EncodeUnknownVInt(8)
    Assert.Equal(8, u8.Length)
    Assert.Equal<byte[]>([| 0x01uy;0xFFuy;0xFFuy;0xFFuy;0xFFuy;0xFFuy;0xFFuy;0xFFuy |], u8)

[<Fact>]
let ``EncodeUInt は最小バイト数のビッグエンディアン`` () =
    Assert.Equal<byte[]>([| 0x00uy |], EBMLWriter.EncodeUInt(0UL))
    Assert.Equal<byte[]>([| 0xFFuy |], EBMLWriter.EncodeUInt(255UL))
    Assert.Equal<byte[]>([| 0x01uy; 0x00uy |], EBMLWriter.EncodeUInt(256UL))
    Assert.Equal<byte[]>([| 0x0Fuy; 0x42uy; 0x40uy |], EBMLWriter.EncodeUInt(1000000UL))

[<Fact>]
let ``WriteElement は ID とサイズVINT と payload を連結する`` () =
    use ms = new MemoryStream()
    EBMLWriter.WriteElement(ms, EBMLWriter.CodecID, System.ReadOnlySpan<byte>(ascii "A_AAC"))
    let expected = Array.concat [ [| 0x86uy; 0x85uy |]; ascii "A_AAC" ]
    Assert.Equal<byte[]>(expected, ms.ToArray())

// ---- 構造検証(ラウンドトリップ) ----

let private onMetaDataBodyOf (width:AMFValue) (height:AMFValue) =
    let dict = System.Collections.Generic.Dictionary<string, AMFValue>()
    dict.["width"]  <- width
    dict.["height"] <- height
    let meta = AMFValue(dict)
    (DataAMF0Message(0L, 0L, "onMetaData", [| meta |])).Body

let private onMetaDataBody (width:float) (height:float) =
    onMetaDataBodyOf (AMFValue(width)) (AMFValue(height))

[<Fact>]
let ``FLV(H264+AAC) を MKV に変換し EBML 構造が成立する`` () =
    let capture = CaptureSink()
    let filter = FLVToMKVContentFilter()
    let sink = filter.Activate(capture)

    let avcSeq = Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]
    let aacSeq = [| 0xAFuy;0x00uy;0x12uy;0x10uy |] // ASC: AAC-LC, 44100Hz, 2ch
    let avcKey = Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; [| 0uy;0uy;0uy;2uy;0x65uy;0x88uy |] ]
    let avcInter = Array.concat [ [| 0x27uy;0x01uy;0x00uy;0x00uy;0x00uy |]; [| 0uy;0uy;0uy;2uy;0x41uy;0x9Auy |] ]
    let aacRaw = Array.concat [ [| 0xAFuy;0x01uy |]; [| 0x21uy;0x10uy;0x04uy |] ]

    // ContentHeader: FLVヘッダ + onMetaData + シーケンスヘッダ(メディアフレーム無し)
    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 avcSeq
            makeTag 8 0 aacSeq
        ]
    // ContentBody: メディアフレーム
    let bodyData =
        Array.concat [
            makeTag 9 0 avcKey
            makeTag 8 0 aacRaw
            makeTag 9 33 avcInter
            makeTag 8 23 aacRaw
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir) // OnStop は processorTask.Wait() で完了を待つ

    let hdr = capture.Header
    let body = capture.Content

    // ChannelInfo が MKV に書き換わっている
    Assert.Equal("MKV", capture.ChannelType)

    // ヘッダ: EBML(0x1A45DFA3) で始まり matroska / 両トラックを含む
    Assert.True(startsWith hdr [| 0x1Auy;0x45uy;0xDFuy;0xA3uy |], "EBML header で始まること")
    Assert.True(contains hdr (ascii "matroska"), "DocType=matroska")
    Assert.True(contains hdr [| 0x2Auy;0xD7uy;0xB1uy |], "TimecodeScale 要素")
    Assert.True(contains hdr (ascii "V_MPEG4/ISO/AVC"), "映像 CodecID")
    Assert.True(contains hdr (ascii "A_AAC"), "音声 CodecID")
    Assert.True(contains hdr [| 0x16uy;0x54uy;0xAEuy;0x6Buy |], "Tracks 要素")

    // 本体: Cluster と キーフレーム SimpleBlock(映像track=1, tc=0, keyframeフラグ0x80)
    Assert.True(contains body [| 0x1Fuy;0x43uy;0xB6uy;0x75uy |], "Cluster 要素")
    Assert.True(contains body [| 0xE7uy |], "Cluster Timecode 要素")
    Assert.True(contains body [| 0x81uy;0x00uy;0x00uy;0x80uy |], "映像キーフレーム SimpleBlock")

[<Fact>]
let ``onMetaData が無い場合は映像を除外し音声のみで構成する`` () =
    let capture = CaptureSink()
    let filter = FLVToMKVContentFilter()
    let sink = filter.Activate(capture)

    let avcSeq = Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]
    let aacSeq = [| 0xAFuy;0x00uy;0x12uy;0x10uy |]
    let avcKey = Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; [| 0uy;0uy;0uy;2uy;0x65uy;0x88uy |] ]
    let aacRaw = Array.concat [ [| 0xAFuy;0x01uy |]; [| 0x21uy;0x10uy;0x04uy |] ]

    let headerData =
        Array.concat [
            flvHeader
            makeTag 9 0 avcSeq
            makeTag 8 0 aacSeq
        ]
    let bodyData =
        Array.concat [
            makeTag 9 0 avcKey
            makeTag 8 0 aacRaw
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    // 音声トラックは含むが、解像度が無いため映像トラックは除外される
    Assert.True(contains hdr (ascii "A_AAC"), "音声 CodecID は含む")
    Assert.False(contains hdr (ascii "V_MPEG4/ISO/AVC"), "映像 CodecID は含まない")

// ---- 音声トラックの宣言(AudioSpecificConfig の解釈) ----

/// EBML 要素(ID + サイズVINT + payload)のバイト列を組み立てる。
let private ebmlElement (id:byte[]) (payload:byte[]) =
    Array.concat [ id; EBMLWriter.EncodeVInt(uint64 payload.Length); payload ]

/// 指定した AudioSpecificConfig を持つ音声のみの入力を流し、生成された MKV ヘッダを得る。
let private mkvHeaderForAsc (asc:byte[]) =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)
    let aacSeq = Array.concat [ [| 0xAFuy;0x00uy |]; asc ]
    let aacRaw = Array.concat [ [| 0xAFuy;0x01uy |]; [| 0x21uy;0x10uy;0x04uy |] ]
    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent (Array.concat [ flvHeader; makeTag 8 0 aacSeq ]))
    sink.OnContent(newContent (makeTag 8 0 aacRaw))
    sink.OnStop(StopReason.OffAir)
    capture.Header

[<Fact>]
let ``7_1chのAACをチャンネル数8として宣言する`` () =
    // channelConfiguration はチャンネル数ではなくインデックスで、7 は 8ch(7.1)を指す。
    // インデックスをそのまま Channels に書くと 7ch と宣言され、Audio 要素を信じる
    // プレイヤーのチャンネルマスク/ダウンミックスが狂う。
    let hdr = mkvHeaderForAsc (audioSpecificConfig 2 4 7)
    Assert.True(contains hdr (ascii "A_AAC"), "音声トラックが含まれる")
    Assert.True(contains hdr (ebmlElement EBMLWriter.Channels (EBMLWriter.EncodeUInt(8UL))),
                "Channels は 8 として宣言される")

[<Fact>]
let ``ステレオのAACをチャンネル数2として宣言する`` () =
    // インデックス 1-6 は個数と一致するので、変換を入れても従来の値を保つ。
    let hdr = mkvHeaderForAsc (audioSpecificConfig 2 4 2)
    Assert.True(contains hdr (ebmlElement EBMLWriter.Channels (EBMLWriter.EncodeUInt(2UL))),
                "Channels は 2 として宣言される")

[<Fact>]
let ``チャンネル数が確定しないAACの設定を破棄する`` () =
    // 0 はレイアウトを PCE で運ぶ指定、8-15 は予約値。いずれも個数が確定しないため、
    // 誤ったチャンネル数を宣言するより設定ごと捨てる(サンプリング周波数と同じ扱い)。
    for ch in [ 0; 8; 15 ] do
        let hdr = mkvHeaderForAsc (audioSpecificConfig 2 4 ch)
        Assert.False(contains hdr (ascii "A_AAC"),
                     sprintf "channelConfiguration=%d の音声トラックは作らない" ch)

[<Fact>]
let ``HE-AACの実際の出力サンプリング周波数を宣言する`` () =
    // SBR の明示signaling では SamplingFrequency にコア(出力の半分)のレートを書く決まりで、
    // 実レートは OutputSamplingFrequency で別途宣言する。これが無いと CodecPrivate を
    // 読み直さないプレイヤーがトラックを半分のレートと解釈し、A/V がずれていく。
    // コア 22050Hz(freqIdx=7)/拡張 44100Hz(freqIdx=4)/2ch の HE-AAC v1。
    let hdr = mkvHeaderForAsc (audioSpecificConfigSbr 5 7 2 4 2)
    Assert.True(contains hdr (ebmlElement EBMLWriter.SamplingFrequency (EBMLWriter.EncodeFloat(22050.0))),
                "SamplingFrequency はコアのレート")
    Assert.True(contains hdr (ebmlElement EBMLWriter.OutputSamplingFrequency (EBMLWriter.EncodeFloat(44100.0))),
                "OutputSamplingFrequency は実際の出力レート")

[<Fact>]
let ``SBRでないAACにはOutputSamplingFrequencyを付けない`` () =
    // コアと出力が同じレートなら冗長なので出さない(既定でコアのレートが出力レート)。
    let hdr = mkvHeaderForAsc (audioSpecificConfig 2 4 2)
    Assert.True(contains hdr (ebmlElement EBMLWriter.SamplingFrequency (EBMLWriter.EncodeFloat(44100.0))),
                "SamplingFrequency は 44100Hz")
    Assert.False(contains hdr EBMLWriter.OutputSamplingFrequency,
                 "OutputSamplingFrequency は出力されない")

// ---- E-RTMP(enhanced タグ)経路 ----

// ダミーのコーデック設定(構造検証では中身は問わない。CodecPrivate へ無加工で入ることだけ確認する)
let private av1C = [| 0x81uy;0x0Cuy;0x3Buy;0x00uy;0x0Auy;0x0Buy;0x77uy;0x88uy |]

/// enhanced AV1 CodedFrames(packetType=1)。AV1 は CTS フィールドを持たない(FourCC 直後が即ペイロード)。
let private exAv1CodedFrames (frameType:int) (obu:byte[]) =
    let b0 = 0x80 ||| ((frameType &&& 0x07) <<< 4) ||| 0x01
    Array.concat [ [| byte b0 |]; ascii "av01"; obu ]

/// enhanced 映像 Multitrack(OneTrack)SequenceStart。payloadOffset は無効化される想定。
let private exVideoMultitrackSeq (fourcc:string) (config:byte[]) =
    // byte0=0x96(frameType=1,packetType=6=Multitrack) / 0x00(multitrackType=0,実packetType=0) / FourCC / config
    Array.concat [ [| 0x96uy; 0x00uy |]; ascii fourcc; config ]

/// enhanced 音声 SequenceStart(soundFormat=9, packetType=0)。
let private exAudioSeq (fourcc:string) (asc:byte[]) =
    Array.concat [ [| 0x90uy |]; ascii fourcc; asc ]

/// enhanced 音声 CodedFrames(soundFormat=9, packetType=1)。音声に CTS は無い。
let private exAudioCodedFrames (fourcc:string) (raw:byte[]) =
    Array.concat [ [| 0x91uy |]; ascii fourcc; raw ]

let private legacyAacSeq = [| 0xAFuy;0x00uy;0x12uy;0x10uy |]
let private legacyAacRaw = Array.concat [ [| 0xAFuy;0x01uy |]; [| 0x21uy;0x10uy;0x04uy |] ]

[<Fact>]
let ``E-RTMP(HEVC+AAC) を MKV に変換する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 (exVideoSeq "hvc1" hvcC)
            makeTag 8 0 legacyAacSeq
        ]
    let bodyData =
        Array.concat [
            makeTag 9 0 (exVideoCodedFrames "hvc1" 1 0 [| 0x00uy;0x00uy;0x00uy;0x02uy;0x26uy;0x01uy |])
            makeTag 8 0 legacyAacRaw
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    let body = capture.Content
    Assert.True(contains hdr (ascii "V_MPEGH/ISO/HEVC"), "HEVC CodecID")
    Assert.True(contains hdr (ascii "A_AAC"), "音声 CodecID")
    Assert.True(contains hdr hvcC, "hvcC が CodecPrivate に無加工で入る")
    Assert.True(contains body [| 0x81uy;0x00uy;0x00uy;0x80uy |], "HEVC キーフレーム SimpleBlock(track=1,tc=0,key)")

[<Fact>]
let ``E-RTMP CodedFrames の CTS が SimpleBlock timecode に反映される`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 (exVideoSeq "hvc1" hvcC)
        ]
    let bodyData =
        Array.concat [
            makeTag 9 0  (exVideoCodedFrames "hvc1" 1 0  [| 0x00uy;0x00uy;0x00uy;0x02uy;0x26uy;0x01uy |])
            makeTag 9 33 (exVideoCodedFrames "hvc1" 2 40 [| 0x00uy;0x00uy;0x00uy;0x02uy;0x02uy;0x01uy |])
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let body = capture.Content
    // キーフレームで Cluster が base=0 で開く。inter は dts=33 + cts=40 = 73(0x0049)。
    Assert.True(contains body [| 0x81uy;0x00uy;0x00uy;0x80uy |], "キーフレーム(tc=0)")
    Assert.True(contains body [| 0x81uy;0x00uy;0x49uy;0x00uy |], "inter の timecode に CTS が加算される(33+40=73)")

[<Fact>]
let ``ModEx プレフィックス付き SequenceStart を解釈する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 (exVideoSeqModEx "avc1" avcC)
        ]
    let bodyData =
        makeTag 9 0 (exVideoCodedFrames "avc1" 1 0 [| 0x00uy;0x00uy;0x00uy;0x02uy;0x65uy;0x88uy |])

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    Assert.True(contains hdr (ascii "V_MPEG4/ISO/AVC"), "avc1 CodecID")
    Assert.True(contains hdr avcC, "ModEx を剥がした avcC が CodecPrivate に入る")

[<Fact>]
let ``E-RTMP(AV1) の SequenceStart が MKV 映像トラックになる`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 1280.0 720.0)
            makeTag 9 0 (exVideoSeq "av01" av1C)
        ]
    // AV1 フレームは先頭が即 OBU(CTS 無し)。先頭3バイトが欠落しないことを検証する。
    let av1Obu = [| 0x0Auy;0x0Euy;0x5Auy;0xA5uy;0x3Cuy;0xC3uy |]
    let bodyData =
        makeTag 9 0 (exAv1CodedFrames 1 av1Obu)

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    let body = capture.Content
    Assert.True(contains hdr (ascii "V_AV1"), "AV1 CodecID(Matroska は V_AV1)")
    Assert.True(contains hdr av1C, "av1C が CodecPrivate に無加工で入る")
    // SimpleBlock: track=1, tc=0, key(0x80) の直後に OBU 先頭が無加工で続く(CTS 由来の欠落なし)
    Assert.True(contains body (Array.concat [ [| 0x81uy;0x00uy;0x00uy;0x80uy |]; av1Obu ]),
                "AV1 フレーム先頭の OBU が CTS 誤読で欠落しない")

[<Fact>]
let ``enhanced AAC(mp4a) を A_AAC として扱う`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let asc = [| 0x12uy;0x10uy;0x56uy;0xE5uy;0x00uy |] // 検証用に長めの AudioSpecificConfig
    let headerData =
        Array.concat [
            flvHeader
            makeTag 8 0 (exAudioSeq "mp4a" asc)
        ]
    let bodyData =
        makeTag 8 0 (exAudioCodedFrames "mp4a" [| 0x21uy;0x10uy;0x04uy |])

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    let body = capture.Content
    Assert.True(contains hdr (ascii "A_AAC"), "音声 CodecID")
    Assert.True(contains hdr asc, "AudioSpecificConfig が CodecPrivate に入る")
    Assert.True(contains body [| 0x82uy;0x00uy;0x00uy;0x80uy |], "音声 SimpleBlock(track=2,tc=0)")

[<Fact>]
let ``未対応の enhanced 映像コーデックは破棄し音声のみ構成する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 (exVideoSeq "vp09" [| 0x01uy;0x02uy;0x03uy |])
            makeTag 8 0 legacyAacSeq
        ]
    let bodyData =
        Array.concat [
            makeTag 9 0 (exVideoCodedFrames "vp09" 1 0 [| 0x00uy;0x11uy;0x22uy |])
            makeTag 8 0 legacyAacRaw
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    Assert.True(contains hdr (ascii "A_AAC"), "音声トラックは構成される")
    Assert.False(contains hdr (ascii "V_MPEG"), "未対応映像(vp09)は CodecID を出さない")
    Assert.False(contains hdr (ascii "V_AV1"), "未対応映像(vp09)は CodecID を出さない")

[<Fact>]
let ``enhanced Multitrack 映像は破棄し音声のみ構成する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 (exVideoMultitrackSeq "hvc1" hvcC)
            makeTag 8 0 legacyAacSeq
        ]
    let bodyData =
        makeTag 8 0 legacyAacRaw

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    Assert.True(contains hdr (ascii "A_AAC"), "音声トラックは構成される")
    Assert.False(contains hdr (ascii "V_MPEGH/ISO/HEVC"), "Multitrack 映像は CodecID を出さない")

// ---- 破損入力に対する回帰テスト ----

let private legacyAvcSeq = Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]
let private legacyAvcKey = Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; [| 0uy;0uy;0uy;2uy;0x65uy;0x88uy |] ]

/// 映像キーフレーム SimpleBlock(track=1, timecode=0, keyframe フラグ)。
let private videoKeyBlockAtZero = [| 0x81uy;0x00uy;0x00uy;0x80uy |]

[<Fact>]
let ``切り詰められた AudioSpecificConfig を捨てて出力を継続する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    // ASC が1バイトしかなく、samplingFrequencyIndex 以降のビットが足りない。
    // 例外を投げると FLVFileParser がタグ先頭に巻き戻し「データ待ち」と誤認するため、
    // この毒タグがバッファ先頭に残って以後の全パースが再スローし、出力が恒久停止していた。
    let truncatedAacSeq = [| 0xAFuy;0x00uy;0x12uy |]
    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 legacyAvcSeq
            makeTag 8 0 truncatedAacSeq
        ]
    let bodyData = makeTag 9 0 legacyAvcKey

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    Assert.True(contains hdr (ascii "V_MPEG4/ISO/AVC"), "映像トラックは構成される")
    Assert.False(contains hdr (ascii "A_AAC"), "壊れた ASC の音声トラックは除外される")
    Assert.True(contains capture.Content videoKeyBlockAtZero, "毒タグの後も映像フレームが出力される")

[<Fact>]
let ``切り詰められた enhanced タグを挟んでも出力を継続する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 legacyAvcSeq
        ]
    // body=[0x80] は Ex ヘッダのマーカーだけで FourCC まで届かない切り詰めタグ。
    let bodyData =
        Array.concat [
            makeTag 9 0 [| 0x80uy |]
            makeTag 9 0 legacyAvcKey
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    Assert.True(contains capture.Header (ascii "V_MPEG4/ISO/AVC"), "映像トラックは構成される")
    Assert.True(contains capture.Content videoKeyBlockAtZero, "切り詰めタグの後も映像フレームが出力される")

[<Fact>]
let ``onMetaData の解像度が文字列でも解釈する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    // "1280.0" は Int32.Parse では FormatException になる。以前はこれで処理タスクが
    // フォルトし、出力が一切出なくなっていた。
    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBodyOf (AMFValue("1280.0")) (AMFValue("720.0")))
            makeTag 9 0 legacyAvcSeq
        ]
    let bodyData = makeTag 9 0 legacyAvcKey

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    Assert.True(contains hdr (ascii "V_MPEG4/ISO/AVC"), "映像トラックが構成される")
    // PixelWidth=0xB0 に 1280(0x0500)、PixelHeight=0xBA に 720(0x02D0)
    Assert.True(contains hdr [| 0xB0uy;0x82uy;0x05uy;0x00uy |], "PixelWidth=1280")
    Assert.True(contains hdr [| 0xBAuy;0x82uy;0x02uy;0xD0uy |], "PixelHeight=720")
    Assert.True(contains capture.Content videoKeyBlockAtZero, "映像フレームが出力される")

[<Fact>]
let ``onMetaData の解像度が数値でない型でもフォルトしない`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    // 数値にも文字列にもできない型(Date)。以前は AMFValue の int キャスト演算子が
    // InvalidCastException を投げ、処理タスクごとフォルトしていた。
    let nonNumeric () = AMFValue(DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Local))
    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBodyOf (nonNumeric()) (nonNumeric()))
            makeTag 9 0 legacyAvcSeq
            makeTag 8 0 legacyAacSeq
        ]
    let bodyData =
        Array.concat [
            makeTag 9 0 legacyAvcKey
            makeTag 8 0 legacyAacRaw
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let hdr = capture.Header
    // 解像度が取れないので映像は除外されるが、音声は通常どおり構成される
    Assert.True(contains hdr (ascii "A_AAC"), "音声トラックは構成される")
    Assert.False(contains hdr (ascii "V_MPEG4/ISO/AVC"), "解像度不明の映像は除外される")
    Assert.True(contains capture.Content [| 0x82uy;0x00uy;0x00uy;0x80uy |], "音声フレームが出力される")

[<Fact>]
let ``先頭フレームが負CTSでも Cluster Timecode とブロック相対値が整合する`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 18 0 (onMetaDataBody 640.0 360.0)
            makeTag 9 0 (exVideoSeq "hvc1" hvcC)
        ]
    // pts = dts(0) + cts(-40) = -40。Cluster Timecode は符号なしなので 0 にクランプされる。
    let bodyData =
        makeTag 9 0 (exVideoCodedFrames "hvc1" 1 -40 [| 0x00uy;0x00uy;0x00uy;0x02uy;0x26uy;0x01uy |])

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let body = capture.Content
    Assert.True(contains body [| 0xE7uy;0x81uy;0x00uy |], "Cluster Timecode は 0 にクランプされる")
    // clusterBaseMs もクランプ後の 0 なので rel は -40(0xFFD8)。
    // クランプ前の -40 を基準にすると rel=0 になり、クラスタ全体が 40ms ずれる。
    Assert.True(contains body [| 0x81uy;0xFFuy;0xD8uy;0x80uy |], "ブロック相対値は Timecode=0 基準の -40")

[<Fact>]
let ``音声のみでタイムスタンプが後退したらクラスタを開き直す`` () =
    let capture = CaptureSink()
    let sink = FLVToMKVContentFilter().Activate(capture)

    let headerData =
        Array.concat [
            flvHeader
            makeTag 8 0 legacyAacSeq
        ]
    // 50000ms まで進んだ後に 10000ms へ後退する(ソース再開やタイムスタンプラップ相当)。
    let bodyData =
        Array.concat [
            makeTag 8 0     legacyAacRaw
            makeTag 8 50000 legacyAacRaw
            makeTag 8 10000 legacyAacRaw
        ]

    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent headerData)
    sink.OnContent(newContent bodyData)
    sink.OnStop(StopReason.OffAir)

    let body = capture.Content
    // 負方向の再クラスタ分岐が無いと3つ目は同じクラスタに留まり、
    // rel が signed16 の下限 -32768(0x8000)に張り付いたままになる。
    Assert.Equal(3, countOf body [| 0x1Fuy;0x43uy;0xB6uy;0x75uy |])
    Assert.False(contains body [| 0x82uy;0x80uy;0x00uy;0x80uy |], "rel が -32768 に張り付かない")
