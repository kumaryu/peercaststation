module Tests

open Xunit
open System
open System.Net
open PeerCastStation.Core
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
    )

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

let pecaWithOwinHost endpoint buildFunc =
    let peca = new PeerCast()
    let owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    buildFunc owinHost
    |> ignore
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    peca.StartListen(
        endpoint,
        OutputStreamType.All,
        OutputStreamType.All
    ) |> ignore
    peca

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

