module PCPYellowPageClientTests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.PCP
open TestCommon

let readAndUpdateAsync reader updater state = 
    async {
        match! reader state with
        | Ok msg ->
            return! updater state msg
        | Error err  ->
            return Error err
    }

let doUpdateAsync updater state = 
    async { return! updater state () }

let readAndUpdateUntil reader breaker updater state = 
    let rec readAndUpdateUntilInternal state = 
        async {
            match! reader state with
            | Ok msg ->
                match breaker state msg with
                | Some result ->
                    return result
                | None ->
                    match! updater state msg with
                    | Ok state ->
                        return! readAndUpdateUntilInternal state
                    | Error _ as result ->
                        return result
            | Error err ->
                return Error err
        }
    readAndUpdateUntilInternal state

let updateSerialAsync initial updaters =
    let rec updateSerialAsyncInternal updaters result =
        async {
            match result with
            | Ok state ->
                match updaters with
                | [] ->
                    return result
                | updater :: cons ->
                    let! result = updater state
                    return! updateSerialAsyncInternal cons result
            | Error _ ->
                return result
        }
    Ok initial
    |> updateSerialAsyncInternal updaters

module PCPYellowPageClientTest =
    type ClientInfo =
        {
            pcpVersion: int
            sessionID: Guid
            bcid: Guid
            agent: string
            remotePort: int option
            remoteEndPoint: EndPoint option
        }
        static member Default = 
            { pcpVersion=0; sessionID=Guid.Empty; bcid=Guid.Empty; agent=""; remotePort=None; remoteEndPoint=None }

    type HostInfo =
        {
            channelID: Guid
            sessionID: Guid
            endPoints: IPEndPoint array
            numListeners: int
            numRelays: int
            version: int
            flags1: PCPHostFlags1
        }

    type ClientState = {
        serverInfo: ClientInfo
        clientInfo: ClientInfo
        channelInfo: IAtomCollection option
        channelTrack: IAtomCollection option
        channelHosts: Map<Guid * Guid, HostInfo>
        quitCode: int option
    }

    type GivenClientState = {
        request: PeerCastServer.HTTPRequest option
    }

    module ClientState =
        let updateClientInfoByHelo state (endpoint, version, sessionID, bcid, agent, port) =
            { state with clientInfo={pcpVersion=version; sessionID=sessionID; bcid=bcid; agent=agent; remotePort=port; remoteEndPoint=Some endpoint} }

        let updateChannelInfo state (info, track) =
            let info = info |> Option.orElse state.channelInfo
            let track = track |> Option.orElse state.channelTrack
            { state with channelInfo=info; channelTrack=track }

        let updateChannelHost state hostInfo =
            let key = (hostInfo.channelID, hostInfo.sessionID)
            { state with channelHosts=Map.add key hostInfo state.channelHosts }


    let readAtomAsync (client, state) =
        async {
            let! result = PeerCastServer.PeerCastClient.readAtomAsync client
            return result
        }

    let readHTTPRequestAsync (client, state) =
        async {
            let! result = PeerCastServer.PeerCastClient.readHTTPRequestAsync client
            match result with
            | Ok msg ->
                sprintf "Received request: %s" (string msg)
                |> System.Diagnostics.Debug.WriteLine
            | Error msg ->
                sprintf "Receiving http request failed: %s" msg
                |> System.Diagnostics.Debug.WriteLine
            return result
        }

    let endpoint = allocateEndPoint IPAddress.Loopback
    let serverInfo = {
        pcpVersion=PCPVersion.Default.ServantVersion
        sessionID=Guid.NewGuid()
        bcid=Guid.Empty
        agent="TestYellowPage/1.0.0"
        remotePort=Some endpoint.Port
        remoteEndPoint=Some endpoint
    }

    let expectAtomName name result (req:Atom) =
        async {
            if req.Name = name then
                return Ok result
            else
                return Error $"Atom {name} expected but {req.Name}"
        }

    let simpleUpdateAsync updater (client, state) value =
        async { return (client, updater state value) |> Ok }

    let expectHelo updater (client, state) (req:Atom) =
        async {
            if req.Name = Atom.PCP_HELO then
                match (
                    Atom.getHeloVersion req.Children,
                    Atom.getHeloSessionID req.Children,
                    Atom.getHeloBCID req.Children,
                    Atom.getHeloAgent req.Children,
                    Atom.getHeloPort req.Children,
                    Atom.getHeloPing req.Children
                ) with
                | (Some version, Some sessionID, Some bcid, Some agent, port, ping) ->
                    //TODO: pingを処理する
                    do!
                        AtomCollection()
                        |> Atom.setHeloVersion serverInfo.pcpVersion
                        |> Atom.setHeloAgent serverInfo.agent
                        |> Atom.setHeloSessionID serverInfo.sessionID
                        |> Atom.setHeloPortOptional serverInfo.remotePort
                        |> Atom.setHeloRemoteIPOptional serverInfo.remoteEndPoint
                        |> Atom.fromChildren Atom.PCP_OLEH
                        |> PeerCastServer.PeerCastClient.writeAtomAsync client
                    return! updater (client, state) (client.pipe.Socket.RemoteEndPoint, version, sessionID, bcid, agent, port)
                | (None, _, _, _, _, _) ->
                    return Error $"{Atom.PCP_HELO_VERSION} missing"
                | (Some _, None, _, _, _, _) ->
                    return Error $"{Atom.PCP_HELO_SESSIONID} missing"
                | (Some _, Some _, None, _, _, _) ->
                    return Error $"{Atom.PCP_HELO_BCID} missing"
                | (Some _, Some _, Some _, None, _, _) ->
                    return Error $"{Atom.PCP_HELO_AGENT} missing"
            else
                return Error $"Atom {Atom.PCP_HELO} expected but {req.Name}"
        }

    let expectChanInfo updater (client, state) (req:Atom) =
        async {
            if req.Name = Atom.PCP_CHAN then
                match (
                    Atom.getChanID req.Children,
                    Atom.getChanBCID req.Children,
                    Atom.getChanInfo req.Children,
                    Atom.getChanTrack req.Children
                ) with
                | (Some _, Some _, None, None) ->
                    return Error $"Either {Atom.PCP_CHAN_INFO} or {Atom.PCP_CHAN_TRACK} missing"
                | (Some channelID, Some bcid, info, track) when bcid=state.clientInfo.bcid ->
                    return! updater (client, state) (info, track)
                | (Some _, Some _, _, _) ->
                    return Error $"{Atom.PCP_CHAN_BCID} differs with client bcid"
                | (Some _, None, _, _) ->
                    return Error $"{Atom.PCP_CHAN_BCID} missing"
                | (None, _, _, _) ->
                    return Error $"{Atom.PCP_CHAN_ID} missing"
            else
                return Error $"Atom {Atom.PCP_CHAN} expected but {req.Name}"
        }

    let expectChanHost updater (client, state) (req:Atom) =
        async {
            if req.Name = Atom.PCP_HOST then
                match (
                    Atom.getHostChannelID req.Children,
                    Atom.getHostSessionID req.Children,
                    Atom.getHostIPs req.Children,
                    Atom.getHostPorts req.Children,
                    Atom.getHostNumListeners req.Children,
                    Atom.getHostNumRelays req.Children,
                    Atom.getHostVersion req.Children,
                    Atom.getHostFlags1 req.Children
                ) with
                | (Some channelID, Some sessionID, ips, ports, numListeners, numRelays, Some version, flags1) when sessionID=state.clientInfo.sessionID ->
                    let host = {
                        channelID=channelID
                        sessionID=sessionID
                        endPoints=Seq.zip ips (Seq.map int ports) |> Seq.map IPEndPoint |> Array.ofSeq
                        numListeners=numListeners |> Option.defaultValue 0
                        numRelays=numRelays |> Option.defaultValue 0
                        version=version
                        flags1=flags1 |> Option.defaultValue PCPHostFlags1.None 
                    }
                    return! updater (client, state) host
                | (Some _, Some _, _, _, _, _, Some _, _) ->
                    return Error $"{Atom.PCP_HOST_ID} differs with client SessionID"
                | (Some _, Some _, _, _, _, _, None _, _) ->
                    return Error $"{Atom.PCP_HOST_VERSION} missing"
                | (Some _, None, _, _, _, _, _, _) ->
                    return Error $"{Atom.PCP_HOST_ID} missing"
                | (None, _, _, _, _, _, _, _) ->
                    return Error $"{Atom.PCP_HOST_CHANID} missing"
            else
                return Error $"Atom {Atom.PCP_HOST} expected but {req.Name}"
        }

    let handleAtoms handler state atoms =
        let handleAtom prev atom =
            async {
                match! prev with
                | Ok state ->
                    return! handler state atom
                | Error _ as result ->
                    return result
            }
        Seq.fold handleAtom (async { return Ok state }) atoms

    let handleAtomOrIgnore handlers state (atom:Atom) =
        let identHandler state _ = async { return Ok state }
        let handler = Map.tryFind atom.Name handlers |> Option.defaultValue identHandler
        handler state atom

    let handleAtomOrError handlers state (atom:Atom) =
        let errorHandler _ (atom:Atom) =
            async {
                let expecteds = Map.keys handlers |> Seq.map string |> String.concat ", "
                return Error $"Atom {expecteds} expected but {atom.Name}"
            }
        let handler = Map.tryFind atom.Name handlers |> Option.defaultValue errorHandler
        handler state atom

    let expectRootBcst childHandler (client, state) (req:Atom) =
        async {
            if req.Name = Atom.PCP_BCST then
                match (
                    Atom.getBcstTTL req.Children,
                    Atom.getBcstHops req.Children,
                    Atom.getBcstFrom req.Children,
                    Atom.getBcstVersion req.Children,
                    Atom.getBcstGroup req.Children
                ) with
                | (Some 1uy, Some 0uy, Some from, Some version, Some BroadcastGroup.Root) when from=state.clientInfo.sessionID && version=PCPVersion.Default.ServantVersion ->
                    return! handleAtoms childHandler (client, state) req.Children
                | (None, _, _, _, _) ->
                    return Error $"{Atom.PCP_BCST_TTL} missing"
                | (Some ttl, _, _, _, _) when ttl<>1uy ->
                    return Error $"{Atom.PCP_BCST_TTL} must be 1"
                | (Some _, None, _, _, _) ->
                    return Error $"{Atom.PCP_BCST_HOPS} missing"
                | (Some _, Some hops, _, _, _) when hops<>0uy->
                    return Error $"{Atom.PCP_BCST_HOPS} must be 0"
                | (Some _, Some _, None, _, _) ->
                    return Error $"{Atom.PCP_BCST_FROM} missing"
                | (Some _, Some _, Some from, _, _) when from<>state.clientInfo.sessionID ->
                    return Error $"{Atom.PCP_BCST_FROM} not matched with client session ID"
                | (Some _, Some _, Some _, None, _) ->
                    return Error $"{Atom.PCP_BCST_VERSION} missing"
                | (Some _, Some _, Some _, Some version, _) when version<>PCPVersion.Default.ServantVersion ->
                    return Error $"{Atom.PCP_BCST_VERSION} must be {PCPVersion.Default.ServantVersion}"
                | (Some _, Some _, Some _, Some _, None) ->
                    return Error $"{Atom.PCP_BCST_GROUP} missing"
                | (Some _, Some _, Some _, Some _, Some group) when group<>BroadcastGroup.Root ->
                    return Error $"{Atom.PCP_BCST_GROUP} must be Root"
                | (Some _, Some _, Some _, Some _, Some _) ->
                    return Error $"Any parameter of {Atom.PCP_BCST} is invalid"
            else
                return Error $"Atom {Atom.PCP_HELO} expected but {req.Name}"
        }

    let isQuit (client, state) atom =
        if Atom.getName atom = Atom.PCP_QUIT then
            Ok (client, { state with quitCode=Atom.fromQUIT atom |> Some }) |> Some
        else
            None

    let sendQuit code (client, state) msg =
        async {
            do!
                Atom.createQUIT code
                |> PeerCastServer.PeerCastClient.writeAtomAsync client
            return Ok (client, state)
        }

    let testAnnounce peca channel examine =
        let ypClient = PCPYellowPageClient(peca, "TestYelloPage", Uri $"pcp://{endpoint}", null)
        async {
            use server = PeerCastServer.start endpoint
            ypClient.Announce(channel) |> ignore
            use! client = PeerCastServer.acceptAsync server
            let handleBody =
                [
                    (Atom.PCP_BCST,
                        [
                            (Atom.PCP_CHAN, expectChanInfo (simpleUpdateAsync ClientState.updateChannelInfo))
                            (Atom.PCP_HOST, expectChanHost (simpleUpdateAsync ClientState.updateChannelHost))
                        ]
                        |> Map.ofList
                        |> handleAtomOrIgnore
                        |> expectRootBcst
                    )
                ]
                |> Map.ofList
                |> handleAtomOrError
            return!
                [
                    readAndUpdateAsync readAtomAsync (expectAtomName Atom.PCP_PCPn)
                    readAndUpdateAsync readAtomAsync (expectHelo (simpleUpdateAsync ClientState.updateClientInfoByHelo))
                    readAndUpdateAsync readAtomAsync handleBody
                    doUpdateAsync (sendQuit Atom.PCP_ERROR_QUIT)
                ]
                |> updateSerialAsync (client, { serverInfo=serverInfo; clientInfo=ClientInfo.Default; channelInfo=None; channelTrack=None; channelHosts=Map.empty; quitCode=None })
        }
        |> Async.RunSynchronously
        |> Assert.ExpectResultOk
        |> Result.iter examine
        ypClient.StopAnnounce()

    let createHostInfoProvider sessionID bcid listenerInfos relayable playable =
        {
            new IPeerCast with
                member _.AgentName = "PCPYellowPageClientTests/1.0.0"
                member _.SessionID = sessionID
                member _.BroadcastID = bcid
                member _.GetListenerInfos () = listenerInfos
                member _.NotifyRemoteAddressAndPort (remoteEndPoint, localEndPoint, notifiedAddress, notifiedPort) = ()
                member _.YellowPageFactories = System.Collections.Generic.List<IYellowPageClientFactory>()
                member _.SourceStreamFactories = System.Collections.Generic.List<ISourceStreamFactory>()
                member _.OutputStreamFactories = System.Collections.Generic.List<IOutputStreamFactory>()
                member _.IsChannelRelayable(channel, local) = relayable
                member _.IsChannelPlayable(channel, local) = playable
        }

    [<Fact>]
    let ``チャンネル掲載情報をYellowPageに送信する`` () =
        let sessionID = Guid.NewGuid()
        let bcid = Guid.NewGuid()
        let listeners = 
            [
                ListenerInfo(PortStatus.Open, IPEndPoint(IPAddress.Loopback, endpoint.Port), AccessControlInfo(OutputStreamType.All, false, AuthenticationKey.Generate()))
                ListenerInfo(PortStatus.Open, endpoint, AccessControlInfo(OutputStreamType.All, false, AuthenticationKey.Generate()))
            ]
        // 基本形
        let peca = createHostInfoProvider sessionID bcid listeners true true
        let channel =
            MockChannel.broadcastingChannel NetworkType.IPv4
            |> MockChannel.updateChannelStatus 
                (fun s ->
                    {
                        s with
                            channelInfo=createChannelInfo "hoge" "FLV" |> Some
                            channelTrack=Some ChannelTrack.empty
                            sourceStatus=SourceStreamStatus.Receiving
                            localRelays=3;
                            localDirects=1;
                            totalRelays=4;
                            totalDirects=2;
                    }
                )
        testAnnounce peca channel (fun (client, state) ->
            Assert.isSome state.channelInfo
            Assert.equal (Some "hoge") (state.channelInfo |> Option.bind Atom.getChanInfoName)
            Assert.equal (Some "FLV") (state.channelInfo |> Option.bind Atom.getChanInfoType)
            Assert.isSome state.channelTrack
            let host = Map.tryFind (channel.ChannelID, peca.SessionID) state.channelHosts
            Assert.isSome host
            let examineHost host =
                Assert.equal channel.ChannelID host.channelID
                Assert.equal peca.SessionID host.sessionID
                Assert.equal PCPVersion.Default.ServantVersion host.version
                Assert.equal 4 host.numRelays
                Assert.equal 2 host.numListeners
                Assert.hasFlag PCPHostFlags1.Tracker host.flags1
                Assert.hasNotFlag PCPHostFlags1.Firewalled host.flags1
                Assert.hasFlag PCPHostFlags1.Receiving host.flags1
            Option.iter examineHost host
        )

        // SourceStatus が Receiving でない時は Revceiving フラグが立たない
        let channel =
            channel
            |> MockChannel.updateChannelStatus (fun s -> { s with sourceStatus=SourceStreamStatus.Idle })
        testAnnounce peca channel (fun (client, state) ->
            let host = Map.find (channel.ChannelID, peca.SessionID) state.channelHosts
            Assert.hasFlag PCPHostFlags1.Tracker host.flags1
            Assert.hasNotFlag PCPHostFlags1.Firewalled host.flags1
            Assert.hasNotFlag PCPHostFlags1.Receiving host.flags1
        )

        // 対応するポートが Open になっていない場合は Firewalled がつく
        let listeners = 
            [
                ListenerInfo(PortStatus.Firewalled, IPEndPoint(IPAddress.Loopback, endpoint.Port), AccessControlInfo(OutputStreamType.All, false, AuthenticationKey.Generate()))
                ListenerInfo(PortStatus.Firewalled, endpoint, AccessControlInfo(OutputStreamType.All, false, AuthenticationKey.Generate()))
            ]
        let peca = createHostInfoProvider sessionID bcid listeners true true
        let channel =
            channel
            |> MockChannel.updateChannelStatus (fun s -> { s with sourceStatus=SourceStreamStatus.Receiving })
        testAnnounce peca channel (fun (client, state) ->
            let host = Map.find (channel.ChannelID, peca.SessionID) state.channelHosts
            Assert.hasFlag PCPHostFlags1.Tracker host.flags1
            Assert.hasFlag PCPHostFlags1.Firewalled host.flags1
            Assert.hasFlag PCPHostFlags1.Receiving host.flags1
        )

    type GIVTestResult =
        | GivenState of GivenClientState
        | YPClientState of ClientState

    [<Fact>]
    let ``PUSH要求が来た場合に指定アドレスにGIVを送信する`` () =
        // GIV を受け取るテスト用サーバー
        let clientEndPoint = allocateEndPoint IPAddress.Loopback
        let givenAsync =
            async {
                use server = PeerCastServer.start clientEndPoint
                use! client = PeerCastServer.acceptAsync server
                let expectGIVRequest (client, state) (req: PeerCastServer.HTTPRequest) =
                    async { return Ok (client, { state with request=Some req}) }
                let! result =
                    [
                        readAndUpdateAsync readHTTPRequestAsync expectGIVRequest //GIV を受け取る
                        // GIV を受けたあとは指定のチャンネルのリレーリクエストを送るのだが、ここでは GIV を受けるのだけ確認できればいいのでそのまま切ってしまう
                    ]
                    |> updateSerialAsync (client, { request=None })
                return Result.map (fun (client, state) -> GIVTestResult.GivenState state) result
            }

        let sessionID = Guid.NewGuid()
        let bcid = Guid.NewGuid()
        let listeners = 
            [
                ListenerInfo(PortStatus.Firewalled, IPEndPoint(IPAddress.Loopback, endpoint.Port), AccessControlInfo(OutputStreamType.All, false, AuthenticationKey.Generate()))
            ]
        let peca = createHostInfoProvider sessionID bcid listeners true true
        let channel =
            MockChannel.broadcastingChannel NetworkType.IPv4
            |> MockChannel.updateChannelStatus 
                (fun s ->
                    {
                        s with
                            channelInfo=createChannelInfo "hoge" "FLV" |> Some
                            channelTrack=Some ChannelTrack.empty
                            sourceStatus=SourceStreamStatus.Receiving
                            localRelays=3;
                            localDirects=1;
                            totalRelays=4;
                            totalDirects=2;
                    }
                )

        // YP サーバーとクライアント
        let ypClient = PCPYellowPageClient(peca, "TestYelloPage", Uri $"pcp://{endpoint}", null)
        let ypServerAsync =
            async {
                let sendPush channelID endPoint (client, state) () =
                    async {
                        do!
                            AtomCollection()
                            |> Atom.setPushChannelID channelID
                            |> Atom.setPushEndPoint endPoint
                            |> Atom.fromChildren Atom.PCP_PUSH
                            |> PeerCastServer.PeerCastClient.writeAtomAsync client
                        return Ok (client, state)
                    }
                use server = PeerCastServer.start endpoint
                ypClient.Announce(channel) |> ignore
                use! client = PeerCastServer.acceptAsync server
                let handleBody =
                    [
                        (Atom.PCP_BCST,
                            [
                                (Atom.PCP_CHAN, expectChanInfo (simpleUpdateAsync ClientState.updateChannelInfo))
                                (Atom.PCP_HOST, expectChanHost (simpleUpdateAsync ClientState.updateChannelHost))
                            ]
                            |> Map.ofList
                            |> handleAtomOrIgnore
                            |> expectRootBcst
                        )
                    ]
                    |> Map.ofList
                    |> handleAtomOrError
                let! result =
                    [
                        readAndUpdateAsync readAtomAsync (expectAtomName Atom.PCP_PCPn)
                        readAndUpdateAsync readAtomAsync (expectHelo (simpleUpdateAsync ClientState.updateClientInfoByHelo))
                        readAndUpdateAsync readAtomAsync handleBody
                        doUpdateAsync (sendPush channel.ChannelID clientEndPoint) //PUSH を送ってみる
                        doUpdateAsync (sendQuit Atom.PCP_ERROR_QUIT)
                    ]
                    |> updateSerialAsync (client, { serverInfo=serverInfo; clientInfo=ClientInfo.Default; channelInfo=None; channelTrack=None; channelHosts=Map.empty; quitCode=None })
                return Result.map (fun (client, state) -> GIVTestResult.YPClientState state) result
            }

        // サーバーを並列に実行する
        let results =
            Async.Parallel [ypServerAsync; givenAsync]
            |> Async.RunSynchronously

        // GIV が送られてきたことを確認する
        let examineGIVRequest result = 
            match result with
            | GIVTestResult.GivenState givenState ->
                Assert.isSome givenState.request
                let req = givenState.request |> Option.get
                Assert.equal "GIV" req.method
                let channelID = channel.ChannelID.ToString("N")
                Assert.equal $"/{channelID}" req.path
            | _ ->
                Assert.Fail("Unexpected type")

        results[0] |> Assert.ExpectResultOk |> ignore
        results[1] |> Assert.ExpectResultOk |> Result.iter examineGIVRequest
        ypClient.StopAnnounce()



