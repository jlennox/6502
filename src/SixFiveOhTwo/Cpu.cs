using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NLog;

[assembly: InternalsVisibleTo("SixFiveOhTwo.Tests")]

namespace SixFiveOhTwo;

// Some notes:
// * `if (Trace)` is always outside the "Log" call. This way the string concatenation allocation only happens if
//    tracing is enabled, instead of always happening then being silently discarded when disabled.
//    Could be improved with `InterpolatedStringHandlerArgumentAttribute`?
// * The excessive `[MethodImpl(MethodImplOptions.AggressiveInlining)]` usage was from an earlier version of .net
//   where it was very bad about inlining properties. It's worth testing if that's still the case.

internal struct Registers
{
    public byte Accumulator;
    public byte IndexX;
    public byte IndexY;
}

public sealed unsafe class Cpu : IDisposable
{
    private const int _opcodeMask = 0b111_000_00;

    public static bool Trace { get; set; }

    private readonly Logger _log = LogManager.GetCurrentClassLogger();

    #region Registers
    private readonly Registers* _registersPtr;

    private readonly byte* _aPtr;
    private readonly byte* _xPtr;
    private readonly byte* _yPtr;
    private ushort _pc;
    private byte _sp;
    private byte _ps;

    public byte Accumulator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _registersPtr->Accumulator;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _registersPtr->Accumulator = value;
            SetZeroFlag(value);
            SetNegativeFlag(value);
        }
    }

    public int AccumulatorWithCarry
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Accumulator + CarryValue;
    }

    public int CarryValue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CarryFlag ? 1 : 0;
    }

    public int CarryValueHigh
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CarryFlag ? 0x80 : 0;
    }

    public byte IndexX
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _registersPtr->IndexX;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _registersPtr->IndexX = value;
            SetZeroFlag(value);
            SetNegativeFlag(value);
        }
    }

    public byte IndexY
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _registersPtr->IndexY;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            _registersPtr->IndexY = value;
            SetZeroFlag(value);
            SetNegativeFlag(value);
        }
    }

    public ushort ProgramCounter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pc;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _pc = value;
    }

    public byte StackPointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _sp;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _sp = value;
    }

    private ushort StackPointerAbsolute
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (ushort)(_sp | 0x0100);
    }

    public byte ProcessorStatus
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _ps;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _ps = (byte)(value | _unusedFlag);
    }
    #endregion

    #region Flags
    private const byte _carryFlag = 1 << 0;
    private const byte _zeroFlag = 1 << 1;
    private const byte _irqDisableFlag = 1 << 2;
    private const byte _decimalModeFlag = 1 << 3;
    private const byte _breakFlag = 1 << 4;
    private const byte _unusedFlag = 1 << 5;
    private const byte _overflowFlag = 1 << 6;
    private const byte _negativeFlag = 1 << 7;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetFlag(byte flag) => (ProcessorStatus & flag) == flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetFlag(byte flag, bool set)
    {
        ProcessorStatus = set
            ? (byte)(ProcessorStatus | flag)
            : (byte)(ProcessorStatus & ~flag);
    }

    public bool CarryFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_carryFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_carryFlag, value);
    }

    public bool ZeroFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_zeroFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_zeroFlag, value);
    }

    public bool IrqDisableFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_irqDisableFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_irqDisableFlag, value);
    }

    public bool DecimalModeFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_decimalModeFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_decimalModeFlag, value);
    }

    public bool BreakFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_breakFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_breakFlag, value);
    }

    public bool UnusedFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => true;

        // TODO JOE 11/2025: Hmmm :) Check the docs if not using `value` is actually correct.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => ProcessorStatus |= _unusedFlag;
    }

    public bool OverflowFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_overflowFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_overflowFlag, value);
    }

    public bool NegativeFlag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetFlag(_negativeFlag);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetFlag(_negativeFlag, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetZeroFlag(byte value)
    {
        ZeroFlag = value == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetNegativeFlag(byte diff)
    {
        NegativeFlag = (diff & 0b1000_0000) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetOverflowFlag(byte diff)
    {
        OverflowFlag = (diff & 0b0100_0000) != 0;
    }
    #endregion

    #region Memory
    private readonly NesRom _rom;
    private readonly PrgMemorySegment _prg;
    private readonly byte* _memory;
    private readonly byte* _constantValues;
    #endregion

    private bool _hasDisposed = false;

    public Cpu(NesRom rom)
    {
        _prg = new PrgMemorySegment(rom);
        _rom = rom;

        UnusedFlag = true;

        _memory = Unsafe.AllocateZero(0xFFFF);

        _constantValues = (byte*)Marshal.AllocHGlobal(0xFF);

        for (var i = 0; i <= 0xFF; ++i)
        {
            _constantValues[i] = (byte)i;
        }

        _registersPtr = (Registers*)Unsafe.Allocate<Registers>();

        _aPtr = &_registersPtr->Accumulator;
        _xPtr = &_registersPtr->IndexX;
        _yPtr = &_registersPtr->IndexY;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _hasDisposed, true)) return;

        // TODO: A bit more safety on these.
        Marshal.FreeHGlobal((IntPtr)_memory);
        Marshal.FreeHGlobal((IntPtr)_constantValues);
        Marshal.FreeHGlobal((IntPtr)_registersPtr);
    }

    private bool GetPtr(
        byte opcode,
        out byte* memory,
        out ushort ptr,
        bool writeAccess = false)
    {
        // These opcodes index based on Y instead of X.
        // 96 100_101_10 nn     ------  4   STX nn,Y    MOV [nn+Y],X        ;[nn+Y]=X
        // B6 101_101_10 nn     nz----  4   LDX nn,Y    MOV X,[nn+Y]        ;X=[nn+Y]
        // BE 101_111_10 nn nn  nz----  4*  LDX nnnn,Y  MOV X,[nnnn+Y]      ;X=[nnnn+Y]
        // 97 100_101_11 nn     ------  4   SAX nn,Y    STA+STX  [nn+Y]=A AND X
        var switchingIndex = IndexX;

        switch (opcode >> 1)
        {
            case 0b100_101_1:
            case 0b101_101_1:
            case 0b101_111_1:
                switchingIndex = IndexY;
                break;
        }

        switch (opcode & 0b000_111_11)
        {
            case 0b000_000_01: // [WORD[nn+X]]
            case 0b000_000_11: { // [WORD[nn+X]]
                memory = _memory;
                var srcPtr = (ushort)((GetImmediate8() + IndexX) & 0xFF);
                ptr = ReadMemory(srcPtr);
                ptr |= (ushort)(ReadMemory((byte)(srcPtr + 1)) << 8);
                _pc += 2;
                break;
            }
            case 0b000_001_00: // [nn]
            case 0b000_001_01: // [nn]
            case 0b000_001_10: // [nn]
            case 0b000_001_11: // [nn]
                memory = _memory;
                ptr = GetImmediate8();
                _pc += 2;
                break;
            case 0b000_000_00: // nn
            case 0b000_010_01: // nn
            case 0b000_000_10: // nn
            case 0b000_010_11: // nn
                if (writeAccess) throw new Exception();

                memory = _constantValues;
                ptr = GetImmediate8();
                _pc += 2;
                break;
            case 0b000_011_00: // [nnnn]
            case 0b000_011_01: // [nnnn]
            case 0b000_011_10: // [nnnn]
            case 0b000_011_11: // [nnnn]
                memory = _memory;
                ptr = GetImmediate16();
                _pc += 3;
                break;
            case 0b000_100_01:
            case 0b000_100_11: { // [WORD[nn]+Y]
                memory = _memory;
                var srcPtr = GetImmediate8();
                ptr = ReadMemory(srcPtr);
                ptr |= (ushort)(ReadMemory((byte)(srcPtr + 1)) << 8);
                ptr += IndexY;
                _pc += 2;
                break;
            }
            case 0b000_101_00: // [nn+X]
            case 0b000_101_01: // [nn+X]
            case 0b000_101_10: // [nn+X]
            case 0b000_101_11: // [nn+X]
                memory = _memory;
                ptr = (byte)((GetImmediate8() + switchingIndex) & 0xFF);
                _pc += 2;
                break;
            case 0b000_110_01:
            case 0b000_110_11:// [nnnn+Y]
                memory = _memory;
                ptr = GetImmediate16();
                ptr = (ushort)((ptr + IndexY) & 0xFFFF);
                _pc += 3;
                break;
            case 0b000_111_00: // [nnnn+X]
            case 0b000_111_01: // [nnnn+X]
            case 0b000_111_10:
            case 0b000_111_11:// [nnnn+X]
                memory = _memory;
                ptr = GetImmediate16();
                ptr = (ushort)((ptr + switchingIndex) & 0xFFFF);
                _pc += 3;
                break;
            case 0b000_010_10: // A
                ptr = 0;
                memory = _aPtr;
                ++_pc;
                break;
            case 0b000_010_00:
            case 0b000_100_00:
            case 0b000_110_00:
            case 0b000_100_10:
            case 0b000_110_10:
            default:
                memory = null;
                ptr = 0;
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte GetImmediate8() => ReadMemory(_pc + 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ushort GetImmediate16() => (ushort)(ReadMemory(_pc + 1) | (ReadMemory(_pc + 2) << 8));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ProcessStatusDescription(byte pc)
    {
        return ((pc & _carryFlag) != 0 ? "C" : " ") +
            ((pc & _zeroFlag) != 0 ? "Z" : " ") +
            ((pc & _irqDisableFlag) != 0 ? "I" : " ") +
            ((pc & _decimalModeFlag) != 0 ? "D" : " ") +
            ((pc & _breakFlag) != 0 ? "B" : " ") +
            ((pc & _unusedFlag) != 0 ? "-" : " ") +
            ((pc & _overflowFlag) != 0 ? "V" : " ") +
            ((pc & _negativeFlag) != 0 ? "N" : " ");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string ProcessStatusDescription() => ProcessStatusDescription(_ps);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Log(string s)
    {
        _log.Debug($"{ProgramCounter:X4} A:{Accumulator:X2} X:{IndexX:X2} Y:{IndexY:X2} P:{_ps:X2} ({ProcessStatusDescription()}) SP:{_sp:X2} " + s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadMemoryUnlogged(byte* memory, ushort addr)
    {
        if (_prg.TryRead(addr, out var result)) return result;

        return memory[addr];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadMemory(byte* memory, ushort addr)
    {
        if (Trace && memory == _memory) Log($"ReadMemory({addr:X4}) = {memory[addr]:X2}");

        return ReadMemoryUnlogged(memory, addr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadMemory(ushort addr) => ReadMemory(_memory, addr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte ReadMemoryUnlogged(ushort addr) => ReadMemoryUnlogged(_memory, addr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadMemory(int addr) => ReadMemory((ushort)addr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteMemory(byte* memory, ushort addr, byte value)
    {
        if (memory == _constantValues) throw new Exception();

        if (Trace && memory == _memory) Log($"WriteMemory({addr:X4}, {value:X2}) (was {memory[addr]:X2})");

        memory[addr] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteMemory(ushort addr, byte value) => WriteMemory(_memory, addr, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ConditionalRelativeJump(bool condition)
    {
        if (!condition)
        {
            _pc += 2;
            return 2;
        }

        var oldPc = ProgramCounter;
        var relative = GetImmediate8();
        _pc = (ushort)(_pc + (sbyte)relative + 2);
        return (oldPc & 0xFF00) == (ProgramCounter & 0xFF00) ? 3 : 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Compare(byte* memory, ushort ptr, byte register)
    {
        var val = ReadMemory(memory, ptr);
        var diff = register - val;
        SetNegativeFlag((byte)diff);
        SetZeroFlag((byte)diff);
        CarryFlag = diff >= 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidOpcode(byte opcode)
    {
        if (Trace) Log($"InvalidOpcode({opcode:X2})");
    }

    public void Execute(out int ticks)
    {
        ticks = 0;
        var opcode = ReadMemory(ProgramCounter);

        if (Trace) Log($"Execute({opcode:X2})");

        switch (opcode)
        {
            case 0b010_011_00:
                // 4C 010_011_00 nn nn  ------  3   JMP nnnn     JMP nnnn                 ;PC=nnnn
                ProgramCounter = GetImmediate16();
                return;
            case 0b011_011_00: {
                // 6C 011_011_00 nn nn  ------  5   JMP (nnnn)   JMP [nnnn]               ;PC=WORD[nnnn]
                // Glitch: For JMP [nnnn] the operand word cannot cross page boundaries, ie. JMP [03FFh] would fetch the MSB from [0300h] instead of [0400h]. Very simple workaround would be to place a ALIGN 2 before the data word.
                var operand = GetImmediate16();
                ushort addr = ReadMemory(operand);
                var highaddr = (operand & 0xFF00) | ((operand + 1) & 0xFF);
                addr |= (ushort)(ReadMemory((ushort)highaddr) << 8);
                ProgramCounter = addr;
                return;
            }
            case 0b001_000_00:
                // 20 001_000_00 nn nn  ------  6   JSR nnnn     CALL nnnn                ;[S]=PC+2,PC=nnnn
                var pc = ProgramCounter + 2;
                WriteMemory(StackPointerAbsolute, (byte)(pc >> 8));
                --StackPointer;
                WriteMemory(StackPointerAbsolute, (byte)(pc & 0xFF));
                --StackPointer;
                ProgramCounter = GetImmediate16();
                return;
            case 0b010_000_00: {
                // 40 010_000_00        nzcidv  6   RTI          RETI ;(from BRK/IRQ/NMI) ;P=[S], PC=[S]
                // Note: RTI cannot modify the B-Flag or the unused flag.
                ++StackPointer;
                ProcessorStatus = ReadMemory(StackPointerAbsolute);
                ++StackPointer;
                var pcl = ReadMemory(StackPointerAbsolute);
                ++StackPointer;
                var pch = ReadMemory(StackPointerAbsolute);
                ProgramCounter = (ushort)(pcl | (pch << 8));
                return;
            }
            case 0b011_000_00: {
                // 60 011_000_00        ------  6   RTS          RET  ;(from CALL)        ;PC=[S]+1
                ++StackPointer;
                var pcl = ReadMemory(StackPointerAbsolute);
                ++StackPointer;
                var pch = ReadMemory(StackPointerAbsolute);
                ProgramCounter = (ushort)(pcl | (pch << 8));
                ++_pc;
                return;
            }
            case 0b111_010_00: {
                // E8 111_010_00        nz----  2   INX         INC X               ;X=X+1
                ++IndexX;
                ++_pc;
                return;
            }
            case 0b110_010_00: {
                // C8 110_010_00        nz----  2   INY         INC Y               ;Y=Y+1
                ++IndexY;
                ++_pc;
                return;
            }
            case 0b110_010_10: {
                // CA 110_010_10        nz----  2   DEX         DEC X               ;X=X-1
                --IndexX;
                ++_pc;
                return;
            }
            case 0b100_010_00: {
                // 88 100_010_00        nz----  2   DEY         DEC Y               ;Y=Y-1
                --IndexY;
                ++_pc;
                return;
            }
            case 0b111_010_10:
                // EA 111_010_10        ------  2   NOP       NOP    ;No operation
                ++_pc;
                return;
            case 0b000_110_00:
            case 0b001_110_00:
                // 18        --0---  2   CLC       CLC    ;Clear carry flag            C=0
                // 38        --1---  2   SEC       STC    ;Set carry flag              C=1
                CarryFlag = (opcode & 0b001_000_00) != 0;
                ++_pc;
                return;
            case 0b010_110_00:
            case 0b011_110_00:
                // 58        ---0--  2   CLI       EI     ;Clear interrupt disable bit I=0
                // 78        ---1--  2   SEI       DI     ;Set interrupt disable bit   I=1
                IrqDisableFlag = (opcode & 0b001_000_00) != 0;
                ++_pc;
                return;
            case 0b110_110_00:
            case 0b111_110_00:
                // D8        ----0-  2   CLD       CLD    ;Clear decimal mode          D=0
                // F8        ----1-  2   SED       STD    ;Set decimal mode            D=1
                DecimalModeFlag = (opcode & 0b001_000_00) != 0;
                ++_pc;
                return;
            case 0b101_110_00:
                // B8        -----0  2   CLV       CLV    ;Clear overflow flag         V=0
                OverflowFlag = false;
                ++_pc;
                return;
            // Conditional Branches (conditional jump to PC=PC+/-dd)
            // 10 000_100_00 dd     ------  2** BPL nnn      JNS nnn     ;N=0 plus/positive
            // 30 001_100_00 dd     ------  2** BMI nnn      JS  nnn     ;N=1 minus/negative/signed
            // 50 010_100_00 dd     ------  2** BVC nnn      JNO nnn     ;V=0 no overflow
            // 70 011_100_00 dd     ------  2** BVS nnn      JO  nnn     ;V=1 overflow
            // 90 100_100_00 dd     ------  2** BCC/BLT nnn  JNC/JB  nnn ;C=0 less/below/no carry
            // B0 101_100_00 dd     ------  2** BCS/BGE nnn  JC/JAE  nnn ;C=1 above/greater/equal/carry
            // D0 110_100_00 dd     ------  2** BNE/BZC nnn  JNZ/JNE nnn ;Z=0 not zero/not equal
            // F0 111_100_00 dd     ------  2** BEQ/BZS nnn  JZ/JE   nnn ;Z=1 zero/equal
            // ** The_ ex_ecution time is 2 cycles if the condition is false (no branch executed). Otherwise, 3 cycles if the destination is in the same memory page, or 4 cycles if it crosses a page boundary (see below for exact info).
            case 0b000_100_00:
                ConditionalRelativeJump(!NegativeFlag);
                return;
            case 0b001_100_00:
                ConditionalRelativeJump(NegativeFlag);
                return;
            case 0b010_100_00:
                ConditionalRelativeJump(!OverflowFlag);
                return;
            case 0b011_100_00:
                ConditionalRelativeJump(OverflowFlag);
                return;
            case 0b100_100_00:
                ConditionalRelativeJump(!CarryFlag);
                return;
            case 0b101_100_00:
                ConditionalRelativeJump(CarryFlag);
                return;
            case 0b110_100_00:
                ConditionalRelativeJump(!ZeroFlag);
                return;
            case 0b111_100_00:
                ConditionalRelativeJump(ZeroFlag);
                return;
            // Push/Pull
            // 48 010_010_00        ------  3   PHA         PUSH A              ;[S]=A, S=S-1
            // 08 000_010_00        ------  3   PHP         PUSH P              ;[S]=P, S=S-1 (flags)
            // 68 011_010_00        nz----  4   PLA         POP  A              ;S=S+1, A=[S]
            // 28 001_010_00        nzcidv  4   PLP         POP  P              ;S=S+1, P=[S] (flags)
            case 0b010_010_00:
                WriteMemory(StackPointerAbsolute, Accumulator);
                --StackPointer;
                ++_pc;
                return;
            case 0b000_010_00:
                WriteMemory(StackPointerAbsolute,
                    (byte)(ProcessorStatus | _breakFlag));
                --StackPointer;
                ++_pc;
                return;
            case 0b011_010_00:
                ++StackPointer;
                Accumulator = ReadMemory(StackPointerAbsolute);
                ++_pc;
                return;
            case 0b001_010_00:
                ++StackPointer;
                var ps = ReadMemory(StackPointerAbsolute);
                ProcessorStatus = (byte)(ps & ~_breakFlag);
                ++_pc;
                return;
            // Register / Immeditate to Register Transfer
            // A8 101_010_00        nz----  2   TAY         MOV Y,A             ;Y=A
            // AA 101_010_10        nz----  2   TAX         MOV X,A             ;X=A
            // BA 101_110_10        nz----  2   TSX         MOV X,S             ;X=S
            // 98 100_110_00        nz----  2   TYA         MOV A,Y             ;A=Y
            // 8A 100_010_10        nz----  2   TXA         MOV A,X             ;A=X
            // 9A 100_110_10        ------  2   TXS         MOV S,X             ;S=X
            // A0 101_000_00 nn     nz----  2   LDY #nn     MOV Y,nn            ;Y=nn
            case 0b101_010_00:
                IndexY = Accumulator;
                ++_pc;
                return;
            case 0b101_010_10:
                IndexX = Accumulator;
                ++_pc;
                return;
            case 0b101_110_10:
                IndexX = StackPointer;
                ++_pc;
                return;
            case 0b100_110_00:
                Accumulator = IndexY;
                ++_pc;
                return;
            case 0b100_010_10:
                Accumulator = IndexX;
                ++_pc;
                return;
            case 0b100_110_10:
                StackPointer = IndexX;
                ++_pc;
                return;
            case 0b101_000_00:
                IndexY = GetImmediate8();
                _pc += 2;
                return;
            // Other Illegal Opcodes
            // 0B nn     nzc---  2  ANC #nn          AND+ASL  A=A AND nn, C=N ;bit7 to carry
            // 2B nn     nzc---  2  ANC #nn          AND+ROL  A=A AND nn, C=N ;same as above
            // 4B nn     nzc---  2  ALR #nn          AND+LSR  A=(A AND nn) SHR 1
            // 6B nn     nzc--v  2  ARR #nn          AND+ROR  A=(A AND nn), V=Overflow(A+A),
            //                                                A=A/2+C*80h, C=A.Bit6
            // CB nn     nzc---  2  AXS #nn          CMP+DEX  X=(X AND A)-nn
            // EB nn     nzc--v  2  SBC #nn          SBC+NOP  A=A-nn         cy?
            // BB nn nn  nz----  4* LAS nnnn,Y       LDA+TSX  A,X,S = [nnnn+Y] AND S
            case 0x0B: InvalidOpcode(opcode); _pc += 2; return;
            case 0x2B: InvalidOpcode(opcode); _pc += 2; return;
            case 0x4B: InvalidOpcode(opcode); _pc += 2; return;
            case 0x6B: InvalidOpcode(opcode); _pc += 2; return;
            case 0xCB: InvalidOpcode(opcode); _pc += 2; return;
            //case 0xEB: InvalidOpcode(opcode); _pc += 2; return;
            case 0xBB: InvalidOpcode(opcode); _pc += 3; return;
        }

        // TODO: Fix `writeAccess` argument.
        var valid = GetPtr(opcode, out var memory, out var ptr);

        if (!valid)
        {
            ++_pc;
            return;
        }

        var operandInformation = (opcode >> 2) & 0b111;

        if ((opcode & 0b11) == 0)
        {
            // operandInformation patterns 0b010, 0b100, 0b110 are filtered
            // out by the return result of GetPtr.
            switch (opcode & _opcodeMask)
            {
                case 0b001_000_00: {
                    // Bit Test
                    // 24 001_001_00 nn     xz---x  3   BIT nn      TEST A,[nn]         ;test and set flags
                    // 2C 001_011_00 nn nn  xz---x  4   BIT nnnn    TEST A,[nnnn]       ;test and set flags
                    // Flags are set as so: Z=((A AND [addr])=00h), N=[addr].Bit7, V=[addr].Bit6. Note that N and V are affected only by [addr] (not by A).
                    if (operandInformation is 0b000 or 0b101 or 0b111) goto endOf00;

                    var val = ReadMemory(memory, ptr);
                    ZeroFlag = (Accumulator & val) == 0;
                    SetNegativeFlag(val);
                    SetOverflowFlag(val);
                    break;
                }
                case 0b100_000_00: {
                    // 84 100_001_00 nn     ------  3   STY nn      MOV [nn],Y          ;[nn]=Y
                    // 94 100_101_00 nn     ------  4   STY nn,X    MOV [nn+X],Y        ;[nn+X]=Y
                    // 8C 100_011_00 nn nn  ------  4   STY nnnn    MOV [nnnn],Y        ;[nnnn]=Y
                    if (operandInformation is 0b000 or 0b111) goto endOf00;

                    WriteMemory(memory, ptr, IndexY);
                    break;
                }
                case 0b101_000_00: {
                    // Load Register from Memory
                    // A4 101_001_00 nn     nz----  3   LDY nn      MOV Y,[nn]          ;Y=[nn]
                    // B4 101_101_00 nn     nz----  4   LDY nn,X    MOV Y,[nn+X]     g   ;Y=[nn+X]
                    // AC 101_011_00 nn nn  nz----  4   LDY nnnn    MOV Y,[nnnn]        ;Y=[nnnn]
                    // BC 101_111_00 nn nn  nz----  4*  LDY nnnn,X  MOV Y,[nnnn+X]      ;Y=[nnnn+X]
                    // * Add one cycle if indexing crosses a page boundary.
                    if (operandInformation is 0b000) goto endOf00;

                    var val = ReadMemory(memory, ptr);
                    IndexY = val;
                    break;
                }
                case 0b110_000_00: {
                    // C0 110_000_00 nn     nzc---  2   CPY #nn     CMP Y,nn            ;Y-nn
                    // C4 110_001_00 nn     nzc---  3   CPY nn      CMP Y,[nn]          ;Y-[nn]
                    // CC 110_011_00 nn nn  nzc---  4   CPY nnnn    CMP Y,[nnnn]        ;Y-[nnnn]
                    if (operandInformation is 0b101 or 0b111) goto endOf00;

                    Compare(memory, ptr, IndexY);
                    break;
                }
                case 0b111_000_00: {
                    // E0 111_000_00 nn     nzc---  2   CPX #nn     CMP X,nn            ;X-nn
                    // E4 111_001_00 nn     nzc---  3   CPX nn      CMP X,[nn]          ;X-[nn]
                    // EC 111_011_00 nn nn  nzc---  4   CPX nnnn    CMP X,[nnnn]        ;X-[nnnn]
                    if (operandInformation is 0b101 or 0b111) goto endOf00;

                    Compare(memory, ptr, IndexX);
                    break;
                }
            }

            endOf00:;
        }

        if ((opcode & 0b11) == 0b11)
        {
            InvalidOpcode(opcode);

            switch (opcode & _opcodeMask)
            {
                // SAX is special cased because the &'ing effect is a
                // natural product of NMOS.
                case 0b100_000_00: {
                    // CPU Illegal Opcodes
                    // SAX
                    // 87 nn     ------  3   SAX nn      STA+STX  [nn]=A AND X
                    // 97 nn     ------  4   SAX nn,Y    STA+STX  [nn+Y]=A AND X
                    // 8F nn nn  ------  4   SAX nnnn    STA+STX  [nnnn]=A AND X
                    // 83 nn     ------  6   SAX (nn,X)  STA+STX  [WORD[nn+X]]=A AND X
                    WriteMemory(memory, ptr, (byte)(Accumulator & IndexX));
                    return;
                }
            }
        }

        if ((opcode & 0b10) != 0)
        {
            switch (opcode & _opcodeMask)
            {
                case 0b000_000_00: {
                    // Shift Left Logical/Arithmetic
                    // 0A 000_010_10        nzc---  2   ASL A       SHL A               ;SHL A
                    // 06 000_001_10 nn     nzc---  5   ASL nn      SHL [nn]            ;SHL [nn]
                    // 16 000_101_10 nn     nzc---  6   ASL nn,X    SHL [nn+X]          ;SHL [nn+X]
                    // 0E 000_011_10 nn nn  nzc---  6   ASL nnnn    SHL [nnnn]          ;SHL [nnnn]
                    // 1E 000_111_10 nn nn  nzc---  7   ASL nnnn,X  SHL [nnnn+X]        ;SHL [nnnn+X]
                    var val = ReadMemory(memory, ptr);
                    var sum = (byte)((val << 1) & 0xFF);
                    WriteMemory(memory, ptr, sum);
                    ZeroFlag = sum == 0;
                    NegativeFlag = (sum & 0x80) != 0;
                    CarryFlag = (val & 0x80) != 0;
                    break;
                }
                case 0b001_000_00: {
                    // Rotate Left through Carry
                    // 2A 001_010_10        nzc---  2   ROL A        RCL A              ;RCL A
                    // 26 001_001_10 nn     nzc---  5   ROL nn       RCL [nn]           ;RCL [nn]
                    // 36 001_101_10 nn     nzc---  6   ROL nn,X     RCL [nn+X]         ;RCL [nn+X]
                    // 2E 001_011_10 nn nn  nzc---  6   ROL nnnn     RCL [nnnn]         ;RCL [nnnn]
                    // 3E 001_111_10 nn nn  nzc---  7   ROL nnnn,X   RCL [nnnn+X]       ;RCL [nnnn+X]
                    var val = ReadMemory(memory, ptr);
                    var sum = (byte)(((val << 1) | CarryValue) & 0xFF);
                    WriteMemory(memory, ptr, sum);
                    ZeroFlag = sum == 0;
                    NegativeFlag = (sum & 0x80) != 0;
                    CarryFlag = (val & 0x80) != 0;
                    break;
                }
                case 0b010_000_00: {
                    // Shift Right Logical
                    // 4A 010_010_10        0zc---  2   LSR A       SHR A               ;SHR A
                    // 46 010_001_10 nn     0zc---  5   LSR nn      SHR [nn]            ;SHR [nn]
                    // 56 010_101_10 nn     0zc---  6   LSR nn,X    SHR [nn+X]          ;SHR [nn+X]
                    // 4E 010_011_10 nn nn  0zc---  6   LSR nnnn    SHR [nnnn]          ;SHR [nnnn]
                    // 5E 010_111_10 nn nn  0zc---  7   LSR nnnn,X  SHR [nnnn+X]        ;SHR [nnnn+X]
                    var val = ReadMemory(memory, ptr);
                    var sum = (byte)(val >> 1);
                    WriteMemory(memory, ptr, sum);
                    ZeroFlag = sum == 0;
                    NegativeFlag = false;
                    CarryFlag = (val & 0x01) != 0;
                    break;
                }
                case 0b011_000_00: {
                    // Rotate Right through Carry
                    // 6A 011_010_10        nzc---  2   ROR A        RCR A              ;RCR A
                    // 66 011_001_10 nn     nzc---  5   ROR nn       RCR [nn]           ;RCR [nn]
                    // 76 011_101_10 nn     nzc---  6   ROR nn,X     RCR [nn+X]         ;RCR [nn+X]
                    // 6E 011_011_10 nn nn  nzc---  6   ROR nnnn     RCR [nnnn]         ;RCR [nnnn]
                    // 7E 011_111_10 nn nn  nzc---  7   ROR nnnn,X   RCR [nnnn+X]       ;RCR [nnnn+X]
                    // 
                    // Notes:
                    // ROR instruction is available on MCS650X microprocessors after June, 1976.
                    // ROL and ROR rotate an 8bit value through carry (rotates 9bits in total).
                    var val = ReadMemory(memory, ptr);
                    var sum = (byte)((val >> 1) | CarryValueHigh);
                    WriteMemory(memory, ptr, sum);
                    ZeroFlag = sum == 0;
                    NegativeFlag = (sum & 0x80) != 0;
                    CarryFlag = (val & 0x01) != 0;
                    break;
                }
                case 0b100_000_00: {
                    // Store Register in Memory
                    // 86 100_001_10 nn     ------  3   STX nn      MOV [nn],X          ;[nn]=X
                    // 96 100_101_10 nn     ------  4   STX nn,Y    MOV [nn+Y],X        ;[nn+Y]=X
                    // 8E 100_011_10 nn nn  ------  4   STX nnnn    MOV [nnnn],X        ;[nnnn]=X
                    WriteMemory(memory, ptr, IndexX);
                    break;
                }
                case 0b101_000_00: {
                    // Load Register from Memory
                    // A2 101_000_10 nn     nz----  2   LDX #nn     MOV X,nn            ;X=nn
                    // A6 101_001_10 nn     nz----  3   LDX nn      MOV X,[nn]          ;X=[nn]
                    // B6 101_101_10 nn     nz----  4   LDX nn,Y    MOV X,[nn+Y]        ;X=[nn+Y]
                    // AE 101_011_10 nn nn  nz----  4   LDX nnnn    MOV X,[nnnn]        ;X=[nnnn]
                    // BE 101_111_10 nn nn  nz----  4*  LDX nnnn,Y  MOV X,[nnnn+Y]      ;X=[nnnn+Y]
                    IndexX = ReadMemory(memory, ptr);
                    break;
                }
                case 0b110_000_00: {
                    // Decrement by one
                    // C6 110_001_10 nn     nz----  5   DEC nn      DEC [nn]            ;[nn]=[nn]-1
                    // D6 110_101_10 nn     nz----  6   DEC nn,X    DEC [nn+X]          ;[nn+X]=[nn+X]-1
                    // CE 110_011_10 nn nn  nz----  6   DEC nnnn    DEC [nnnn]          ;[nnnn]=[nnnn]-1
                    // DE 110_111_10 nn nn  nz----  7   DEC nnnn,X  DEC [nnnn+X]        ;[nnnn+X]=[nnnn+X]-1

                    switch (operandInformation)
                    {
                        case 0b010: goto endOf10;
                    }

                    var val = ReadMemory(memory, ptr);
                    var diff = --val;
                    SetNegativeFlag(diff);
                    SetZeroFlag(diff);
                    WriteMemory(memory, ptr, diff);
                    break;
                }
                case 0b111_000_00: {
                    // Increment by one
                    // E6 111_001_10 nn     nz----  5   INC nn      INC [nn]            ;[nn]=[nn]+1
                    // F6 111_101_10 nn     nz----  6   INC nn,X    INC [nn+X]          ;[nn+X]=[nn+X]+1
                    // EE 111_011_10 nn nn  nz----  6   INC nnnn    INC [nnnn]          ;[nnnn]=[nnnn]+1
                    // FE 111_111_10 nn nn  nz----  7   INC nnnn,X  INC [nnnn+X]        ;[nnnn+X]=[nnnn+X]+1

                    // EA 111_010_10 is nop. This flow through happens with
                    // the illegal SBC call EB 111_010_11
                    switch (operandInformation)
                    {
                        case 0b010: goto endOf10;
                    }

                    var val = ReadMemory(memory, ptr);
                    var diff = ++val;
                    SetNegativeFlag(diff);
                    SetZeroFlag(diff);
                    WriteMemory(memory, ptr, diff);
                    break;
                }
            }

            endOf10:;
        }

        if ((opcode & 0b01) != 0)
        {
            switch (opcode & _opcodeMask)
            {
                case 0b000_000_00:
                    // Logical OR memory with accumulator
                    // 09 000_010_01 nn     nz----  2   ORA #nn     OR  A,nn            ;A=A OR nn
                    // 05 000_001_01 nn     nz----  3   ORA nn      OR  A,[nn]          ;A=A OR [nn]
                    // 15 000_101_01 nn     nz----  4   ORA nn,X    OR  A,[nn+X]        ;A=A OR [nn+X]
                    // 0D 000_011_01 nn nn  nz----  4   ORA nnnn    OR  A,[nnnn]        ;A=A OR [nnnn]
                    // 1D 000_111_01 nn nn  nz----  4*  ORA nnnn,X  OR  A,[nnnn+X]      ;A=A OR [nnnn+X]
                    // 19 000_110_01 nn nn  nz----  4*  ORA nnnn,Y  OR  A,[nnnn+Y]      ;A=A OR [nnnn+Y]
                    // 01 000_000_01 nn     nz----  6   ORA (nn,X)  OR  A,[[nn+X]]      ;A=A OR [word[nn+X]]
                    // 11 000_100_01 nn     nz----  5*  ORA (nn),Y  OR  A,[[nn]+Y]      ;A=A OR [word[nn]+Y]
                    // * Add one cycle if indexing crosses a page boundary.
                    Accumulator = (byte)(Accumulator | ReadMemory(memory, ptr));
                    break;
                case 0b001_000_00:
                    // Logical AND memory with accumulator
                    // 29 001_010_01 nn     nz----  2   AND #nn     AND A,nn            ;A=A AND nn
                    // 25 001_001_01 nn     nz----  3   AND nn      AND A,[nn]          ;A=A AND [nn]
                    // 35 001_101_01 nn     nz----  4   AND nn,X    AND A,[nn+X]        ;A=A AND [nn+X]
                    // 2D 001_011_01 nn nn  nz----  4   AND nnnn    AND A,[nnnn]        ;A=A AND [nnnn]
                    // 3D 001_111_01 nn nn  nz----  4*  AND nnnn,X  AND A,[nnnn+X]      ;A=A AND [nnnn+X]
                    // 39 001_110_01 nn nn  nz----  4*  AND nnnn,Y  AND A,[nnnn+Y]      ;A=A AND [nnnn+Y]
                    // 21 001_000_01 nn     nz----  6   AND (nn,X)  AND A,[[nn+X]]      ;A=A AND [word[nn+X]]
                    // 31 001_100_01 nn     nz----  5*  AND (nn),Y  AND A,[[nn]+Y]      ;A=A AND [word[nn]+Y]
                    // * Add one cycle if indexing crosses a page boundary.
                    Accumulator = (byte)(Accumulator & ReadMemory(memory, ptr));
                    break;
                case 0b010_000_00:
                    // Exclusive-OR memory with accumulator
                    // 49 010_010_01 nn     nz----  2   EOR #nn     XOR A,nn            ;A=A XOR nn
                    // 45 010_001_01 nn     nz----  3   EOR nn      XOR A,[nn]          ;A=A XOR [nn]
                    // 55 010_101_01 nn     nz----  4   EOR nn,X    XOR A,[nn+X]        ;A=A XOR [nn+X]
                    // 4D 010_011_01 nn nn  nz----  4   EOR nnnn    XOR A,[nnnn]        ;A=A XOR [nnnn]
                    // 5D 010_111_01 nn nn  nz----  4*  EOR nnnn,X  XOR A,[nnnn+X]      ;A=A XOR [nnnn+X]
                    // 59 010_110_01 nn nn  nz----  4*  EOR nnnn,Y  XOR A,[nnnn+Y]      ;A=A XOR [nnnn+Y]
                    // 41 010_000_01 nn     nz----  6   EOR (nn,X)  XOR A,[[nn+X]]      ;A=A XOR [word[nn+X]]
                    // 51 010_100_01 nn     nz----  5*  EOR (nn),Y  XOR A,[[nn]+Y]      ;A=A XOR [word[nn]+Y]
                    // * Add one cycle if indexing crosses a page boundary.
                    Accumulator = (byte)(Accumulator ^ ReadMemory(memory, ptr));
                    break;
                case 0b011_000_00: {
                    // Add memory to accumulator with carry
                    // 69 011_010_01 nn     nzc--v  2   ADC #nn     ADC A,nn            ;A=A+C+nn
                    // 65 011_001_01 nn     nzc--v  3   ADC nn      ADC A,[nn]          ;A=A+C+[nn]
                    // 75 011_101_01 nn     nzc--v  4   ADC nn,X    ADC A,[nn+X]        ;A=A+C+[nn+X]
                    // 6D 011_011_01 nn nn  nzc--v  4   ADC nnnn    ADC A,[nnnn]        ;A=A+C+[nnnn]
                    // 7D 011_111_01 nn nn  nzc--v  4*  ADC nnnn,X  ADC A,[nnnn+X]      ;A=A+C+[nnnn+X]
                    // 79 011_110_01 nn nn  nzc--v  4*  ADC nnnn,Y  ADC A,[nnnn+Y]      ;A=A+C+[nnnn+Y]
                    // 61 011_000_01 nn     nzc--v  6   ADC (nn,X)  ADC A,[[nn+X]]      ;A=A+C+[word[nn+X]]
                    // 71 011_100_01 nn     nzc--v  5*  ADC (nn),Y  ADC A,[[nn]+Y]      ;A=A+C+[word[nn]+Y]
                    // * Add one cycle if indexing crosses a page boundary.
                    var val = ReadMemory(memory, ptr);
                    var acc = Accumulator;
                    var sum = acc + val + CarryValue;
                    Accumulator = (byte)(sum & 0xFF);
                    CarryFlag = sum > 0xFF;
                    var signage = (val ^ acc) >> 7;
                    OverflowFlag = signage == 0 && ((val ^ Accumulator) & 0x80) == 0x80;
                    break;
                }
                case 0b100_000_00:
                    // Store Register in Memory
                    // 85 100_001_01 nn     ------  3   STA nn      MOV [nn],A          ;[nn]=A
                    // 95 100_101_01 nn     ------  4   STA nn,X    MOV [nn+X],A        ;[nn+X]=A
                    // 8D 100_011_01 nn nn  ------  4   STA nnnn    MOV [nnnn],A        ;[nnnn]=A
                    // 9D 100_111_01 nn nn  ------  5   STA nnnn,X  MOV [nnnn+X],A      ;[nnnn+X]=A
                    // 99 100_110_01 nn nn  ------  5   STA nnnn,Y  MOV [nnnn+Y],A      ;[nnnn+Y]=A
                    // 81 100_000_01 nn     ------  6   STA (nn,X)  MOV [[nn+x]],A      ;[WORD[nn+x]]=A
                    // 91 100_100_01 nn     ------  6   STA (nn),Y  MOV [[nn]+y],A      ;[WORD[nn]+y]=A
                    // * Add one cycle if indexing crosses a page boundary.
                    WriteMemory(memory, ptr, Accumulator);
                    break;
                case 0b101_000_00:
                    // Load Register from Memory
                    // A9 101_010_01 nn     nz----  2   LDA #nn     MOV A,nn            ;A=nn
                    // A5 101_001_01 nn     nz----  3   LDA nn      MOV A,[nn]          ;A=[nn]
                    // B5 101_101_01 nn     nz----  4   LDA nn,X    MOV A,[nn+X]        ;A=[nn+X]
                    // AD 101_011_01 nn nn  nz----  4   LDA nnnn    MOV A,[nnnn]        ;A=[nnnn]
                    // BD 101_111_01 nn nn  nz----  4*  LDA nnnn,X  MOV A,[nnnn+X]      ;A=[nnnn+X]
                    // B9 101_110_01 nn nn  nz----  4*  LDA nnnn,Y  MOV A,[nnnn+Y]      ;A=[nnnn+Y]
                    // A1 101_000_01 nn     nz----  6   LDA (nn,X)  MOV A,[[nn+X]]      ;A=[WORD[nn+X]]
                    // B1 101_100_01 nn     nz----  5*  LDA (nn),Y  MOV A,[[nn]+Y]      ;A=[WORD[nn]+Y]
                    Accumulator = ReadMemory(memory, ptr);
                    break;
                case 0b110_000_00: {
                    // Compare
                    // C9 110_010_01 nn     nzc---  2   CMP #nn     CMP A,nn            ;A-nn
                    // C5 110_001_01 nn     nzc---  3   CMP nn      CMP A,[nn]          ;A-[nn]
                    // D5 110_101_01 nn     nzc---  4   CMP nn,X    CMP A,[nn+X]        ;A-[nn+X]
                    // CD 110_011_01 nn nn  nzc---  4   CMP nnnn    CMP A,[nnnn]        ;A-[nnnn]
                    // DD 110_111_01 nn nn  nzc---  4*  CMP nnnn,X  CMP A,[nnnn+X]      ;A-[nnnn+X]
                    // D9 110_110_01 nn nn  nzc---  4*  CMP nnnn,Y  CMP A,[nnnn+Y]      ;A-[nnnn+Y]
                    // C1 110_000_01 nn     nzc---  6   CMP (nn,X)  CMP A,[[nn+X]]      ;A-[word[nn+X]]
                    // D1 110_100_01 nn     nzc---  5*  CMP (nn),Y  CMP A,[[nn]+Y]      ;A-[word[nn]+Y]
                    // Note: Compared with normal 80x86 and Z80 CPUs, resulting Carry Flag is reversed.
                    Compare(memory, ptr, Accumulator);
                    break;
                }
                case 0b111_000_00: {
                    // Subtract memory from accumulator with borrow
                    // E9 111_010_01 nn     nzc--v  2   SBC #nn     SBC A,nn            ;A=A+C-1-nn
                    // E5 111_001_01 nn     nzc--v  3   SBC nn      SBC A,[nn]          ;A=A+C-1-[nn]
                    // F5 111_101_01 nn     nzc--v  4   SBC nn,X    SBC A,[nn+X]        ;A=A+C-1-[nn+X]
                    // ED 111_011_01 nn nn  nzc--v  4   SBC nnnn    SBC A,[nnnn]        ;A=A+C-1-[nnnn]
                    // FD 111_111_01 nn nn  nzc--v  4*  SBC nnnn,X  SBC A,[nnnn+X]      ;A=A+C-1-[nnnn+X]
                    // F9 111_110_01 nn nn  nzc--v  4*  SBC nnnn,Y  SBC A,[nnnn+Y]      ;A=A+C-1-[nnnn+Y]
                    // E1 111_000_01 nn     nzc--v  6   SBC (nn,X)  SBC A,[[nn+X]]      ;A=A+C-1-[word[nn+X]]
                    // F1 111_100_01 nn     nzc--v  5*  SBC (nn),Y  SBC A,[[nn]+Y]      ;A=A+C-1-[word[nn]+Y]
                    // * Add one cycle if indexing crosses a page boundary.
                    // Note: Compared with normal 80x86 and Z80 CPUs, incoming and resulting Carry Flag are reversed.

                    // Illegal
                    // EB 111_010_11 nn     nzc--v  2  SBC #nn          SBC+NOP  A=A-nn         cy?
                    var val = ReadMemory(memory, ptr);
                    var acc = Accumulator;
                    var diff = AccumulatorWithCarry - 1 - val;
                    Accumulator = (byte)(diff & 0xFF);
                    CarryFlag = diff >= 0;
                    var signage = (val ^ acc) >> 7;
                    OverflowFlag = signage == 1 && ((val ^ Accumulator) & 0x80) == 0x00;
                    break;
                }
            }
        }
    }
}