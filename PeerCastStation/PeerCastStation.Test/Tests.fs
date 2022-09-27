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
        |> HttpClient.getString
        |> Assert.equal "Hello World!"

    [<Fact>]
    let ``HEADリクエストの場合はボディが返らない`` () =
        use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
        let req =
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> HttpRequestMessage.head
        let rsp = HttpClient.send req
        Assert.Equal("", rsp.Content.ReadAsStringAsync().Result)
        Assert.Equal(12L, rsp.Content.Headers.ContentLength |> Option.ofNullable |> Option.get)

    [<Fact>]
    let ``Dateヘッダがレスポンスに入ってくる`` () =
        use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
        let rsp =
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> HttpClient.get
        Assert.Equal("Hello World!", rsp.Content.ReadAsStringAsync().Result)
        let date = DateTimeOffset.ParseExact(rsp.Headers.GetValues("Date") |> Seq.head, "R", System.Globalization.CultureInfo.InvariantCulture)
        Assert.InRange((DateTimeOffset.Now - date).TotalSeconds, 0.0, 10.0)

    [<Fact>]
    let ``アプリで処理されなかったら404が返る`` () =
        use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
        sprintf "http://%s/index.html" (endpoint.ToString())
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.NotFound

    [<Fact>]
    let ``複数回リクエストしても正しく返ってくる`` () =
        use peca =
            pecaWithOwinHost endpoint (fun owinHost ->
                messageApp "/index.txt" "Hello World!" owinHost |> ignore
                messageApp "/index.html" "<html><body>Hello World!</body></html>" owinHost |> ignore
            )
        let testRequest path expected =
            sprintf "http://%s%s" (endpoint.ToString()) path
            |> HttpClient.getString
            |> Assert.equal expected
        testRequest "/index.txt" "Hello World!"
        testRequest "/index.html" "<html><body>Hello World!</body></html>"
        testRequest "/index.txt" "Hello World!"

    [<Fact>]
    let ``再起動後のリクエストも正しく処理される`` () =
        let test () =
            use peca = pecaWithOwinHost endpoint (helloWorldApp "/index.txt")
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> HttpClient.getString
            |> Assert.equal "Hello World!"
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
        |> HttpClient.getString
        |> Assert.equal ""
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
        |> HttpClient.getString
        |> Assert.equal ""
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
        |> HttpClient.getString
        |> Assert.equal ""
        Assert.NotNull(peercast)
        Assert.IsType(typeof<PeerCast>, peercast)

    [<Fact>]
    let ``EnvにLocalEndPointが入ってくる`` () =
        let mutable localaddr : IPAddress = null
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
        |> HttpClient.getString
        |> Assert.equal ""
        Assert.Equal(endpoint.Address, localaddr)
        Assert.Equal(endpoint.Port, localport |> Option.defaultValue 80)

    [<Fact>]
    let ``EnvにRemoteEndPointが入ってくる`` () =
        let mutable remoteaddr : IPAddress = null
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
        |> HttpClient.getString
        |> Assert.equal ""
        Assert.Equal(endpoint.Address, remoteaddr)
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
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.Forbidden

        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> HttpClient.getString
        |> Assert.equal "Hello World!"

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
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.Forbidden

        sprintf "http://%s/relay.txt" (endpoint.ToString())
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.Unauthorized

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
        use handler = new System.Net.Http.HttpClientHandler()
        handler.Credentials <- NetworkCredential(authinfo.id, authinfo.pass)
        use client = new System.Net.Http.HttpClient(handler)
        let url = sprintf "http://%s/relay.txt" (endpoint.ToString())
        client.GetStringAsync(url).Result
        |> Assert.equal "Hello World!"

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
        use handler = new System.Net.Http.HttpClientHandler()
        handler.CookieContainer <- CookieContainer()
        handler.CookieContainer.Add(sprintf "http://%s/relay.txt" (endpoint.ToString()) |> Uri, Cookie("auth", AuthInfo.toToken authinfo))
        use client = new System.Net.Http.HttpClient(handler)
        let url = sprintf "http://%s/relay.txt" (endpoint.ToString())
        client.GetStringAsync(url).Result
        |> Assert.equal "Hello World!"

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
        |> HttpClient.getString
        |> Assert.equal "Hello World!"

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
        let res =
            sprintf "http://%s/echo" (endpoint.ToString())
            |> HttpClient.postString "Hello Hoge!"
        res.Content.ReadAsStringAsync().Result
        |> Assert.equal "Hello Hoge!"

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
        let res =
            sprintf "http://%s/echo" (endpoint.ToString())
            |> HttpClient.postString "Hello Hoge!"
        res.Content.ReadAsStringAsync().Result
        |> Assert.equal "Hello Hoge!"
        sprintf "http://%s/echo" (endpoint.ToString())
        |> HttpClient.getString
        |> Assert.equal "hello"
        sprintf "http://%s/hoge" (endpoint.ToString())
        |> HttpClient.get
        |> Assert.statusCode HttpStatusCode.NotFound
        sprintf "http://%s/echo" (endpoint.ToString())
        |> HttpRequestMessage.delete
        |> HttpClient.send
        |> Assert.statusCode HttpStatusCode.MethodNotAllowed

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
        let rsp =
            sprintf "http://%s/index.txt" (endpoint.ToString())
            |> HttpClient.get
        Assert.Equal("Hello World!", rsp.Content.ReadAsStringAsync().Result)
        Assert.Equal("fuga", rsp.Headers.GetValues("x-hoge") |> Seq.head)

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

