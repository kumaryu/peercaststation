// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
module FLVToMPEG2TSTests

open Xunit
open System
open PeerCastStation.Core
open PeerCastStation.FLV
open PeerCastStation.FLV.RTMP
open FLVTestHelpers

// ---- helpers ----

/// 出力が MPEG-2 TS パケット列(188バイト境界、同期バイト 0x47)であることを確認する。
let private assertValidTS (bytes:byte[]) =
    Assert.True(bytes.Length>0, "TS パケットが出力される")
    Assert.Equal(0, bytes.Length % 188)
    for i in 0..(bytes.Length/188 - 1) do
        Assert.Equal(0x47uy, bytes.[i*188])

/// FLVヘッダ+シーケンスヘッダを ContentHeader、フレームを ContentBody として流す。
let private run (seqTags:byte[]) (frameTags:byte[]) =
    let capture = CaptureSink()
    let sink = FLVToTSContentFilter().Activate(capture)
    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent (Array.concat [ flvHeader; seqTags ]))
    sink.OnContent(newContent frameTags)
    sink.OnStop(StopReason.OffAir) // OnStop は processorTask.Wait() で完了を待つ
    capture

// ---- tests ----

[<Fact>]
let ``レガシー FLV(H264) を TS に変換する`` () =
    let capture =
        run (makeTag 9 0 (Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]))
            (makeTag 9 0 (Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; avcNalus ]))
    Assert.Equal("TS", capture.ChannelType)
    assertValidTS capture.Header
    assertValidTS capture.Content

[<Fact>]
let ``E-RTMP(avc1) のタグを TS に変換する`` () =
    // 共有分類器を使う前は body[0]&0x0F を codecId として読んでいたため、
    // Ex タグ(SequenceStart は 0、CodedFrames は 1)はいずれも AVC と認識されず
    // 黙って破棄され、出力が一切出なかった。
    let capture =
        run (makeTag 9 0 (exVideoSeq "avc1" avcC))
            (makeTag 9 0 (exVideoCodedFrames "avc1" 1 0 avcNalus))
    assertValidTS capture.Header
    assertValidTS capture.Content

[<Fact>]
let ``E-RTMP の ModEx タグを AVC と誤認しない`` () =
    // byte0=0x97 は packetType=7(ModEx)。レガシー解釈では codecId=7(AVC)かつ
    // body[1]=0 なので AVCSequenceHeader と誤認し、FourCC 部分を avcC として
    // 読んで NAL ユニット数を取り違え、例外でフィルタが停止していた。
    let capture =
        run (makeTag 9 0 (exVideoSeqModEx "avc1" avcC))
            (makeTag 9 0 (exVideoCodedFrames "avc1" 1 0 avcNalus))
    assertValidTS capture.Header
    assertValidTS capture.Content

[<Fact>]
let ``TS フィルタが未対応の enhanced 映像コーデックを破棄する`` () =
    // 本フィルタは H.264 のみ対応。HEVC は誤ったストリームタイプで出力せず破棄する。
    let capture =
        run (makeTag 9 0 (exVideoSeq "hvc1" hvcC))
            (makeTag 9 0 (exVideoCodedFrames "hvc1" 1 0 [| 0uy;0uy;0uy;2uy;0x26uy;0x01uy |]))
    Assert.Empty(capture.Header)
    Assert.Empty(capture.Content)

[<Fact>]
let ``切り詰められた enhanced タグを挟んでも TS 出力を継続する`` () =
    let capture =
        run (makeTag 9 0 (exVideoSeq "avc1" avcC))
            (Array.concat [
                makeTag 9 0 [| 0x80uy |] // Ex マーカーだけで FourCC まで届かない
                makeTag 9 0 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    assertValidTS capture.Content

// ---- 破損入力に対する回帰テスト ----

/// シーケンスヘッダ1つとフレーム1つだけを流したときの TS 出力長。
/// 破損タグが「黙って捨てられた」ことを、この基準と比較して確認する。
let private baselineContentLength () =
    (run (makeTag 9 0 (exVideoSeq "avc1" avcC))
         (makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus))).Content.Length

[<Fact>]
let ``シーケンスヘッダより前の映像フレームを捨てて TS 出力を継続する`` () =
    // avcC 未受信だと nalSizeLen が 0 のままで、NAL 長を常に 0 と読み
    // NALUnit.ReadFrom が new byte[-1] を確保しようとして OverflowException になる。
    // FLVFileParser がこれをタグ先頭への巻き戻しとして扱うと同じ毒タグを永久に
    // 再パースし続け、出力が恒久停止していた。
    let capture =
        run (makeTag 9 0 (exVideoCodedFrames "avc1" 1 0 avcNalus)) // ヘッダより先に来たフレーム
            (Array.concat [
                makeTag 9 0  (exVideoSeq "avc1" avcC)
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    assertValidTS capture.Content
    Assert.Equal(baselineContentLength(), capture.Content.Length)

[<Fact>]
let ``切り詰められた avcC を捨てて TS 出力を継続する`` () =
    // 0xE3 = sps_count 3。実データには SPS が1つしか無く、以前は span の Slice が
    // 範囲外例外を投げてフィルタが停止していた(音声側だけが TryParse 形で堅牢だった)。
    let brokenAvcC =
        [| 1uy;0x42uy;0x00uy;0x1Fuy;0xFFuy;0xE3uy;0x00uy;0x04uy;0x67uy;0x42uy;0x00uy;0x1Fuy |]
    let capture =
        run (Array.concat [
                makeTag 9 0 (exVideoSeq "avc1" brokenAvcC)
                makeTag 9 0 (exVideoSeq "avc1" avcC)
             ])
            (makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus))
    assertValidTS capture.Content
    Assert.Equal(baselineContentLength(), capture.Content.Length)

[<Fact>]
let ``NALユニット長が不正な映像フレームを捨てて TS 出力を継続する`` () =
    // avcC の lengthSizeMinusOne=3(nalSizeLen=4)。長さを符号付き Int32 で読むと
    // 0xFFFFFFFF は -1 になり new byte[-2] で OverflowException、
    // 0x7FFFFFFF は約2GBの確保を試みて OutOfMemoryException になっていた。
    let capture =
        run (makeTag 9 0 (exVideoSeq "avc1" avcC))
            (Array.concat [
                makeTag 9 0  (exVideoCodedFrames "avc1" 1 0 [| 0xFFuy;0xFFuy;0xFFuy;0xFFuy;0x65uy;0x88uy |])
                makeTag 9 5  (exVideoCodedFrames "avc1" 1 0 [| 0x7Fuy;0xFFuy;0xFFuy;0xFFuy;0x65uy;0x88uy |])
                makeTag 9 10 (exVideoCodedFrames "avc1" 1 0 [| 0x00uy;0x00uy;0x10uy;0x00uy;0x65uy;0x88uy |]) // 残バイト超過
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    assertValidTS capture.Content
    Assert.Equal(baselineContentLength(), capture.Content.Length)

[<Fact>]
let ``壊れた Script タグを挟んでも TS 出力を継続する`` () =
    // Script タグは DispatchTag 内の DataAMF0Message コンストラクタで即座に AMF0 解析される。
    // 未知マーカー(0xFF)は切り詰めではないので InvalidDataException になり、
    // catch(EndOfStreamException) だけでは取り逃がして ProcessMessagesAsync まで抜け、
    // OnStop(NotIdentifiedError) で1つの壊れたタグが配信全体を落としていた。
    let capture =
        run (makeTag 9 0 (exVideoSeq "avc1" avcC))
            (Array.concat [
                makeTag 18 0 [| 0xFFuy |]
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    assertValidTS capture.Content
    Assert.Equal(baselineContentLength(), capture.Content.Length)
