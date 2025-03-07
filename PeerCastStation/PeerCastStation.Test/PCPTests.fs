﻿module PCPTests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.PCP
open TestCommon
open System.Net.Http
open System.Threading.Tasks

let registerPCPRelay (host:PeerCastStation.Core.Http.OwinHost) =
    host.Register(fun builder -> PCPRelayOwinApp.BuildApp(builder))

module Stream =
    let encoding = System.Text.UTF8Encoding(false)
    let eol = [| '\r'B; '\n'B |]
    let puts text (stream:System.IO.Stream) =
        let bytes = encoding.GetBytes(string text)
        stream.Write(bytes, 0, bytes.Length)
        stream.Write(eol, 0, eol.Length)

    let putsEmpty (stream:System.IO.Stream) =
        stream.Write(eol, 0, eol.Length)

    let gets (stream:System.IO.Stream) =
        let rec getc last lst =
            match (last,stream.ReadByte()) with
            | (last,-1) ->
                last :: lst
            | ('\r'B, 10) -> //CrLf
                lst
            | (last,b) ->
                getc (byte b) (last :: lst)
        let result =
            System.Text.StringBuilder()
            |> (List.foldBack (fun b s -> s.Append(char b)) <| match stream.ReadByte() with | -1 -> [] | b -> getc (byte b) [])
        result.ToString()

module HTTPHandler =
    open System.Net.Sockets
    type Response =
        {
            protocol: string;
            reasonPhrase: string;
            status: int;
            headers: Map<string,string>;
            stream: System.IO.Stream;
        }

    type Request =
        {
            method: string;
            pathAndQuery: string;
            protocol: string;
            headers: Map<string,string>;
            stream: System.IO.Stream;
        }

    let connect endpoint =
        let client = new TcpClient()
        client.Connect(endpoint)
        client

    let listen (endpoint:IPEndPoint) =
        let server = new TcpListener(endpoint)
        server.Start()
        async {
            let! client = server.AcceptTcpClientAsync() |> Async.AwaitTask
            server.Stop()
            return client
        }

    let sendGetRequest path headers (client:TcpClient) =
        let stream = client.GetStream()
        Stream.puts (sprintf "GET %s HTTP/1.1" path) stream
        Map.iter (fun k v -> Stream.puts (sprintf "%s:%s" k v) stream) headers
        Stream.putsEmpty stream

    let sendResponse status headers (client:TcpClient) =
        let stream = client.GetStream()
        Stream.puts (sprintf "HTTP/1.1 %d %s" (int status) (string status)) stream
        Map.iter (fun k v -> Stream.puts (sprintf "%s:%s" k v) stream) headers
        Stream.putsEmpty stream

    let (|ParseResponseLine|_|) str =
        let md = System.Text.RegularExpressions.Regex(@"(HTTP/1.\d) (\d+) (.+)").Match(str)
        if md.Success then
            Some (md.Groups.[1].Value.Trim(), md.Groups.[2].Value.Trim() |> int, md.Groups.[3].Value.Trim())
        else
            None

    let (|ParseHeader|_|) str =
        let md = System.Text.RegularExpressions.Regex(@"(.+):(.+)").Match(str)
        if md.Success then
            Some (md.Groups.[1].Value.Trim(), md.Groups.[2].Value.Trim())
        else
            None

    let rec recvHeaders stream headers =
        match Stream.gets stream with
        | "" ->
            headers
        | ParseHeader (k,v) ->
            headers
            |> Map.add k v
            |> recvHeaders stream
        | _ ->
            headers

    let recvResponse (client:TcpClient) =
        let stream = client.GetStream()
        match Stream.gets stream with
        | ParseResponseLine (protocol, status, reason) ->
            {
                protocol=protocol;
                reasonPhrase=reason;
                status=status;
                headers=recvHeaders stream Map.empty;
                stream=stream
            }
            |> Ok
        | _ ->
            Error "response Error"

    let (|ParseRequestLine|_|) str =
        let md = System.Text.RegularExpressions.Regex(@"(GET|HEAD|OPTION|POST|PUT|DELETE) (\S+) (HTTP/1.\d)").Match(str)
        if md.Success then
            Some (md.Groups.[1].Value.Trim(), md.Groups.[2].Value.Trim(), md.Groups.[3].Value.Trim())
        else
            None

    let recvRequest (client:TcpClient) =
        let stream = client.GetStream()
        match Stream.gets stream with
        | ParseRequestLine (method, pathAndQuery, protocol) ->
            {
                protocol=protocol;
                method=method;
                pathAndQuery=pathAndQuery;
                headers=recvHeaders stream Map.empty;
                stream=stream
            }
            |> Ok
        | _ ->
            Error "request Error"

type RelayClientConnection =
    {
        endpoint : IPEndPoint;
        channelId : Guid;
        connection : System.Net.Sockets.TcpClient;
        response: HTTPHandler.Response;
        localEndpoint : IPEndPoint;
    }
    interface IDisposable with
        member this.Dispose() =
            this.connection.Dispose()

module RelayClientConnection =
    let connect endpoint (channelId: Guid) =
        let client = HTTPHandler.connect endpoint
        let headers =
            [
                ("x-peercast-pcp", "1")
                ("Host", endpoint.Address.ToString())
            ]
            |> Map.ofList
        HTTPHandler.sendGetRequest (sprintf "/channel/%s" <| channelId.ToString("N")) headers client
        match HTTPHandler.recvResponse client with
        | Ok rsp ->
            { endpoint=endpoint; channelId=channelId; connection=client; response=rsp; localEndpoint=client.Client.LocalEndPoint :?> IPEndPoint }
        | Error err ->
            failwith err

    let sendHelo connection =
        AtomCollection()
        |> Atom.setHeloAgent "TestClient"
        |> Atom.setHeloSessionID (Guid.NewGuid())
        |> Atom.setHeloVersion 1218
        |> Atom.parentAtom Atom.PCP_HELO
        |> connection.response.stream.Write

    let recvAtom connection =
        connection.response.stream.ReadAtom()

    let sendAtom (value:Atom) connection =
        connection.response.stream.Write(value)

module PCPPongServer =
    let (|ParseAtom|_|) name (atom : Atom) =
        if atom.Name = name then
            Some ()
        else
            None

    let listen port =
        async {
            let listener = System.Net.Sockets.TcpListener.Create(port)
            listener.Start()
            use! onCancel = Async.OnCancel (fun () -> listener.Stop())
            let! ct = Async.CancellationToken
            try
                try
                    use! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
                    let strm = client.GetStream()
                    let rec readAndProcessAtom () =
                        async {
                            let! atom = strm.ReadAtomAsync(ct) |> Async.AwaitTask
                            match atom with
                            | ParseAtom Atom.PCP_HELO ->
                                let sessionId = Guid.NewGuid()
                                let remoteSessionId = atom.Children.GetHeloSessionID()
                                let oleh = AtomCollection()
                                oleh.SetHeloSessionID(sessionId)
                                do!
                                    strm.WriteAsync(Atom(Atom.PCP_OLEH, oleh)) |> Async.AwaitTask
                                let stopReason =
                                    if remoteSessionId.HasValue then
                                        StopReason.None
                                    else
                                        StopReason.NotIdentifiedError
                                do!
                                    strm.WriteAsync(Atom(Atom.PCP_QUIT, int stopReason)) |> Async.AwaitTask
                                return Some ()
                            | ParseAtom Atom.PCP_QUIT ->
                                return None
                            | _ ->
                                return! readAndProcessAtom ()
                        }
                    return! readAndProcessAtom()
                with
                | :? ObjectDisposedException
                | :? System.IO.IOException
                | :? OperationCanceledException ->
                    return None
            finally
                listener.Stop()
        }

module RelaySinkTests =
    let endpoint = allocateEndPoint IPAddress.Loopback

    let rec recvMetadata connection (metadata: {| hosts: Atom list; chans: Atom list |}) =
        let atom = RelayClientConnection.recvAtom connection
        if atom.Name = Atom.PCP_HOST then
            {| metadata with hosts=(atom :: metadata.hosts) |}
            |> recvMetadata connection
        elif atom.Name = Atom.PCP_CHAN then
            recvMetadata connection {| metadata with chans=(atom :: metadata.chans) |}
        else
            metadata, atom

    let rec recvHost connection hosts =
        let atom = RelayClientConnection.recvAtom connection
        if atom.Name = Atom.PCP_HOST then
            recvHost connection (atom :: hosts)
        else
            hosts, atom

    let rec waitForOutputStream (channel:Channel) =
        match Seq.tryHead channel.OutputStreams with
        | Some os ->
            os
        | None ->
            Threading.Thread.Sleep(100)
            waitForOutputStream channel
    
    let expectRelay503 (channel:Channel) =
        use connection = RelayClientConnection.connect endpoint channel.ChannelID
        Assert.Equal(503, connection.response.status)
        RelayClientConnection.sendHelo connection
        let oleh = RelayClientConnection.recvAtom connection
        Assert.Equal(Atom.PCP_OLEH, oleh.Name)
        let metadata, last = recvMetadata connection {| hosts=[]; chans=[] |}
        Assert.ExpectAtomName Atom.PCP_QUIT last
        metadata.hosts

    let expectRelay200 (channel:Channel) stopReason =
        use connection = RelayClientConnection.connect endpoint channel.ChannelID
        Assert.Equal(200, connection.response.status)
        RelayClientConnection.sendHelo connection
        RelayClientConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OLEH
        RelayClientConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OK
        let os = waitForOutputStream channel
        os.OnStopped(stopReason)
        let metadata, last = recvMetadata connection {| hosts=[]; chans=[] |}
        Assert.ExpectAtomName Atom.PCP_QUIT last
        metadata.hosts

    [<Fact>]
    let ``チャンネルIDを渡さないとエラーが返る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let getRequest404 (path, expected:HttpStatusCode) =
            sprintf "http://%s/%s" (endpoint.ToString()) path
            |> HttpClient.get
            |> Assert.statusCode expected
        [
            ("channel/", HttpStatusCode.Forbidden);
            ("channel/hoge", HttpStatusCode.NotFound);
        ]
        |> List.iter getRequest404

    [<Fact>]
    let ``無いチャンネルIDを指定すると404が返る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        peca.AddChannel channel
        sprintf "http://%s/channel/%s" (endpoint.ToString()) (Guid.NewGuid().ToString("N"))
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.NotFound

    [<Fact>]
    let ``受信状態でないチャンネルを指定すると404が返る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        peca.AddChannel channel
        sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.NotFound

    [<Fact>]
    let ``PCPバージョンが指定されていないと400が返る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Start(null)
        peca.AddChannel channel
        sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.BadRequest

    [<Fact>]
    let ``ネットワークが違うチャンネルを指定すると404が返る`` () =
        let testNetwork (endpoint, network, pcpver) = 
            use peca = pecaWithOwinHost endpoint registerPCPRelay
            let channel = DummyBroadcastChannel(peca, network, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
            channel.Start(null)
            peca.AddChannel channel
            sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
            |> HttpClient.getWithHeader ["x-peercast-pcp", pcpver]
            |> Assert.statusCode HttpStatusCode.NotFound
        [
            (allocateEndPoint IPAddress.Loopback, NetworkType.IPv6, "1")
            (allocateEndPoint IPAddress.Loopback, NetworkType.IPv6, "100")
            (allocateEndPoint IPAddress.Loopback, NetworkType.IPv4, "100")
            if TestCommon.isIPv6Supported then
                (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv4, "1")
                (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv4, "100")
                (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv6, "1")
        ]
        |> List.iter testNetwork

    [<Fact>]
    let ``503の時は他のリレー候補を返して切る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Relayable <- false
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        let hosts = expectRelay503 channel
        Assert.Equal(32, channel.Nodes.Count)
        Assert.Equal(8, List.length hosts)

    [<Fact>]
    let ``ブロードキャストされてきたホスト情報を保持する`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Relayable <- true
        channel.Start(null)
        peca.AddChannel channel
        use connection = RelayClientConnection.connect endpoint (channel.ChannelID)
        Assert.Equal(200, connection.response.status)
        RelayClientConnection.sendHelo connection
        RelayClientConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OLEH
        RelayClientConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OK
        let createHostInfo i =
            let host = AtomCollection()
            host.SetHostChannelID(channel.ChannelID)
            host.SetHostSessionID(Guid.NewGuid())
            host.SetHostNumListeners(1+i)
            host.SetHostNumRelays(1+i/2)
            host.SetHostUptime(TimeSpan.FromMinutes(42.0))
            PCPVersion.SetHostVersion(host)
            host.SetHostFlags1(PCPHostFlags1.Relay ||| PCPHostFlags1.Direct ||| PCPHostFlags1.Receiving)
            host.SetHostUphostIP((connection.connection.Client.RemoteEndPoint :?> IPEndPoint).Address)
            host.SetHostUphostPort((connection.connection.Client.RemoteEndPoint :?> IPEndPoint).Port)
            Atom(Atom.PCP_HOST, host)
        let sendBcst host =
            let bcst = AtomCollection()
            bcst.SetBcstFrom(peca.SessionID)
            bcst.SetBcstGroup(BroadcastGroup.Trackers)
            bcst.SetBcstHops(0uy)
            bcst.SetBcstTTL(11uy)
            PCPVersion.SetBcstVersion(bcst)
            bcst.SetBcstChannelID(channel.ChannelID)
            bcst.Add(host)
            RelayClientConnection.sendAtom (Atom(Atom.PCP_BCST, bcst)) connection
        Seq.init 32 createHostInfo
        |> Seq.iter sendBcst
        TestCommon.waitForConditionOrTimeout (fun () -> 32 <= channel.Nodes.Count) 10000
        Assert.Equal(32, channel.Nodes.Count)

    [<Fact>]
    let ``チャンネルがUnavailableで終了した時には他のリレー候補を返して切る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        let hosts = expectRelay200 channel StopReason.UnavailableError
        Assert.Equal(32, channel.Nodes.Count)
        Assert.Equal(8, List.length hosts)

    [<Fact>]
    let ``チャンネルがUserShutdownで終了した時には上のノードをリレー候補として返して切る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyRelayChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        let hosts = expectRelay200 channel StopReason.UserShutdown
        Assert.Equal(32, channel.Nodes.Count)
        Assert.Equal(1, List.length hosts)

    [<Fact>]
    let ``チャンネルがその他のコードで終了した時にはリレー候補を送らずに切る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyRelayChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        [
            StopReason.OffAir
            StopReason.Any
            StopReason.ConnectionError
            StopReason.NoHost
        ]
        |> List.iter (fun status ->
            let hosts = expectRelay200 channel status
            Assert.Equal(32, channel.Nodes.Count)
            Assert.Equal(0, List.length hosts)
        )

    [<Fact>]
    let ``チャンネルがUnavailableで終了した時に接続していたIPアドレスが一定時間Banされる`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        expectRelay200 channel StopReason.UnavailableError |> ignore
        Assert.True(channel.HasBanned("127.0.0.1"))

    [<Fact>]
    let ``BanされてるIPアドレスから接続すると一定時間503が返る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Start(null)
        channel.Ban("127.0.0.1", DateTimeOffset.Now.AddMilliseconds(1000.0))
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        expectRelay503 channel |> ignore
        Assert.True(channel.HasBanned("127.0.0.1"))
        Threading.Thread.Sleep(1000)
        expectRelay200 channel StopReason.UserShutdown |> ignore
        Assert.False(channel.HasBanned("127.0.0.1"))

    [<Fact>]
    let ``PINGが設定されているとポートチェックをする`` () =
        let pongEndPoint = allocateEndPoint IPAddress.Loopback
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
        channel.Relayable <- true
        channel.Start(null)
        peca.AddChannel channel
        use connection = RelayClientConnection.connect endpoint (channel.ChannelID)
        Assert.Equal(200, connection.response.status)
        async {
            let! pong = Async.StartChild(PCPPongServer.listen pongEndPoint.Port, 10000)
            let test = async {
                AtomCollection()
                |> Atom.setHeloAgent "TestClient"
                |> Atom.setHeloSessionID (Guid.NewGuid())
                |> Atom.setHeloVersion 1218
                |> Atom.setHeloPing pongEndPoint.Port
                |> Atom.setHeloPort pongEndPoint.Port
                |> Atom.parentAtom Atom.PCP_HELO
                |> connection.response.stream.Write
                RelayClientConnection.sendHelo connection
                RelayClientConnection.recvAtom connection
                |> Assert.ExpectAtomName Atom.PCP_OLEH
                RelayClientConnection.recvAtom connection
                |> Assert.ExpectAtomName Atom.PCP_OK
                return Some ()
            }
            return! Async.Parallel([ pong; test ])
        }
        |> Async.RunSynchronously
        |> Seq.forall Option.isSome
        |> Assert.True

type RelayServerConnection =
    {
        endpoint : IPEndPoint;
        channelId : Guid;
        connection : System.Net.Sockets.TcpClient;
        request: HTTPHandler.Request;
        localEndpoint : IPEndPoint;
    }
    interface IDisposable with
        member this.Dispose() =
            this.connection.Dispose()

module RelayServerConnection =
    let (|ParseChannelPath|_|) str =
        let md = System.Text.RegularExpressions.Regex(@"^/channel/([A-Fa-f0-9]{32})[./?]?").Match(str)
        if md.Success then
            Some (Guid.Parse(md.Groups.[1].Value.Trim()))
        else
            None
        
    let listen endpoint =
        async {
            let! client = HTTPHandler.listen endpoint
            match HTTPHandler.recvRequest client with
            | Ok req ->
                match req.pathAndQuery with
                | ParseChannelPath channelId ->
                    return Ok {
                        endpoint = client.Client.RemoteEndPoint :?> IPEndPoint
                        channelId = channelId
                        connection = client
                        request = req
                        localEndpoint = endpoint
                    }
                | _ ->
                    HTTPHandler.sendResponse HttpStatusCode.NotFound Map.empty client
                    return Error "No channel requested"
            | Error err ->
                return Error "No channel requested"
        }

    let sendResponse status connection =
        HTTPHandler.sendResponse status Map.empty connection.connection

    let recvAtom connection =
        connection.request.stream.ReadAtom()

    let sendAtom (value:Atom) connection =
        connection.request.stream.Write(value)

module RelaySourceTests =
    let endpoint = allocateEndPoint IPAddress.Loopback

    let expectHelo connection =
        let helo = RelayServerConnection.recvAtom connection
        Assert.ExpectAtomName Atom.PCP_HELO helo
        Assert.NotNull(helo.Children.GetHeloSessionID())
        Assert.NotNull(helo.Children.GetHeloAgent())
        Assert.NotNull(helo.Children.GetHeloVersion())

    let sendOleh sessionId connection =
        let oleh = AtomCollection()
        oleh.SetHeloSessionID(sessionId)
        RelayServerConnection.sendAtom (Atom(Atom.PCP_OLEH, oleh)) connection

    let sendChanInfo (chanInfo:ChannelInfo) connection =
        let chan = AtomCollection()
        chan.SetChanInfo(chanInfo.Extra)
        RelayServerConnection.sendAtom (Atom(Atom.PCP_CHAN, chan)) connection

    let sendQuit connection =
        RelayServerConnection.sendAtom (Atom(Atom.PCP_QUIT, Atom.PCP_ERROR_QUIT)) connection

    [<Fact>]
    let ``普通に接続したらデータ受信を開始する`` () =
        use peca = new PeerCast()
        let factory = new PCPSourceStreamFactory(peca)
        Assert.True(factory.PCPHandshakeTimeout > 15000)
        peca.SourceStreamFactories.Add(factory)
        let sessionId = Guid.NewGuid()
        let channelId = Guid.NewGuid()
        let channel = RelayChannel(peca, NetworkType.IPv4, channelId)
        let client = RelayServerConnection.listen endpoint
        channel.Start(sprintf "pcp://%s:%d/channel/%s" (endpoint.Address.ToString()) endpoint.Port (channelId.ToString("N")) |> Uri)
        peca.AddChannel channel
        match Async.RunSynchronously client with
        | Ok conn ->
            use conn = conn
            Assert.Equal(channelId, conn.channelId)
            RelayServerConnection.sendResponse HttpStatusCode.OK conn
            expectHelo conn
            sendOleh sessionId conn
            sendChanInfo (createChannelInfo "hoge" "FLV") conn
            Threading.Thread.Sleep(1000)
            Assert.Equal("hoge", channel.ChannelInfo.Name)
            Assert.Equal("FLV", channel.ChannelInfo.ContentType)
            sendQuit conn
        | Error err ->
            failwith err

    [<Fact>]
    let ``一定時間内にレスポンスを返さないとタイムアウトして接続を切る`` () =
        use peca = new PeerCast()
        let factory = new PCPSourceStreamFactory(peca)
        factory.PCPHandshakeTimeout <- 2000
        peca.SourceStreamFactories.Add(factory)
        let channelId = Guid.NewGuid()
        let channel = RelayChannel(peca, NetworkType.IPv4, channelId)
        let client = RelayServerConnection.listen endpoint
        channel.Start(sprintf "pcp://%s:%d/channel/%s" (endpoint.Address.ToString()) endpoint.Port (channelId.ToString("N")) |> Uri)
        peca.AddChannel channel
        match Async.RunSynchronously client with
        | Ok conn ->
            use conn = conn
            Assert.Equal(channelId, conn.channelId)
            Threading.Thread.Sleep(3000)
            RelayServerConnection.sendResponse HttpStatusCode.OK conn
            Assert.ThrowsAny<System.IO.IOException>(fun () ->
                RelayServerConnection.recvAtom conn |> ignore
            )
        | Error err ->
            failwith err

    [<Fact>]
    let ``一定時間内にOLEHを返さないとタイムアウトして接続を切る`` () =
        use peca = new PeerCast()
        let factory = new PCPSourceStreamFactory(peca)
        factory.PCPHandshakeTimeout <- 2000
        peca.SourceStreamFactories.Add(factory)
        let sessionId = Guid.NewGuid()
        let channelId = Guid.NewGuid()
        let channel = RelayChannel(peca, NetworkType.IPv4, channelId)
        let client = RelayServerConnection.listen endpoint
        channel.Start(sprintf "pcp://%s:%d/channel/%s" (endpoint.Address.ToString()) endpoint.Port (channelId.ToString("N")) |> Uri)
        peca.AddChannel channel
        match Async.RunSynchronously client with
        | Ok conn ->
            use conn = conn
            Assert.Equal(channelId, conn.channelId)
            RelayServerConnection.sendResponse HttpStatusCode.OK conn
            expectHelo conn
            Threading.Thread.Sleep(3000)
            sendOleh sessionId conn
            Assert.ThrowsAny<System.IO.IOException>(fun () ->
                RelayServerConnection.recvAtom conn |> ignore
            )
        | Error err ->
            failwith err

