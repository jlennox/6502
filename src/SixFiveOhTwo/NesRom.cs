using System;

namespace SixFiveOhTwo;

[Flags]
public enum NesRomFlags6 : byte
{
    MirroringMode = 1 << 0,
    HasBattery = 1 << 1,
    HasTrainer = 1 << 2,
    HasFourScreenVram = 1 << 4
    // Then Lower nybble of mapper number
}

[Flags]
public enum NesRomFlags7 : byte
{
    VSUnisystem = 1 << 0,
    PlayChoise10 = 1 << 1,
    // 2 bits If equal to 2, flags 8-15 are in NES 2.0 format
    // Then Upper nybble of mapper number
}

[Flags]
public enum NesRomFlags9 : byte
{
    MirroringMode = 1 << 0,
    HasBattery = 1 << 1,
    HasTrainer = 1 << 2,
    HasFourScreenVram = 1 << 4
}

public enum NesMirroringMode
{
    Horizontal, Vertical
}

public enum NesTVSystem
{
    Ntsc, Pal
}

public sealed class NesRom
{
    public int Magic { get; set; }
    public int PrgRomSize { get; set; }
    public int ChrRomSize { get; set; }
    public NesRomFlags6 Flags6 { get; set; }
    public NesRomFlags7 Flags7 { get; set; }
    public byte PrgRamSize { get; set; }
    public NesRomFlags9 Flags9 { get; set; }
    public byte Flags10 { get; set; }
    public required byte[] Reserved { get; init; }

    public required byte[] PrgRom { get; init; }
    public required byte[] ChrRom { get; init; }

    public byte Mapper => (byte)(((byte)Flags6 >> 4) | ((byte)Flags7 & 0xF0));

    public NesMirroringMode MirroringMode =>
        (Flags6 & NesRomFlags6.MirroringMode) != 0
            ? NesMirroringMode.Horizontal
            : NesMirroringMode.Vertical;

    private NesRom() { }

    public static async Task<NesRom> FromFile(Stream file, CancellationToken cancel)
    {
        var contents = new byte[file.Length];
        await file.ReadFully(contents, 0, contents.Length, cancel);
        var prgSize = contents[4] * 16 * 1024;
        var chrSize = contents[5] * 8 * 1024;
        var rom = new NesRom
        {
            Magic = BitConverter.ToInt32(contents, 0),
            PrgRomSize = prgSize,
            ChrRomSize = chrSize,
            Flags6 = (NesRomFlags6)contents[6],
            Flags7 = (NesRomFlags7)contents[7],
            PrgRamSize = contents[8],
            Flags9 = (NesRomFlags9)contents[9],
            Flags10 = contents[10],
            Reserved = contents.AsSpan(11, 5).ToArray(),
            PrgRom = contents.AsSpan(16, prgSize).ToArray(),
            ChrRom = contents.AsSpan(16 + prgSize, chrSize).ToArray(),
        };

        return rom;
    }
}