module HttpTests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.HTTP
open TestCommon

let registerHttpDirect (host:PeerCastStation.Core.Http.OwinHost) =
    host.Register(fun builder -> HTTPDirectOwinApp.BuildApp(builder))

module HttpOutputTest =
    let endpoint = allocateEndPoint IPAddress.Loopback

    [<Fact>]
    let ``チャンネルIDを渡さないとエラーが返る`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let getRequest404 (path, expected) =
            sprintf "http://%s/%s" (endpoint.ToString()) path
            |> HttpClient.get
            |> Assert.statusCode expected
        [
            ("stream/", HttpStatusCode.Forbidden);
            ("stream/hoge", HttpStatusCode.NotFound);
            ("pls/", HttpStatusCode.Forbidden);
            ("pls/fuga", HttpStatusCode.NotFound);
        ]
        |> List.iter getRequest404

    [<Fact>]
    let ``無いチャンネルIDを指定すると404が返る`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        peca.AddChannel channel
        ["pls"; "stream"]
        |> List.iter (fun subpath ->
            sprintf "http://%s/%s/%s" (endpoint.ToString()) subpath (Guid.NewGuid().ToString("N"))
            |> HttpClient.get
            |> Assert.statusCode HttpStatusCode.NotFound
        )

    [<Fact>]
    let ``チャンネル情報が無いチャンネルを指定すると10秒でタイムアウトして504が返る`` () =
        task {
            use peca = pecaWithOwinHost endpoint registerHttpDirect
            let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
            peca.AddChannel channel
            let! results =
                ["pls"; "stream"]
                |> List.map (fun subpath ->
                    let url = sprintf "http://%s/%s/%s" (endpoint.ToString()) subpath (channel.ChannelID.ToString("N"))
                    task {
                        use client = new System.Net.Http.HttpClient()
                        client.Timeout <- TimeSpan.FromMilliseconds(11000)
                        let! rsp = client.GetAsync(url)
                        rsp
                        |> Assert.statusCode HttpStatusCode.GatewayTimeout
                    }
                )
                |> System.Threading.Tasks.Task.WhenAll
            results |> ignore
        }

module PlayListTest =
    let endpoint = allocateEndPoint IPAddress.Loopback
    let authinfo = { id="hoge"; pass="fuga" }

    type Entry = { name: string; streamUrl: string }
    let m3u entry =
        let builder = System.Text.StringBuilder()
        builder.AppendLine("#EXTM3U")
               .AppendLine("#EXTINF:-1, " + entry.name)
               .AppendLine(entry.streamUrl)
               .ToString()

    let asx entry =
        let builder = System.Text.StringBuilder()
        builder.AppendLine("<ASX version=\"3.0\">")
               .AppendLine(sprintf "  <Title>%s</Title>" entry.name)
               .AppendLine("  <Entry>")
               .AppendLine(sprintf "    <Title>%s</Title>" entry.name)
               .AppendLine(sprintf "    <Ref href=\"%s\" />" entry.streamUrl)
               .AppendLine("  </Entry>")
               .Append("</ASX>")
               .ToString()

    [<Fact>]
    let ``指定したチャンネルIDのプレイリストが取得できる`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        peca.AddChannel channel
        let playlist =
            {
                name=channel.ChannelInfo.Name;
                streamUrl=sprintf "http://%s/stream/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N").ToUpperInvariant());
            }
            |> m3u
        sprintf "http://%s/pls/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> HttpClient.getString
        |> Assert.equal playlist

    [<Fact>]
    let ``WMVチャンネルのプレイリストは標準でASXが返る`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "WMV"
        peca.AddChannel channel
        let playlist =
            {
                name=channel.ChannelInfo.Name;
                streamUrl=sprintf "mms://%s/stream/%s.wmv" (endpoint.ToString()) (channel.ChannelID.ToString("N").ToUpperInvariant());
            }
            |> asx
        sprintf "http://%s/pls/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> HttpClient.getString
        |> Assert.equal playlist

    [<Fact>]
    let ``クエリパラメータでスキームを変更できる`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        peca.AddChannel channel
        let playlist =
            {
                name=channel.ChannelInfo.Name;
                streamUrl=sprintf "rtmp://%s/stream/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N").ToUpperInvariant());
            }
            |> m3u
        sprintf "http://%s/pls/%s?scheme=rtmp" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> HttpClient.getString
        |> Assert.equal playlist

    [<Fact>]
    let ``クエリパラメータか拡張子でフォーマットを変更できる`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        peca.AddChannel channel
        [
            ("", "http", m3u);
            ("?pls=m3u", "http", m3u);
            (".m3u", "http", m3u);
            ("?pls=asx", "mms", asx);
            (".asx", "mms", asx);
        ]
        |> List.iter (fun (postfix, scheme, pls) ->
            let entry =
                {
                    name = "hoge";
                    streamUrl = sprintf "%s://%s/stream/%s" scheme (endpoint.ToString()) (channel.ChannelID.ToString("N").ToUpperInvariant());
                }
            sprintf "http://%s/pls/%s%s" (endpoint.ToString()) (channel.ChannelID.ToString("N")) postfix
            |> HttpClient.getString
            |> Assert.equal (pls entry)
        )

    [<Fact>]
    let ``m3u8のプレイリストを要求するとhlsのパスにリダイレクトされる`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        peca.AddChannel channel
        [
            ("?pls=m3u8", "");
            (".m3u8", "");
            (".m3u8?hoge=fuga", "?hoge=fuga");
            (".m3u8?hoge=fuga&pls=m3u8&foo=bar", "?hoge=fuga&foo=bar");
        ]
        |> List.iter (fun (postfix, query) ->
            let url = sprintf "http://%s/pls/%s%s" (endpoint.ToString()) (channel.ChannelID.ToString("N")) postfix
            let task = task {
                use handler = new System.Net.Http.HttpClientHandler()
                handler.AllowAutoRedirect <- false
                use client = new System.Net.Http.HttpClient(handler)
                return! client.GetAsync(url)
            }
            let rsp = task.Result
            Assert.statusCode HttpStatusCode.MovedPermanently rsp
            let hls = sprintf "http://%s/hls/%s%s" (endpoint.ToString()) (channel.ChannelID.ToString("N")) query
            Assert.equal hls (rsp.Headers.Location.ToString())
        )
