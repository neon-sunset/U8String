// This file contains modified code that originally appeared in
// https://ayende.com/blog/197412-B/high-performance-net-building-a-redis-clone-naively
// Permission was granted by the author to license this code under the MIT license.
// On Unix-like systems, you can benchmark this example using the following commands:
// - DOTNET_gcServer=1 dotnet run -c Release
// - memtier_benchmark –s localhost -t 8 -c 16 --test-time=30 --distinct-client-seed -d 256 --pipeline=30

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

using var listener = new TcpListener(IPAddress.Any, 6379);
var state = new ConcurrentDictionary<U8String, U8String>();

listener.Start();
while (true) {
    _ = HandleConnection(listener.AcceptSocket());
}

async Task HandleConnection(Socket socket) {
    using var _ = socket;
    using var reader = socket.AsU8Reader(disposeSource: false);

    try {
        var args = new List<U8String>();
        while (true) {
            args.Clear();
            var lineRead = await reader.ReadLineAsync();
            if (lineRead is not U8String line) break;
            if (!line.StartsWith('*')) FormatException();

            var argsv = int.Parse(line[1..]);
            for (var i = 0; i < argsv; i++) {
                line = await reader.ReadLineAsync() ?? [];

                if (!line.StartsWith('$')) FormatException();
                var argLen = int.Parse(line[1..]);

                line = await reader.ReadLineAsync() ?? [];
                if (line.Length != argLen) FormatException();
                args.Add(line);
            }
            var reply = ExecuteCommand(args);
            if (reply == null) {
                await socket.SendAsync(u8("$-1\r\n"));
            }
            else {
                // Zero-allocation interpolated socket output
                await socket.SendAsync($"${reply.Value.Length}\r\n{reply}\r\n");
            }
        }
    }
    catch (Exception e) {
        try {
            foreach (var line in u8(e.ToString()).Lines) {
                await socket.SendAsync($"-{line}\r\n");
            }
        }
        catch (Exception) {
            // nothing we can do
        }
    }
}

U8String? ExecuteCommand(List<U8String> args) {
    var cmd = args[0];
    if (cmd == "GET"u8)
        return state.TryGetValue(args[1], out var value) ? value : null;
    if (cmd == "SET"u8)
        state[args[1]] = args[2];
    else
        FormatException();

    return null;
}

[DoesNotReturn]
static void FormatException() {
    throw new FormatException();
}