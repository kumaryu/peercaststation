module Tests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open Owin

let helloWorldApp (owinHost:PeerCastStation.Core.Http.OwinHost) =
    owinHost.Register(
        fun builder ->
            builder.Map(
                "/index.txt",
                fun builder ->
                    builder.Run (fun env ->
                        async {
                            env.Response.ContentType <- "text/plain"
                            env.Response.Write "Hello World!"
                        }
                        |> Async.StartAsTask
                        :> System.Threading.Tasks.Task
                    )
            )
            |> ignore
    )

let endpoint = IPEndPoint(IPAddress.Loopback, 8080)

[<Fact>]
let ``アプリからテキストを取得できる`` () =
    let peca = PeerCast()
    use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    use app = helloWorldApp owinHost
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            endpoint,
            OutputStreamType.All,
            OutputStreamType.All
        )
    let req =
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
    let result = req.GetResponse()
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())
    listener.Stop()
    peca.Stop()

[<Fact>]
let ``アプリで処理されなかったら404が返る`` () =
    let peca = PeerCast()
    use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    use app = helloWorldApp owinHost
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            endpoint,
            OutputStreamType.All,
            OutputStreamType.All
        )
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
    listener.Stop()
    peca.Stop()

[<Fact>]
let ``複数回リクエストしても正しく返ってくる`` () =
    let peca = PeerCast()
    use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    use app = helloWorldApp owinHost
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            endpoint,
            OutputStreamType.All,
            OutputStreamType.All
        )
    let testRequest () =
        let req =
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> WebRequest.CreateHttp
        let result = req.GetResponse()
        use strm = new System.IO.StreamReader(result.GetResponseStream())
        Assert.Equal("Hello World!", strm.ReadToEnd())
    testRequest ()
    testRequest ()
    testRequest ()
    listener.Stop()
    peca.Stop()

[<Fact>]
let ``再起動後のリクエストも正しく処理される`` () =
    let test () =
        let peca = PeerCast()
        use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
        use app = helloWorldApp owinHost
        peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
        let listener =
            peca.StartListen(
                endpoint,
                OutputStreamType.All,
                OutputStreamType.All
            )
        let req =
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> WebRequest.CreateHttp
        let result = req.GetResponse()
        use strm = new System.IO.StreamReader(result.GetResponseStream())
        Assert.Equal("Hello World!", strm.ReadToEnd())
        listener.Stop()
        peca.Stop()
    test ()
    test ()


