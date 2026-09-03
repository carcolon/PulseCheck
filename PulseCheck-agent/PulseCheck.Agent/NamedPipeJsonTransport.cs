using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PulseCheck.Agent;

internal static class NamedPipeJsonTransport
{
    private const int MaxMessageBytes = 1024 * 1024;

    public static async Task WriteAsync<T>(
        PipeStream stream,
        T payload,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, jsonTypeInfo);
        if (json.Length > MaxMessageBytes)
        {
            throw new InvalidOperationException("Pipe message is too large.");
        }

        var lengthPrefix = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, json.Length);
        await stream.WriteAsync(lengthPrefix, cancellationToken);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T> ReadAsync<T>(
        PipeStream stream,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        var lengthPrefix = new byte[4];
        await ReadExactlyAsync(stream, lengthPrefix, cancellationToken);
        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthPrefix);
        if (length <= 0 || length > MaxMessageBytes)
        {
            throw new InvalidOperationException("Pipe message length is invalid.");
        }

        var buffer = new byte[length];
        await ReadExactlyAsync(stream, buffer, cancellationToken);
        return JsonSerializer.Deserialize(buffer, jsonTypeInfo)
            ?? throw new InvalidOperationException("Pipe message could not be deserialized.");
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Pipe closed before the message was fully read.");
            }

            offset += read;
        }
    }
}
