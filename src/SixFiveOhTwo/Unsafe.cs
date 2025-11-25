using System;
using System.Runtime.InteropServices;

namespace SixFiveOhTwo;

// Note: This was named prior to an official Unsafe existing :)
public static unsafe class Unsafe
{
    public static void Zero(byte* buffer, int length)
    {
        var end = buffer + length;
        for (var i = buffer; i < end; ++i)
        {
            i[0] = 0;
        }
    }

    public static byte* AllocateZero(int length)
    {
        var buffer = (byte*)Marshal.AllocHGlobal(length);
        Zero(buffer, length);
        return buffer;
    }

    public static byte* Allocate(byte[] source)
    {
        var buffer = (byte*)Marshal.AllocHGlobal(source.Length);
        fixed (byte* sourcePtr = source)
        {
            for (var i = 0; i < source.Length; ++i)
            {
                buffer[i] = sourcePtr[i];
            }
        }

        return buffer;
    }

    public static byte* Allocate<T>()
        where T : unmanaged
    {
        var size = sizeof(T);
        var ptr = (byte*)Marshal.AllocHGlobal(size);
        Zero(ptr, size);

        return ptr;
    }
}
