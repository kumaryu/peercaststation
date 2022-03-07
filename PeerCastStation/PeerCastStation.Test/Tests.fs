module Tests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.Core.Http
open TestCommon

module OwinResponse =
    let writeStrAsync (rsp:OwinEnvironment.OwinResponse) (data:string) =
        async {
            let! ct = Async.CancellationToken
            do!
                rsp.WriteAsync (data, ct)
                |> Async.AwaitTask
        }

    let writeBytesAsync (rsp:OwinEnvironment.OwinResponse) (data:byte[]) =
        async {
            let! ct = Async.CancellationToken
            do!
                rsp.WriteAsync (data, ct)
                |> Async.AwaitTask
        }

let messageApp path msg =
    registerApp path (fun env ->
        async {
            if env.Request.Path = null then
                env.Response.StatusCode <- HttpStatusCode.NotFound
            else
                env.Response.ContentType <- "text/plain;charset=utf-8"
                do!
                    OwinResponse.writeStrAsync env.Response msg
        }
    )

module OwinHostTest =
    let helloWorldApp path =
        messageApp path "Hello World!"

    let endpoint = allocateEndPoint IPAddress.Loopback
    let authinfo = { id="hoge"; pass="fuga" }

    [<Fact>]
    let ``アプリからテキストを取得できる`` () =
        use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse "Hello World!"

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
        sprintf "http://%s/index.html" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode HttpStatusCode.NotFound

    [<Fact>]
    let ``複数回リクエストしても正しく返ってくる`` () =
        use peca =
            pecaWithOwinHost endpoint (fun owinHost ->
                messageApp "/index.txt" "Hello World!" owinHost |> ignore
                messageApp "/index.html" "<html><body>Hello World!</body></html>" owinHost |> ignore
            )
        let testRequest path expected =
            sprintf "http://%s%s" (endpoint.ToString()) path
            |> WebRequest.CreateHttp
            |> Assert.ExpectResponse expected
        testRequest "/index.txt" "Hello World!"
        testRequest "/index.html" "<html><body>Hello World!</body></html>"
        testRequest "/index.txt" "Hello World!"

    [<Fact>]
    let ``再起動後のリクエストも正しく処理される`` () =
        let test () =
            use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> WebRequest.CreateHttp
            |> Assert.ExpectResponse "Hello World!"
        test ()
        test ()

    [<Fact>]
    let ``Envにマップしたパスとそれより後のパスが入ってくる`` () =
        let mutable basePath : obj = null
        let mutable path : obj = null
        use peca =
            pecaWithOwinHost endpoint (
                registerApp "/hoge" (fun env ->
                    async {
                        env.Environment.TryGetValue(OwinEnvironment.Owin.RequestPathBase, &basePath) |> ignore
                        env.Environment.TryGetValue(OwinEnvironment.Owin.RequestPath, &path) |> ignore
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response ""
                    }
                )
            )
        sprintf "http://%s/hoge/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse ""
        Assert.Equal("/hoge", string basePath)
        Assert.Equal("/index.txt", string path)

    [<Fact>]
    let ``EnvにAccessControlInfoが入ってくる`` () =
        let mutable acinfo : obj = null
        use peca =
            pecaWithOwinHost endpoint (
                registerApp "/index.txt" (fun env ->
                    async {
                        env.Environment.TryGetValue("peercaststation.AccessControlInfo", &acinfo) |> ignore
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response ""
                    }
                )
            )
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse ""
        Assert.NotNull(acinfo)
        Assert.IsType(typeof<AccessControlInfo>, acinfo)

    [<Fact>]
    let ``EnvにPeerCastが入ってくる`` () =
        let mutable peercast : obj = null
        use peca =
            pecaWithOwinHost endpoint (
                registerApp "/index.txt" (fun env ->
                    async {
                        env.Environment.TryGetValue("peercaststation.PeerCast", &peercast) |> ignore
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response ""
                    }
                )
            )
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse ""
        Assert.NotNull(peercast)
        Assert.IsType(typeof<PeerCast>, peercast)

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
                        do!
                            OwinResponse.writeStrAsync env.Response ""
                    }
                )
            )
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse ""
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
                        do!
                            OwinResponse.writeStrAsync env.Response ""
                    }
                )
            )
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse ""
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
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
                registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                    async {
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
            )
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode HttpStatusCode.Forbidden

        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse "Hello World!"

    [<Fact>]
    let ``認証が必要であれば401を返す`` () =
        let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
        use peca =
            pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
                registerAppWithType OutputStreamType.Interface "/index.txt" (fun env ->
                    async {
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
                registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                    async {
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
            )
        sprintf "http://%s/index.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode HttpStatusCode.Forbidden

        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode HttpStatusCode.Unauthorized

    [<Fact>]
    let ``Basic認証で認証が通る`` () =
        let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
        use peca =
            pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
                registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                    async {
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
            )
        let req =
            sprintf "http://%s/relay.txt" (endpoint.ToString())
            |> WebRequest.CreateHttp
        req.Credentials <- NetworkCredential(authinfo.id, authinfo.pass)
        Assert.ExpectResponse "Hello World!" req

    [<Fact>]
    let ``Cookieで認証が通る`` () =
        let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
        use peca =
            pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
                registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                    async {
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
            )
        let req =
            sprintf "http://%s/relay.txt" (endpoint.ToString())
            |> WebRequest.CreateHttp
        req.CookieContainer <- CookieContainer()
        req.CookieContainer.Add(sprintf "http://%s/relay.txt" (endpoint.ToString()) |> Uri, Cookie("auth", AuthInfo.toToken authinfo))
        Assert.ExpectResponse "Hello World!" req

    [<Fact>]
    let ``クエリパラメータで認証が通る`` () =
        let acinfo = AccessControlInfo(OutputStreamType.Relay, true, AuthInfo.toKey authinfo)
        use peca =
            pecaWithOwinHostAccessControl acinfo endpoint (fun owinHost ->
                registerAppWithType OutputStreamType.Relay "/relay.txt" (fun env ->
                    async {
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
                    }
                ) owinHost |> ignore
            )
        sprintf "http://%s/relay.txt?auth=%s" (endpoint.ToString()) (AuthInfo.toToken authinfo)
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse "Hello World!"

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
                        do!
                            OwinResponse.writeStrAsync env.Response req
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
        Assert.ExpectResponse "Hello Hoge!" req

    [<Fact>]
    let ``許可されていないメソッドを実行すると405が返る`` () =
        use peca =
            pecaWithOwinHost endpoint (fun owinHost ->
                owinHost.Register(
                    fun builder ->
                        builder.MapPOST(
                            "/echo",
                            fun builder ->
                                builder.Run (fun (env:OwinEnvironment) ->
                                    async {
                                        use strm = new System.IO.StreamReader(env.Request.Body, System.Text.Encoding.UTF8, false, 2048, true)
                                        let! req = strm.ReadToEndAsync() |> Async.AwaitTask
                                        env.Response.ContentType <- "text/plain"
                                        env.Response.Headers.Set("Transfer-Encoding", "chunked")
                                        do!
                                            OwinResponse.writeStrAsync env.Response req
                                    }
                                    |> Async.StartAsTask
                                    :> System.Threading.Tasks.Task
                                )
                        )
                        |> ignore
                        builder.MapGET(
                            "/echo",
                            fun builder ->
                                builder.Run (fun (env:OwinEnvironment) ->
                                    async {
                                        env.Response.ContentType <- "text/plain"
                                        do!
                                            OwinResponse.writeStrAsync env.Response "hello"
                                    }
                                    |> Async.StartAsTask
                                    :> System.Threading.Tasks.Task
                                )
                        )
                        |> ignore
                ) |> ignore
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
        Assert.ExpectResponse "Hello Hoge!" req
        let req =
            sprintf "http://%s/echo" (endpoint.ToString())
            |> WebRequest.CreateHttp
        req.Method <- "GET"
        Assert.ExpectResponse "hello" req
        let req =
            sprintf "http://%s/hoge" (endpoint.ToString())
            |> WebRequest.CreateHttp
        req.Method <- "GET"
        Assert.ExpectStatusCode HttpStatusCode.NotFound req
        let req =
            sprintf "http://%s/echo" (endpoint.ToString())
            |> WebRequest.CreateHttp
        req.Method <- "DELETE"
        Assert.ExpectStatusCode HttpStatusCode.MethodNotAllowed req

    [<Fact>]
    let ``OnSendingHeadersに登録したアクションでヘッダを書き換えられる`` () =
        use peca =
            pecaWithOwinHost endpoint (fun owinHost ->
                registerApp "/index.txt" (fun env ->
                    async {
                        env.Response.OnSendingHeaders((fun _ -> env.Response.Headers.Set("x-hoge", "fuga")), ())
                        env.Response.ContentType <- "text/plain"
                        do!
                            OwinResponse.writeStrAsync env.Response "Hello World!"
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

