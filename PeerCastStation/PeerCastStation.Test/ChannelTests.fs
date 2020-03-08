module ChannelTests

open Xunit
open System
open PeerCastStation.Core
open TestCommon

[<Fact>]
let ``チャンネルがリレー可能な時にMakeRelayableを呼んでもリレー不能なChannelSinkが止められない`` () =
    use peca = new PeerCast()
    peca.AccessController.MaxUpstreamRate <- 6000
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfoBitrate "hoge" "FLV" 500
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
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfoBitrate "hoge" "FLV" 500
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
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    channel.ChannelInfo <- createChannelInfoBitrate "hoge" "FLV" 500
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


