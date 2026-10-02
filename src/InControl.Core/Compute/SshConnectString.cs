namespace InControl.Core.Compute;

/// <summary>
/// Reads the ssh command a rental console prints. It does not start SSH.
/// </summary>
public static class SshConnectString
{
    public static bool TryParse(string? text, out SshConnectFields? fields, out string? error)
    {
        fields = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Paste the ssh command from the rental.";
            return false;
        }

        if (text.Contains('\r') || text.Contains('\n'))
        {
            error = "The ssh command has to be one line.";
            return false;
        }

        List<string> tokens;
        try
        {
            tokens = Tokenize(text.Trim());
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }

        if (tokens.Count > 0 && string.Equals(tokens[0], "ssh", StringComparison.OrdinalIgnoreCase))
        {
            tokens.RemoveAt(0);
        }

        string? user = null;
        string? host = null;
        var port = 22;
        string? identity = null;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token is "-p")
            {
                if (!TryTakePort(tokens, ref i, out port, out error))
                {
                    return false;
                }

                continue;
            }

            if (token.StartsWith("-p", StringComparison.Ordinal) && token.Length > 2 && char.IsDigit(token[2]))
            {
                if (!int.TryParse(token[2..], out port) || port is < 1 or > 65535)
                {
                    error = "The -p value is not a port.";
                    return false;
                }

                continue;
            }

            if (token is "-i")
            {
                if (i + 1 >= tokens.Count)
                {
                    error = "The -i flag needs a key file path.";
                    return false;
                }

                identity = tokens[++i];
                continue;
            }

            if (token.StartsWith("-i", StringComparison.Ordinal) && token.Length > 2 && !token.StartsWith("-i-", StringComparison.Ordinal))
            {
                identity = token[2..];
                continue;
            }

            if (token.StartsWith('-'))
            {
                if (token is "-N" or "-n" or "-T" or "-t" or "-A" or "-a" or "-v" or "-vv" or "-vvv" or "-4" or "-6" or "-f" or "-g")
                {
                    continue;
                }

                if (token is "-o" or "-J" or "-L" or "-R" or "-W" or "-c" or "-m" or "-F" or "-l")
                {
                    if (i + 1 >= tokens.Count)
                    {
                        error = $"The {token} flag is missing its value.";
                        return false;
                    }

                    if (token == "-l")
                    {
                        user = tokens[++i];
                    }
                    else
                    {
                        i++;
                    }

                    continue;
                }

                error = $"InControl does not use the {token} flag. Paste user, host, port, and key only.";
                return false;
            }

            if (!TrySplitUserHost(token, out var parsedUser, out var parsedHost, out var parsedPort, out error))
            {
                return false;
            }

            if (host is not null)
            {
                error = "The command has more than one host.";
                return false;
            }

            user ??= parsedUser;
            host = parsedHost;
            if (parsedPort is int parsed)
            {
                port = parsed;
            }
        }

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
        {
            error = "The command needs a user@host.";
            return false;
        }

        fields = new SshConnectFields(user, host, port, identity);
        return true;
    }

    private static bool TryTakePort(List<string> tokens, ref int index, out int port, out string? error)
    {
        port = 0;
        error = null;
        if (index + 1 >= tokens.Count || !int.TryParse(tokens[index + 1], out port) || port is < 1 or > 65535)
        {
            error = "The -p value is not a port.";
            return false;
        }

        index++;
        return true;
    }

    private static bool TrySplitUserHost(
        string token,
        out string? user,
        out string? host,
        out int? port,
        out string? error)
    {
        user = null;
        host = null;
        port = null;
        error = null;

        var at = token.LastIndexOf('@');
        if (at <= 0 || at == token.Length - 1)
        {
            error = "Expected user@host from the rental's ssh command.";
            return false;
        }

        user = token[..at];
        var rest = token[(at + 1)..];
        if (rest.Contains('[', StringComparison.Ordinal))
        {
            error = "IPv6 hosts are not accepted. Use the IPv4 address or hostname from the rental.";
            return false;
        }

        var colon = rest.LastIndexOf(':');
        if (colon > 0 && colon < rest.Length - 1 && rest[(colon + 1)..].All(char.IsDigit))
        {
            if (!int.TryParse(rest[(colon + 1)..], out var parsed) || parsed is < 1 or > 65535)
            {
                error = "The host port is not a port.";
                return false;
            }

            port = parsed;
            rest = rest[..colon];
        }

        host = rest;
        return true;
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            if (text[i] == '"')
            {
                var end = text.IndexOf('"', i + 1);
                if (end < 0)
                {
                    throw new FormatException("A quote in the ssh command is not closed.");
                }

                tokens.Add(text[(i + 1)..end]);
                i = end + 1;
                continue;
            }

            var start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            tokens.Add(text[start..i]);
        }

        return tokens;
    }
}
