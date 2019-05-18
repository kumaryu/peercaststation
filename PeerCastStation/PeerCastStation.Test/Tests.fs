module Tests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open Owin

let messageApp path msg (owinHost:PeerCastStation.Core.Http.OwinHost) =
    owinHost.Register(
        fun builder ->
            builder.Map(
                string path,
                fun builder ->
                    builder.Run (fun env ->
                        async {
                            env.Response.ContentType <- "text/plain"
                            env.Response.Write (string msg)
                        }
                        |> Async.StartAsTask
                        :> System.Threading.Tasks.Task
                    )
            )
            |> ignore
    )

let helloWorldApp path (owinHost:PeerCastStation.Core.Http.OwinHost) =
    messageApp path "Hello World!" owinHost

let endpoint = IPEndPoint(IPAddress.Loopback, 8080)

[<Fact>]
let ``アプリからテキストを取得できる`` () =
    let peca = PeerCast()
    use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    use app = helloWorldApp "/index.txt" owinHost
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
    use app = helloWorldApp "/index.txt" owinHost
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
    use app = messageApp "/index.txt" "Hello World!" owinHost
    use app = messageApp "/index.html" "<html><body>Hello World!</body></html>" owinHost
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            endpoint,
            OutputStreamType.All,
            OutputStreamType.All
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
    listener.Stop()
    peca.Stop()

[<Fact>]
let ``再起動後のリクエストも正しく処理される`` () =
    let test () =
        let peca = PeerCast()
        use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
        use app = helloWorldApp "/index.txt" owinHost
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


