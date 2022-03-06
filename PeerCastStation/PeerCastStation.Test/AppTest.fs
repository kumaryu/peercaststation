module AppTest

open Xunit
open PeerCastStation.App
open TestCommon

module OptionParserTest =
    [<Fact>]
    let ``オプションをパースできる`` () =
        let opts = OptionParser ()
        opts.Add("--simpleoption", "-s")
        opts.Add("--arg-required", "-r", OptionArg.Required)
        opts.Add("--arg-optional", "-o", OptionArg.Optional)

        let r = opts.Parse(Array.empty)
        Assert.False(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.Equal(0, r.Arguments.Count)

        let r = opts.Parse([| "--simpleoption"; "ARG1"; "ARG2" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.Equal(2, r.Arguments.Count)
        Assert.Equal("ARG1", r.Arguments[0])
        Assert.Equal("ARG2", r.Arguments[1])

        let r = opts.Parse([| "--simpleoption"; "--arg-required=ARG1"; "--arg-optional"; "ARG2"; "ARG3" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.True(r.HasOption "--arg-required")
        Assert.Equal("ARG1", r.GetArgumentOf("--arg-required"))
        Assert.True(r.HasOption "--arg-optional")
        Assert.Equal("ARG2", r.GetArgumentOf("--arg-optional"))
        Assert.Equal(1, r.Arguments.Count)
        Assert.Equal("ARG3", r.Arguments[0])

        let r = opts.Parse([| "-s"; "-r=ARG1"; "-o"; "ARG2"; "ARG3" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.True(r.HasOption "--arg-required")
        Assert.Equal("ARG1", r.GetArgumentOf("--arg-required"))
        Assert.True(r.HasOption "--arg-optional")
        Assert.Equal("ARG2", r.GetArgumentOf("--arg-optional"))
        Assert.Equal(1, r.Arguments.Count)
        Assert.Equal("ARG3", r.Arguments[0])

        Assert.Throws<OptionParseErrorException>(fun () ->
            opts.Parse([| "--arg-required"; |]) |> ignore
        ) |> ignore

        Assert.Throws<OptionParseErrorException>(fun () ->
            opts.Parse([| "--arg-required"; "-s" |]) |> ignore
        ) |> ignore

        let r = opts.Parse([| "--arg-optional"; |])
        Assert.True(r.HasOption "--arg-optional")
        let (_, v) = r.TryGetOption("--arg-optional")
        Assert.Equal(0, v.Arguments.Count)

        let r = opts.Parse([| "--arg-optional"; "-s" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.True(r.HasOption "--arg-optional")
        let (_, v) = r.TryGetOption("--arg-optional")
        Assert.Equal(0, v.Arguments.Count)

    [<Fact>]
    let ``名前付き引数をパースできる`` () =
        let opts = OptionParser ()
        opts.Add("--simpleoption", "-s")
        opts.Add("--arg-required", "-r", OptionArg.Required)
        opts.Add("--arg-optional", "-o", OptionArg.Optional)
        opts.Add(NamedArgument("REQUIREDARG", OptionType.Required))
        opts.Add(NamedArgument("OPTIONALARG", OptionType.None))

        Assert.Throws<OptionParseErrorException>(fun () ->
            opts.Parse(Array.empty) |> ignore
        ) |> ignore

        let r = opts.Parse([| "--simpleoption"; "ARG1" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.True(r.HasOption "REQUIREDARG")
        Assert.False(r.HasOption "OPTIONALARG")
        Assert.Equal("ARG1", r.GetArgumentOf("REQUIREDARG"))
        Assert.Equal(0, r.Arguments.Count)

        let r = opts.Parse([| "ARG1"; "ARG2"; "ARG3" |])
        Assert.True(r.HasOption "REQUIREDARG")
        Assert.True(r.HasOption "OPTIONALARG")
        Assert.Equal("ARG1", r.GetArgumentOf("REQUIREDARG"))
        Assert.Equal("ARG2", r.GetArgumentOf("OPTIONALARG"))
        Assert.Equal(1, r.Arguments.Count)
        Assert.Equal("ARG3", r.Arguments[0])

        let r = opts.Parse([| "-s"; "-r=ARG1"; "-o"; "ARG2"; "ARG3"; "ARG4"; "ARG5" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.True(r.HasOption "--arg-required")
        Assert.Equal("ARG1", r.GetArgumentOf("--arg-required"))
        Assert.True(r.HasOption "--arg-optional")
        Assert.Equal("ARG2", r.GetArgumentOf("--arg-optional"))
        Assert.Equal("ARG3", r.GetArgumentOf("REQUIREDARG"))
        Assert.Equal("ARG4", r.GetArgumentOf("OPTIONALARG"))
        Assert.Equal(1, r.Arguments.Count)
        Assert.Equal("ARG5", r.Arguments[0])

    [<Fact>]
    let ``サブコマンドをパースできる`` () =
        let opts = OptionParser ()
        opts.Add("--simpleoption", "-s")
        opts.Add("--arg-required", "-r", OptionArg.Required)
        opts.Add("--arg-optional", "-o", OptionArg.Optional)
        let subA = Subcommand("suba")
        subA.Add("--suboption", "-s")
        subA.Add(NamedArgument("REQUIREDARG", OptionType.Required))
        subA.Add(NamedArgument("OPTIONALARG", OptionType.None))
        opts.Add(subA)
        let subB = Subcommand("subb")
        subB.Add("--suboption", "-s")
        subB.Add(NamedArgument("OPTIONALARG", OptionType.None))
        opts.Add(subB)

        let r = opts.Parse(Array.empty)
        Assert.False(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.False(r.HasOption "suba")
        Assert.False(r.HasOption "subb")
        Assert.Equal(0, r.Arguments.Count)

        let r = opts.Parse([| "--simpleoption"; "ARG1" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.False(r.HasOption "suba")
        Assert.False(r.HasOption "subb")
        Assert.Equal(1, r.Arguments.Count)
        Assert.Equal("ARG1", r.Arguments[0])

        let r = opts.Parse([| "--simpleoption"; "suba"; "-s"; "ARG1"; "ARG2"; "ARG3" |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.True(r.HasOption "suba")
        Assert.False(r.HasOption "subb")
        Assert.Equal(0, r.Arguments.Count)
        let (_, sub) = r.TryGetOption("suba")
        Assert.True(sub.HasOption "--suboption")
        Assert.True(sub.HasOption "REQUIREDARG")
        Assert.True(sub.HasOption "OPTIONALARG")
        Assert.Equal("ARG1", sub.GetArgumentOf("REQUIREDARG"))
        Assert.Equal("ARG2", sub.GetArgumentOf("OPTIONALARG"))
        Assert.Equal(1, sub.Arguments.Count)
        Assert.Equal("ARG3", sub.Arguments[0])

        Assert.Throws<OptionParseErrorException>(fun () ->
            opts.Parse([| "--simpleoption"; "suba"; "-s" |]) |> ignore
        ) |> ignore

        let r = opts.Parse([| "--simpleoption"; "subb"; "-s"; |])
        Assert.True(r.HasOption "--simpleoption")
        Assert.False(r.HasOption "--arg-required")
        Assert.False(r.HasOption "--arg-optional")
        Assert.False(r.HasOption "suba")
        Assert.True(r.HasOption "subb")
        Assert.Equal(0, r.Arguments.Count)
        let (_, sub) = r.TryGetOption("subb")
        Assert.True(sub.HasOption "--suboption")
        Assert.False(sub.HasOption "REQUIREDARG")
        Assert.False(sub.HasOption "OPTIONALARG")
        Assert.Equal(0, sub.Arguments.Count)



