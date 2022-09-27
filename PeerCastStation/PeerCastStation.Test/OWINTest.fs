
module OWINTest

open Xunit
open System
open PeerCastStation.Core.Http

module OwinEnvironmentTest =
    [<Fact>]
    let ``RemoteIpAddressとRemotePortが取得できる`` () =
        let env =
            Map [
                (OwinEnvironment.Owin.RequestBody, System.IO.Stream.Null :> obj)
                (OwinEnvironment.Server.RemoteIpAddress, "127.0.0.1")
                (OwinEnvironment.Server.RemotePort, "7144")
            ]
            |> OwinEnvironment
        Assert.Equal(Nullable 7144, env.Request.RemotePort)
        Assert.Equal(System.Net.IPAddress.Parse("127.0.0.1"), env.Request.RemoteIpAddress)
        Assert.Equal(System.Net.IPEndPoint.Parse("127.0.0.1:7144"), env.Request.GetRemoteEndPoint())
        
    [<Fact>]
    let ``LocalIpAddressとLocalPortが取得できる`` () =
        let env =
            Map [
                (OwinEnvironment.Owin.RequestBody, System.IO.Stream.Null :> obj)
                (OwinEnvironment.Server.LocalIpAddress, "::1")
                (OwinEnvironment.Server.LocalPort, "8080")
            ]
            |> OwinEnvironment
        Assert.Equal(Nullable 8080, env.Request.LocalPort)
        Assert.Equal(System.Net.IPAddress.Parse("::1"), env.Request.LocalIpAddress)
        


