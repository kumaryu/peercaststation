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
open FLVTestHelpers

// ---- helpers ----

/// AMF0 の文字列(マーカー付き)。
let private amf0String (s:string) =
    let b = ascii s
    Array.concat [ [| 0x02uy; byte (b.Length>>>8); byte b.Length |]; b ]

/// AMF0 の連想配列キー(マーカー無しの UTF-8)。
let private amf0Key (s:string) =
    let b = ascii s
    Array.concat [ [| byte (b.Length>>>8); byte b.Length |]; b ]

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
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
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
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    Assert.False(capture.Headers |> Array.exists (fun h -> contains h tsBootstrap),
                 "TS ブートストラップはチャンネルヘッダに埋め込まれない")
    // ヘッダ生成は最初のコンテンツで1回だけ。昇格させていると2回以上になる。
    Assert.Equal(1, capture.HeaderCount)

[<Fact>]
let ``レガシー映像の非キーフレームをシーケンスヘッダとして昇格させない`` () =
    // レガシー AVC の判定を AVCPacketType(body[1]==0)だけで行うと、AVCPacketType が
    // 0 に化けた壊れたインターフレーム(0x27 0x00 ...)や frameType=5 のコマンドフレーム
    // (body[1] はコマンド番号で 0=StartOfClientSeek)までコーデック設定と見なしてしまう。
    // 昇格すると OnHeaderChanged が GenerateStreamID() を呼ぶので、この手のタグ1つで
    // 全視聴者の再初期化を繰り返し起こせる。master は body[0]==0x17 を要求していた。
    let garbage = [| 0xAAuy;0xBBuy;0xCCuy;0xDDuy |]
    let capture =
        run (Array.concat [
                flvHeader
                makeTag 9 0  (Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ])
                makeTag 9 10 (Array.concat [ [| 0x27uy;0x00uy;0x00uy;0x00uy;0x00uy |]; garbage ]) // インターフレーム
                makeTag 9 15 (Array.concat [ [| 0x57uy;0x00uy;0x00uy;0x00uy;0x00uy |]; garbage ]) // コマンドフレーム
                makeTag 9 20 (Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; avcNalus ])
             ])
    Assert.False(capture.Headers |> Array.exists (fun h -> contains h garbage),
                 "非キーフレームの中身はチャンネルヘッダに埋め込まれない")
    Assert.True(capture.Headers |> Array.exists (fun h -> contains h avcC),
                "本物の avcC は昇格する")
    // ヘッダ生成は本物の avcC による1回だけ。昇格させていると破損タグごとに増える。
    Assert.Equal(1, capture.HeaderCount)

[<Fact>]
let ``小数点付きの videodatarate をホストのロケールに依らず解釈する`` () =
    // onMetaData の数値文字列は配信者側のエンコーダが '.' を小数点として書くもので、
    // ホストのロケールとは無関係。カルチャ依存の TryParse で読んでいたため、
    // '.' を桁区切りとする de-DE のホストでは "2500.5" が 25005 になり、
    // ChanInfo のビットレートが約10倍で PCP に広告されて全ノードのリレー判断を狂わせていた。
    let onMetaData =
        Array.concat [
            amf0String "onMetaData"
            [| 0x08uy; 0uy;0uy;0uy;1uy |] // ECMAArray(associative-count=1)
            amf0Key "videodatarate"
            amf0String "2500.5"
            [| 0uy;0uy;0x09uy |]          // object end marker
        ]
    let original = Thread.CurrentThread.CurrentCulture
    try
        Thread.CurrentThread.CurrentCulture <- Globalization.CultureInfo.GetCultureInfo("de-DE")
        let capture =
            run (Array.concat [
                    flvHeader
                    makeTag 18 0 onMetaData
                    makeTag 9 0  (exVideoSeq "avc1" avcC)
                 ])
        Assert.NotNull(capture.ChannelInfo)
        Assert.Equal(2500, capture.ChannelInfo.Bitrate)
    finally
        Thread.CurrentThread.CurrentCulture <- original

[<Fact>]
let ``数値でない videodatarate を含む onMetaData で停止しない`` () =
    // videodatarate は (double) キャストで読んでいたため、文字列だと FormatException、
    // 非数値型だと InvalidCastException になる。FLVFileParser を経由しない RTMP 受信経路では
    // これが接続ごと落とし、配信者が同じ onMetaData を送り直すので再接続を繰り返す。
    let onMetaData =
        Array.concat [
            amf0String "onMetaData"
            [| 0x08uy; 0uy;0uy;0uy;1uy |] // ECMAArray(associative-count=1)
            amf0Key "videodatarate"
            amf0String "2500k"
            [| 0uy;0uy;0x09uy |]          // object end marker
        ]
    let capture =
        run (Array.concat [
                flvHeader
                makeTag 18 0 onMetaData
                makeTag 9 0  (exVideoSeq "avc1" avcC)
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    // onMetaData が最後まで処理されればチャンネルヘッダが生成される。
    // 途中で例外になっていればタグごと読み飛ばされ、ヘッダにも現れない。
    Assert.True(capture.Headers |> Array.exists (fun h -> contains h (ascii "videodatarate")),
                "onMetaData がチャンネルヘッダに取り込まれる")
    Assert.True(capture.Content.Length>0, "後続のフレームが流れ続ける")
