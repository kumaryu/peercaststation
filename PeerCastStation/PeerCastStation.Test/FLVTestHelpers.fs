// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
/// FLV/E-RTMP のフィルタ試験で共通に使うタグ組み立て・キャプチャ用のヘルパ。
/// FLVToMKVTests / FLVToMPEG2TSTests / FLVContentBufferTests が同じタグ形式を
/// 組み立てるので、形式の定義はここに一本化する。
module FLVTestHelpers

open System
open PeerCastStation.Core

// ---- バイト列ユーティリティ ----

let ascii (s:string) = System.Text.Encoding.ASCII.GetBytes(s)

let indexOf (haystack:byte[]) (needle:byte[]) =
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

let contains (haystack:byte[]) (needle:byte[]) = indexOf haystack needle >= 0

/// haystack 中に needle が現れる回数(重なりも数える)。
let countOf (haystack:byte[]) (needle:byte[]) =
    if needle.Length=0 then 0
    else
        let mutable count = 0
        for i in 0..(haystack.Length - needle.Length) do
            let mutable j = 0
            while j<needle.Length && haystack.[i+j]=needle.[j] do j <- j+1
            if j=needle.Length then count <- count+1
        count

// ---- FLV コンテナ ----

/// FLVタグ(11バイトヘッダ + body + 4バイトPreviousTagSize)を組み立てる。
let makeTag (typ:int) (timestamp:int) (body:byte[]) =
    let ds = body.Length
    let header =
        [| byte typ
           byte (ds>>>16); byte (ds>>>8); byte ds
           byte (timestamp>>>16); byte (timestamp>>>8); byte timestamp; byte (timestamp>>>24)
           0uy; 0uy; 0uy |]
    let tagsize = ds + 11
    let footer = [| byte (tagsize>>>24); byte (tagsize>>>16); byte (tagsize>>>8); byte tagsize |]
    Array.concat [ header; body; footer ]

let flvHeader =
    // "FLV" v1 flags(audio+video) DataOffset=9 PreviousTagSize0=0
    [| 0x46uy;0x4Cuy;0x56uy; 1uy; 0x05uy; 0uy;0uy;0uy;9uy; 0uy;0uy;0uy;0uy |]

// ---- ビット詰めヘッダの組み立て ----

/// (ビット幅, 値) の並びを MSB 詰めでバイト列にする。末尾は 0 でパディングする。
/// AudioSpecificConfig のようなバイト境界に揃わないヘッダを、手計算の16進数ではなく
/// フィールド単位で書けるようにする(手計算はビットのずれをテスト側に持ち込む)。
let packBits (fields:(int*int) seq) =
    let bits = ResizeArray<int>()
    for (width, value) in fields do
        for i in (width-1) .. -1 .. 0 do
            bits.Add((value >>> i) &&& 1)
    while bits.Count % 8 <> 0 do bits.Add(0)
    [| for i in 0 .. (bits.Count/8 - 1) ->
         let mutable b = 0
         for j in 0..7 do b <- (b <<< 1) ||| bits.[i*8+j]
         byte b |]

/// AudioSpecificConfig。audioObjectType / samplingFrequencyIndex / channelConfiguration のみ。
let audioSpecificConfig (aot:int) (freqIdx:int) (channels:int) =
    packBits [ (5, aot); (4, freqIdx); (4, channels) ]

/// SBR/PS の明示signaling付き AudioSpecificConfig(HE-AAC は AOT=5、HE-AACv2 は AOT=29)。
/// コアの samplingFrequencyIndex に続けて拡張レートと本来のコア audioObjectType が並ぶ。
let audioSpecificConfigSbr (aot:int) (freqIdx:int) (channels:int) (extFreqIdx:int) (coreAot:int) =
    packBits [ (5, aot); (4, freqIdx); (4, channels); (4, extFreqIdx); (5, coreAot) ]

/// samplingFrequencyIndex に明示レートのエスケープ(0x0F)を使った AudioSpecificConfig。
let audioSpecificConfigExplicitRate (aot:int) (rate:int) (channels:int) =
    packBits [ (5, aot); (4, 0x0F); (24, rate); (4, channels) ]

// ---- コーデック設定/ペイロードのサンプル ----

/// nalSizeLen=4、SPS/PPS を1つずつ持つ最小の avcC。
/// SPS は解像度を取り出せるところまでは書かれていない(中身を見ない試験用)。
let avcC =
    [| 1uy;0x42uy;0x00uy;0x1Fuy;0xFFuy;0xE1uy;0x00uy;0x04uy;0x67uy;0x42uy;0x00uy;0x1Fuy;0x01uy;0x00uy;0x04uy;0x68uy;0xCEuy;0x3Cuy;0x80uy |]

/// 符号なし Exp-Golomb(ue(v))のビット列。packBits に渡す形で返す。
let ue (v:int) =
    let x = v + 1
    let mutable bits = 0
    let mutable t = x
    while t>1 do
        t <- t >>> 1
        bits <- bits + 1
    [ (bits, 0); (bits+1, x) ]

/// 解像度を取り出せる H.264 SPS の NAL ユニット(baseline profile)。
/// width  = (widthMbsMinus1+1)*16 - 2*(cropLeft+cropRight)
/// height = (heightMapUnitsMinus1+1)*16 - 2*(cropTop+cropBottom)
let h264Sps (widthMbsMinus1:int) (heightMapUnitsMinus1:int) (cropBottom:int) =
    let bits =
        [ [ (8, 66); (8, 0); (8, 31) ] // profile_idc(baseline) / constraint flags / level_idc
          ue 0                          // seq_parameter_set_id
          ue 0                          // log2_max_frame_num_minus4
          ue 2                          // pic_order_cnt_type
          ue 1                          // max_num_ref_frames
          [ (1, 0) ]                    // gaps_in_frame_num_value_allowed_flag
          ue widthMbsMinus1
          ue heightMapUnitsMinus1
          [ (1, 1) ]                    // frame_mbs_only_flag
          [ (1, 1) ]                    // direct_8x8_inference_flag
          [ (1, 1) ]                    // frame_cropping_flag
          ue 0; ue 0; ue 0; ue cropBottom
          [ (1, 0) ]                    // vui_parameters_present_flag
          [ (1, 1) ]                    // rbsp_stop_one_bit
        ] |> List.concat
    Array.append [| 0x67uy |] (packBits bits)

/// 指定した SPS を持つ avcC。PPS は既存の avcC と同じダミー。
let avcCWith (sps:byte[]) =
    Array.concat [
        [| 1uy;0x42uy;0x00uy;0x1Fuy;0xFFuy;0xE1uy |]
        [| byte (sps.Length>>>8); byte sps.Length |]
        sps
        [| 0x01uy;0x00uy;0x04uy;0x68uy;0xCEuy;0x3Cuy;0x80uy |]
    ]

/// ダミーの hvcC(構造検証では中身は問わない)。
let hvcC = [| 1uy;0x01uy;0x60uy;0x00uy;0x00uy;0x03uy;0x00uy;0x90uy;0x12uy;0x34uy |]

/// 4バイト長プレフィックス付きの NAL ユニット列(IDR)。
let avcNalus = [| 0uy;0uy;0uy;2uy;0x65uy;0x88uy |]

// ---- E-RTMP(enhanced タグ)の組み立て ----

/// enhanced 映像 SequenceStart(frameType=1, packetType=0)。
let exVideoSeq (fourcc:string) (config:byte[]) =
    Array.concat [ [| 0x90uy |]; ascii fourcc; config ]

/// enhanced 映像 SequenceStart を ModEx(1バイトの modExData)で包んだもの。
/// byte0=0x97(frameType=1,packetType=7=ModEx) / 0x00(size-1=0) / 0xAA(modExData) / 0x00(実packetType=0)
/// レガシー解釈では下位ニブル 7 が codecId=7(AVC)と誤読される。
let exVideoSeqModEx (fourcc:string) (config:byte[]) =
    Array.concat [ [| 0x97uy; 0x00uy; 0xAAuy; 0x00uy |]; ascii fourcc; config ]

/// enhanced 映像 CodedFrames(packetType=1)。AVC/HEVC は FourCC 直後に符号付き24bit CTS を持つ。
let exVideoCodedFrames (fourcc:string) (frameType:int) (cts:int) (payload:byte[]) =
    let b0 = 0x80 ||| ((frameType &&& 0x07) <<< 4) ||| 0x01
    Array.concat [ [| byte b0 |]; ascii fourcc; [| byte (cts>>>16); byte (cts>>>8); byte cts |]; payload ]

/// enhanced 映像 CodedFrames のうち CTS フィールドを持たないもの(AV1/VP9 は FourCC 直後が即ペイロード)。
let exVideoCodedFramesNoCts (fourcc:string) (frameType:int) (payload:byte[]) =
    let b0 = 0x80 ||| ((frameType &&& 0x07) <<< 4) ||| 0x01
    Array.concat [ [| byte b0 |]; ascii fourcc; payload ]

/// enhanced 映像 MPEG2TSSequenceStart(frameType=1, packetType=5)。
/// 中身はコーデック設定ではなく MPEG-2 TS のブートストラップ生バイト列。
let exVideoMpeg2TsSeq (fourcc:string) (payload:byte[]) =
    Array.concat [ [| 0x95uy |]; ascii fourcc; payload ]

/// enhanced 映像 Multitrack(OneTrack)SequenceStart。payloadOffset は無効化される想定。
/// byte0=0x96(frameType=1,packetType=6=Multitrack) / 0x00(multitrackType=0,実packetType=0)
let exVideoMultitrackSeq (fourcc:string) (config:byte[]) =
    Array.concat [ [| 0x96uy; 0x00uy |]; ascii fourcc; config ]

/// enhanced 音声 SequenceStart(soundFormat=9, packetType=0)。
let exAudioSeq (fourcc:string) (asc:byte[]) =
    Array.concat [ [| 0x90uy |]; ascii fourcc; asc ]

/// enhanced 音声 CodedFrames(soundFormat=9, packetType=1)。音声に CTS は無い。
let exAudioCodedFrames (fourcc:string) (raw:byte[]) =
    Array.concat [ [| 0x91uy |]; ascii fourcc; raw ]

// ---- レガシータグの組み立て ----

/// AAC-LC 44100Hz 2ch の AudioSpecificConfig を持つレガシー音声シーケンスヘッダ。
let legacyAacSeq = [| 0xAFuy;0x00uy;0x12uy;0x10uy |]

/// 任意の AudioSpecificConfig を持つレガシー音声シーケンスヘッダ。
let legacyAacSeqWith (asc:byte[]) = Array.concat [ [| 0xAFuy;0x00uy |]; asc ]

/// レガシー AAC の生フレーム。
let legacyAacFrame (payload:byte[]) = Array.concat [ [| 0xAFuy;0x01uy |]; payload ]

/// レガシー AAC の生フレーム(既定のダミーペイロード)。
let legacyAacRaw = legacyAacFrame [| 0x21uy;0x10uy;0x04uy |]

/// レガシー AVC のシーケンスヘッダ(キーフレーム)。
let legacyAvcSeq (config:byte[]) =
    Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; config ]

/// レガシー AVC のキーフレーム。
let legacyAvcKey (nalus:byte[]) =
    Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; nalus ]

// ---- IContentSink のキャプチャ ----

/// 下流に流れた内容を保持するだけの IContentSink。
/// ContentHeader は連結後(Header)とパケット単位(Headers)の両方で取れるようにしてある。
/// ヘッダの再生成回数そのものを検証する側は HeaderCount を見る。
type CaptureSink() =
    let headers = System.Collections.Generic.List<byte[]>()
    let content = System.Collections.Generic.List<byte>()
    member val ChannelType : string = null with get, set
    member val ChannelInfo : ChannelInfo = null with get, set
    member _.Header = Array.concat headers
    member _.Headers = headers.ToArray()
    member _.HeaderCount = headers.Count
    member _.Content = content.ToArray()
    interface IContentSink with
        member this.OnChannelInfo(ci) =
            this.ChannelType <- ci.ContentType
            this.ChannelInfo <- ci
        member _.OnChannelTrack(_) = ()
        member _.OnContentHeader(c) = headers.Add(c.Data.ToArray())
        member _.OnContent(c) = content.AddRange(c.Data.ToArray())
        member _.OnStop(_) = ()

let newContent (data:byte[]) =
    Content(0, TimeSpan.Zero, 0L, data, 0, data.Length, PCPChanPacketContinuation.None)
