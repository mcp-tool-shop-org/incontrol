using System.Text.Json;
using System.Text.Json.Serialization;
using InControl.Core.Errors;

namespace InControl.Core.State;

/// <summary>
/// Provides deterministic serialization for application state.
/// </summary>
public static class StateSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// Serializes state to JSON string.
    /// </summary>
    public static string Serialize<T>(T state, bool compact = false)
    {
        var options = compact ? CompactOptions : SerializerOptions;
        return JsonSerializer.Serialize(state, options);
    }

    /// <summary>
    /// Serializes state to UTF-8 bytes.
    /// </summary>
    public static byte[] SerializeToBytes<T>(T state, bool compact = false)
    {
        var options = compact ? CompactOptions : SerializerOptions;
        return JsonSerializer.SerializeToUtf8Bytes(state, options);
    }

    /// <summary>
    /// Deserializes state from JSON string.
    /// </summary>
    public static Result<T> Deserialize<T>(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (result is null)
            {
                return InControlError.Create(ErrorCode.DeserializationFailed, "Deserialization returned null.");
            }
            return result;
        }
        catch (JsonException ex)
        {
            return InControlError.Create(ErrorCode.DeserializationFailed, $"Invalid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Deserializes state from UTF-8 bytes.
    /// </summary>
    public static Result<T> Deserialize<T>(ReadOnlySpan<byte> json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<T>(json, SerializerOptions);
            if (result is null)
            {
                return InControlError.Create(ErrorCode.DeserializationFailed, "Deserialization returned null.");
            }
            return result;
        }
        catch (JsonException ex)
        {
            return InControlError.Create(ErrorCode.DeserializationFailed, $"Invalid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Serializes state to a stream asynchronously.
    /// </summary>
    public static async Task SerializeAsync<T>(
        Stream stream,
        T state,
        bool compact = false,
        CancellationToken ct = default)
    {
        var options = compact ? CompactOptions : SerializerOptions;
        await JsonSerializer.SerializeAsync(stream, state, options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Deserializes state from a stream asynchronously.
    /// </summary>
    public static async Task<Result<T>> DeserializeAsync<T>(
        Stream stream,
        CancellationToken ct = default)
    {
        try
        {
            var result = await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, ct).ConfigureAwait(false);
            if (result is null)
            {
                return InControlError.Create(ErrorCode.DeserializationFailed, "Deserialization returned null.");
            }
            return result;
        }
        catch (JsonException ex)
        {
            return InControlError.Create(ErrorCode.DeserializationFailed, $"Invalid JSON: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return InControlError.Cancelled("Deserialization");
        }
    }

    /// <summary>
    /// Tests if a round-trip serialization produces equal results.
    /// </summary>
    public static bool ValidateRoundTrip<T>(T state) where T : IEquatable<T>
    {
        var json = Serialize(state);
        var result = Deserialize<T>(json);
        return result.IsSuccess && state.Equals(result.Value);
    }
}
