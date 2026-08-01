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

/// 映像 PES(stream_id=0xE0)のヘッダから (PES_packet_length, PTS) を出現順に取り出す。
/// PES ヘッダは PUSI 付き TS パケットの先頭側に収まるので連続バイトとして読める。
let private videoPesHeaders (bytes:byte[]) =
    [ for i in 0 .. bytes.Length-14 do
        if bytes.[i]=0uy && bytes.[i+1]=0uy && bytes.[i+2]=1uy && bytes.[i+3]=0xE0uy then
            let len = (int bytes.[i+4] <<< 8) ||| int bytes.[i+5]
            let flags = (int bytes.[i+7] >>> 6) &&& 0x03
            let pts =
                if flags=0 then -1L
                else
                    let hi  = ((int64 bytes.[i+9]) >>> 1) &&& 0x07L
                    let mid = ((((int64 bytes.[i+10]) <<< 8) ||| (int64 bytes.[i+11])) >>> 1) &&& 0x7FFFL
                    let lo  = ((((int64 bytes.[i+12]) <<< 8) ||| (int64 bytes.[i+13])) >>> 1) &&& 0x7FFFL
                    (hi <<< 30) ||| (mid <<< 15) ||| lo
            yield (len, pts) ]

/// 出力から ADTS ヘッダ(syncword 0xFFF)を探し (profile, samplingFreqIndex, channelConfiguration)
/// を出現順に取り出す。syncword だけで探すと TS の adaptation field を埋める
/// スタッフィングバイト(0xFF の連続)を拾ってしまうため、syncword に続く
/// layer(2bit、常に 0)まで含めて絞る(0xFF は layer=3 になり除外される)。
let private adtsHeaders (bytes:byte[]) =
    [ for i in 0 .. bytes.Length-7 do
        if bytes.[i]=0xFFuy && (bytes.[i+1] &&& 0xF6uy)=0xF0uy then
            let profile = (int bytes.[i+2] >>> 6) &&& 0x03
            let freqIdx = (int bytes.[i+2] >>> 2) &&& 0x0F
            let channels =
                ((int bytes.[i+2] &&& 0x01) <<< 2) ||| ((int bytes.[i+3] >>> 6) &&& 0x03)
            yield (profile, freqIdx, channels) ]

/// AAC-LC 44100Hz 2ch の AudioSpecificConfig を持つレガシー音声シーケンスヘッダ。
let private legacyAacSeq = [| 0xAFuy;0x00uy;0x12uy;0x10uy |]

/// 任意の AudioSpecificConfig を持つレガシー音声シーケンスヘッダ。
let private legacyAacSeqWith (asc:byte[]) = Array.concat [ [| 0xAFuy;0x00uy |]; asc ]

/// レガシー AAC の生フレーム。
let private legacyAacFrame (payload:byte[]) = Array.concat [ [| 0xAFuy;0x01uy |]; payload ]

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
let ``Activate 前に構築されたフィルタは変換ループを開始していない`` () =
    // 変換ループを基底コンストラクタから起動すると、派生クラスのコンストラクタ本体より
    // 先にループが走り出す。派生が状態を初期化する前にループがそれを読むと NRE になり、
    // ProcessMessagesAsync の catch 経由で下流が NotIdentifiedError で止まるため、
    // 症状は「配信が途中で死ぬ」でログには構築順の話が出てこない。
    // Activate が Start を呼ぶ二相構築になっていることを、二重 Start の拒否で確認する。
    let capture = CaptureSink()
    let sink = FLVToTSContentFilter().Activate(capture) :?> FLVContentFilterSinkBase
    Assert.Throws<InvalidOperationException>(fun () -> sink.Start()) |> ignore
    sink.OnStop(StopReason.OffAir)

[<Fact>]
let ``停止後に届いたコンテンツを積み上げない`` () =
    // 変換ループの終了後も enqueue を受け付けると、消費者のいないキューに
    // ストリームビットレートで積み上がる。OnStop 後の呼び出しは黙って捨てる。
    let capture = CaptureSink()
    let sink = FLVToTSContentFilter().Activate(capture)
    sink.OnChannelInfo(ChannelInfo(AtomCollection()))
    sink.OnContentHeader(newContent (Array.concat [ flvHeader; makeTag 9 0 (Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]) ]))
    sink.OnStop(StopReason.OffAir)
    let after = capture.Content.Length
    // 停止後の呼び出しは例外にならず、出力も増えない。
    sink.OnContent(newContent (makeTag 9 0 (Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; avcNalus ])))
    sink.OnStop(StopReason.OffAir)
    Assert.Equal(after, capture.Content.Length)

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

[<Fact>]
let ``最初に出力するフレームを基準に PTS を正規化する`` () =
    // ptsBase を「Timestamp>0 の最初のタグ」で確定させていたため、ts=0 のフレームでは
    // 基準が決まらず、2番目のフレーム(ts=10)で ts=10 が基準になっていた。
    // 結果 PTS は 0,0,900 となり先頭2フレームが同じ時刻に潰れる。
    let capture =
        run (makeTag 9 0 (exVideoSeq "avc1" avcC))
            (Array.concat [
                makeTag 9 0  (exVideoCodedFrames "avc1" 1 0 avcNalus)
                makeTag 9 10 (exVideoCodedFrames "avc1" 1 0 avcNalus)
                makeTag 9 20 (exVideoCodedFrames "avc1" 1 0 avcNalus)
             ])
    let pts = videoPesHeaders capture.Content |> List.map snd
    Assert.Equal<int64 list>([ 0L; 900L; 1800L ], pts)

[<Fact>]
let ``65535バイトを超える映像フレームの PES 長を 0 にする`` () =
    // PES_packet_length は16bit。WriteUInt16BE が黙って下位16bitに丸めるため、
    // 1080pのIDRのような大きなアクセスユニットでは嘘の長さを宣言していた。
    // 映像ESに限り 0(長さ未指定)が許されているのでそちらを使う。
    let bigNal = Array.append [| 0x41uy |] (Array.create 69999 0x55uy)
    let payload = Array.concat [ [| 0uy;0x01uy;0x11uy;0x70uy |]; bigNal ] // 4バイト長 = 70000
    let capture =
        run (makeTag 9 0 (exVideoSeq "avc1" avcC))
            (makeTag 9 0 (exVideoCodedFrames "avc1" 1 0 payload))
    assertValidTS capture.Content
    let lengths = videoPesHeaders capture.Content |> List.map fst
    Assert.Equal<int list>([ 0 ], lengths)

[<Fact>]
let ``ADTSのframe_lengthに収まらない音声フレームを捨てて出力を継続する`` () =
    // frame_length は13bit(最大8191)。超過分を BitWriter が落とすと実長と食い違う長さを
    // 宣言することになり、ADTS は次フレームをこの長さで探すため以降の同期が壊れる。
    let small = legacyAacFrame (Array.create 32 0x55uy)
    let baseline = (run (makeTag 8 0 legacyAacSeq) (makeTag 8 20 small)).Content.Length
    let capture =
        run (makeTag 8 0 legacyAacSeq)
            (Array.concat [
                makeTag 8 0  (legacyAacFrame (Array.create 8200 0x55uy))
                makeTag 8 20 small
             ])
    assertValidTS capture.Content
    Assert.Equal(baseline, capture.Content.Length)

[<Fact>]
let ``SBRの明示signalingを名乗るのに拡張情報を持たない設定を破棄する`` () =
    // 0x2A 0x10 = AOT 5(SBR の明示signaling)/ freqIdx 4(44100)/ 1ch。AOT 5 は
    // 拡張レートとコア audioObjectType が後続する形式なのに、そこで打ち切られている。
    // コアの audioObjectType が判らない以上 ADTS の profile を決められないので破棄する。
    let capture =
        run (makeTag 8 0 [| 0xAFuy;0x00uy;0x2Auy;0x10uy |])
            (makeTag 8 20 (legacyAacFrame (Array.create 32 0x55uy)))
    Assert.Empty(capture.Content)

[<Fact>]
let ``HE-AAC(SBRの明示signaling)の音声を出力する`` () =
    // AOT=5 を「ADTS の profile 2bit に収まらない」として設定ごと破棄していたため、
    // libfdk_aac -profile:a aac_he のような配信は hasAudio が立たず、以後の全AACフレームが
    // 捨てられたうえ PMT も音声ES抜きで確定し、TS が恒久的に無音になっていた。
    // ADTS は SBR を暗黙signalingで運ぶ形式なので、通知されたコアの audioObjectType
    // (AAC LC=2)で profile を決めれば正しく再生できる。
    // コア 22050Hz(freqIdx=7)/拡張 44100Hz(freqIdx=4)/2ch の HE-AAC v1。
    let asc = audioSpecificConfigSbr 5 7 2 4 2
    let capture =
        run (makeTag 8 0 (legacyAacSeqWith asc))
            (makeTag 8 20 (legacyAacFrame (Array.create 32 0x55uy)))
    assertValidTS capture.Content
    // profile は コアAOT-1 = 1(AAC LC)、samplingFreqIndex はコア側の 7 のまま。
    Assert.Equal<(int*int*int) list>([ (1, 7, 2) ], adtsHeaders capture.Content)

[<Fact>]
let ``明示レートで通知されたサンプリング周波数を表引きインデックスに直して出力する`` () =
    // samplingFrequencyIndex=0x0F は「続く24bitが実レート」の合法なエスケープ。
    // これを予約値(13/14)と一緒に弾いていたため、明示レートで通知する配信は
    // 音声が丸ごと出なくなっていた。実レートが表にあるなら ADTS で表現できる。
    let asc = audioSpecificConfigExplicitRate 2 44100 2
    let capture =
        run (makeTag 8 0 (legacyAacSeqWith asc))
            (makeTag 8 20 (legacyAacFrame (Array.create 32 0x55uy)))
    assertValidTS capture.Content
    // 44100Hz は表引きインデックス 4。
    Assert.Equal<(int*int*int) list>([ (1, 4, 2) ], adtsHeaders capture.Content)

[<Fact>]
let ``表引きできない明示レートの設定は破棄する`` () =
    // 表にない実レートは ADTS の4bitインデックスで表現できない。禁止インデックスを
    // 書くくらいなら設定ごと捨てる(既存の予約値13/14と同じ扱い)。
    let asc = audioSpecificConfigExplicitRate 2 44056 2
    let capture =
        run (makeTag 8 0 (legacyAacSeqWith asc))
            (makeTag 8 20 (legacyAacFrame (Array.create 32 0x55uy)))
    Assert.Empty(capture.Content)

[<Fact>]
let ``ADTSで表現できないchannelConfigurationの設定を破棄する`` () =
    // channel_configuration は3bit。0 はレイアウトを PCE で運ぶ指定だが裸の ADTS には
    // PCE を載せないため受信側が構成を determine できず、多くのデコーダが音声ESごと捨てる。
    // 予約値(8-15)は3bitに収まらず、以前は BitWriter が黙って下位3bitへ丸めていた
    // (8→0、9→1 と別のレイアウトに化ける)。どちらも設定として不正なので破棄する。
    for ch in [ 0; 8; 15 ] do
        let capture =
            run (makeTag 8 0 (legacyAacSeqWith (audioSpecificConfig 2 4 ch)))
                (makeTag 8 20 (legacyAacFrame (Array.create 32 0x55uy)))
        Assert.Empty(capture.Content)

[<Fact>]
let ``7_1chのAACをchannelConfigurationそのままで出力する`` () =
    // channelConfiguration=7 は 8ch(7.1)を意味するインデックス。ADTS の
    // channel_configuration はインデックスをそのまま載せる形式なので 7 のまま書く。
    let capture =
        run (makeTag 8 0 (legacyAacSeqWith (audioSpecificConfig 2 4 7)))
            (makeTag 8 20 (legacyAacFrame (Array.create 32 0x55uy)))
    assertValidTS capture.Content
    Assert.Equal<(int*int*int) list>([ (1, 4, 7) ], adtsHeaders capture.Content)

[<Fact>]
let ``キーフレーム以外として通知されたavcCでも映像を出力する`` () =
    // avcC を frameType=2(inter)で送るエンコーダ/中継実装が実在する。分類器が
    // frameType でシーケンスヘッダを絞っていたため avcC を取り逃し、nalSizeLen が
    // 決まらないまま以後の全フレームが捨てられて映像が一切出なくなっていた。
    let capture =
        run (makeTag 9 0 (Array.concat [ [| 0x27uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ]))
            (makeTag 9 0 (Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; avcNalus ]))
    assertValidTS capture.Content
    Assert.NotEmpty(videoPesHeaders capture.Content)

[<Fact>]
let ``frameType5のコマンドフレームをコーデック設定として扱わない`` () =
    // frameType=5 は video info/command frame で、body[1] は AVCPacketType ではなく
    // コマンド番号(0=StartOfClientSeek)。AVCPacketType 0 として解釈すると
    // コマンドフレームの中身を avcC として読んでしまう。
    let capture =
        run (Array.concat [
                makeTag 9 0 (Array.concat [ [| 0x57uy;0x00uy;0x00uy;0x00uy;0x00uy |]; Array.create 8 0xAAuy ])
                makeTag 9 0 (Array.concat [ [| 0x17uy;0x00uy;0x00uy;0x00uy;0x00uy |]; avcC ])
             ])
            (makeTag 9 0 (Array.concat [ [| 0x17uy;0x01uy;0x00uy;0x00uy;0x00uy |]; avcNalus ]))
    // コマンドフレームで壊れず、後続の本物の avcC で映像が出る。
    assertValidTS capture.Content
    Assert.NotEmpty(videoPesHeaders capture.Content)
