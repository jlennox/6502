using System;

namespace SixFiveOhTwo;

public static class StreamExtensions
{
    public static async Task ReadFully(
        this Stream stream,
        byte[] buffer, int offset, int count,
        CancellationToken cancel)
    {
        var currentOffset = offset;
        var bytesLeft = count;

        while (bytesLeft > 0)
        {
            var bytesRead = await stream.ReadAsync(buffer, currentOffset, bytesLeft, cancel);

            currentOffset += bytesRead;
            bytesLeft -= bytesRead;

            if (bytesRead == 0 && bytesLeft > 0) throw new EndOfStreamException();
        }
    }
}