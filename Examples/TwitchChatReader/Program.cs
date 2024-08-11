using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using U8.IO;

using var cts = new CancellationTokenSource();
using var sigint = PosixSignalRegistration.Create(
    PosixSignal.SIGINT, ctx => {
        ctx.Cancel = true;
        cts.Cancel();
    });
var ct = cts.Token;

var channels = args.Select(U8String.Create).ToArray();
if (channels is []) {
    U8Console.WriteLine(u8("Usage: <channel1> <channel2>..."));
    return;
}

U8Console.WriteLine($"Connecting to {U8String.Join(", "u8, channels)}...");
using var tcp = new TcpClient();
await tcp.ConnectAsync("irc.chat.twitch.tv", 6697, ct);

using var stream = new SslStream(tcp.GetStream());
await stream.AuthenticateAsClientAsync(new() { TargetHost = "irc.chat.twitch.tv" }, ct);
await stream.WriteAsync(u8("PASS SCHMOOPIIE\r\n"), ct);
await stream.WriteAsync(u8("NICK justinfan54970\r\n"), ct);
await stream.WriteAsync(u8("USER justinfan54970 8 * :justinfan54970\r\n"), ct);
foreach (var chan in channels) {
    await stream.WriteAsync($"JOIN #{chan}\r\n", ct);
}
U8Console.WriteLine(u8("Connected! To exit, press Ctrl+C."));

try {
    var lines = stream
        .ReadU8Lines(disposeSource: false)
        .WithCancellation(ct);
    await foreach (var line in lines) {
        var msg = Message.Parse(line);
        if (msg is null) continue;
        if (msg.Command == "PING"u8) {
            await stream.WriteAsync(u8("PONG :tmi.twitch.tv\r\n"));
        }
        else U8Console.WriteLine($"#{msg.Channel} {msg.Nickname}: {msg.Body}");
    }
}
catch (OperationCanceledException) { }

foreach (var chan in channels) {
    await stream.WriteAsync($"PART #{chan}\r\n");
}
U8Console.WriteLine(u8("Goodbye!"));
