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
        entry.streamUrl + "\r\n"

    let asx entry =
        sprintf "<ASX version=\"3.0\">\r\n  <Title>%s</Title>\r\n  <Entry>\r\n    <Title>%s</Title>\r\n    <Ref href=\"%s\" />\r\n  </Entry>\r\n</ASX>" entry.name entry.name entry.streamUrl

    [<Fact>]
    let ``指定したチャンネルIDのプレイリストが取得できる`` () =
        use peca = pecaWithOwinHost endpoint registerHttpDirect
        let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        peca.AddChannel channel
        let streamUrl = sprintf "http://%s/stream/%s\r\n" (endpoint.ToString()) (channel.ChannelID.ToString("N").ToUpperInvariant())
        sprintf "http://%s/pls/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> WebRequest.CreateHttp
        |> Assert.ExpectResponse streamUrl

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

