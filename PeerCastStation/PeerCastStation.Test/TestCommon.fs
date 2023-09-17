module TestCommon

open System
open PeerCastStation.Core
open PeerCastStation.Core.Http
open System.Net
open Xunit
open System.Net.Http
open System.Threading.Tasks
open System.Threading

[<assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)>]
do
    ()

let isIPv6Supported =
    if Sockets.Socket.OSSupportsIPv6 then
        use sock = new Sockets.Socket(Sockets.AddressFamily.InterNetworkV6, Sockets.SocketType.Stream, Sockets.ProtocolType.Tcp)
        try
            IPEndPoint(IPAddress.IPv6Loopback, 0)
            |> sock.Bind
            true
        with
        | _ ->
            false
    else
        false

let allocateEndPoint localAddr =
    let listener = System.Net.Sockets.TcpListener(localAddr, 0)
    try
        listener.Start()
        listener.LocalEndpoint :?> IPEndPoint
    finally
        listener.Stop()

let waitForConditionOrTimeout cond timeout =
    let rec waitForCond retry =
        System.Threading.Thread.Sleep 100
        if not (cond ()) && retry>0 then
            waitForCond (retry-1)
        else
            ()
    waitForCond ((timeout+99)/100)

module ChannelTrack =
    let empty = ChannelTrack(AtomCollection())

type MockSourceStreamFactory (scheme, sourceStreamType, creator) =
    interface ISourceStreamFactory with
        member this.Name = "MockContentReader"
        member this.Scheme = scheme
        member this.Type = sourceStreamType
        member this.IsContentReaderRequired = false
        member this.DefaultUri = null
        member this.Create (channel, uri) =
            creator channel uri
        member this.Create (channel, uri, content_reader) =
            NotImplementedException "not implemented" |> raise

type MockSourceStreamFactoryWithContentReader (scheme, sourceStreamType, creator) =
    interface ISourceStreamFactory with
        member this.Name = "MockContentReader"
        member this.Scheme = scheme
        member this.Type = sourceStreamType
        member this.IsContentReaderRequired = true
        member this.DefaultUri = null
        member this.Create (channel, uri) =
            NotImplementedException "not implemented" |> raise
        member this.Create (channel, uri, content_reader) =
            creator channel uri content_reader

type DummySourceStream (sstype, channel) =
    let runTask = System.Threading.Tasks.TaskCompletionSource<StopReason>()

    static member FactoryForRelay = 
        MockSourceStreamFactory("", SourceStreamType.Relay, fun channel _ -> DummySourceStream(SourceStreamType.Relay, channel))

    static member FactoryForBroadcast = 
        MockSourceStreamFactoryWithContentReader("", SourceStreamType.Broadcast, fun channel _ _ -> DummySourceStream(SourceStreamType.Broadcast, channel))

    interface ISourceStream with 
        member this.Run cancellationToken = 
            let sink = ChannelContentSink(channel, false)
            sink.OnContentHeader(Content(0, TimeSpan.Zero, 0l, ReadOnlyMemory<byte>.Empty, PCPChanPacketContinuation.None))
            cancellationToken.Register (fun () -> runTask.TrySetResult(StopReason.UserShutdown) |> ignore) |> ignore
            runTask.Task

        member this.Reconnect () = ()
        member this.Post (from, packet) = ()
        member this.Type = sstype
        member this.Status = SourceStreamStatus.Receiving
        member this.GetConnectionInfo () =
            match sstype with
            | SourceStreamType.Relay ->
                ConnectionInfo(
                    "dummy",
                    ConnectionType.Source ||| ConnectionType.Relay,
                    ConnectionStatus.Connected,
                    "dummy source",
                    IPEndPoint(IPAddress.Parse("203.0.113.1"), 7144),
                    RemoteHostStatus.Receiving,
                    Guid.NewGuid() |> Nullable,
                    Nullable 0L,
                    Nullable 0.0f,
                    Nullable 0.0f,
                    Nullable(),
                    Nullable(),
                    "PeerCastStation.Tests")
            | _ ->
                ConnectionInfo("dummy", ConnectionType.Source, ConnectionStatus.Connected, "", null, RemoteHostStatus.Local, Nullable(), Nullable 0L, Nullable 0.0f, Nullable 0.0f, Nullable(), Nullable(), "dummy")

type DummyOutputStream () =
    let connectionInfo = ConnectionInfoBuilder()
    member this.ConnectionType
        with get ()    = connectionInfo.Type
        and  set value = connectionInfo.Type <- value
    member this.LocalDirects
        with get ()    = connectionInfo.LocalDirects
        and  set value = connectionInfo.LocalDirects <- value
    member this.LocalRelays
        with get ()    = connectionInfo.LocalRelays
        and  set value = connectionInfo.LocalRelays <- value
    interface IChannelSink with
        member this.OnBroadcast(from, packet) = ()
        member this.OnStopped(reason) = ()
        member this.GetConnectionInfo() = connectionInfo.Build()


type MockContentReader (channel) =
    interface IContentReader with
        member this.Name = "MockContentReader"
        member this.Channel = channel
        member this.ReadAsync (sink, stream, cancel_token) =
            Task.CompletedTask

type MockContentReaderFactory () =
    interface IContentReaderFactory with
        member this.Name = "MockContentReader"
        member this.Create channel =
            MockContentReader channel

        member this.TryParseContentType (header, content_type, mime_type) =
            content_type <- null
            mime_type <- null
            false

type ChannelStatus =
    {
        sourceStatus: SourceStreamStatus
        uptime: TimeSpan
        isBroadCasting: bool
        localRelays: int
        localDirects: int
        totalRelays: int
        totalDirects: int
        channelInfo: ChannelInfo option
        channelTrack: ChannelTrack option
        oldestContentPosision: int64 option
        newestContentPosision: int64 option
        isRelayable: bool
        isPlayable: bool
    }
    static member Default = 
        {
            sourceStatus=SourceStreamStatus.Idle
            uptime=TimeSpan.Zero
            isBroadCasting=false
            localRelays=0
            localDirects=0
            totalRelays=0
            totalDirects=0
            channelInfo=None
            channelTrack=None
            oldestContentPosision=None
            newestContentPosision=None
            isRelayable=false
            isPlayable=false
        }

type MockChannel (network, channelStatus) =
    let channelID = Guid.NewGuid()
    let mutable monitors = List.empty<IChannelMonitor>
    let mutable channelStatus = channelStatus

    member this.ChannelID = channelID
    member this.ChannelStatus
        with get () = channelStatus
        and  set newStatus =
            let oldStatus = channelStatus
            channelStatus <- newStatus
            if Option.isNone oldStatus.newestContentPosision && Option.isSome newStatus.newestContentPosision then
                monitors |> List.iter (fun monitor -> monitor.OnContentChanged(ChannelContentType.ContentHeader))
            if oldStatus.channelInfo<>newStatus.channelInfo then
                monitors |> List.iter (fun monitor -> monitor.OnContentChanged(ChannelContentType.ChannelInfo))
            if oldStatus.channelTrack<>newStatus.channelTrack then
                monitors |> List.iter (fun monitor -> monitor.OnContentChanged(ChannelContentType.ChannelTrack))
            ()

    interface IChannel with
        member this.ChannelID = channelID
        member this.Network = network
        member this.GetChannelStatus () =
            let status = channelStatus
            ChannelStatus(
                channelID,
                network,
                status.sourceStatus,
                status.uptime,
                status.isBroadCasting,
                status.localRelays,
                status.localDirects,
                status.totalRelays,
                status.totalDirects,
                Option.toObj status.channelInfo,
                Option.toObj status.channelTrack,
                status.oldestContentPosision |> Option.toNullable,
                status.newestContentPosision |> Option.toNullable,
                status.isRelayable,
                status.isPlayable
            )

        member this.AddMonitor (monitor) =
            monitors <- monitor :: monitors
            {
                new IDisposable with
                    member this.Dispose () =
                        monitors <- List.filter ((=) monitor) monitors
            }

        member this.Broadcast (from, packet, group) =
            ()

module MockChannel =
    let relayChannel network =
        MockChannel(network, { ChannelStatus.Default with isBroadCasting=false })

    let broadcastingChannel network =
        MockChannel(network, { ChannelStatus.Default with isBroadCasting=true })

    let setChannelStatus newStatus (channel:MockChannel) =
        channel.ChannelStatus <- newStatus
        channel

    let updateChannelStatus updater (channel:MockChannel) =
        channel.ChannelStatus <- updater channel.ChannelStatus
        channel


type DummyBroadcastChannel (peercast, network, channelId, channelInfo, channelTrack) =
    inherit BroadcastChannel(peercast, network, channelId, channelInfo, channelTrack, MockContentReaderFactory())

    let mutable relayable = true

    member this.Relayable
        with get ()    = relayable
        and  set value = relayable <- value

    override this.IsRelayable(local) =
        if relayable then
            base.IsRelayable(local)
        else
            false

    override this.MakeRelayable(local) =
        if relayable then
            base.MakeRelayable(local)
        else
            false

type DummyRelayChannel (peercast, network, channelId) =
    inherit RelayChannel(peercast, network, channelId)

let createChannelInfo name contentType =
    let info = AtomCollection()
    info.SetChanInfoName name
    info.SetChanInfoType contentType
    ChannelInfo info

let createChannelInfoBitrate name contentType bitrate =
    let info = AtomCollection()
    info.SetChanInfoName name
    info.SetChanInfoType contentType
    info.SetChanInfoBitrate bitrate
    ChannelInfo info

type ChannelTrackDesc = {
    name: string option
    album: string option
    creator: string option
    genre: string option
    url: string option
}

module ChannelTrackDesc =
    let empty = { name=None; album=None; creator=None; genre=None; url=None }
    let toChannelTrack desc =
        let atoms = AtomCollection()
        if Option.isSome desc.name then
            Option.get desc.name
            |> atoms.SetChanTrackTitle
        if Option.isSome desc.album then
            Option.get desc.album
            |> atoms.SetChanTrackAlbum
        if Option.isSome desc.genre then
            Option.get desc.genre
            |> atoms.SetChanTrackGenre
        if Option.isSome desc.creator then
            Option.get desc.creator
            |> atoms.SetChanTrackCreator
        if Option.isSome desc.url then
            Option.get desc.url
            |> atoms.SetChanTrackURL
        ChannelTrack(atoms)


let registerApp path appFunc (owinHost:PeerCastStation.Core.Http.OwinHost) =
    owinHost.Register(
        fun builder ->
            let buildApp (builder:IAppBuilder) =
                builder.Run (fun (env:OwinEnvironment) ->
                    appFunc env
                    |> Async.StartAsTask
                    :> System.Threading.Tasks.Task
                )
            builder.MapGET(string path, fun builder -> buildApp builder) |> ignore
            builder.MapPOST(string path, fun builder -> buildApp builder) |> ignore
    ) |> ignore

let registerAppWithType appType path appFunc (owinHost:PeerCastStation.Core.Http.OwinHost) =
    owinHost.Register(
        fun builder ->
            builder.MapGET(
                string path,
                fun builder ->
                    builder.UseAuth appType |> ignore
                    builder.Run (fun (env:OwinEnvironment) ->
                        appFunc env
                        |> Async.StartAsTask
                        :> System.Threading.Tasks.Task
                    )
            )
            |> ignore
    ) |> ignore

module Opaque =
    open System.Collections.Generic
    open System.Threading
    open System.Threading.Tasks
    let upgrade (env:IDictionary<string,obj>) handler =
        let upgrade =
            env.["opaque.Upgrade"]
            :?> Action<IDictionary<string,obj>,Func<IDictionary<string,obj>,Task>>
        upgrade.Invoke(
            Dictionary<string,obj>(),
            fun opaqueEnv ->
                let ct =
                    opaqueEnv.["opaque.CallCancelled"]
                    :?> CancellationToken
                Async.StartAsTask(handler opaqueEnv, TaskCreationOptions.None, ct)
                :> Task
        )

    let stream (opaqueEnv:IDictionary<string,obj>) =
        opaqueEnv.["opaque.Stream"]
        :?> System.IO.Stream

type AuthInfo = { id: string; pass: string }
module AuthInfo =
    let toToken info =
        let { id=id; pass=pass } = info
        sprintf "%s:%s" id pass
        |> System.Text.Encoding.ASCII.GetBytes
        |> Convert.ToBase64String

    let toKey info =
        let { id=id; pass=pass } = info
        AuthenticationKey(id, pass)

let pecaWithOwinHostAccessControl acinfo endpoint buildFunc =
    let peca = new PeerCast()
    let owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    buildFunc owinHost
    |> ignore
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            endpoint,
            OutputStreamType.None,
            OutputStreamType.None
        )
    listener.LoopbackAccessControlInfo <- acinfo
    peca

let pecaWithOwinHost endpoint buildFunc =
    pecaWithOwinHostAccessControl (AccessControlInfo(OutputStreamType.All, false, null)) endpoint buildFunc

let httpFileHost url path =
    let ct = new System.Threading.CancellationTokenSource()
    let hostTask =
        async {
            use server = new HttpListener()
            server.Prefixes.Add(url)
            server.Start()
            use _ = ct.Token.Register(fun () -> server.Stop())
            let rec processRequestAsync () =
                async {
                    let! ctx =
                        server.GetContextAsync()
                        |> Async.AwaitTask
                    let responseBytes status contenttype bytes =
                        ctx.Response.StatusCode <- status
                        ctx.Response.ContentType <- contenttype
                        ctx.Response.ContentLength64 <- Array.length bytes |> int64
                        ctx.Response.Close(bytes, false)
                    let responseText status text =
                        System.Text.Encoding.UTF8.GetBytes(string text)
                        |> responseBytes status "text/plain" 
                    match ctx.Request.HttpMethod with
                    | "HEAD" -> ()
                    | "GET" -> 
                        let localPath = System.IO.Path.Combine(path, ctx.Request.Url.AbsolutePath)
                        if System.IO.File.Exists(localPath) then
                            ctx.Response.StatusCode <- 200
                            let! bytes =
                                System.IO.File.ReadAllBytesAsync(localPath)
                                |> Async.AwaitTask
                            responseBytes 200 "application/octet-stream" bytes
                        else
                            responseText 404 "File not found."
                    | _ ->
                        responseText 400 "Invalid request."
                    if ct.IsCancellationRequested then
                        return ()
                    else
                        return! processRequestAsync()
                }
            do!
                processRequestAsync()
            server.Close()
        }
        |> Async.StartAsTask
    {
        new IDisposable with
            member self.Dispose() =
                ct.Cancel()
                hostTask.Wait()
                ct.Dispose()
    }

module WebRequest =
    let addHeader (header:string) value (req:WebRequest) =
        req.Headers.Add(header, value)
        req


module HttpRequestMessage =
    let get (url:string) =
        new HttpRequestMessage(HttpMethod.Get, url)

    let head (url:string) =
        new HttpRequestMessage(HttpMethod.Head, url)

    let post (url:string) =
        new HttpRequestMessage(HttpMethod.Post, url)

    let delete (url:string) =
        new HttpRequestMessage(HttpMethod.Delete, url)

module HttpClient =
    let send req =
        use client = new HttpClient()
        client.Send(req)

    let get (url:string) =
        let task = task {
            use client = new HttpClient()
            return! client.GetAsync(url)
        }
        task.Result

    let postString (data:string) (url:string) =
        let req = HttpRequestMessage.post url
        req.Content <- new StringContent(data)
        req.Headers.TransferEncodingChunked <- true
        send req

    let getWithTimeout timeout_ms (url:string) =
        let task = task {
            use client = new HttpClient()
            client.Timeout <- TimeSpan.FromMilliseconds(timeout_ms)
            return! client.GetAsync(url)
        }
        task.Result

    let getString (url:string) =
        let task = task {
            use client = new HttpClient()
            return! client.GetStringAsync(url)
        }
        task.Result

    let getWithHeader (headers: seq<string*string>) (url:string) =
        let task = task {
            use client = new HttpClient()
            headers
            |> Seq.iter client.DefaultRequestHeaders.Add
            return! client.GetAsync(url)
        }
        task.Result

module Assert =
    let equal<'a> (expected:'a) (actual:'a) =
        Assert.Equal(expected, actual)

    let equalAsync<'a> (expected:'a) (actualTask:Task<'a>) =
        task {
            let! actual = actualTask
            Assert.Equal(expected, actual)
        }

    let isSome actual =
        Assert.True(Option.isSome actual)

    let isNone actual =
        Assert.True(Option.isNone actual)

    let hasFlag expected (actual:#Enum) =
        actual.HasFlag(expected)
        |> Assert.True

    let hasNotFlag expected (actual:#Enum) =
        actual.HasFlag(expected)
        |> Assert.False

    let statusCode code (rsp:HttpResponseMessage) =
        Assert.Equal(code, rsp.StatusCode)

    let ExpectStatusCode code (req:WebRequest) =
        try
            req.GetResponse() |> ignore
            Assert.True(false)
        with
        | :? WebException as ex ->
            Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status)
            Assert.Equal(code, (ex.Response :?> HttpWebResponse).StatusCode)

    let ExpectResponse expected (req:WebRequest) =
        let res = req.GetResponse()
        use strm = new System.IO.StreamReader(res.GetResponseStream())
        Assert.Equal(expected, strm.ReadToEnd())

    let ExpectAtomName expected (atom:Atom) =
        Assert.Equal(expected, atom.Name)

    let ExpectResultOk result =
        match result with
        | Ok _ ->
            Assert.True(Result.isOk result)
        | Error msg ->
            Assert.Fail($"Result error with {msg}")
        result

type TempDirectory() =
    let name = System.IO.Path.GetTempFileName()
    do
        System.IO.File.Delete(name)
    let directoryInfo = System.IO.Directory.CreateDirectory(name)

    member self.Name = directoryInfo.Name
    member self.FullName = directoryInfo.FullName

    interface IDisposable with
        member self.Dispose() =
            System.IO.Directory.Delete(self.FullName, true)

