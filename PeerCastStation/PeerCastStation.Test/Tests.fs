module Tests

open Xunit
open System
open System.Collections.Generic
open System.Net
open System.Net.Http
open System.Threading
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

[<Fact>]
let ``アプリからテキストを取得できる`` () =
    let test port =
        let peca = PeerCast()
        use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
        use app = helloWorldApp owinHost
        peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
        let listener =
            peca.StartListen(
                IPEndPoint(IPAddress.Parse("127.0.0.1"), port),
                OutputStreamType.All,
                OutputStreamType.All
            )
        let req =
            sprintf "http://127.0.0.1:%d/index.txt" port
            |> WebRequest.CreateHttp
        req.KeepAlive <- false
        let result = req.GetResponse()
        use strm = new System.IO.StreamReader(result.GetResponseStream())
        Assert.Equal("Hello World!", strm.ReadToEnd())
        listener.Stop()
        peca.Stop()
    test 8080
    test 8080

[<Fact>]
let ``アプリで処理されなかったら404が返る`` () =
    let port = 8080
    let peca = PeerCast()
    use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    use app = helloWorldApp owinHost
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            IPEndPoint(IPAddress.Parse("127.0.0.1"), port),
            OutputStreamType.All,
            OutputStreamType.All
        )
    let req =
        sprintf "http://127.0.0.1:%d/index.html" port
        |> WebRequest.CreateHttp
    req.KeepAlive <- false
    try
        req.GetResponse() |> ignore
        Assert.True(false)
    with
    | :? WebException as ex ->
        Assert.Equal(WebExceptionStatus.ProtocolError, ex.Status)
        Assert.Equal(HttpStatusCode.NotFound, (ex.Response :?> HttpWebResponse).StatusCode)
    listener.Stop()
    peca.Stop()


