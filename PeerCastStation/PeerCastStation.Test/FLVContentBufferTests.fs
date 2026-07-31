// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
module FLVContentBufferTests

open Xunit
open System
open System.IO
open System.Threading
open PeerCastStation.Core
open PeerCastStation.FLV
open TestCommon

// ---- helpers ----

let private ascii (s:string) = System.Text.Encoding.ASCII.GetBytes(s)

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

let private contains (haystack:byte[]) (needle:byte[]) = indexOf haystack needle >= 0

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
    [| 0x46uy;0x4Cuy;0x56uy; 1uy; 0x05uy; 0uy;0uy;0uy;9uy; 0uy;0uy;0uy;0uy |]

let private avcC =
    [| 1uy;0x42uy;0x00uy;0x1Fuy;0xFFuy;0xE1uy;0x00uy;0x04uy;0x67uy;0x42uy;0x00uy;0x1Fuy;0x01uy;0x00uy;0x04uy;0x68uy;0xCEuy;0x3Cuy;0x80uy |]

/// enhanced 映像 SequenceStart(frameType=1, packetType=0)。avcC 等のコーデック設定を運ぶ。
let private exVideoSeq (fourcc:string) (config:byte[]) =
    Array.concat [ [| 0x90uy |]; ascii fourcc; config ]

/// enhanced 映像 MPEG2TSSequenceStart(frameType=1, packetType=5)。
/// 中身はコーデック設定ではなく MPEG-2 TS のブートストラップ生バイト列。
let private exVideoMpeg2TsSeq (fourcc:string) (payload:byte[]) =
    Array.concat [ [| 0x95uy |]; ascii fourcc; payload ]

/// enhanced 映像 CodedFrames(packetType=1)。AVC は FourCC 直後に符号付き24bit CTS を持つ。
let private exVideoCodedFrames (fourcc:string) (frameType:int) (payload:byte[]) =
    let b0 = 0x80 ||| ((frameType &&& 0x07) <<< 4) ||| 0x01
    Array.concat [ [| byte b0 |]; ascii fourcc; [| 0uy;0uy;0uy |]; payload ]

let private avcNalus = [| 0uy;0uy;0uy;2uy;0x65uy;0x88uy |]

type private CaptureSink() =
    let headers = System.Collections.Generic.List<byte[]>()
    let content = System.Collections.Generic.List<byte>()
    member _.Headers = headers.ToArray()
    member _.HeaderCount = headers.Count
    member _.Content = content.ToArray()
    interface IContentSink with
        member _.OnChannelInfo(_) = ()
        member _.OnChannelTrack(_) = ()
        member _.OnContentHeader(c) = headers.Add(c.Data.ToArray())
        member _.OnContent(c) = content.AddRange(c.Data.ToArray())
        member _.OnStop(_) = ()

/// FLVContentReader 経由で FLVContentBuffer にタグ列を流す。
let private run (data:byte[]) =
    use peca = new PeerCast()
    let channel =
        DummyBroadcastChannel(
            peca, NetworkType.IPv4, Guid.NewGuid(),
            createChannelInfoBitrate "flvcontentbuffer" "FLV" 500, ChannelTrack.empty)
    let capture = CaptureSink()
    let reader = FLVContentReader(channel)
    use ms = new MemoryStream(data)
    reader.ReadAsync(capture, ms, CancellationToken.None).Wait()
    capture

// ---- tests ----

[<Fact>]
let ``映像シーケンスヘッダはチャンネルヘッダに昇格する`` () =
    let capture =
        run (Array.concat [
                flvHeader
                makeTag 9 0  (exVideoSeq "avc1" avcC)
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 avcNalus)
             ])
    Assert.True(capture.HeaderCount>0, "チャンネルヘッダが生成される")
    Assert.True(capture.Headers |> Array.exists (fun h -> contains h avcC),
                "avcC がチャンネルヘッダに埋め込まれる")

[<Fact>]
let ``MPEG2TSSequenceStart をチャンネルヘッダに昇格させない`` () =
    // MPEG2TSSequenceStart はコーデック設定ではなく TS ブートストラップの生バイト列なので、
    // チャンネルヘッダに埋めても下流の初期化には使えない。にもかかわらず昇格させると
    // OnHeaderChanged が GenerateStreamID() を呼び、このタグが来るたびに全視聴者が
    // 再初期化される(master は Ex タグを一切昇格させていなかった)。
    let tsBootstrap = [| 0x47uy;0x40uy;0x00uy;0x10uy;0xDEuy;0xADuy;0xBEuy;0xEFuy |]
    let capture =
        run (Array.concat [
                flvHeader
                makeTag 9 0  (exVideoMpeg2TsSeq "avc1" tsBootstrap)
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 avcNalus)
             ])
    Assert.False(capture.Headers |> Array.exists (fun h -> contains h tsBootstrap),
                 "TS ブートストラップはチャンネルヘッダに埋め込まれない")
    // ヘッダ生成は最初のコンテンツで1回だけ。昇格させていると2回以上になる。
    Assert.Equal(1, capture.HeaderCount)
