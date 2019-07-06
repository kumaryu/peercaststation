module PCPTests

open Xunit
open System
open System.Net
open PeerCastStation.Core
open PeerCastStation.PCP
open TestCommon

let registerPCPRelay (host:PeerCastStation.Core.Http.OwinHost) =
    host.Register(fun builder -> PCPRelayOwinApp.BuildApp(builder))

let endpoint = allocateEndPoint IPAddress.Loopback

[<Fact>]
let ``チャンネルIDを渡さないとエラーが返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let getRequest404 (path, expected) =
        sprintf "http://%s/%s" (endpoint.ToString()) path
        |> WebRequest.CreateHttp
        |> Assert.ExpectStatusCode expected
    [
        ("channel/", HttpStatusCode.Forbidden);
        ("channel/hoge", HttpStatusCode.NotFound);
    ]
    |> List.iter getRequest404

[<Fact>]
let ``無いチャンネルIDを指定すると404が返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
    peca.AddChannel channel
    sprintf "http://%s/channel/%s" (endpoint.ToString()) (Guid.NewGuid().ToString("N"))
    |> WebRequest.CreateHttp
    |> Assert.ExpectStatusCode HttpStatusCode.NotFound

[<Fact>]
let ``受信状態でないチャンネルを指定すると404が返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
    peca.AddChannel channel
    sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
    |> WebRequest.CreateHttp
    |> Assert.ExpectStatusCode HttpStatusCode.NotFound

[<Fact>]
let ``PCPバージョンが指定されていないと400が返る`` () =
    use peca = pecaWithOwinHost endpoint registerPCPRelay
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
    channel.Start(null)
    peca.AddChannel channel
    sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
    |> WebRequest.CreateHttp
    |> Assert.ExpectStatusCode HttpStatusCode.BadRequest

[<Fact>]
let ``ネットワークが違うチャンネルを指定すると404が返る`` () =
    let testNetwork (endpoint, network, pcpver) = 
        use peca = pecaWithOwinHost endpoint registerPCPRelay
        let channel = DummyBroadcastChannel(peca, network, Guid.NewGuid())
        channel.ChannelInfo <- createChannelInfo "hoge" "FLV"
        channel.Start(null)
        peca.AddChannel channel
        sprintf "http://%s/channel/%s" (endpoint.ToString()) (channel.ChannelID.ToString("N"))
        |> WebRequest.CreateHttp
        |> WebRequest.addHeader "x-peercast-pcp" pcpver
        |> Assert.ExpectStatusCode HttpStatusCode.NotFound
    [
        (allocateEndPoint IPAddress.Loopback, NetworkType.IPv6, "1");
        (allocateEndPoint IPAddress.Loopback, NetworkType.IPv6, "100");
        (allocateEndPoint IPAddress.Loopback, NetworkType.IPv4, "100");
        (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv4, "1");
        (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv4, "100");
        (allocateEndPoint IPAddress.IPv6Loopback, NetworkType.IPv6, "1");
    ]
    |> List.iter testNetwork

