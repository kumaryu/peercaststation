module HttpTests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.HTTP
open TestCommon

let registerHttpDirect (host:PeerCastStation.Core.Http.OwinHost) =
    host.Register(fun builder -> HTTPDirectOwinApp.BuildApp(builder))

let endpoint = allocateEndPoint IPAddress.Loopback

[<Fact>]
let ``チャンネルIDを渡さないとエラーが返る`` () =
    use peca = pecaWithOwinHost endpoint registerHttpDirect
    let getRequest404 (path, expected) =
        sprintf "http://%s/%s" (endpoint.ToString()) path
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode expected
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
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode HttpStatusCode.NotFound
    )

[<Fact>]
let ``チャンネル情報が無いチャンネルを指定すると10秒でタイムアウトして504が返る`` () =
    use peca = pecaWithOwinHost endpoint registerHttpDirect
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    peca.AddChannel channel
    ["pls"; "stream"]
    |> List.iter (fun subpath ->
        let req =
            sprintf "http://%s/%s/%s" (endpoint.ToString()) subpath (channel.ChannelID.ToString("N"))
            |> WebRequest.CreateHttp
        req.Timeout <- 11000
        Assert.ExpectStatusCode HttpStatusCode.GatewayTimeout req
    )

module PlayList =
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
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse playlist

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
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse playlist

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
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse playlist

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
            |> WebRequest.CreateHttp
            |> Assert.ExpectResponse (pls entry)
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
            let req =
                sprintf "http://%s/pls/%s%s" (endpoint.ToString()) (channel.ChannelID.ToString("N")) postfix
                |> WebRequest.CreateHttp
            req.AllowAutoRedirect <- false
            let res =
                try
                    req.GetResponse() :?> HttpWebResponse
                with
                | :? WebException as ex when ex.Response <> null ->
                    ex.Response :?> HttpWebResponse
            Assert.Equal(HttpStatusCode.MovedPermanently, res.StatusCode)
            let hls = sprintf "http://%s/hls/%s%s" (endpoint.ToString()) (channel.ChannelID.ToString("N")) query
            Assert.Equal(hls, res.GetResponseHeader("Location"))
        )
