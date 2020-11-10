using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SixFiveOhTwo
{
    public static unsafe class Unsafe
    {
        public static void Zero(byte* buffer, int length)
        {
            for (var i = buffer; i < buffer + length; ++i)
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
            where T : struct
        {
            var regSize = Marshal.SizeOf<T>();
            var ptr = (byte*)Marshal.AllocHGlobal(regSize);

            for (var i = 0; i < regSize; ++i)
            {
                ptr[i] = 0;
            }

            return ptr;
        }
    }

    /*public unsafe class UnsafeBuffer : IDisposable
    {
        private byte* _buffer;

        public byte this[int index]
        {
            get => _buffer[(int)index];
            set => _buffer[index] = value;
        }

        public void Dispose()
        {
            var ptr = Interlocked.Exchange(ref _buffer, (byte*)0);

            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }*/
}
