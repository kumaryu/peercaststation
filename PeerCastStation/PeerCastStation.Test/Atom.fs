module Atom

open System
open PeerCastStation.Core
open System.Net

let getName (atom:Atom) =
    atom.Name

let createQUIT (code:int) =
    Atom(Atom.PCP_QUIT, code)

let fromQUIT (atom:Atom) =
    atom.GetInt32()

let fromChildren name (collection : AtomCollection) =
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

let setHeloPortOptional value collection =
    match value with
    | Some value ->
        setHeloPort value collection
    | None ->
        collection

let setHeloRemotePort value (collection : AtomCollection) =
    collection.SetHeloRemotePort(value)
    collection

let setHeloRemoteIP value (collection : AtomCollection) =
    collection.SetHeloRemoteIP(value)
    collection

let setHeloRemoteIPOptional (value:EndPoint option) collection =
    match value with
    | Some value ->
        match value with
        | :? IPEndPoint as value ->
            setHeloRemoteIP value.Address collection
        | _ ->
            collection
    | None ->
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

let setPush value (collection : AtomCollection) =
    collection.SetPush(value)
    collection

let setPushChannelID value (collection : AtomCollection) =
    collection.SetPushChannelID(value)
    collection

let setPushIP value (collection : AtomCollection) =
    collection.SetPushIP(value)
    collection

let setPushPort value (collection : AtomCollection) =
    collection.SetPushPort(value)
    collection

let setPushEndPoint (value:IPEndPoint) (collection : AtomCollection) =
    collection.SetPushIP(value.Address)
    collection.SetPushPort(value.Port)
    collection

let setQuit value (collection : AtomCollection) =
    collection.SetQuit(value)
    collection

let getHeloPort (collection : IAtomCollection) =
    collection.GetHeloPort() |> Option.ofNullable

let getHeloPing (collection : IAtomCollection) =
    collection.GetHeloPing() |> Option.ofNullable

let getHeloSessionID (collection : IAtomCollection) =
    collection.GetHeloSessionID() |> Option.ofNullable

let getHeloBCID (collection : IAtomCollection) =
    collection.GetHeloBCID() |> Option.ofNullable

let getHeloAgent (collection : IAtomCollection) =
    collection.GetHeloAgent() |> Option.ofObj

let getHeloDisable (collection : IAtomCollection) =
    collection.GetHeloDisable() |> Option.ofNullable

let getHeloVersion (collection : IAtomCollection) =
    collection.GetHeloVersion() |> Option.ofNullable

let getHeloRemoteIP (collection : IAtomCollection) =
    collection.GetHeloRemoteIP() |> Option.ofObj

let getHeloRemotePort (collection : IAtomCollection) =
    collection.GetHeloRemotePort() |> Option.ofNullable

let getBcstChannelID (collection : IAtomCollection) =
    collection.GetBcstChannelID() |> Option.ofNullable

let getBcstGroup (collection : IAtomCollection) =
    collection.GetBcstGroup() |> Option.ofNullable

let getBcstTTL (collection : IAtomCollection) =
    collection.GetBcstTTL() |> Option.ofNullable

let getBcstHops (collection : IAtomCollection) =
    collection.GetBcstHops() |> Option.ofNullable

let getBcstFrom (collection : IAtomCollection) =
    collection.GetBcstFrom() |> Option.ofNullable

let getBcstVersion (collection : IAtomCollection) =
    collection.GetBcstVersion() |> Option.ofNullable

let getChan (collection : IAtomCollection) =
    collection.GetChan() |> Option.ofObj

let getChanByAtom (collection : IAtomCollection) =
    collection.GetChan()
    |> Option.ofObj
    |> Option.map (fun children -> Atom(Atom.PCP_CHAN,  children))

let getHost (collection : IAtomCollection) =
    collection.GetHost() |> Option.ofObj

let getHostByAtom (collection : IAtomCollection) =
    collection.GetHost()
    |> Option.ofObj
    |> Option.map (fun children -> Atom(Atom.PCP_HOST,  children))

let getHostChannelID (collection : IAtomCollection) =
    collection.GetHostChannelID() |> Option.ofNullable

let getHostClapPP (collection : IAtomCollection) =
    collection.GetHostClapPP() |> Option.ofNullable

let getHostFlags1 (collection : IAtomCollection) =
    collection.GetHostFlags1() |> Option.ofNullable

let getHostIP (collection : IAtomCollection) =
    collection.GetHostIP() |> Option.ofObj

let getHostIPs (collection : IAtomCollection) =
    collection
    |> Seq.filter (fun atom -> atom.Name=Atom.PCP_HOST_IP)
    |> Seq.map (fun atom -> atom.GetIPAddress())

let getHostNewPos (collection : IAtomCollection) =
    collection.GetHostNewPos() |> Option.ofNullable

let getHostOldPos (collection : IAtomCollection) =
    collection.GetHostOldPos() |> Option.ofNullable

let getHostNumListeners (collection : IAtomCollection) =
    collection.GetHostNumListeners() |> Option.ofNullable

let getHostNumRelays (collection : IAtomCollection) =
    collection.GetHostNumRelays() |> Option.ofNullable

let getHostPort (collection : IAtomCollection) =
    collection.GetHostPort() |> Option.ofNullable

let getHostPorts (collection : IAtomCollection) =
    collection
    |> Seq.filter (fun atom -> atom.Name=Atom.PCP_HOST_PORT)
    |> Seq.map (fun atom -> atom.GetUInt16())

let getHostSessionID (collection : IAtomCollection) =
    collection.GetHostSessionID() |> Option.ofNullable

let getHostUphostHops (collection : IAtomCollection) =
    collection.GetHostUphostHops() |> Option.ofNullable

let getHostUphostIP (collection : IAtomCollection) =
    collection.GetHostUphostIP() |> Option.ofObj

let getHostUphostPort (collection : IAtomCollection) =
    collection.GetHostUphostPort() |> Option.ofNullable

let getHostUptime (collection : IAtomCollection) =
    collection.GetHostUptime() |> Option.ofNullable

let getHostVersion (collection : IAtomCollection) =
    collection.GetHostVersion() |> Option.ofNullable

let getHostVersionVP (collection : IAtomCollection) =
    collection.GetHostVersionVP() |> Option.ofNullable

let getHostVersionEXNumber (collection : IAtomCollection) =
    collection.GetHostVersionEXNumber() |> Option.ofNullable

let getHostVersionEXPrefix (collection : IAtomCollection) =
    collection.GetHostVersionEXPrefix() |> Option.ofObj

let getChanBCID (collection : IAtomCollection) =
    collection.GetChanBCID() |> Option.ofNullable

let getChanID (collection : IAtomCollection) =
    collection.GetChanID() |> Option.ofNullable

let getChanInfo (collection : IAtomCollection) =
    collection.GetChanInfo() |> Option.ofObj

let getChanInfoBitrate (collection : IAtomCollection) =
    collection.GetChanInfoBitrate() |> Option.ofNullable

let getChanInfoPPFlags (collection : IAtomCollection) =
    collection.GetChanInfoPPFlags() |> Option.ofNullable

let getChanInfoComment (collection : IAtomCollection) =
    collection.GetChanInfoComment() |> Option.ofObj

let getChanInfoDesc (collection : IAtomCollection) =
    collection.GetChanInfoDesc() |> Option.ofObj

let getChanInfoGenre (collection : IAtomCollection) =
    collection.GetChanInfoGenre() |> Option.ofObj

let getChanInfoName (collection : IAtomCollection) =
    collection.GetChanInfoName() |> Option.ofObj

let getChanInfoType (collection : IAtomCollection) =
    collection.GetChanInfoType() |> Option.ofObj

let getChanInfoURL (collection : IAtomCollection) =
    collection.GetChanInfoURL() |> Option.ofObj

let getChanInfoStreamType (collection : IAtomCollection) =
    collection.GetChanInfoStreamType() |> Option.ofObj

let getChanInfoStreamExt (collection : IAtomCollection) =
    collection.GetChanInfoStreamExt() |> Option.ofObj

let getChanTrack (collection : IAtomCollection) =
    collection.GetChanTrack() |> Option.ofObj

let getChanTrackAlbum (collection : IAtomCollection) =
    collection.GetChanTrackAlbum() |> Option.ofObj

let getChanTrackGenre (collection : IAtomCollection) =
    collection.GetChanTrackGenre() |> Option.ofObj

let getChanTrackCreator (collection : IAtomCollection) =
    collection.GetChanTrackCreator() |> Option.ofObj

let getChanTrackTitle (collection : IAtomCollection) =
    collection.GetChanTrackTitle() |> Option.ofObj

let getChanTrackURL (collection : IAtomCollection) =
    collection.GetChanTrackURL() |> Option.ofObj



