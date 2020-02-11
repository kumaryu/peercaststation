module CoreTests

open Xunit
open System
open PeerCastStation.Core
open TestCommon

[<Fact>]
let ``チャンネルのローカル視聴・リレー数が正しく取れる`` () =
    use peca = new PeerCast()
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    Assert.Equal(0, channel.LocalDirects)
    Assert.Equal(0, channel.LocalRelays)
    let directs =
        seq { 1..4 }
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Direct
                        info.LocalDirects <- Some 0 |> Option.toNullable
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    let relays =
        seq { 1..5 }
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Relay
                        info.LocalDirects <- Some i |> Option.toNullable
                        info.LocalRelays <- Some (i*2) |> Option.toNullable
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    Assert.Equal(4, channel.LocalDirects)
    Assert.Equal(5, channel.LocalRelays)

[<Fact>]
let ``チャンネルの合計視聴・リレー数が正しく取れる`` () =
    use peca = new PeerCast()
    let channel = DummyBroadcastChannel(peca, NetworkType.IPv4, Guid.NewGuid())
    Assert.Equal(0, channel.TotalDirects)
    Assert.Equal(0, channel.TotalRelays)
    let directs =
        seq { 1..4 }
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Direct
                        info.LocalDirects <- Some 0 |> Option.toNullable
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    let relays =
        seq { 1..5 }
        |> Seq.map (fun i ->
            {
                new IChannelSink with
                    member this.OnBroadcast(from, packet) = ()
                    member this.OnStopped(reason) = ()
                    member this.GetConnectionInfo() =
                        let info = ConnectionInfoBuilder()
                        info.Type <- ConnectionType.Relay
                        info.LocalDirects <- Some i |> Option.toNullable
                        info.LocalRelays <- Some (i*2) |> Option.toNullable
                        info.Build()
            }
        )
        |> Seq.map channel.AddOutputStream 
        |> Seq.toArray
    let buildHost directs relays =
        let host = HostBuilder()
        host.SessionID <- Guid.NewGuid()
        host.DirectCount <- directs
        host.RelayCount <- relays
        host.ToHost()
    seq { 5..8 }
    |> Seq.map (fun i -> buildHost i (i*2))
    |> Seq.iter channel.AddNode
    Assert.Equal(4+5+6+7+8, channel.TotalDirects)
    Assert.Equal(5+(5+6+7+8)*2, channel.TotalRelays)


