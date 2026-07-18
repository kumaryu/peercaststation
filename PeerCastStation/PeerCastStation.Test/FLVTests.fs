module FLVTests

open System
open System.IO
open Xunit
open PeerCastStation.FLV

[<Fact>]
let ``Oversized video PES packet uses unspecified packet length`` () =
    let payload = Array.zeroCreate<byte> 70_000
    let packet = FLVToMPEG2TS.PESPacket(0xE0uy, Nullable(), Nullable(), payload)
    use output = new MemoryStream()

    FLVToMPEG2TS.PESPacket.WriteTo(output, packet)

    let bytes = output.ToArray()
    Assert.Equal(0uy, bytes.[4])
    Assert.Equal(0uy, bytes.[5])

[<Fact>]
let ``Normal video PES packet preserves packet length`` () =
    let payload = Array.zeroCreate<byte> 100
    let packet = FLVToMPEG2TS.PESPacket(0xE0uy, Nullable(), Nullable(), payload)
    use output = new MemoryStream()

    FLVToMPEG2TS.PESPacket.WriteTo(output, packet)

    let bytes = output.ToArray()
    Assert.Equal(0uy, bytes.[4])
    Assert.Equal(103uy, bytes.[5])
