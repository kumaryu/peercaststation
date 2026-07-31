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

// ---- コーデック設定/ペイロードのサンプル ----

/// nalSizeLen=4、SPS/PPS を1つずつ持つ最小の avcC。
let avcC =
    [| 1uy;0x42uy;0x00uy;0x1Fuy;0xFFuy;0xE1uy;0x00uy;0x04uy;0x67uy;0x42uy;0x00uy;0x1Fuy;0x01uy;0x00uy;0x04uy;0x68uy;0xCEuy;0x3Cuy;0x80uy |]

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

// ---- IContentSink のキャプチャ ----

/// 下流に流れた内容を保持するだけの IContentSink。
/// ContentHeader は連結後(Header)とパケット単位(Headers)の両方で取れるようにしてある。
/// ヘッダの再生成回数そのものを検証する側は HeaderCount を見る。
type CaptureSink() =
    let headers = System.Collections.Generic.List<byte[]>()
    let content = System.Collections.Generic.List<byte>()
    member val ChannelType : string = null with get, set
    member _.Header = Array.concat headers
    member _.Headers = headers.ToArray()
    member _.HeaderCount = headers.Count
    member _.Content = content.ToArray()
    interface IContentSink with
        member this.OnChannelInfo(ci) = this.ChannelType <- ci.ContentType
        member _.OnChannelTrack(_) = ()
        member _.OnContentHeader(c) = headers.Add(c.Data.ToArray())
        member _.OnContent(c) = content.AddRange(c.Data.ToArray())
        member _.OnStop(_) = ()

let newContent (data:byte[]) =
    Content(0, TimeSpan.Zero, 0L, data, 0, data.Length, PCPChanPacketContinuation.None)
