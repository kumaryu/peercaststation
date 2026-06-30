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

// ---- helpers ----

let private indexOf (haystack:byte[]) (needle:byte[]) =
    if needle.Length=0 || haystack.Length<needle.Length then -1
    else
        let last = haystack.Length - needle.Length
        let rec search i =
            if i>last then -1
            else
                let mutable j = 0
                while j<needle.Length && haystack.[i+j]=needle.[j] do j <- j+1
                if j=needle.Length then i else search (i+1)
        search 0

let private contains (haystack:byte[]) (needle:byte[]) =
    indexOf haystack needle >= 0

let private startsWith (haystack:byte[]) (needle:byte[]) =
    haystack.Length>=needle.Length &&
    Array.forall2 (=) (Array.sub haystack 0 needle.Length) needle

let private ascii (s:string) = System.Text.Encoding.ASCII.GetBytes(s)

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

/// FLVタグ(11バイトヘッダ + body + 4バイトPreviousTagSize)を組み立てる。
let private makeTag (typ:int) (timestamp:int) (body:byte[]) =
    let ds = body.Length
    let header =
        [| byte typ
           byte (ds>>>16); byte (ds>>>8); byte ds
           byte (timestamp>>>16); byte (timestamp>>>8); byte timestamp; byte (timestamp>>>24)
           0uy; 0uy; 0uy |]
    let tagsize = ds + 11
    let footer = [| byte (tagsize>>>24); byte (tagsize>>>16); byte (tagsize>>>8); byte tagsize |]
    Array.concat [ header; body; footer ]

let private flvHeader =
    // "FLV" v1 flags(audio+video) DataOffset=9 PreviousTagSize0=0
    [| 0x46uy;0x4Cuy;0x56uy; 1uy; 0x05uy; 0uy;0uy;0uy;9uy; 0uy;0uy;0uy;0uy |]

let private onMetaDataBody (width:float) (height:float) =
    let dict = System.Collections.Generic.Dictionary<string, AMFValue>()
    dict.["width"]  <- AMFValue(width)
    dict.["height"] <- AMFValue(height)
    let meta = AMFValue(dict)
    (DataAMF0Message(0L, 0L, "onMetaData", [| meta |])).Body

// 最小の avcC(中身は検証では問わない。先頭5バイトを除いた部分が CodecPrivate になる)
let private avcC =
    [| 1uy;0x42uy;0x00uy;0x1Fuy;0xFFuy;0xE1uy;0x00uy;0x04uy;0x67uy;0x42uy;0x00uy;0x1Fuy;0x01uy;0x00uy;0x04uy;0x68uy;0xCEuy;0x3Cuy;0x80uy |]

type private CaptureSink() =
    let header = System.Collections.Generic.List<byte>()
    let content = System.Collections.Generic.List<byte>()
    member val ChannelType : string = null with get, set
    member _.Header = header.ToArray()
    member _.Content = content.ToArray()
    interface IContentSink with
        member this.OnChannelInfo(ci) = this.ChannelType <- ci.ContentType
        member _.OnChannelTrack(_) = ()
        member _.OnContentHeader(c) = header.AddRange(c.Data.ToArray())
        member _.OnContent(c) = content.AddRange(c.Data.ToArray())
        member _.OnStop(_) = ()

let private newContent (data:byte[]) =
    Content(0, TimeSpan.Zero, 0L, data, 0, data.Length, PCPChanPacketContinuation.None)

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
