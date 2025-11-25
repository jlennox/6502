using System;

namespace SixFiveOhTwo;

internal interface IMemorySegment
{
    bool TryRead(ushort address, out byte result);
}

internal readonly struct PrgMemorySegment : IMemorySegment
{
    private readonly byte[] _memory;
    private readonly int _size;

    public PrgMemorySegment(NesRom rom)
    {
        _memory = rom.PrgRom;
        _size = rom.PrgRom.Length;
    }

    public bool TryRead(ushort address, out byte result)
    {
        if (address is < 0x8000 or > 0xFFFF)
        {
            result = 0;
            return false;
        }

        // This behavior is mapper dependent, but most behave that the PRG is mirrored for the PRG address space
        // if it does not fill the full 32kb. Mirroring simulated with the modulus operation.
        var prgAddress = (address - 0x8000) % _size;
        result = _memory[prgAddress];
        return true;
    }
}

internal readonly struct WorkMemorySegment : IMemorySegment
{
    public bool TryRead(ushort address, out byte result)
    {
        // address >= 0x0000 && address <= 0x07FF
        throw new NotImplementedException();
    }
}

internal readonly struct PpuMemorySegment : IMemorySegment
{
    public bool TryRead(ushort address, out byte result)
    {
        // address >= 0x2000 && address <= 0x2007
        throw new NotImplementedException();
    }
}

internal readonly struct CartExpansionMemorySegment : IMemorySegment
{
    public bool TryRead(ushort address, out byte result)
    {
        // address >= 0x4000 && address <= 0x4017
        throw new NotImplementedException();
    }
}

internal readonly struct CartSramMemorySegment : IMemorySegment
{
    public bool TryRead(ushort address, out byte result)
    {
        // address >= 0x6000 && address <= 0x7FFF
        throw new NotImplementedException();
    }
}