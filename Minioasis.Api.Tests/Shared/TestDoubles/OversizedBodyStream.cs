using Microsoft.AspNetCore.Http;

namespace Minioasis.Api.Tests.Shared.TestDoubles;

/// <summary>
/// A request body that fails partway through as Kestrel's over-size check does: one byte, then
/// <see cref="BadHttpRequestException"/> with status 413.
/// </summary>
public sealed class OversizedBodyStream : Stream
{
    private bool started;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (!started && buffer.Length > 0)
        {
            started = true;
            buffer[0] = (byte)'{';
            return 1;
        }

        throw new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Read(buffer.Span);
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
