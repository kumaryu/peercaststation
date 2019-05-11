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
            builder.Run (fun env ->
                async {
                    env.Response.ContentType <- "text/plain"
                    env.Response.Write "Hello World!"
                }
                |> Async.StartAsTask
                :> System.Threading.Tasks.Task
            )
    )

[<Fact>]
let ``My test`` () =
    let peca = PeerCast()
    use owinHost = new PeerCastStation.Core.Http.OwinHost(null, peca)
    use app = helloWorldApp owinHost
    peca.OutputStreamFactories.Add(PeerCastStation.Core.Http.OwinHostOutputStreamFactory(peca, owinHost))
    let listener =
        peca.StartListen(
            IPEndPoint(IPAddress.Parse("127.0.0.1"), 8080),
            OutputStreamType.All,
            OutputStreamType.All
        )
    let req = WebRequest.CreateHttp "http://127.0.0.1:8080/"
    let result =
        req.GetResponseAsync()
        |> Async.AwaitTask
        |> Async.RunSynchronously
    use strm = new System.IO.StreamReader(result.GetResponseStream())
    Assert.Equal("Hello World!", strm.ReadToEnd())
