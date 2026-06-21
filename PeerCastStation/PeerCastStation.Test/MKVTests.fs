module MKVTests

open System.IO
open System.Text
open PeerCastStation.MKV
open Xunit

// VInt.FromSize: 最小バイト長で符号化し、ReadUInt で読み戻すと元の値に一致する。
// 127/16383/2097151 は全ビット1(unknown size)と衝突するため 1 バイト繰り上がる。
[<Theory>]
[<InlineData(0, 1)>]
[<InlineData(126, 1)>]
[<InlineData(127, 2)>]
[<InlineData(128, 2)>]
[<InlineData(16382, 2)>]
[<InlineData(16383, 3)>]
[<InlineData(16384, 3)>]
[<InlineData(2097150, 3)>]
[<InlineData(2097151, 4)>]
let ``VInt.FromSize は最小長で符号化し読み戻せる`` (value: int, expectedLen: int) =
    let v = VInt.FromSize(int64 value)
    Assert.Equal(expectedLen, v.Length)
    use ms = new MemoryStream(v.Binary)
    let read = VInt.ReadUInt(ms)
    Assert.Equal(int64 value, read.Value)

[<Fact>]
let ``VInt.FromSize は4バイト超の値も往復できる`` () =
    let value = 5_000_000L
    let v = VInt.FromSize(value)
    Assert.Equal(4, v.Length)
    use ms = new MemoryStream(v.Binary)
    Assert.Equal(value, VInt.ReadUInt(ms).Value)

[<Fact>]
let ``VInt.FromSize は負値を拒否する`` () =
    Assert.Throws<System.ArgumentOutOfRangeException>(fun () -> VInt.FromSize(-1L) |> ignore)
    |> ignore

// MakeElement: ID とデータを保ち、既存リーダーで読み戻せる。
[<Fact>]
let ``MakeElement は ID とデータを保ち読み戻せる`` () =
    let data = [| 0x12uy; 0x34uy; 0x56uy |]
    let elt = MatroskaWriter.MakeElement(Elements.DocType, data)
    use ms = new MemoryStream(elt.ToArray())
    let mutable header = ElementHeader.Read(ms)
    Assert.True(header.ID.BinaryEquals(Elements.DocType))
    Assert.Equal(int64 data.Length, header.Size.Value)
    let read = Element.ReadBody(&header, ms)
    Assert.Equal<byte[]>(data, read.Data)

// 既知の EBML バイト列(DocType="matroska") を read→write して完全一致。
[<Fact>]
let ``既知の EBML バイト列を read write で完全一致`` () =
    let known =
        Array.concat [
            [| 0x42uy; 0x82uy; 0x88uy |] // DocType id + size(len1,val8)
            Encoding.UTF8.GetBytes("matroska")
        ]
    use ms = new MemoryStream(known)
    let mutable header = ElementHeader.Read(ms)
    let elt = Element.ReadBody(&header, ms)
    let rebuilt = MatroskaWriter.MakeElement(header.ID.Binary, elt.Data)
    Assert.Equal<byte[]>(known, rebuilt.ToArray())

// MakeMaster: 子要素を内包し、ネスト境界とサイズが正しい。
[<Fact>]
let ``MakeMaster は子要素を内包し境界が正しい`` () =
    let child1 = MatroskaWriter.MakeUInt(Elements.TimecodeScale, 1000000UL)
    let child2 = MatroskaWriter.MakeString(Elements.DocType, "webm")
    let master = MatroskaWriter.MakeMaster(Elements.Info, child1, child2)
    use ms = new MemoryStream(master.ToArray())
    let header = ElementHeader.Read(ms)
    Assert.True(header.ID.BinaryEquals(Elements.Info))

    let mutable h1 = ElementHeader.Read(ms)
    Assert.True(h1.ID.BinaryEquals(Elements.TimecodeScale))
    let c1 = Element.ReadBody(&h1, ms)
    Assert.Equal(1000000L, Element.ReadUInt(new MemoryStream(c1.Data), c1.Data.LongLength))

    let mutable h2 = ElementHeader.Read(ms)
    Assert.True(h2.ID.BinaryEquals(Elements.DocType))
    let c2 = Element.ReadBody(&h2, ms)
    Assert.Equal("webm", Encoding.UTF8.GetString(c2.Data))

    // master のサイズ = 子要素の合計(余りなく読み切る)
    Assert.Equal(ms.Length, ms.Position)

// MakeUInt: 最小バイト長で符号なし整数を符号化する。
[<Theory>]
[<InlineData(0, 1)>]
[<InlineData(255, 1)>]
[<InlineData(256, 2)>]
[<InlineData(65535, 2)>]
[<InlineData(65536, 3)>]
let ``MakeUInt は最小バイト長で符号化する`` (value: int, expectedLen: int) =
    let elt = MatroskaWriter.MakeUInt(Elements.TimecodeScale, uint64 value)
    Assert.Equal(expectedLen, elt.Data.Length)
    Assert.Equal(int64 value, Element.ReadUInt(new MemoryStream(elt.Data), elt.Data.LongLength))
