module Tests

open System
open Xunit

open PeerCastStation.Core.IPC
open PeerCastStation.Core
open PeerCastStation.UI.HTTP
open PeerCastStation.UI.IPC
open Newtonsoft.Json.Linq
open System.IO
open System.Diagnostics

type TestApp(plugins:IPlugin list) as self =
    inherit PeerCastApplication()

    let settings = PecaSettings("settings.xml")
    let peercast = PeerCast()

    let dispose () =
        for plugin in plugins do
            plugin.Detach()

    let start () =
        for plugin in plugins do
            plugin.Attach(self)
        for plugin in plugins do
            plugin.Start()
    
    let stop () =
        peercast.Stop()
        for plugin in plugins do
            plugin.Stop()

    override self.Settings = settings
    override self.Plugins = plugins |> Seq.ofList
    override self.PeerCast = peercast
    override self.BasePath = AppDomain.CurrentDomain.BaseDirectory
    override self.SaveSettings() = ()
    override self.Stop(exit_code:int) = stop()
    member self.Start() = start()

    interface IDisposable with
        member self.Dispose() = dispose()

let readHTTPHeader (stream:System.IO.Stream) =
    let rec readHeader lines line lastToken =
        async {
            let! b = stream.AsyncRead(1)
            return!
                match line, lastToken, b.[0] with
                | [], Some '\r'B, '\n'B ->
                    async { return lines }
                | _, Some '\r'B, '\n'B ->
                    let lines = line :: lines
                    readHeader lines [] None
                | _, None, b ->
                    let lastToken = Some b
                    readHeader lines line lastToken
                | _, Some lastToken, b ->
                    let line = lastToken :: line
                    let lastToken = Some b
                    readHeader lines line lastToken
        }
    async {
        let! lines = readHeader [] [] None
        return
            lines
            |> List.map (fun ln -> List.rev ln |> List.toArray |> System.Text.Encoding.ASCII.GetString)
            |> List.rev
    }

let sendHttpRequestAsync (stream:System.IO.Stream) method path (body:string) =
    async {
        let reqbody = body |> System.Text.Encoding.UTF8.GetBytes
        let req =
            sprintf "%s %s HTTP/1.1\r\nHost:localhost\r\nX-REQUESTED-WITH:IPC\r\nContent-Type:application/json\r\nContent-Length:%d\r\nConnection:close\r\n\r\n" method path reqbody.Length
            |> System.Text.Encoding.ASCII.GetBytes
        do!
            stream.AsyncWrite(Array.append req reqbody)
    }

let receiveHttpResponseAsync (stream:System.IO.Stream) =
    let (|Regex|_|) pattern str =
       let md = System.Text.RegularExpressions.Regex(pattern).Match(str)
       if md.Success then
           Some (List.tail [ for x in md.Groups -> x.Value ])
       else
           None
    async {
        let! header = readHTTPHeader stream
        let mutable len = -1
        match header with
        | [] -> failwith "Empty response"
        | resLine :: headers ->
            match resLine with
            | Regex "\AHTTP/(\d.\d) (\d+) (.*)\Z" md -> ()
            | _ -> failwith "Invalid response"
            for line in headers do
                match line with
                | Regex "\AContent-Length:\s*(\d+)\s*\Z" [ln] -> len <- int ln
                | Regex "\AContent-Type:\s*(\d+)\s*\Z" md -> ()
                | _ -> ()
        return!
            match len with
            | 0 -> async { return "" }
            | -1 ->
                async {
                    let! bytes = stream.AsyncRead(-1)
                    return bytes
                        |> System.Text.Encoding.UTF8.GetString
                }
            | len ->
                async {
                    let! bytes = stream.AsyncRead(len)
                    return bytes
                        |> System.Text.Encoding.UTF8.GetString
                }
    }

let httpRequestAsync stream method path body =
    async {
        do! sendHttpRequestAsync stream method path body
        return! receiveHttpResponseAsync stream
    }

let getTextAsync stream path (body:string) =
    httpRequestAsync stream "GET" path body

let postJSON (stream:System.IO.Stream) path (body:string) =
    async {
        let! rsp = httpRequestAsync stream "POST" path body
        return JObject.Parse(rsp)
    }

type IPCRequest =
    | WithoutArgs of method:string
    | ArgsByName of method:string * args:Map<string,string>
    | ArgsByLocation of method:string * args:string array

type PeerCastStationUIIPCTests () =
    let ipcListener = IPCOutputListener()
    let app = new TestApp([OWINHost(); APIHost(); HTMLHost(); ipcListener])
    do
        app.Start()

    let dispose () =
        app.Stop()
        (app :> IDisposable).Dispose()

    let createRequestBody req =
        let doc = JObject()
        doc.["jsonrpc"] <- JValue("2.0")
        doc.["id"] <- JValue(1)
        match req with
        | WithoutArgs(method) ->
            doc.["method"] <- JValue(method)
            doc.["params"] <- JArray()
        | ArgsByName(method, args) ->
            doc.["method"] <- JValue(method)
            let parameters = JObject()
            Map.iter (fun k (v:string) ->
                parameters.[k] <- JValue(v)
            ) args
            doc.["params"] <- parameters
        | ArgsByLocation(method, args) ->
            doc.["method"] <- JValue(method)
            doc.["params"] <-
                args
                |> Seq.map JValue
                |> JArray
        doc.ToString()

    let invokeAPIAsync requestPath req =
        async {
            use client = IPCClient.Create(ipcListener.IPCPath)
            do!
                client.ConnectAsync(Async.DefaultCancellationToken)
                |> Async.AwaitTask
            use stream = client.GetStream()
            let reqbody = createRequestBody req
            let! response = postJSON stream requestPath reqbody
            let mutable jsonrpc : JToken = null
            let mutable id      : JToken = null
            let mutable result  : JToken = null
            if not (response.TryGetValue("jsonrpc", &jsonrpc)) || string jsonrpc<>"2.0" ||
               not (response.TryGetValue("id", &id))           || int id<>1 ||
               not (response.TryGetValue("result", &result))   || result.Type<>JTokenType.Object then
                failwith "invalid reponse"
            return result :?> JObject
        }

    let invokeGETAsync requestPath =
        async {
            use client = IPCClient.Create(ipcListener.IPCPath)
            do!
                client.ConnectAsync(Async.DefaultCancellationToken)
                |> Async.AwaitTask
            use stream = client.GetStream()
            return! getTextAsync stream requestPath ""
        }

    [<Fact>]
    let ``ユーザーのIPCパスが使われる`` () =
        let path = PeerCastStation.Core.IPC.IPCEndPoint.GetDefaultPath(PeerCastStation.Core.IPC.IPCEndPoint.PathType.User, "peercaststation")
        Assert.Equal(path, ipcListener.IPCPath)
        
    [<Fact>]
    let ``JSON RPCのgetVersionInfoでバージョン情報が取れる`` () =
        let result = invokeAPIAsync "/api/1" (WithoutArgs "getVersionInfo") |> Async.RunSynchronously
        let mutable value : JToken = null
        Assert.True(result.TryGetValue("apiVersion", &value))
        Assert.Matches(@"1\.0\.\d+", value.ToString())
        Assert.True(result.TryGetValue("agentName", &value))
        Assert.Matches(@"PeerCastStation.+", value.ToString())
        Assert.True(result.TryGetValue("jsonrpc", &value))
        Assert.Equal(@"2.0", value.ToString())

    [<Fact>]
    let ``HTMLの取得ができる`` () =
        let result = invokeGETAsync "/html/index.html" |> Async.RunSynchronously
        Assert.Matches(@"(?s).*<html>.*</html>.*", result)

    [<Fact>]
    let ``中途半端なリクエストを送ると数秒でタイムアウトする`` () =
        let time = Stopwatch.StartNew()
        Assert.Throws(
            typeof<EndOfStreamException>,
            fun () ->
                async {
                    use client = IPCClient.Create(ipcListener.IPCPath)
                    do!
                        client.ConnectAsync(Async.DefaultCancellationToken)
                        |> Async.AwaitTask
                    use stream = client.GetStream()
                    let req =
                        "GET /html/index.html HTTP/1.1\r\nHost:localhost\r\nConnection:close\r\n"
                        |> System.Text.Encoding.ASCII.GetBytes
                    do!
                        stream.AsyncWrite(req)
                    return! receiveHttpResponseAsync stream
                }
                |> Async.Ignore
                |> Async.RunSynchronously
        ) |> ignore
        Assert.True(time.ElapsedMilliseconds < 10000L)

    [<Fact>]
    let ``壊れたリクエストを送ると数秒でタイムアウトする`` () =
        let time = Stopwatch.StartNew()
        Assert.Throws(
            typeof<EndOfStreamException>,
            fun () ->
                async {
                    use client = IPCClient.Create(ipcListener.IPCPath)
                    do!
                        client.ConnectAsync(Async.DefaultCancellationToken)
                        |> Async.AwaitTask
                    use stream = client.GetStream()
                    let req =
                        "hoge asdf nyan/1.1\r\nHost:localhost\r\nConnection:close\r\n\r\n"
                        |> System.Text.Encoding.ASCII.GetBytes
                    do!
                        stream.AsyncWrite(req)
                    return! receiveHttpResponseAsync stream
                }
                |> Async.Ignore
                |> Async.RunSynchronously
        ) |> ignore
        Assert.True(time.ElapsedMilliseconds < 10000L)

    interface IDisposable with
        member this.Dispose () =
            dispose ()

