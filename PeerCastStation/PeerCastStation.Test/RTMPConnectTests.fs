// SPDX-License-Identifier: GPL-3.0-or-later
// (C) 2026 Philmist
module RTMPConnectTests

open System.Collections.Generic
open PeerCastStation.FLV.AMF
open PeerCastStation.FLV.RTMP
open Xunit

// StrictArray の AMFValue を文字列列から作る。
let private strictArray (items: string seq) =
    let values = items |> Seq.map (fun s -> AMFValue(s)) |> Seq.toArray
    AMFValue(values :> IEnumerable<AMFValue>)

// ECMAArray(キーが FourCC のマップ)の AMFValue を作る。値は適当な番号。
let private ecmaArray (items: string seq) =
    let dic = Dictionary<string, AMFValue>()
    items |> Seq.iteri (fun i s -> dic.Add(s, AMFValue(i)))
    AMFValue(dic :> IDictionary<string, AMFValue>)

// command_object を AMF0 で直列化 → 読み戻して CommandAMF0Message にする。
let private roundTripCommand (commandName: string) (commandObject: AMFValue) =
    let created = CommandMessage.Create(0, 0L, 0L, commandName, 1, commandObject, Array.empty<AMFValue>)
    let raw = RTMPMessage(created.MessageType, created.Timestamp, created.StreamId, created.Body)
    CommandAMF0Message(raw)

// connect の CommandObject 内 fourCcList(StrictArray)を AMF バイト往復後に抽出できる。
[<Fact>]
let ``connect の fourCcList(StrictArray)を抽出できる`` () =
    let co = AMFObject()
    co.Add("app", "live")
    co.Add("fourCcList", strictArray ["av01"; "hvc1"])
    let parsed = roundTripCommand "connect" (AMFValue(co))
    let list = EnhancedRTMP.ParseClientFourCcList(parsed.CommandObject) |> Seq.toArray
    Assert.Equal<string[]>([| "av01"; "hvc1" |], list)

// fourCcList が無ければ Enhanced RTMP v2 の videoFourCcInfoMap のキーを使う。
[<Fact>]
let ``fourCcList が無ければ videoFourCcInfoMap のキーを使う`` () =
    let co = AMFObject()
    co.Add("videoFourCcInfoMap", ecmaArray ["av01"; "avc1"])
    let parsed = roundTripCommand "connect" (AMFValue(co))
    let list = EnhancedRTMP.ParseClientFourCcList(parsed.CommandObject) |> Set.ofSeq
    Assert.Equal<Set<string>>(Set.ofList ["av01"; "avc1"], list)

// クライアントが何も指定しなければ全サポートコーデックを広告する。
[<Fact>]
let ``fourCcList 未指定なら全サポートコーデックを広告する`` () =
    let result = EnhancedRTMP.NegotiateVideoFourCc(Array.empty<string>)
    Assert.Equal<string[]>(EnhancedRTMP.SupportedVideoFourCc, result)

// 広告するのはクライアントとの交差で、順序はサポート一覧基準。
[<Fact>]
let ``広告コーデックはクライアントとの交差をサポート順で返す`` () =
    let result1 = EnhancedRTMP.NegotiateVideoFourCc([| "av01"; "vp09" |])
    Assert.Equal<string[]>([| "av01" |], result1)
    // クライアントの並びに依らずサポート一覧(av01, hvc1, avc1)の順で返る。
    let result2 = EnhancedRTMP.NegotiateVideoFourCc([| "hvc1"; "av01" |])
    Assert.Equal<string[]>([| "av01"; "hvc1" |], result2)

// _result 応答に載せた capsEx / fourCcList が AMF 往復で復元される。
[<Fact>]
let ``_result 応答の capsEx と fourCcList が往復で復元される`` () =
    let info = AMFObject()
    info.Add("code", "NetConnection.Connect.Success")
    info.Add("capsEx", EnhancedRTMP.CapsEx)
    info.Add("fourCcList", strictArray (EnhancedRTMP.SupportedVideoFourCc))
    let parsed = roundTripCommand "_result" (AMFValue(info))
    let co = parsed.CommandObject
    Assert.True(co.ContainsKey("capsEx"))
    Assert.Equal(float EnhancedRTMP.CapsEx, co.["capsEx"].Value :?> float)
    let list = EnhancedRTMP.ParseClientFourCcList(co) |> Seq.toArray
    Assert.Equal<string[]>(EnhancedRTMP.SupportedVideoFourCc, list)
