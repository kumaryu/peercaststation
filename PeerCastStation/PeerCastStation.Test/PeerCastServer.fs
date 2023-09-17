module PeerCastServer

open PeerCastStation.Core
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open System
open System.IO
open System.Buffers

type SocketPipe (socket:Socket) =
    let socket = socket
    let closedCancellationTokenSource = new CancellationTokenSource()
    let opts = Pipelines.PipeOptions(useSynchronizationContext=false)

    let recvPipe = System.IO.Pipelines.Pipe(opts)
    let receiver = backgroundTask {
        let mutable completed = false
        try
            while not (completed || closedCancellationTokenSource.IsCancellationRequested) do
                let mem = recvPipe.Writer.GetMemory(Math.Max(socket.Available, socket.ReceiveBufferSize))
                let! len = socket.ReceiveAsync(mem, SocketFlags.None, closedCancellationTokenSource.Token)
                recvPipe.Writer.Advance(len)
                if len=0 then
                    completed <- true
                else
                    let! flush_result = recvPipe.Writer.FlushAsync(closedCancellationTokenSource.Token)
                    if flush_result.IsCanceled || flush_result.IsCompleted then
                        completed <- true
            recvPipe.Writer.Complete()
        with
        | :? OperationCanceledException ->
            recvPipe.Writer.Complete()
        | ex ->
            recvPipe.Writer.Complete(ex)
        try
            socket.Shutdown(SocketShutdown.Receive)
        with
        | _ ->
            ()
        socket.Close(1000)
    }

    let sendPipe = System.IO.Pipelines.Pipe(opts)
    let sender = backgroundTask {
        let mutable completed = false
        try
            let readAsync () =
                backgroundTask {
                    use read_timeout = new CancellationTokenSource(300)
                    use _ = read_timeout.Token.Register (fun () -> sendPipe.Reader.CancelPendingRead())
                    return! sendPipe.Reader.ReadAsync()
                }
            while not completed do
                let! read_result = readAsync()
                use send_timeout = new CancellationTokenSource(3000)
                if read_result.Buffer.Length=0 then
                    let! _ = socket.SendAsync(Memory<byte>.Empty, SocketFlags.None, send_timeout.Token)
                    ()
                else
                    let mutable pos = read_result.Buffer.Start
                    let mutable mem = read_result.Buffer.First
                    let! _ = socket.SendAsync(mem, SocketFlags.None, send_timeout.Token)
                    while read_result.Buffer.TryGet(&pos, &mem) do
                        let! _ = socket.SendAsync(mem, SocketFlags.None, send_timeout.Token)
                        ()
                    if read_result.IsCompleted then
                        completed <- true
                    sendPipe.Reader.AdvanceTo(read_result.Buffer.End)
            sendPipe.Reader.Complete()
        with
        | :? OperationCanceledException ->
            sendPipe.Reader.Complete()
        | ex ->
            sendPipe.Reader.Complete(ex)
        try
            socket.Shutdown(SocketShutdown.Send)
        with
        | _ ->
            ()
        closedCancellationTokenSource.CancelAfter(1000)
    }

    let mutable disposed = false
    let closeAsync () =
        if not disposed then
            disposed <- true
            sendPipe.Writer.Complete()
            backgroundTask {
                do! sender
                do! receiver
            }
            |> ValueTask
        else
            ValueTask.CompletedTask

    member this.Socket = socket
    member this.Writer = sendPipe.Writer
    member this.Reader = recvPipe.Reader
    member this.CloseAsync () = closeAsync ()

    interface IAsyncDisposable with
        member this.DisposeAsync () =
            closeAsync()

module SocketPipe =
    let acceptAsync (server: TcpListener) cancellationToken =
        backgroundTask {
            let! socket = server.AcceptSocketAsync(cancellationToken)
            return SocketPipe(socket)
        }

type AtomBuilder =
    | BuildingEmpty
    | BuildingValue of name: ID4 * value: byte array * position: int64
    | BuildingChildren of name: ID4 * children: AtomBuilder list
    | Value of name: ID4 * value: byte array
    | Children of name: ID4 * AtomBuilder list

module AtomBuilder =
    let isCompleted builder =
        match builder with
        | Value _
        | Children _ ->
            true
        | _ ->
            false

    let rec build builder =
        match builder with
        | Value (name, value) ->
            Atom(name, value)
        | Children (name, children) ->
            Atom(name, Seq.map build children |> Array.ofSeq)
        | _ ->
            failwith "Imcompleted builder"

    let rec readAtom (buffer:ReadOnlySequence<byte>) builder =
        match builder with
        | BuildingEmpty ->
            if buffer.Length<8 then
                (buffer.End, None, builder)
            else
                let header = Array.zeroCreate 8
                buffer.Slice(0, 8).CopyTo(Span header)
                let header = ReadOnlySpan header
                let name = ID4(header.Slice(0, 4))
                let len = Binary.BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(4, 4))
                match len with
                | 0u ->
                    (buffer.GetPosition(8), buffer.Slice(8) |> Some, AtomBuilder.Value(name, Array.empty<byte>))
                | cnt when (cnt &&& 0x80000000u)<>0u ->
                    let cnt = cnt &&& 0x7FFFFFFFu
                    let body_buffer = buffer.Slice(8)
                    let children = List.replicate (int cnt) AtomBuilder.BuildingEmpty
                    let pos, buffer, children = readAtoms children body_buffer
                    if Option.isSome buffer then
                        (pos, buffer, AtomBuilder.Children(name, children))
                    else
                        (pos, buffer, AtomBuilder.BuildingChildren(name, children))
                | len ->
                    let len = int64 len
                    let body = Array.zeroCreate (int(len))
                    let body_buffer = buffer.Slice(8, Math.Min(len, buffer.Length - 8L))
                    body_buffer.CopyTo(Span(body))
                    if buffer.Length>=8L+len then
                        (body_buffer.End, buffer.Slice(8L+len) |> Some, AtomBuilder.Value(name, body))
                    else
                        (body_buffer.End, None, AtomBuilder.BuildingValue(name, body, body_buffer.Length))
        | BuildingValue (name, value, position) ->
            let len = value.LongLength - position
            let body_buffer = buffer.Slice(0, Math.Min(len, buffer.Length))
            body_buffer.CopyTo(Span(value, int position, value.Length - int position))
            if buffer.Length>=len then
                (body_buffer.End, buffer.Slice(8L+len) |> Some, AtomBuilder.Value(name, value))
            else
                (body_buffer.End, None, AtomBuilder.BuildingValue(name, value, position + body_buffer.Length))
        | BuildingChildren (name, children) ->
            let pos, buffer, children = readAtoms children buffer
            if Option.isSome buffer then
                (pos, buffer, AtomBuilder.Children(name, children))
            else
                (pos, buffer, AtomBuilder.BuildingChildren(name, children))
        | Value _ ->
            (buffer.Start, Some buffer, builder)
        | Children _ ->
            (buffer.Start, Some buffer, builder)

    and readAtoms lst (buffer:ReadOnlySequence<byte>) =
        let readBuilder (buffer, pos) builder =
            match buffer with
            | Some buffer ->
                let pos, buffer, builder = readAtom buffer builder
                builder, (buffer, pos)
            | None ->
                builder, (None, pos)
        let lst, (buffer, pos) = List.mapFold readBuilder (Some buffer, buffer.Start) lst
        pos, buffer, lst

type HTTPRequest = {
    protocol: string
    method: string
    path: string
    query: string array
    headers: (string * string) list
}
type ClientMessage =
    | HTTPRequest of request: HTTPRequest
    | PCPAtom of atom : Atom
    | ConnectionClosed
    | ConnectionCanceled

type RequestMessageType =
    | Unknown
    | PCP
    | HTTP

type PeerCastClient =
    {
        pipe: SocketPipe
    }
    interface IAsyncDisposable with
        member this.DisposeAsync () =
            this.pipe.CloseAsync()
    interface IDisposable with
        member this.Dispose() =
            (this :> IAsyncDisposable).DisposeAsync().AsTask().Wait()

module PeerCastClient =
    let remoteEndPoint client =
        client.pipe.Socket.RemoteEndPoint

    let closeAsync client =
        async {
            let clientToClose : IAsyncDisposable = client
            do! clientToClose.DisposeAsync().AsTask() |> Async.AwaitTask
        }

    let acceptAsync server =
        async {
            let! cancellationToken = Async.CancellationToken
            let! pipe = SocketPipe.acceptAsync server cancellationToken |> Async.AwaitTask
            return { pipe=pipe }
        }

    let writeLineAsync client (text:string) = 
        async {
            let! ct = Async.CancellationToken
            use mem = MemoryPool<byte>.Shared.Rent(2048)
            let len = System.Text.Encoding.UTF8.GetBytes(text, mem.Memory.Span)
            do! client.pipe.Writer.WriteAsync(mem.Memory.Slice(0, len), ct).AsTask() |> Async.AwaitTask |> Async.Ignore
        }

    type ReadingState =
        | ReadingUnknownRequest
        | ReadingPCPRequest of AtomBuilder
        | ReadingHTTPRequest of requestLine: string option * headers: string list

    let readStringFromSequence (sequence: ReadOnlySequence<byte>) =
        let decoder = System.Text.Encoding.UTF8.GetDecoder()
        let builder = System.Text.StringBuilder()
        let rec read pos =
            let mutable pos = pos
            match sequence.TryGet(&pos) with
            | (true, seg) ->
                let eos = sequence.End=pos
                let cnt = decoder.GetCharCount(seg.Span, eos)
                use buf = MemoryPool<char>.Shared.Rent(cnt)
                let len = decoder.GetChars(seg.Span, buf.Memory.Span, eos)
                builder.Append(buf.Memory.Slice(0, len)) |> ignore
                if eos then
                    builder.ToString()
                else
                    read pos
            | (false, _) ->
                builder.ToString()
        read sequence.Start

    let parseHttpRequest requestLine headers =
        let md = System.Text.RegularExpressions.Regex.Match(requestLine, @"([A-Za-z]+)(?: ([A-Za-z0-9_!$&'()*+,;=/.]+)(?:\?([A-Za-z0-9_!$&'()*+,;=/.]+))?(?: ([A-Za-z0-9/._]+))?)?")
        if md.Success then
            let rec parseHeaders headers parsedHeaders =
                match headers with
                | line :: rest ->
                    let md = System.Text.RegularExpressions.Regex.Match(line, @"(\S+):(.*)")
                    if md.Success then
                        (md.Groups[1].Value, md.Groups[2].Value.Trim()) :: parsedHeaders |> parseHeaders rest
                    else
                        parseHeaders rest parsedHeaders
                | [] ->
                    parsedHeaders
            let headers = parseHeaders headers []
            let method = md.Groups[1].Value
            let path =
                if md.Groups[2].Success then
                    md.Groups[2].Value
                else
                    ""
            let query =
                if md.Groups[3].Success then
                    md.Groups[3].Value.Split('&')
                else
                    [| |]
            let protocol =
                if md.Groups[4].Success then
                    md.Groups[4].Value
                else
                    ""
            {
                protocol=protocol
                method=method
                path=path
                query=query
                headers=headers
            }
        else
            failwith "Invalid request"

    let rec httpRequestReader (requestLine, headers) (buffer:ReadOnlySequence<byte>) =
        let reader = SequenceReader(buffer)
        let mutable read_buffer = ReadOnlySequence<byte>()
        if reader.TryReadTo(&read_buffer, "\r\n"B, advancePastDelimiter=true) then
            match requestLine with
            | Some requestLine ->
                let headerLine = readStringFromSequence read_buffer
                if headerLine<>"" then
                    httpRequestReader (Some requestLine, headerLine :: headers) reader.UnreadSequence
                else
                    let req = parseHttpRequest requestLine headers
                    {| consumed=reader.Position; examined=reader.Position; message=HTTPRequest req |> Some; state=ReadingHTTPRequest (None, []) |}
            | None ->
                let requestLine = readStringFromSequence read_buffer
                httpRequestReader (Some requestLine, headers) reader.UnreadSequence
        else
            {| consumed=buffer.Start; examined=buffer.End; message=None; state=ReadingHTTPRequest (requestLine, headers) |}

    let rec pcpRequestReader builder (buffer:ReadOnlySequence<byte>) =
        let pos, next, builder = AtomBuilder.readAtom buffer builder
        if Option.isSome next then
            let atom = AtomBuilder.build builder
            {| consumed=pos; examined=pos; message=PCPAtom atom |> Some; state=ReadingPCPRequest builder |}
        else
            {| consumed=buffer.Start; examined=pos; message=None; state=ReadingPCPRequest builder |}

    let initialRequestReader (buffer:ReadOnlySequence<byte>) =
        if buffer.Length<4 then
            {| consumed=buffer.Start; examined=buffer.End; message=None; state=ReadingUnknownRequest |}
        else
            let header = Array.zeroCreate 4
            buffer.CopyTo(Span(header))
            if header[0]=byte 'p' && header[1]=byte 'c' && header[2]=byte 'p' && header[3]=byte '\n' then
                {| consumed=buffer.Start; examined=buffer.Start; message=None; state=ReadingPCPRequest BuildingEmpty|}
            else
                {| consumed=buffer.Start; examined=buffer.Start; message=None; state=ReadingHTTPRequest (None, []) |}

    let readRequestAsync msgType client =
        async {
            let! ct = Async.CancellationToken
            let pipe = client.pipe
            let mutable state =
                match msgType with
                | Unknown ->
                    ReadingUnknownRequest
                | PCP ->
                    ReadingPCPRequest BuildingEmpty
                | HTTP ->
                    ReadingHTTPRequest (None, [])
            let mutable result: ClientMessage option = None
            while Option.isNone result do
                let! read_result = pipe.Reader.ReadAsync(ct).AsTask() |> Async.AwaitTask
                let buf = read_result.Buffer
                if not buf.IsEmpty then
                    let reader =
                        match state with
                        | ReadingUnknownRequest -> initialRequestReader
                        | ReadingPCPRequest state -> pcpRequestReader state
                        | ReadingHTTPRequest (requestLine, headers) -> httpRequestReader (requestLine, headers)
                    let res = reader buf
                    pipe.Reader.AdvanceTo(res.consumed, res.examined)
                    state <- res.state
                    result <- res.message
                elif read_result.IsCompleted then
                    result <- Some ConnectionClosed
                elif read_result.IsCanceled then
                    result <- Some ConnectionCanceled
                else
                    result <- None
            return Option.get result
        }

    let readRequestAnyAsync =
        readRequestAsync Unknown

    let readHTTPRequestAsync client = 
        async {
            match! readRequestAsync HTTP client with
            | PCPAtom atom ->
                return Error $"Unexpected atom: {atom.Name}"
            | HTTPRequest req ->
                return Ok req
            | ConnectionCanceled
            | ConnectionClosed ->
                return Error "Connection closed"
        }

    let readAtomAsync client =
        async {
            match! readRequestAsync PCP client with
            | PCPAtom atom ->
                return Ok atom
            | HTTPRequest req ->
                return Error $"Unexpected http request: {req.method} {req.path}"
            | ConnectionCanceled
            | ConnectionClosed ->
                return Error "Connection closed"
        }

    let writeAtomAsync client (atom:Atom) =
        async {
            let! ct = Async.CancellationToken
            let pipe = client.pipe
            let dest = pipe.Writer.GetSpan(atom.GetLength())
            let len = atom.CopyTo(dest)
            pipe.Writer.Advance(len)
            let! result = pipe.Writer.FlushAsync(ct).AsTask() |> Async.AwaitTask
            result |> ignore
        }

    let handlePCPHandshakeAsync (helo:Atom) client =
        async {
            let pingAsync (port: int) (session_id: Guid) =
                async {
                    //TODO: 実装
                    return true
                }
            if helo.Name=Atom.PCP_HELO then
                // helo.Children.GetHeloAgent() |> Option.ofObj
                // helo.Children.GetHeloVersion() |> Option.ofNullable
                let port = Atom.getHeloPort helo.Children
                let ping = Atom.getHeloPing helo.Children
                let session_id = Atom.getHeloSessionID helo.Children
                let! remote_port =
                    async {
                        match ping, session_id with
                        | Some ping, Some session_id ->
                            let! ping_result = pingAsync ping session_id
                            if ping_result then
                                return ping
                            else
                                return port |> Option.defaultValue 0
                        | _ ->
                            return port |> Option.defaultValue 0
                    }
                let remote_ip =
                    match remoteEndPoint client with
                    | :? System.Net.IPEndPoint as ep ->
                        Some ep.Address
                    | _ ->
                        None
                do!
                    AtomCollection()
                    |> Atom.setHeloRemotePort remote_port
                    |> Option.foldBack Atom.setHeloRemoteIP remote_ip
                    |> Atom.setHeloAgent "test"
                    |> Atom.setHeloSessionID (Guid.NewGuid())
                    |> Atom.setHeloVersion PeerCastStation.PCP.PCPVersion.Default.ServantVersion
                    |> Atom.fromChildren Atom.PCP_OLEH
                    |> writeAtomAsync client
                return Ok helo
            else
                return Error $"HELO expected but {helo.Name.ToString()} arrived"
        }

type PeerCastServer =
    {
        server: TcpListener
    }
    interface IDisposable with
        member this.Dispose () =
            this.server.Stop()

let start (endPoint:System.Net.IPEndPoint) =
    let server = TcpListener(endPoint)
    server.Start()
    { server=server }

let stop server =
    server.server.Stop()

let acceptAsync server =
    PeerCastClient.acceptAsync server.server

