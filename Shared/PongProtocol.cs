using System.Globalization;

namespace Pong.Protocol;

public static class PongCommands
{
    public const string Id = "ID";
    public const string Wait = "WAIT";
    public const string Room = "ROOM";
    public const string Ready = "READY";
    public const string ReadyStatus = "READY_STATUS";
    public const string Start = "START";
    public const string Update = "UPDATE";
    public const string Move = "MOVE";
    public const string Quit = "QUIT";
    public const string Over = "OVER";

    public const string Resume = "RESUME";
    public const string Reconnected = "RECONNECTED";
    public const string OpponentDisconnected = "OPPONENT_DISCONNECTED";
    public const string OpponentReconnected = "OPPONENT_RECONNECTED";
}

public readonly record struct PongMessage(string Command, string? Payload)
{
    public override string ToString() => PongWire.Format(this);
}

public static class PongWire
{
    public static bool TryParseLine(string? line, out PongMessage message)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            message = default;
            return false;
        }

        string trimmed = line.Trim();
        int sep = trimmed.IndexOf('|');
        if (sep < 0)
        {
            message = new PongMessage(trimmed, null);
            return true;
        }

        string cmd = trimmed[..sep];
        string payload = sep + 1 < trimmed.Length ? trimmed[(sep + 1)..] : string.Empty;
        message = new PongMessage(cmd, payload);
        return true;
    }

    public static string Format(PongMessage message)
    {
        if (string.IsNullOrEmpty(message.Payload))
        {
            return message.Command;
        }

        return string.Concat(message.Command, "|", message.Payload);
    }

    public static string Csv(params int[] values)
        => string.Join(',', values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    public static bool TryParseInt(string? text, out int value)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseTwoIntsCsv(string? csv, out int a, out int b)
    {
        a = 0;
        b = 0;
        if (string.IsNullOrWhiteSpace(csv)) return false;

        string[] parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;

        return TryParseInt(parts[0], out a) && TryParseInt(parts[1], out b);
    }

    public static bool TryParseSixIntsCsv(string? csv, out int a, out int b, out int c, out int d, out int e, out int f)
    {
        a = b = c = d = e = f = 0;
        if (string.IsNullOrWhiteSpace(csv)) return false;

        string[] parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 6) return false;

        return TryParseInt(parts[0], out a)
            && TryParseInt(parts[1], out b)
            && TryParseInt(parts[2], out c)
            && TryParseInt(parts[3], out d)
            && TryParseInt(parts[4], out e)
            && TryParseInt(parts[5], out f);
    }
}
