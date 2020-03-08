module PCPTests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.PCP
open TestCommon

let registerPCPRelay (host:PeerCastStation.Core.Http.OwinHost) =
    host.Register(fun builder -> PCPRelayOwinApp.BuildApp(builder))

let endpoint = allocateEndPoint IPAddress.Loopback

[<Fact>]
let ``チャンネルIDを渡さないとエラーが返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let getRequest404 (path, expected) =
        sprintf "http://%s/%s" (endpoint.ToString()) path
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode expected
    [
        ("channel/", HttpStatusCode.Forbidden);
        ("channel/hoge", HttpStatusCode.NotFound);
    ]
    |> List.iter getRequest404

[<Fact>]
let ``無いチャンネルIDを指定すると404が返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
    peca.AddChannel channel
    sprintf "http://%s/channel/%s" (endpoint.ToString()) (Guid.NewGuid().ToString("N"))
    |> WebRequest.CreateHttp
    |> Assert.ExpectStatusCode HttpStatusCode.NotFound

[<Fact>]
let ``受信状態でないチャンネルを指定すると404が返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
    peca.AddChannel channel
    sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
    |> WebRequest.CreateHttp
    |> Assert.ExpectStatusCode HttpStatusCode.NotFound

[<Fact>]
let ``PCPバージョンが指定されていないと400が返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
    channel.Start(null)
    peca.AddChannel channel
    sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
    |> WebRequest.CreateHttp
    |> Assert.ExpectStatusCode HttpStatusCode.BadRequest

[<Fact>]
let ``ネットワークが違うチャンネルを指定すると404が返る`` () =
    let testNetwork (endpoint, network, pcpver) = 
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, network, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        peca.AddChannel channel
        sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> WebRequest.CreateHttp
        |> WebRequest.addHeader "x-peercast-pcp" pcpver
        |> Assert.ExpectStatusCode HttpStatusCode.NotFound
    [
        (allocateEndPoint IPAddress.Loopback, NetworkType.IPv6, "1");
        (allocateEndPoint IPAddress.Loopback, NetworkType.IPv6, "100");
        (allocateEndPoint IPAddress.Loopback, NetworkType.IPv4, "100");
        (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv4, "1");
        (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv4, "100");
        (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv6, "1");
    ]
    |> List.iter testNetwork


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

module HTTPClient =
    open System.Net.Sockets
    type Response =
        {
            protocol: string;
            reasonPhrase: string;
            status: int;
            headers: Map<string,string>;
            stream: System.IO.Stream;
        }

    let connect endpoint =
        let client = new TcpClient()
        client.Connect(endpoint)
        client

    let sendGetRequest path headers (client:TcpClient) =
        let stream = client.GetStream()
        Stream.puts (sprintf "GET %s HTTP/1.1" path) stream
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

    let recvResponse (client:TcpClient) =
        let stream = client.GetStream()
        match Stream.gets stream with
        | ParseResponseLine (protocol, status, reason) ->
            let rec recvHeaders headers =
                match Stream.gets stream with
                | "" ->
                    headers
                | ParseHeader (k,v) ->
                    headers
                    |> Map.add k v
                    |> recvHeaders
                | _ ->
                    headers
            {
                protocol=protocol;
                reasonPhrase=reason;
                status=status;
                headers=recvHeaders Map.empty;
                stream=stream
            }
            |> Ok
        | _ ->
            Error "response Error"


type PCPRelayConnection =
    {
        endpoint : IPEndPoint;
        channelId : Guid;
        connection : System.Net.Sockets.TcpClient;
        response: HTTPClient.Response;
    }
    interface IDisposable with
        member this.Dispose() =
            this.connection.Dispose()

module PCPRelayConnection =
    let connect endpoint (channelId: Guid) =
        let client = HTTPClient.connect endpoint
        let headers =
            [
                ("x-peercast-pcp", "1")
                ("Host", endpoint.Address.ToString())
            ]
            |> Map.ofList
        HTTPClient.sendGetRequest (sprintf "/channel/%s" <| channelId.ToString("N")) headers client
        match HTTPClient.recvResponse client with
        | Ok rsp ->
            { endpoint=endpoint; channelId=channelId; connection=client; response=rsp }
        | Error err ->
            failwith err

    let sendHelo connection =
        let helo = AtomCollection()
        helo.SetHeloAgent "TestClient"
        helo.SetHeloSessionID <| Guid.NewGuid()
        helo.SetHeloVersion 1218
        connection.response.stream.Write(Atom(Atom.PCP_HELO, helo))

    let recvAtom connection =
        connection.response.stream.ReadAtom()

    let sendAtom value connection =
        connection.response.stream.Write(value)

module RelayTests =
    let endpoint = allocateEndPoint IPAddress.Loopback

    [<Fact>]
    let ``503の時は他のリレー候補を返して切る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.Relayable <- false
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        use connection = PCPRelayConnection.connect endpoint (channel.ChannelID)
        Assert.Equal(503, connection.response.status)
        PCPRelayConnection.sendHelo connection
        let oleh = PCPRelayConnection.recvAtom connection
        Assert.Equal(Atom.PCP_OLEH, oleh.Name)
        let rec recvHost hosts =
            let atom = PCPRelayConnection.recvAtom connection
            if atom.Name = Atom.PCP_HOST then
                atom :: hosts
                |> recvHost
            else
                hosts, atom
        let hosts, last = recvHost []
        Assert.Equal(32, channel.Nodes.Count)
        Assert.Equal(8, List.length hosts)
        Assert.ExpectAtomName Atom.PCP_QUIT last

    [<Fact>]
    let ``ブロードキャストされてきたホスト情報を保持する`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.Relayable <- true
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        peca.AddChannel channel
        use connection = PCPRelayConnection.connect endpoint (channel.ChannelID)
        Assert.Equal(200, connection.response.status)
        PCPRelayConnection.sendHelo connection
        PCPRelayConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OLEH
        PCPRelayConnection.recvAtom connection
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
            PCPRelayConnection.sendAtom (Atom(Atom.PCP_BCST, bcst)) connection
        Seq.init 32 createHostInfo
        |> Seq.iter sendBcst
        Threading.Thread.Sleep(1000)
        Assert.Equal(32, channel.Nodes.Count)

    [<Fact>]
    let ``チャンネルがUnavailableで終了した時には他のリレー候補を返して切る`` () =
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        Seq.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
        |> Seq.iter (channel.AddNode)
        peca.AddChannel channel
        use connection = PCPRelayConnection.connect endpoint (channel.ChannelID)
        Assert.Equal(200, connection.response.status)
        PCPRelayConnection.sendHelo connection
        PCPRelayConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OLEH
        PCPRelayConnection.recvAtom connection
        |> Assert.ExpectAtomName Atom.PCP_OK
        let rec waitForOutputStream () =
            match Seq.tryHead channel.OutputStreams with
            | Some os ->
                os
            | None ->
                Threading.Thread.Sleep(100)
                waitForOutputStream()
        let os = waitForOutputStream()
        os.OnStopped(StopReason.UnavailableError)
        let rec recvHost hosts =
            let atom = PCPRelayConnection.recvAtom connection
            if atom.Name = Atom.PCP_HOST then
                atom :: hosts
                |> recvHost
            else
                hosts, atom
        let hosts, last = recvHost []
        Assert.Equal(32, channel.Nodes.Count)
        Assert.Equal(8, List.length hosts)
        Assert.ExpectAtomName Atom.PCP_QUIT last


