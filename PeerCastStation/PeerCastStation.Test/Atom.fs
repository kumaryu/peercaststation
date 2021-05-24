module Atom

open System
open PeerCastStation.Core
open System.Net

let parentAtom name (collection : AtomCollection) =
    Atom(name, collection)

let setHelo value (collection : AtomCollection) =
    collection.SetHelo(value)
    collection

let setHeloAgent value (collection : AtomCollection) =
    collection.SetHeloAgent(value)
    collection

let setHeloBCID value (collection : AtomCollection) =
    collection.SetHeloBCID(value)
    collection

let setHeloDisable value (collection : AtomCollection) =
    collection.SetHeloDisable(value)
    collection

let setHeloPing value (collection : AtomCollection) =
    collection.SetHeloPing(value)
    collection

let setHeloPort value (collection : AtomCollection) =
    collection.SetHeloPort(value)
    collection

let setHeloRemotePort value (collection : AtomCollection) =
    collection.SetHeloRemotePort(value)
    collection

let setHeloRemoteIP value (collection : AtomCollection) =
    collection.SetHeloRemoteIP(value)
    collection

let setHeloSessionID value (collection : AtomCollection) =
    collection.SetHeloSessionID(value)
    collection

let setHeloVersion value (collection : AtomCollection) =
    collection.SetHeloVersion(value)
    collection

let setBcst value (collection : AtomCollection) =
    collection.SetBcst(value)
    collection

let setBcstChannelID value (collection : AtomCollection) =
    collection.SetBcstChannelID(value)
    collection

let setBcstDest value (collection : AtomCollection) =
    collection.SetBcstDest(value)
    collection

let setBcstFrom value (collection : AtomCollection) =
    collection.SetBcstFrom(value)
    collection

let setBcstGroup value (collection : AtomCollection) =
    collection.SetBcstGroup(value)
    collection

let setBcstHops value (collection : AtomCollection) =
    collection.SetBcstHops(value)
    collection

let setBcstTTL value (collection : AtomCollection) =
    collection.SetBcstTTL(value)
    collection

let setBcstVersion value (collection : AtomCollection) =
    collection.SetBcstVersion(value)
    collection

let setBcstVersionVP value (collection : AtomCollection) =
    collection.SetBcstVersionVP(value)
    collection

let setBcstVersionEXNumber value (collection : AtomCollection) =
    collection.SetBcstVersionEXNumber(value)
    collection

let setBcstVersionEXPrefix value (collection : AtomCollection) =
    collection.SetBcstVersionEXPrefix(value)
    collection

let setChan value (collection : AtomCollection) =
    collection.SetChan(value)
    collection

let setChanBCID value (collection : AtomCollection) =
    collection.SetChanBCID(value)
    collection

let setChanID value (collection : AtomCollection) =
    collection.SetChanID(value)
    collection

let setChanInfo value (collection : AtomCollection) =
    collection.SetChanInfo(value)
    collection

let setChanInfoBitrate value (collection : AtomCollection) =
    collection.SetChanInfoBitrate(value)
    collection

let setChanInfoPPFlags value (collection : AtomCollection) =
    collection.SetChanInfoPPFlags(value)
    collection

let setChanInfoComment value (collection : AtomCollection) =
    collection.SetChanInfoComment(value)
    collection

let setChanInfoDesc value (collection : AtomCollection) =
    collection.SetChanInfoDesc(value)
    collection

let setChanInfoGenre value (collection : AtomCollection) =
    collection.SetChanInfoGenre(value)
    collection

let setChanInfoName value (collection : AtomCollection) =
    collection.SetChanInfoName(value)
    collection

let setChanInfoType value (collection : AtomCollection) =
    collection.SetChanInfoType(value)
    collection

let setChanInfoURL value (collection : AtomCollection) =
    collection.SetChanInfoURL(value)
    collection

let setChanInfoStreamType value (collection : AtomCollection) =
    collection.SetChanInfoStreamType(value)
    collection

let setChanInfoStreamExt value (collection : AtomCollection) =
    collection.SetChanInfoStreamExt(value)
    collection

let setChanPkt value (collection : AtomCollection) =
    collection.SetChanPkt(value)
    collection

let setChanPktDataMemory (value : ReadOnlyMemory<byte>) (collection : AtomCollection) =
    collection.SetChanPktData(value)
    collection

let setChanPktDataArray (value : byte array) (collection : AtomCollection) =
    collection.SetChanPktData(value)
    collection

let setChanPktCont value (collection : AtomCollection) =
    collection.SetChanPktCont(value)
    collection

let setChanPktPos value (collection : AtomCollection) =
    collection.SetChanPktPos(value)
    collection

let setChanPktType value (collection : AtomCollection) =
    collection.SetChanPktType(value)
    collection

let setChanTrack value (collection : AtomCollection) =
    collection.SetChanTrack(value)
    collection

let setChanTrackAlbum value (collection : AtomCollection) =
    collection.SetChanTrackAlbum(value)
    collection

let setChanTrackGenre value (collection : AtomCollection) =
    collection.SetChanTrackGenre(value)
    collection

let setChanTrackCreator value (collection : AtomCollection) =
    collection.SetChanTrackCreator(value)
    collection

let setChanTrackTitle value (collection : AtomCollection) =
    collection.SetChanTrackTitle(value)
    collection

let setChanTrackURL value (collection : AtomCollection) =
    collection.SetChanTrackURL(value)
    collection

let setHost value (collection : AtomCollection) =
    collection.SetHost(value)
    collection

let setHostChannelID value (collection : AtomCollection) =
    collection.SetHostChannelID(value)
    collection

let setHostClapPP value (collection : AtomCollection) =
    collection.SetHostClapPP(value)
    collection

let setHostFlags1 value (collection : AtomCollection) =
    collection.SetHostFlags1(value)
    collection

let setHostIP value (collection : AtomCollection) =
    collection.SetHostIP(value)
    collection

let addHostIP value (collection : AtomCollection) =
    collection.AddHostIP(value)
    collection

let setHostNewPos value (collection : AtomCollection) =
    collection.SetHostNewPos(value)
    collection

let setHostOldPos value (collection : AtomCollection) =
    collection.SetHostOldPos(value)
    collection

let setHostNumListeners value (collection : AtomCollection) =
    collection.SetHostNumListeners(value)
    collection

let setHostNumRelays value (collection : AtomCollection) =
    collection.SetHostNumRelays(value)
    collection

let setHostPort value (collection : AtomCollection) =
    collection.SetHostPort(value)
    collection

let addHostPort value (collection : AtomCollection) =
    collection.AddHostPort(value)
    collection

let setHostSessionID value (collection : AtomCollection) =
    collection.SetHostSessionID(value)
    collection

let setHostUphostHops value (collection : AtomCollection) =
    collection.SetHostUphostHops(value)
    collection

let setHostUphostIP value (collection : AtomCollection) =
    collection.SetHostUphostIP(value)
    collection

let setHostUphostPort value (collection : AtomCollection) =
    collection.SetHostUphostPort(value)
    collection

let setHostUptime value (collection : AtomCollection) =
    collection.SetHostUptime(value)
    collection

let setHostVersion value (collection : AtomCollection) =
    collection.SetHostVersion(value)
    collection

let setHostVersionVP value (collection : AtomCollection) =
    collection.SetHostVersionVP(value)
    collection

let setHostVersionEXNumber value (collection : AtomCollection) =
    collection.SetHostVersionEXNumber(value)
    collection

let setHostVersionEXPrefix value (collection : AtomCollection) =
    collection.SetHostVersionEXPrefix(value)
    collection

let setOk value (collection : AtomCollection) =
    collection.SetOk(value)
    collection

let setOleh value (collection : AtomCollection) =
    collection.SetOleh(value)
    collection

let setQuit value (collection : AtomCollection) =
    collection.SetQuit(value)
    collection

