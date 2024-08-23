// Simplified IRC message container for type-safe parsing and formatting.
record Message(u8str Command, u8str? Nickname, u8str? Channel, u8str? Body) {
    // The parsing logic below does not allocate, besides the Message object itself.
    public static Message? Parse(u8str line) {
        line = line.StripSuffix("\r\n"u8);
        if (line.IsEmpty) return null;
        // Skip tags
        if (line.StartsWith('@'))
            line = line[1..].SplitFirst(' ').Remainder;

        var nickname = (u8str?)null;
        if (line.StartsWith(':')) {
            (var hostmask, line) = line[1..].SplitFirst(' ');
            nickname = hostmask.SplitFirst('!').Segment;
        }

        (var command, line) = line.SplitFirst(' ');
        if (command.IsEmpty)
            throw new FormatException("Command not found in the message.");

        var channel = (u8str?)null;
        if (line.StartsWith('#')) {
            (var chan, line) = line[1..].SplitFirst(' ');
            channel = chan;
        }

        var body = !line.IsEmpty ? line.StripPrefix(':') : (u8str?)null;
        return new(command, nickname, channel, body);
    }
}
