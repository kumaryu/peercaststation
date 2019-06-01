module Tests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.Core.Http
open Owin

let registerApp path appFunc (owinHost:PeerCastStation.Core.Http.OwinHost) =
    owinHost.Register(
        fun builder ->
            builder.Map(
                string path,
                fun builder ->
                    builder.Run (fun env ->
                        appFunc env
                        |> Async.StartAsTask
                        :> System.Threading.Tasks.Task
                    )
            )
            |> ignore
    ) |> ignore

let registerAppWithType appType path appFunc (owinHost:PeerCastStation.Core.Http.OwinHost) =
    owinHost.Register(
        fun builder ->
            builder.Map(
                string path,
                fun builder ->
                    builder.UseAuth appType |> ignore
                    builder.Run (fun env ->
                        appFunc env
                        |> Async.StartAsTask
                        :> System.Threading.Tasks.Task
                    )
            )
            |> ignore
    ) |> ignore

let messageApp path msg =
    registerApp path (fun env ->
        async {
            if env.Request.Path.HasValue then
                env.Response.StatusCode <- 404
            else
                env.Response.ContentType <- "text/plain;charset=utf-8"
                env.Response.Write (string msg)
        }
    )

let helloWorldApp path =
    messageApp path "Hello World!"

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

let endpoint = IPEndPoint(IPAddress.Loopback, 8080)
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

let authinfo = { id="hoge"; pass="fuga" }

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

let pecaWithOwinHost =
    AccessControlInfo(OutputStreamType.All, false, null)
    |> pecaWithOwinHostAccessControl

[<Fact>]
let ``アプリからテキストを取得できる`` () =
    use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())

[<Fact>]
let ``HEADリクエストの場合はボディが返らない`` () =
    use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    req.Method <- "HEAD"
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("", strm.ReadToEnd())
    Assert.Equal(12L, result.ContentLength)

[<Fact>]
let ``Dateヘッダがレスポンスに入ってくる`` () =
    use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())
    let date = DateTimeOffset.ParseExact(result.Headers.["Date"], "R", System.Globalization.CultureInfo.InvariantCulture)
    Assert.InRange((DateTimeOffset.Now - date).TotalSeconds, 0.0, 10.0)

[<Fact>]
let ``アプリで処理されなかったら404が返る`` () =
    use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
    let req =
        sprintf "http://%s/index.html" (endpoint.ToString())
        |> WebRequest.CreateHttp
    try
        req.GetResponse() |> ignore
        Assert.True(false)
    with
    | :? WebException as ex ->
        Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status)
        Assert.Equal(HttpStatusCode.NotFound, (ex.Response :?> HttpWebResponse).StatusCode)

[<Fact>]
let ``複数回リクエストしても正しく返ってくる`` () =
    use peca =
        pecaWithOwinHost endpoint (fun owinHost ->
            messageApp "/index.txt" "Hello World!" owinHost |> ignore
            messageApp "/index.html" "<html><body>Hello World!</body></html>" owinHost |> ignore
        )
    let testRequest path expected =
        let req =
            sprintf "http://%s%s" (endpoint.ToString()) path
            |> WebRequest.CreateHttp
        let result = req.GetResponse()
        use strm = new System.IO.StreamReader(result.GetResponseStream())
        Assert.Equal(expected, strm.ReadToEnd())
    testRequest "/index.txt" "Hello World!"
    testRequest "/index.html" "<html><body>Hello World!</body></html>"
    testRequest "/index.txt" "Hello World!"

[<Fact>]
let ``再起動後のリクエストも正しく処理される`` () =
    let test () =
        use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
        let req =
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> WebRequest.CreateHttp
        let result = req.GetResponse()
        use strm = new System.IO.StreamReader(result.GetResponseStream())
        Assert.Equal("Hello World!", strm.ReadToEnd())
    test ()
    test ()

[<Fact>]
let ``EnvにAccessControlInfoが入ってくる`` () =
    let mutable acinfo : obj = null
    use peca =
        pecaWithOwinHost endpoint (
            registerApp "/index.txt" (fun env ->
                async {
                    env.Environment.TryGetValue("peercaststation.AccessControlInfo", &acinfo) |> ignore
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write ""
                }
            )
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("", strm.ReadToEnd())
    Assert.NotNull(acinfo)
    Assert.IsType(typeof<AccessControlInfo>, acinfo)

[<Fact>]
let ``EnvにLocalEndPointが入ってくる`` () =
    let mutable localaddr : string = ""
    let mutable localport : int option = Some -1
    use peca =
        pecaWithOwinHost endpoint (
            registerApp "/index.txt" (fun env ->
                async {
                    localaddr <- env.Request.LocalIpAddress
                    localport <- env.Request.LocalPort |> Option.ofNullable
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write ""
                }
            )
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("", strm.ReadToEnd())
    Assert.Equal(endpoint.Address.ToString(), localaddr)
    Assert.Equal(endpoint.Port, localport |> Option.defaultValue 80)

[<Fact>]
let ``EnvにRemoteEndPointが入ってくる`` () =
    let mutable remoteaddr : string = ""
    let mutable remoteport : int option = Some -1
    use peca =
        pecaWithOwinHost endpoint (
            registerApp "/index.txt" (fun env ->
                async {
                    remoteaddr <- env.Request.RemoteIpAddress
                    remoteport <- env.Request.RemotePort |> Option.ofNullable
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write ""
                }
            )
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("", strm.ReadToEnd())
    Assert.Equal(endpoint.Address.ToString(), remoteaddr)
    Assert.NotEqual(endpoint.Port, remoteport |> Option.defaultValue 80)

[<Fact>]
let ``OutputStreamTypeが一致しないアプリは403を返す`` () =
    let acinfo = AccessControlInfo(OutputStreamType.Relay, false, null)
    use peca =
        pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
            registerAppWithType OutputStreamType.Interface "/index.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
            registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    try
        req.GetResponse() |> ignore
        Assert.True(false)
    with
    | :? WebException as ex ->
        Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status)
        Assert.Equal(HttpStatusCode.Forbidden, (ex.Response :?> HttpWebResponse).StatusCode)

    let req =
        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())

[<Fact>]
let ``認証が必要であれば401を返す`` () =
    let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
    use peca =
        pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
            registerAppWithType OutputStreamType.Interface "/index.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
            registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    try
        req.GetResponse() |> ignore
        Assert.True(false)
    with
    | :? WebException as ex ->
        Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status)
        Assert.Equal(HttpStatusCode.Forbidden, (ex.Response :?> HttpWebResponse).StatusCode)

    let req =
        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    try
        req.GetResponse() |> ignore
        Assert.True(false)
    with
    | :? WebException as ex ->
        Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status)
        Assert.Equal(HttpStatusCode.Unauthorized, (ex.Response :?> HttpWebResponse).StatusCode)

[<Fact>]
let ``Basic認証で認証が通る`` () =
    let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
    use peca =
        pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
            registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    req.Credentials <- NetworkCredential(authinfo.id, authinfo.pass)
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())

[<Fact>]
let ``Cookieで認証が通る`` () =
    let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
    use peca =
        pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
            registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    req.CookieContainer <- CookieContainer()
    req.CookieContainer.Add(sprintf "http://%s/relay.txt" (endpoint.ToString()) |> Uri, Cookie("auth", AuthInfo.toToken authinfo))
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())

[<Fact>]
let ``クエリパラメータで認証が通る`` () =
    let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
    use peca =
        pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
            registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/relay.txt?auth=%s" (endpoint.ToString()) (AuthInfo.toToken authinfo)
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())

[<Fact>]
let ``chunkedエンコーディングで送受信できる`` () =
    use peca =
        pecaWithOwinHost endpoint (fun owinHost ->
            registerApp "/echo" (fun env ->
                async {
                    use strm = new System.IO.StreamReader(env.Request.Body, System.Text.Encoding.UTF8, false, 2048, true)
                    let! req = strm.ReadToEndAsync() |> Async.AwaitTask
                    env.Response.ContentType <- "text/plain"
                    env.Response.Headers.Set("Transfer-Encoding", "chunked")
                    env.Response.Write req
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/echo" (endpoint.ToString())
        |> WebRequest.CreateHttp
    req.Method <- "POST"
    req.ContentType <- "text/plain"
    req.SendChunked <- true
    use reqstrm = req.GetRequestStream()
    reqstrm.WriteUTF8("Hello ")
    reqstrm.WriteUTF8("Hoge!")
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello Hoge!", strm.ReadToEnd())

[<Fact>]
let ``OnSendingHeadersに登録したアクションでヘッダを書き換えられる`` () =
    use peca =
        pecaWithOwinHost endpoint (fun owinHost ->
            registerApp "/index.txt" (fun env ->
                async {
                    env.Response.OnSendingHeaders((fun _ -> env.Response.Headers.Set("x-hoge", "fuga")), ())
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
            ) owinHost |> ignore
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())
    Assert.Equal("fuga", result.Headers.Get("x-hoge"))

[<Fact>]
let ``opaque.Upgradeで好きなように通信できる`` () =
    use peca =
        pecaWithOwinHost endpoint (fun owinHost ->
            registerApp "/opaque" (fun env ->
                async {
                    Opaque.upgrade env.Environment (fun opaqueEnv ->
                        async {
                            let stream = Opaque.stream opaqueEnv
                            let! len = stream.AsyncRead(1)
                            let! bytes = stream.AsyncRead(len.[0] |> int)
                            do! stream.AsyncWrite(len)
                            do! stream.AsyncWrite(bytes)
                        }
                    )
                }
            ) owinHost |> ignore
        )
    use conn = new System.Net.Sockets.TcpClient()
    conn.Connect(endpoint)
    let sendReq strm =
        use writer = new System.IO.StreamWriter(strm, System.Text.Encoding.ASCII, 2048, true)
        writer.NewLine <- "\r\n"
        writer.WriteLine("GET /opaque HTTP/1.1")
        writer.WriteLine(endpoint.ToString() |> sprintf "Host:%s")
        writer.WriteLine()
    let strm = conn.GetStream()
    sendReq strm
    let data = seq { 0uy..42uy } |> Array.ofSeq
    strm.WriteByte(data.Length |> byte)
    strm.Write(data, 0, data.Length)
    let len = strm.ReadByte()
    Assert.Equal(len, data.Length)
    let buf = Array.create len 0uy
    Assert.Equal(len, strm.Read(buf, 0, len))
    Assert.Equal<byte>(data, buf)

