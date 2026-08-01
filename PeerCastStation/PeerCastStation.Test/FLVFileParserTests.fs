// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
module FLVFileParserTests

open Xunit
open System
open System.IO
open PeerCastStation.FLV
open PeerCastStation.FLV.RTMP
open FLVTestHelpers

// ---- helpers ----

/// パーサから届いた呼び出しを数えるだけの sink。
/// onVideo に例外を仕込めるようにして、下流が投げた場合のパーサの挙動を観察する。
type private RecordingSink(?onVideo:RTMPMessage -> unit) =
    let videos = ResizeArray<RTMPMessage>()
    member val FLVHeaderCount = 0 with get, set
    member _.Videos = videos.ToArray()
    member _.VideoCount = videos.Count
    interface IRTMPContentSink with
        member this.OnFLVHeader(_) = this.FLVHeaderCount <- this.FLVHeaderCount + 1
        member _.OnData(_) = ()
        member _.OnAudio(_) = ()
        member _.OnVideo(msg) =
            videos.Add(msg)
            match onVideo with
            | Some f -> f msg
            | None -> ()

/// FLVParseBuffer と同じ手順(追記 → Read → 消費済みを捨てる)で分割入力を再現する。
/// FLVParseBuffer は internal なので、ここでは同じ契約を最小限で写している。
type private FeedBuffer() =
    let parser = FLVFileParser()
    let buffer = new MemoryStream()
    /// 追記して読めるだけ読む。読み終えた時点の未消費バイト数を返す。
    member _.Feed(data:byte[], sink:IRTMPContentSink) =
        let pos = buffer.Position
        buffer.Seek(0L, SeekOrigin.End) |> ignore
        buffer.Write(data, 0, data.Length)
        buffer.Position <- pos
        parser.Read(buffer, sink) |> ignore
        let consumed = buffer.Position
        let remain = int (buffer.Length - consumed)
        if consumed>0L then
            if remain>0 then
                let raw = buffer.GetBuffer()
                Array.Copy(raw, int consumed, raw, 0, remain)
            buffer.SetLength(int64 remain)
            buffer.Position <- 0L
        remain

let private avcSeq = Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]
let private avcKey = Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; avcNalus ]

// ---- tests ----

[<Fact>]
let ``分割して届いたFLVファイルヘッダを認識する`` () =
    // 再同期スキャンは末尾に達しても、そこに残っているのが次の Feed で FLV ファイルヘッダ
    // (13バイト)に育つ断片である可能性を残さなければならない。'F'/'L'/'V' はどれも
    // タグ候補バイトではないのでスキャンは必ず空振りし、走査済みとして丸ごと捨てると
    // 次の Feed でヘッダを組み立て直せない。OnFLVHeader は下流フィルタが状態をリセットする
    // 唯一の契機なので、取りこぼすと古い avcC やヘッダ送信済みフラグが新しいストリームに残る。
    let sink = RecordingSink()
    let buf = FeedBuffer()
    buf.Feed(Array.concat [ flvHeader; makeTag 9 0 avcSeq ], sink) |> ignore
    Assert.Equal(1, sink.FLVHeaderCount)
    // ゴミ + 再送されたFLVヘッダの先頭7バイトで切る(ヘッダ途中で分割される)。
    let garbage = Array.create 32 0xFFuy
    buf.Feed(Array.concat [ garbage; Array.sub flvHeader 0 7 ], sink) |> ignore
    // 残り6バイトが届いた時点でヘッダとして認識される。
    buf.Feed(Array.concat [ Array.sub flvHeader 7 6; makeTag 9 0 avcKey ], sink) |> ignore
    Assert.Equal(2, sink.FLVHeaderCount)

[<Fact>]
let ``タグ候補のないゴミで解析バッファが伸び続けない`` () =
    // ヘッダ断片を残す処理が「毎回巻き戻す」になっていると、0xFF 埋めのような入力で
    // 未消費分が Feed のたびに積み上がり、走査量がバイト数の二乗で増える。
    let sink = RecordingSink()
    let buf = FeedBuffer()
    buf.Feed(flvHeader, sink) |> ignore
    let mutable remain = 0
    for _ in 1..10 do
        remain <- buf.Feed(Array.create 4096 0xFFuy, sink)
    // 残ってよいのは FLV ファイルヘッダに育ちうる末尾の断片(12バイト)まで。
    Assert.True(remain<=12, sprintf "未消費が %d バイト残っている" remain)

[<Fact>]
let ``桁違いのDataSizeを持つヘッダをタグとして受け付けない`` () =
    // DataSize は24bitなので、破損した1バイトだけで最大16MBのタグ長になりうる。
    // それを信じると長さが揃うまで何も出力できず、その間バッファが伸び続ける。
    let sink = RecordingSink()
    let buf = FeedBuffer()
    buf.Feed(flvHeader, sink) |> ignore
    // type=9 / DataSize=0xFFFFFF の壊れたタグヘッダ11バイト + 後続の正常なタグ。
    let bogus = [| 9uy; 0xFFuy;0xFFuy;0xFFuy; 0uy;0uy;0uy;0uy; 0uy;0uy;0uy |]
    let remain = buf.Feed(Array.concat [ bogus; makeTag 9 0 avcKey ], sink)
    // 壊れたヘッダは読み飛ばされ、後続の正常なタグが届く。
    Assert.Equal(1, sink.VideoCount)
    Assert.True(remain<=12, sprintf "未消費が %d バイト残っている" remain)

[<Fact>]
let ``下流が投げた例外を握り潰さず同じタグを再配信しない`` () =
    // sink の呼び出しをパース用の try の中で行うと、下流の EndOfStreamException を
    // 「データ待ち」と誤認してタグ先頭へ巻き戻す。未消費のまま同じ毒タグが残るので、
    // 次の Feed で再び配信して再び例外になり、出力が恒久停止したうえで
    // バッファが配信レートのまま伸び続ける。
    let mutable thrown = 0
    let sink = RecordingSink(fun _ ->
        thrown <- thrown + 1
        raise (EndOfStreamException()))
    let buf = FeedBuffer()
    buf.Feed(flvHeader, sink) |> ignore
    // 例外は握り潰されず呼び出し元まで出る。
    Assert.Throws<EndOfStreamException>(fun () ->
        buf.Feed(makeTag 9 0 avcKey, sink) |> ignore) |> ignore
    Assert.Equal(1, thrown)
    // 毒タグは消費済みになっているので、次の Feed で再配信されない。
    let sink2 = RecordingSink()
    buf.Feed(makeTag 9 10 avcKey, sink2) |> ignore
    Assert.Equal(1, sink2.VideoCount)
