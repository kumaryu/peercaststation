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
                env.Response.ContentType <- "text/plain"
                env.Response.Write (string msg)
        }
    )

let helloWorldApp path =
    messageApp path "Hello World!"

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

