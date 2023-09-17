module ChannelTests

open Xunit
open System
open PeerCastStation.Core
open TestCommon
open System.Net

[<Fact>]
let ``チャンネルがリレー可能な時にMakeRelayableを呼んでもリレー不能なChannelSinkが止められない`` () =
    use peca = new PeerCast()
    peca.AccessController.MaxUpstreamRate <- 6000
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "FLV" 500, ChannelTrack.empty)
    peca.AddChannel(channel)
    let relays =
        [| 0; 1; 1; 2; 3; 0 |]
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Relay
                        info.LocalDirects <- Some i |> Option.toNullable
                        info.LocalRelays <- Some i |> Option.toNullable
                        info.RemoteHostStatus <- RemoteHostStatus.RelayFull
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    Assert.Equal(6, channel.LocalRelays)
    Assert.Equal(true, channel.MakeRelayable(false))
    Assert.Equal(6, channel.LocalRelays)

[<Fact>]
let ``チャンネルがいっぱいの時にMakeRelayableで必要な分だけChannelSinkを止める`` () =
    use peca = new PeerCast()
    peca.AccessController.MaxUpstreamRate <- 3000
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "FLV" 500, ChannelTrack.empty)
    peca.AddChannel(channel)
    let relays =
        [| 0; 1; 1; 2; 3; 0 |]
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Relay
                        info.LocalDirects <- Some i |> Option.toNullable
                        info.LocalRelays <- Some i |> Option.toNullable
                        info.RemoteHostStatus <- RemoteHostStatus.RelayFull
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    Assert.Equal(6, channel.LocalRelays)
    Assert.Equal(true, channel.MakeRelayable(false))
    Assert.Equal(5, channel.LocalRelays)

[<Fact>]
let ``チャンネルがいっぱいの時にMakeRelayableで切れる分を切っても新しくリレーできない場合はfalseを返す`` () =
    use peca = new PeerCast()
    peca.AccessController.MaxUpstreamRate <- 2000
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "FLV" 500, ChannelTrack.empty)
    peca.AddChannel(channel)
    let relays =
        [| 0; 1; 1; 2; 3; 0 |]
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Relay
                        info.LocalDirects <- Some i |> Option.toNullable
                        info.LocalRelays <- Some i |> Option.toNullable
                        info.RemoteHostStatus <- RemoteHostStatus.RelayFull
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    Assert.Equal(6, channel.LocalRelays)
    Assert.Equal(false, channel.MakeRelayable(false))
    Assert.Equal(4, channel.LocalRelays)

[<Fact>]
let ``指定したキーをBanするとHasBannedがtrueを返す`` () =
    use peca = new PeerCast()
    let channel1 = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "FLV" 500, ChannelTrack.empty)
    let channel2 = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "FLV" 500, ChannelTrack.empty)
    channel1.Ban("hoge", DateTimeOffset.Now.AddMilliseconds(100.0))
    channel2.Ban("fuga", DateTimeOffset.Now.AddMilliseconds(100.0))
    channel1.Ban("piyo", DateTimeOffset.Now.AddMilliseconds(100.0))
    Assert.True(channel1.HasBanned("hoge"))
    Assert.False(channel1.HasBanned("fuga"))
    Assert.True(channel1.HasBanned("piyo"))
    Assert.False(channel2.HasBanned("hoge"))
    Assert.True(channel2.HasBanned("fuga"))
    Assert.False(channel2.HasBanned("piyo"))
    Threading.Thread.Sleep(100);
    Assert.False(channel1.HasBanned("hoge"))
    Assert.False(channel1.HasBanned("fuga"))
    Assert.False(channel1.HasBanned("piyo"))
    Assert.False(channel2.HasBanned("hoge"))
    Assert.False(channel2.HasBanned("fuga"))
    Assert.False(channel2.HasBanned("piyo"))

[<Fact>]
let ``ノード情報が変更されるとIChannelMonitorのOnNodeChangedが呼び出される`` () =
    use peca = new PeerCast()
    let mutable nodes = []
    let monitor = {
        new IChannelMonitor with
            member this.OnContentChanged _ = 
                ()
            member this.OnStopped _ = 
                ()
            member this.OnNodeChanged(action, node) =
                nodes <- (action, node) :: nodes
                ()
    }
    let channel = DummyRelayChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    use subscription = channel.AddMonitor monitor
    peca.AddChannel channel
    let hosts = 
        Array.init 32 (fun i -> Host(Guid.NewGuid(), Guid.Empty, IPEndPoint(IPAddress.Loopback, 1234+i), IPEndPoint(IPAddress.Loopback, 1234+i), 1+i, 1+i/2, false, false, false, false, true, false, Seq.empty, AtomCollection()))
    Array.iter (fun h -> channel.AddNode(h)) hosts
    Array.iter (fun h -> channel.RemoveNode(h)) hosts
    TestCommon.waitForConditionOrTimeout (fun () -> List.length nodes >= 64) 10000
    Assert.Equal(64, List.length nodes)
    Assert.True(Array.forall (fun h -> List.contains (ChannelNodeAction.Updated, h) nodes) hosts)
    Assert.True(Array.forall (fun h -> List.contains (ChannelNodeAction.Removed, h) nodes) hosts)


[<Fact>]
let ``LoopbackSourceStreamで元のチャンネルのトラック情報がコピーされる`` () =
    use peca = new PeerCast()
    let channel1 = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfoBitrate "hoge" "FLV" 500, ChannelTrack.empty)
    channel1.ChannelTrack <- ChannelTrackDesc.toChannelTrack { ChannelTrackDesc.empty with name=Some "fuga"; album=Some "piyo"; genre=Some "foo"; creator=Some "bar"; url=Some "http://example.com/index.html" }
    peca.AddChannel channel1
    channel1.Start(DummySourceStream.FactoryForBroadcast, "http://example.com/" |> Uri)

    let channel2 = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid(), createChannelInfo "hoge" "FLV", ChannelTrack.empty)
    peca.AddChannel channel2
    channel2.Start(LoopbackSourceStreamFactory(peca), channel1.ChannelID.ToString("N") |> sprintf "loopback:%s" |> Uri)

    TestCommon.waitForConditionOrTimeout (fun () -> channel2.ChannelInfo.Bitrate = 500) 1000
    Assert.Equal(500, channel2.ChannelInfo.Bitrate)
    Assert.Equal("fuga", channel2.ChannelTrack.Name)

