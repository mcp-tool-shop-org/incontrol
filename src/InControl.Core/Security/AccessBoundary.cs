namespace InControl.Core.Security;

/// <summary>
/// Scheme, host, and port compared on parsed URIs. Uri.Host is the host, so userinfo is not another host.
/// A path matches only when it is equal or the next character is a slash. An empty pattern matches nothing.
/// </summary>
internal static class EndpointPattern
{
    public static bool Covers(string? pattern, string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(endpoint))
            return false;

        if (!TryParseHttp(pattern, out var patternUri) || !string.IsNullOrEmpty(patternUri.UserInfo))
            return false;

        if (!TryParseHttp(endpoint, out var endpointUri))
            return false;

        if (!string.Equals(patternUri.Scheme, endpointUri.Scheme, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(patternUri.Host, endpointUri.Host, StringComparison.OrdinalIgnoreCase))
            return false;

        if (patternUri.Port != endpointUri.Port)
            return false;

        return PathCovers(patternUri.AbsolutePath, endpointUri.AbsolutePath);
    }

    /// <summary>
    /// Longer path is more specific. An origin with no path scores zero. Unusable patterns score below that.
    /// </summary>
    public static int Specificity(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || !TryParseHttp(pattern, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
            return -1;

        return uri.AbsolutePath.TrimEnd('/').Length;
    }

    private static bool TryParseHttp(string value, out Uri uri)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri!))
            return false;

        return uri.IsAbsoluteUri
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrEmpty(uri.Host);
    }

    private static bool PathCovers(string patternPath, string endpointPath)
    {
        var pattern = patternPath.TrimEnd('/');
        if (pattern.Length == 0)
            return true;

        var endpoint = endpointPath.TrimEnd('/');
        if (string.Equals(endpoint, pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        return endpointPath.StartsWith(pattern + "/", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Full path, then the root itself or the root plus a directory separator. A sibling name does not match.
/// </summary>
internal static class PathBoundary
{
    public static bool IsInside(string? candidate, string? root)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(root))
            return false;

        try
        {
            var full = Path.GetFullPath(candidate);
            var rootFull = Path.GetFullPath(root);
            if (string.Equals(Trim(full), Trim(rootFull), StringComparison.OrdinalIgnoreCase))
                return true;

            return full.StartsWith(WithSeparator(rootFull), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    private static string WithSeparator(string path)
    {
        var trimmed = Trim(path);
        if (trimmed.EndsWith(Path.DirectorySeparatorChar) || trimmed.EndsWith(Path.AltDirectorySeparatorChar))
            return trimmed;

        return trimmed + Path.DirectorySeparatorChar;
    }

    private static string Trim(string path)
    {
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) && string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
            return path;

        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
