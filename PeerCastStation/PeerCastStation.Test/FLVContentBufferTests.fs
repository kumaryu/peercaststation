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

/// enhanced 映像 MPEG2TSSequenceStart(frameType=1, packetType=5)。
/// 中身はコーデック設定ではなく MPEG-2 TS のブートストラップ生バイト列。
let private exVideoMpeg2TsSeq (fourcc:string) (payload:byte[]) =
    Array.concat [ [| 0x95uy |]; ascii fourcc; payload ]

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
