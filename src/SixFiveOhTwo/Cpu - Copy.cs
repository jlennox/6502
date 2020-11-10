using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixFiveOhTwo
{
    [Flags]
    enum ProcessorStatusRegister
    {
        Carry = 0x1
    }

    public unsafe class Cpu
    {
        private const byte _carryFlag = 1 << 0;
        private const byte _zeroFlag = 1 << 1;
        private const byte _irqDisableFlag = 1 << 2;
        private const byte _decimalModeFlag = 1 << 3;
        private const byte _breakFlag = 1 << 4;
        private const byte _unusedFlag = 1 << 5;
        private const byte _overflowFlag = 1 << 6;
        private const byte _negativeFlag = 1 << 7;

        private byte _a;
        private byte _x;
        private byte _y;
        private short _pc;
        private byte _sp;
        private byte _ps;

        public byte Accumulator
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _a;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _a = value;
                SetZeroFlag(value);
                SetNegativeFlag(value);
            }
        }

        public int AccumulatorWithCarry
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _a + (CarryFlag ? 0xFF : 0);
        }

        public byte IndexX
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _x;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _x = value;
                SetZeroFlag(value);
                SetNegativeFlag(value);
            }
        }

        public byte IndexY
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _y;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _y = value;
                SetZeroFlag(value);
                SetNegativeFlag(value);
            }
        }

        public short ProgramCounter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pc;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _pc = value;
                ZeroFlag = value == 0;
                // TODO: flags?
            }
        }

        public byte StackPointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _sp;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                _sp = value;
                SetZeroFlag(value);
                SetNegativeFlag(value);
            }
        }

        public byte ProcessorStatus;

        public bool CarryFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _carryFlag) == _carryFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _carryFlag)
                : (byte)(ProcessorStatus & ~_carryFlag);
        }

        public bool ZeroFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _zeroFlag) == _zeroFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _zeroFlag)
                : (byte)(ProcessorStatus & ~_zeroFlag);
        }

        public bool IrqDisableFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _irqDisableFlag) == _irqDisableFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _irqDisableFlag)
                : (byte)(ProcessorStatus & ~_irqDisableFlag);
        }

        public bool DecimalModeFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _decimalModeFlag) == _decimalModeFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _decimalModeFlag)
                : (byte)(ProcessorStatus & ~_decimalModeFlag);
        }

        public bool BreakFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _breakFlag) == _breakFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _breakFlag)
                : (byte)(ProcessorStatus & ~_breakFlag);
        }

        public bool UnusedFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => true;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus |= _unusedFlag;
        }

        public bool OverflowFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _overflowFlag) == _overflowFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _overflowFlag)
                : (byte)(ProcessorStatus & ~_overflowFlag);
        }

        public bool NegativeFlag
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (ProcessorStatus & _negativeFlag) == _negativeFlag;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ProcessorStatus = value
                ? (byte)(ProcessorStatus | _negativeFlag)
                : (byte)(ProcessorStatus & ~_negativeFlag);
        }

        public Cpu()
        {
            UnusedFlag = true;

            Memory = (byte*)Marshal.AllocHGlobal(0xFFFF);
            /*WorkMemory = (byte*)Marshal.AllocHGlobal(0x07FF - 0x0000);
            PpuMemory = (byte*)Marshal.AllocHGlobal(0x2007 - 0x2000);
            ApuRegisters = (byte*)Marshal.AllocHGlobal(0x4017 - 0x4000);
            CartExpansionArea = (byte*)Marshal.AllocHGlobal(0x5FFF - 0x4018);
            CartSram = (byte*)Marshal.AllocHGlobal(0x7FFF - 0x6000);
            CartPrgRom = (byte*)Marshal.AllocHGlobal(0xFFFF - 0x8000);*/
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetZeroFlag(byte value)
        {
            ZeroFlag = value != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetNegativeFlag(byte value)
        {
            NegativeFlag = (value & 0x80) != 0;
        }

        public byte* Memory;

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte* MemoryRegionFromAddress(int address)
        {
            if (address >= 0x0000 && address <= 0x07FF)
            {
                return WorkMemory;
            }
            else if (address >= 0x2000 && address <= 0x2007)
            {
                return PpuMemory;
            }
            else if (address >= 0x4000 && address <= 0x4017)
            {
                return ApuRegisters;
            }
            else if (address >= 0x4018 && address <= 0x5FFF)
            {
                return CartExpansionArea;
            }
            else if (address >= 0x6000 && address <= 0x7FFF)
            {
                return CartSram;
            }
            else if (address >= 0x8000 && address <= 0xFFFF)
            {
                return CartPrgRom;
            }

            throw new Exception();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte* MemoryRegionFromAddress(int address, int offset)
        {
            if (address >= 0x0000 && address <= 0x07FF - offset)
            {
                return WorkMemory;
            }
            else if (address >= 0x2000 && address <= 0x2007 - offset)
            {
                return PpuMemory;
            }
            else if (address >= 0x4000 && address <= 0x4017 - offset)
            {
                return ApuRegisters;
            }
            else if (address >= 0x4018 && address <= 0x5FFF - offset)
            {
                return CartExpansionArea;
            }
            else if (address >= 0x6000 && address <= 0x7FFF - offset)
            {
                return CartSram;
            }
            else if (address >= 0x8000 && address <= 0xFFFF - offset)
            {
                return CartPrgRom;
            }

            throw new Exception();
        }*/

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadInt8(byte address)
        {
            return Memory[address];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadInt8(byte addressA, byte addressB)
        {
            // Implied inbounds by byte arguments.
            var address = addressA | (byte)(addressB << 8);
            return Memory[address];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadInt8(byte addressA, byte addressB, byte offset)
        {
            var address = addressA | (byte)(addressB << 8);
            // 
            return Memory[address & 0xFFFF];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadInt16(int address)
        {
            var region = MemoryRegionFromAddress(address, 1);

            return region[address] | region[address + 1];
        }*/

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetZeroPagePtr(byte addr)
        {
            return &Memory[addr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetZeroPagePtr(byte addr, byte offset)
        {
            return &Memory[(addr + offset) & 0xFF];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetAbsolutePtr(byte addra, byte addrb)
        {
            var addr = addra + (addrb << 8);
            return &Memory[addr & 0xFFFF];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetAbsolutePtr(byte addra, byte addrb, byte offset)
        {
            var addr = addra + (addrb << 8);
            addr += offset;
            return &Memory[addr & 0xFFFF];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetMemoryPtr(byte addr, byte addrb, byte offset)
        {
            // TODO: does it wrap?
            return &Memory[(addr + addrb + offset) & 0xFFFF];
        }

        public void SetAdditionFlags(int sum)
        {
            CarryFlag = sum > 0xFF;
            OverflowFlag = sum > 0x80;
        }

        public void SetSubtractionToAccumulator(int diff)
        {
            Accumulator = (byte)(diff < 0 ? (0xFF - diff) : diff);
            CarryFlag = diff > 0xFF;
            OverflowFlag = diff < -0x80;
        }

        public void ExecuteUnsafe(byte* instructions, int length)
        {
            var ip = instructions;
            var end = instructions + length;

            while (ip >= instructions && ip < end)
            {
                var ticks = 0;

                switch (*ip)
                {
                    // Register/Immeditate to Register Transfer
                    case 0xA8: // A8        nz----  2   TAY         MOV Y,A             ;Y=A
                        IndexY = IndexX;
                        SetNegativeFlag(IndexY);
                        SetZeroFlag(IndexY);
                        ticks = 2;
                        ++ip;
                        continue;
                    case 0xAA: // AA        nz----  2   TAX         MOV X,A             ;X=A
                        IndexX = Accumulator;
                        SetNegativeFlag(IndexX);
                        SetZeroFlag(IndexX);
                        ticks = 2;
                        ++ip;
                        continue;
                    case 0xBA: // BA        nz----  2   TSX         MOV X,S             ;X=S
                        IndexX = StackPointer;
                        SetNegativeFlag(IndexX);
                        SetZeroFlag(IndexX);
                        ticks = 2;
                        ++ip;
                        continue;
                    case 0x98: // 98        nz----  2   TYA         MOV A,Y             ;A=Y
                        Accumulator = IndexY;
                        SetNegativeFlag(Accumulator);
                        SetZeroFlag(Accumulator);
                        ticks = 2;
                        ++ip;
                        continue;
                    case 0x8A: // 8A        nz----  2   TXA         MOV A,X             ;A=X
                        Accumulator = IndexX;
                        SetNegativeFlag(Accumulator);
                        SetZeroFlag(Accumulator);
                        ticks = 2;
                        ++ip;
                        continue;
                    case 0x9A: // 9A        ------  2   TXS         MOV S,X             ;S=X
                        StackPointer = IndexX;
                        SetNegativeFlag(StackPointer);
                        SetZeroFlag(StackPointer);
                        ticks = 2;
                        ip += 2;
                        continue;
                    case 0xA9: // A9 nn     nz----  2   LDA #nn     MOV A,nn            ;A=nn
                        Accumulator = ip[1];
                        SetNegativeFlag(Accumulator);
                        SetZeroFlag(Accumulator);
                        ticks = 2;
                        ip += 2;
                        continue;
                    case 0xA2: // A2 nn     nz----  2   LDX #nn     MOV X,nn            ;X=nn
                        IndexX = ip[1];
                        SetNegativeFlag(IndexX);
                        SetZeroFlag(IndexX);
                        ticks = 2;
                        ip += 2;
                        continue;
                    case 0xA0: // A0 nn     nz----  2   LDY #nn     MOV Y,nn            ;Y=nn
                        IndexY = ip[1];
                        SetNegativeFlag(IndexY);
                        SetZeroFlag(IndexY);
                        ticks = 2;
                        ip += 2;
                        continue;

                    // Load Register from Memory
                    case 0xA5: { // A5 nn     nz----  3   LDA nn      MOV A,[nn]          ;A=[nn]
                        var ptr = GetZeroPagePtr(ip[1]);
                        Accumulator = *ptr;
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0xB5: { // B5 nn     nz----  4   LDA nn,X    MOV A,[nn+X]        ;A=[nn+X]
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        Accumulator = *ptr;
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0xAD: { // AD nn nn  nz----  4   LDA nnnn    MOV A,[nnnn]        ;A=[nnnn]
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        Accumulator = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0xBD: { // BD nn nn  nz----  4*  LDA nnnn,X  MOV A,[nnnn+X]      ;A=[nnnn+X]
                        // TODO: Increase cycle if indexing crosses a page boundary.
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexX);
                        Accumulator = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0xB9: { // B9 nn nn  nz----  4*  LDA nnnn,Y  MOV A,[nnnn+Y]      ;A=[nnnn+Y]
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexY);
                        Accumulator = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0xA1: { // A1 nn     nz----  6   LDA (nn,X)  MOV A,[[nn+X]]      ;A=[WORD[nn+X]]
                        // TODO: Is this zero page?
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        ptr = GetZeroPagePtr(*ptr);
                        Accumulator = *ptr;
                        ticks = 6;
                        ip += 2;
                        continue;
                    }
                    case 0xB1: { // B1 nn     nz----  5*  LDA (nn),Y  MOV A,[[nn]+Y]      ;A=[WORD[nn]+Y]
                        var ptr = GetZeroPagePtr(ip[1]);
                        ptr = GetZeroPagePtr(*ptr, IndexY);
                        Accumulator = *ptr;
                        ticks = 5;
                        ip += 2;
                        continue;
                    }
                    case 0xA6: { // A6 nn     nz----  3   LDX nn      MOV X,[nn]          ;X=[nn]
                        var ptr = GetZeroPagePtr(ip[1]);
                        IndexX = *ptr;
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0xB6: { // B6 nn     nz----  4   LDX nn,Y    MOV X,[nn+Y]        ;X=[nn+Y]
                        var ptr = GetZeroPagePtr(ip[1], IndexY);
                        IndexX = *ptr;
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0xAE: { // AE nn nn  nz----  4   LDX nnnn    MOV X,[nnnn]        ;X=[nnnn]
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        IndexX = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0xBE: { // BE nn nn  nz----  4*  LDX nnnn,Y  MOV X,[nnnn+Y]      ;X=[nnnn+Y]
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexY);
                        IndexX = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0xA4: { // A4 nn     nz----  3   LDY nn      MOV Y,[nn]          ;Y=[nn]
                        var ptr = GetZeroPagePtr(ip[1]);
                        IndexY = *ptr;
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0xB4: { // B4 nn     nz----  4   LDY nn,X    MOV Y,[nn+X]        ;Y=[nn+X]
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        IndexY = *ptr;
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0xAC: { // AC nn nn  nz----  4   LDY nnnn    MOV Y,[nnnn]        ;Y=[nnnn]
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        IndexY = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0xBC: { // BC nn nn  nz----  4*  LDY nnnn,X  MOV Y,[nnnn+X]      ;Y=[nnnn+X]
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexX);
                        IndexY = *ptr;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    // * Add one cycle if indexing crosses a page boundary.

                    // Store Register in Memory
                    case 0x85: { // 85 nn     ------  3   STA nn      MOV [nn],A          ;[nn]=A
                        var ptr = GetZeroPagePtr(ip[1]);
                        ptr[0] = Accumulator;
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0x95: { // 95 nn     ------  4   STA nn,X    MOV [nn+X],A        ;[nn+X]=A
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        ptr[0] = Accumulator;
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x8D: { // 8D nn nn  ------  4   STA nnnn    MOV [nnnn],A        ;[nnnn]=A
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        ptr[0] = Accumulator;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0x9D: { // 9D nn nn  ------  5   STA nnnn,X  MOV [nnnn+X],A      ;[nnnn+X]=A
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexX);
                        ptr[0] = Accumulator;
                        ticks = 5;
                        ip += 3;
                        continue;
                    }
                    case 0x99: { // 99 nn nn  ------  5   STA nnnn,Y  MOV [nnnn+Y],A      ;[nnnn+Y]=A
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexY);
                        ptr[0] = Accumulator;
                        ticks = 5;
                        ip += 3;
                        continue;
                    }
                    case 0x81: { // 81 nn     ------  6   STA (nn,X)  MOV [[nn+x]],A      ;[WORD[nn+x]]=A
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        ptr = GetZeroPagePtr(*ptr);
                        ptr[0] = Accumulator;
                        ticks = 6;
                        ip += 2;
                        continue;
                    }
                    case 0x91: { // 91 nn     ------  6   STA (nn),Y  MOV [[nn]+y],A      ;[WORD[nn]+y]=A
                        var ptr = GetZeroPagePtr(ip[1]);
                        ptr = GetZeroPagePtr(*ptr, IndexY);
                        ptr[0] = Accumulator;
                        ticks = 6;
                        ip += 2;
                        continue;
                    }
                    case 0x86: { // 86 nn     ------  3   STX nn      MOV [nn],X          ;[nn]=X
                        var ptr = GetZeroPagePtr(ip[1]);
                        ptr[0] = IndexX;
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0x96: { // 96 nn     ------  4   STX nn,Y    MOV [nn+Y],X        ;[nn+Y]=X
                        var ptr = GetZeroPagePtr(ip[1], IndexY);
                        ptr[0] = IndexX;
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x8E: { // 8E nn nn  ------  4   STX nnnn    MOV [nnnn],X        ;[nnnn]=X
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        ptr[0] = IndexX;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }
                    case 0x84: { // 84 nn     ------  3   STY nn      MOV [nn],Y          ;[nn]=Y
                        var ptr = GetZeroPagePtr(ip[1]);
                        ptr[0] = IndexY;
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0x94: { // 94 nn     ------  4   STY nn,X    MOV [nn+X],Y        ;[nn+X]=Y
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        ptr[0] = IndexY;
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x8C: { // 8C nn nn  ------  4   STY nnnn    MOV [nnnn],Y        ;[nnnn]=Y
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        ptr[0] = IndexY;
                        ticks = 4;
                        ip += 3;
                        continue;
                    }

                    // Push/Pull
                    case 0x48: { // 48        ------  3   PHA         PUSH A              ;[S]=A, S=S-1
                        var ptr = GetZeroPagePtr(StackPointer);
                        ptr[0] = Accumulator;
                        --_sp; // TODO: Wrapping behavior?
                        ticks = 3;
                        ip += 1;
                        continue;
                    }
                    case 0x08: { // 08        ------  3   PHP         PUSH P              ;[S]=P, S=S-1 (flags)
                        var ptr = GetZeroPagePtr(StackPointer);
                        ptr[0] = _ps;
                        --_sp;
                        ticks = 3;
                        ip += 1;
                        continue;
                    }
                    case 0x68: { // 68        nz----  4   PLA         POP  A              ;S=S+1, A=[S]
                        ++StackPointer;
                        var ptr = GetZeroPagePtr(StackPointer);
                        Accumulator = *ptr;
                        ticks = 4;
                        ip += 1;
                        continue;
                    }
                    case 0x28: { // 28        nzcidv  4   PLP         POP  P              ;S=S+1, P=[S] (flags)
                        ++StackPointer;
                        var ptr = GetZeroPagePtr(StackPointer);
                        _pc = (byte)(*ptr | _unusedFlag);
                        ticks = 4;
                        ip += 1;
                        continue;
                    }
                    // Notes: PLA sets Z and N according to content of A. The B-flag and unused flags cannot be changed by PLP, these flags are always written as "1" by PHP.

                    // Add memory to accumulator with carry
                    case 0x69: { // 69 nn     nzc--v  2   ADC #nn     ADC A,nn            ;A=A+C+nn
                        var sum = Accumulator + ip[1];
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 2;
                        ip += 2;
                        continue;
                    }
                    case 0x65: { // 65 nn     nzc--v  3   ADC nn      ADC A,[nn]          ;A=A+C+[nn]
                        var ptr = GetZeroPagePtr(ip[1]);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0x75: { // 75 nn     nzc--v  4   ADC nn,X    ADC A,[nn+X]        ;A=A+C+[nn+X]
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x6D: { // 6D nn nn  nzc--v  4   ADC nnnn    ADC A,[nnnn]        ;A=A+C+[nnnn]
                        var ptr = GetAbsolutePtr(ip[1], ip[2]);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x7D: { // 7D nn nn  nzc--v  4*  ADC nnnn,X  ADC A,[nnnn+X]      ;A=A+C+[nnnn+X]
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexX);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x79: { // 79 nn nn  nzc--v  4*  ADC nnnn,Y  ADC A,[nnnn+Y]      ;A=A+C+[nnnn+Y]
                        var ptr = GetAbsolutePtr(ip[1], ip[2], IndexY);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 4;
                        ip += 2;
                        continue;
                    }
                    case 0x61: { // 61 nn     nzc--v  6   ADC (nn,X)  ADC A,[[nn+X]]      ;A=A+C+[word[nn+X]]
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        ptr = GetZeroPagePtr(*ptr);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 6;
                        ip += 2;
                        continue;
                    }
                    case 0x71: { // 71 nn     nzc--v  5*  ADC (nn),Y  ADC A,[[nn]+Y]      ;A=A+C+[word[nn]+Y]
                        var ptr = GetZeroPagePtr(ip[1]);
                        ptr = GetZeroPagePtr(*ptr, IndexX);
                        var sum = Accumulator + *ptr;
                        Accumulator = (byte)(sum & 0xFF);
                        SetAdditionFlags(sum);
                        ticks = 5;
                        ip += 2;
                        continue;
                    }
                    // * Add one cycle if indexing crosses a page boundary.

                    // Subtract memory from accumulator with borrow
                    case 0xE9: { // E9 nn     nzc--v  2   SBC #nn     SBC A,nn            ;A=A+C-1-nn
                        var diff = AccumulatorWithCarry - 1 - ip[1];
                        SetSubtractionToAccumulator(diff);
                        ticks = 2;
                        ip += 2;
                        continue;
                    }
                    case 0xE5: { // E5 nn     nzc--v  3   SBC nn      SBC A,[nn]          ;A=A+C-1-[nn]
                        var ptr = GetZeroPagePtr(ip[1]);
                        var diff = AccumulatorWithCarry - 1 - *ptr;
                        SetSubtractionToAccumulator(diff);
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0xF5: { // F5 nn     nzc--v  4   SBC nn,X    SBC A,[nn+X]        ;A=A+C-1-[nn+X]
                        var ptr = GetZeroPagePtr(ip[1], IndexX);
                        var diff = AccumulatorWithCarry - 1 - *ptr;
                        SetSubtractionToAccumulator(diff);
                        ticks = 3;
                        ip += 2;
                        continue;
                    }
                    case 0xED: // ED nn nn  nzc--v  4   SBC nnnn    SBC A,[nnnn]        ;A=A+C-1-[nnnn]
                    case 0xFD: // FD nn nn  nzc--v  4*  SBC nnnn,X  SBC A,[nnnn+X]      ;A=A+C-1-[nnnn+X]
                    case 0xF9: // F9 nn nn  nzc--v  4*  SBC nnnn,Y  SBC A,[nnnn+Y]      ;A=A+C-1-[nnnn+Y]
                    case 0xE1: // E1 nn     nzc--v  6   SBC (nn,X)  SBC A,[[nn+X]]      ;A=A+C-1-[word[nn+X]]
                    case 0xF1: // F1 nn     nzc--v  5*  SBC (nn),Y  SBC A,[[nn]+Y]      ;A=A+C-1-[word[nn]+Y]
                               // *Add one cycle if indexing crosses a page boundary.
                               // Note:
                               // Compared with normal 80x86 and Z80 CPUs, incoming and resulting Carry Flag are reversed.

                    // Logical AND memory with accumulator
                    case 0x29: // 29 nn     nz----  2   AND #nn     AND A,nn            ;A=A AND nn
                    case 0x25: // 25 nn     nz----  3   AND nn      AND A,[nn]          ;A=A AND [nn]
                    case 0x35: // 35 nn     nz----  4   AND nn,X    AND A,[nn+X]        ;A=A AND [nn+X]
                    case 0x2D: // 2D nn nn  nz----  4   AND nnnn    AND A,[nnnn]        ;A=A AND [nnnn]
                    case 0x3D: // 3D nn nn  nz----  4*  AND nnnn,X  AND A,[nnnn+X]      ;A=A AND [nnnn+X]
                    case 0x39: // 39 nn nn  nz----  4*  AND nnnn,Y  AND A,[nnnn+Y]      ;A=A AND [nnnn+Y]
                    case 0x21: // 21 nn     nz----  6   AND (nn,X)  AND A,[[nn+X]]      ;A=A AND [word[nn+X]]
                    case 0x31: // 31 nn     nz----  5*  AND (nn),Y  AND A,[[nn]+Y]      ;A=A AND [word[nn]+Y]
                               // * Add one cycle if indexing crosses a page boundary.

                    // Exclusive-OR memory with accumulator
                    case 0x49: // 49 nn     nz----  2   EOR #nn     XOR A,nn            ;A=A XOR nn
                    case 0x45: // 45 nn     nz----  3   EOR nn      XOR A,[nn]          ;A=A XOR [nn]
                    case 0x55: // 55 nn     nz----  4   EOR nn,X    XOR A,[nn+X]        ;A=A XOR [nn+X]
                    case 0x4D: // 4D nn nn  nz----  4   EOR nnnn    XOR A,[nnnn]        ;A=A XOR [nnnn]
                    case 0x5D: // 5D nn nn  nz----  4*  EOR nnnn,X  XOR A,[nnnn+X]      ;A=A XOR [nnnn+X]
                    case 0x59: // 59 nn nn  nz----  4*  EOR nnnn,Y  XOR A,[nnnn+Y]      ;A=A XOR [nnnn+Y]
                    case 0x41: // 41 nn     nz----  6   EOR (nn,X)  XOR A,[[nn+X]]      ;A=A XOR [word[nn+X]]
                    case 0x51: // 51 nn     nz----  5*  EOR (nn),Y  XOR A,[[nn]+Y]      ;A=A XOR [word[nn]+Y]
                               // * Add one cycle if indexing crosses a page boundary.

                    // Logical OR memory with accumulator
                    case 0x09: // 09 nn     nz----  2   ORA #nn     OR  A,nn            ;A=A OR nn
                    case 0x05: // 05 nn     nz----  3   ORA nn      OR  A,[nn]          ;A=A OR [nn]
                    case 0x15: // 15 nn     nz----  4   ORA nn,X    OR  A,[nn+X]        ;A=A OR [nn+X]
                    case 0x0D: // 0D nn nn  nz----  4   ORA nnnn    OR  A,[nnnn]        ;A=A OR [nnnn]
                    case 0x1D: // 1D nn nn  nz----  4*  ORA nnnn,X  OR  A,[nnnn+X]      ;A=A OR [nnnn+X]
                    case 0x19: // 19 nn nn  nz----  4*  ORA nnnn,Y  OR  A,[nnnn+Y]      ;A=A OR [nnnn+Y]
                    case 0x01: // 01 nn     nz----  6   ORA (nn,X)  OR  A,[[nn+X]]      ;A=A OR [word[nn+X]]
                    case 0x11: // 11 nn     nz----  5*  ORA (nn),Y  OR  A,[[nn]+Y]      ;A=A OR [word[nn]+Y]
                               // * Add one cycle if indexing crosses a page boundary.

                    // Compare
                    case 0xC9: // C9 nn     nzc---  2   CMP #nn     CMP A,nn            ;A-nn
                    case 0xC5: // C5 nn     nzc---  3   CMP nn      CMP A,[nn]          ;A-[nn]
                    case 0xD5: // D5 nn     nzc---  4   CMP nn,X    CMP A,[nn+X]        ;A-[nn+X]
                    case 0xCD: // CD nn nn  nzc---  4   CMP nnnn    CMP A,[nnnn]        ;A-[nnnn]
                    case 0xDD: // DD nn nn  nzc---  4*  CMP nnnn,X  CMP A,[nnnn+X]      ;A-[nnnn+X]
                    case 0xD9: // D9 nn nn  nzc---  4*  CMP nnnn,Y  CMP A,[nnnn+Y]      ;A-[nnnn+Y]
                    case 0xC1: // C1 nn     nzc---  6   CMP (nn,X)  CMP A,[[nn+X]]      ;A-[word[nn+X]]
                    case 0xD1: // D1 nn     nzc---  5*  CMP (nn),Y  CMP A,[[nn]+Y]      ;A-[word[nn]+Y]
                    case 0xE0: // E0 nn     nzc---  2   CPX #nn     CMP X,nn            ;X-nn
                    case 0xE4: // E4 nn     nzc---  3   CPX nn      CMP X,[nn]          ;X-[nn]
                    case 0xEC: // EC nn nn  nzc---  4   CPX nnnn    CMP X,[nnnn]        ;X-[nnnn]
                    case 0xC0: // C0 nn     nzc---  2   CPY #nn     CMP Y,nn            ;Y-nn
                    case 0xC4: // C4 nn     nzc---  3   CPY nn      CMP Y,[nn]          ;Y-[nn]
                    case 0xCC: // CC nn nn  nzc---  4   CPY nnnn    CMP Y,[nnnn]        ;Y-[nnnn]
                               // * Add one cycle if indexing crosses a page boundary.
                               // Note: Compared with normal 80x86 and Z80 CPUs, resulting Carry Flag is reversed.

                    // Bit Test
                    case 0x24: // 24 nn     xz---x  3   BIT nn      TEST A,[nn]         ;test and set flags
                    case 0x2C: // 2C nn nn  xz---x  4   BIT nnnn    TEST A,[nnnn]       ;test and set flags
                               // Flags are set as so: Z = ((A AND[addr])= 00h), N =[addr].Bit7, V =[addr].Bit6.Note that N and V are affected only by[addr] (not by A).

                    // Increment by one
                    case 0xE6: // E6 nn     nz----  5   INC nn      INC [nn]            ;[nn]=[nn]+1
                    case 0xF6: // F6 nn     nz----  6   INC nn,X    INC [nn+X]          ;[nn+X]=[nn+X]+1
                    case 0xEE: // EE nn nn  nz----  6   INC nnnn    INC [nnnn]          ;[nnnn]=[nnnn]+1
                    case 0xFE: // FE nn nn  nz----  7   INC nnnn,X  INC [nnnn+X]        ;[nnnn+X]=[nnnn+X]+1
                    case 0xE8: // E8        nz----  2   INX         INC X               ;X=X+1
                    case 0xC8: // C8        nz----  2   INY         INC Y               ;Y=Y+1

                    // Decrement by one
                    case 0xC6: // C6 nn     nz----  5   DEC nn      DEC [nn]            ;[nn]=[nn]-1
                    case 0xD6: // D6 nn     nz----  6   DEC nn,X    DEC [nn+X]          ;[nn+X]=[nn+X]-1
                    case 0xCE: // CE nn nn  nz----  6   DEC nnnn    DEC [nnnn]          ;[nnnn]=[nnnn]-1
                    case 0xDE: // DE nn nn  nz----  7   DEC nnnn,X  DEC [nnnn+X]        ;[nnnn+X]=[nnnn+X]-1
                    case 0xCA: // CA        nz----  2   DEX         DEC X               ;X=X-1
                    case 0x88: // 88        nz----  2   DEY         DEC Y               ;Y=Y-1


                    //  CPU Rotate and Shift Instructions

                    // Shift Left Logical/Arithmetic
                    case 0x0A: // 0A        nzc---  2   ASL A       SHL A               ;SHL A
                    case 0x06: // 06 nn     nzc---  5   ASL nn      SHL [nn]            ;SHL [nn]
                    case 0x16: // 16 nn     nzc---  6   ASL nn,X    SHL [nn+X]          ;SHL [nn+X]
                    case 0x0E: // 0E nn nn  nzc---  6   ASL nnnn    SHL [nnnn]          ;SHL [nnnn]
                    case 0x1E: // 1E nn nn  nzc---  7   ASL nnnn,X  SHL [nnnn+X]        ;SHL [nnnn+X]

                    // Shift Right Logical
                    case 0x4A: // 4A        0zc---  2   LSR A       SHR A               ;SHR A
                    case 0x46: // 46 nn     0zc---  5   LSR nn      SHR [nn]            ;SHR [nn]
                    case 0x56: // 56 nn     0zc---  6   LSR nn,X    SHR [nn+X]          ;SHR [nn+X]
                    case 0x4E: // 4E nn nn  0zc---  6   LSR nnnn    SHR [nnnn]          ;SHR [nnnn]
                    case 0x5E: // 5E nn nn  0zc---  7   LSR nnnn,X  SHR [nnnn+X]        ;SHR [nnnn+X]

                    // Rotate Left through Carry
                    case 0x2A: // 2A        nzc---  2   ROL A        RCL A              ;RCL A
                    case 0x26: // 26 nn     nzc---  5   ROL nn       RCL [nn]           ;RCL [nn]
                    case 0x36: // 36 nn     nzc---  6   ROL nn,X     RCL [nn+X]         ;RCL [nn+X]
                    case 0x2E: // 2E nn nn  nzc---  6   ROL nnnn     RCL [nnnn]         ;RCL [nnnn]
                    case 0x3E: // 3E nn nn  nzc---  7   ROL nnnn,X   RCL [nnnn+X]       ;RCL [nnnn+X]

                    // Rotate Right through Carry
                    case 0x6A: // 6A        nzc---  2   ROR A        RCR A              ;RCR A
                    case 0x66: // 66 nn     nzc---  5   ROR nn       RCR [nn]           ;RCR [nn]
                    case 0x76: // 76 nn     nzc---  6   ROR nn,X     RCR [nn+X]         ;RCR [nn+X]
                    case 0x6E: // 6E nn nn  nzc---  6   ROR nnnn     RCR [nnnn]         ;RCR [nnnn]
                    case 0x7E: // 7E nn nn  nzc---  7   ROR nnnn,X   RCR [nnnn+X]       ;RCR [nnnn+X]

                    // Notes:
                    // ROR instruction is available on MCS650X microprocessors after June, 1976.
                    // ROL and ROR rotate an 8bit value through carry (rotates 9bits in total).


                    //  CPU Jump and Control Instructions

                    // Normal Jumps & Subroutine Calls/Returns
                    case 0x4C: // 4C nn nn  ------  3   JMP nnnn     JMP nnnn                 ;PC=nnnn
                    case 0x6C: // 6C nn nn  ------  5   JMP (nnnn)   JMP [nnnn]               ;PC=WORD[nnnn]
                    case 0x20: // 20 nn nn  ------  6   JSR nnnn     CALL nnnn                ;[S]=PC+2,PC=nnnn
                    case 0x40: // 40        nzcidv  6   RTI          RETI ;(from BRK/IRQ/NMI) ;P=[S], PC=[S]
                    case 0x60: // 60        ------  6   RTS          RET  ;(from CALL)        ;PC=[S]+1
                               // Note: RTI cannot modify the B-Flag or the unused flag.
                               // Glitch: For JMP [nnnn] the operand word cannot cross page boundaries, ie. JMP [03FFh] would fetch the MSB from [0300h] instead of [0400h]. Very simple workaround would be to place a ALIGN 2 before the data word.

                    // Conditional Branches (conditional jump to PC=PC+/-dd)
                    case 0x10: // 10 dd     ------  2** BPL nnn      JNS nnn     ;N=0 plus/positive
                    case 0x30: // 30 dd     ------  2** BMI nnn      JS  nnn     ;N=1 minus/negative/signed
                    case 0x50: // 50 dd     ------  2** BVC nnn      JNO nnn     ;V=0 no overflow
                    case 0x70: // 70 dd     ------  2** BVS nnn      JO  nnn     ;V=1 overflow
                    case 0x90: // 90 dd     ------  2** BCC/BLT nnn  JNC/JB  nnn ;C=0 less/below/no carry
                    case 0xB0: // B0 dd     ------  2** BCS/BGE nnn  JC/JAE  nnn ;C=1 above/greater/equal/carry
                    case 0xD0: // D0 dd     ------  2** BNE/BZC nnn  JNZ/JNE nnn ;Z=0 not zero/not equal
                    case 0xF0: // F0 dd     ------  2** BEQ/BZS nnn  JZ/JE   nnn ;Z=1 zero/equal
                               // ** The execution time is 2 cycles if the condition is false (no branch executed). Otherwise, 3 cycles if the destination is in the same memory page, or 4 cycles if it crosses a page boundary (see below for exact info).
                               // Note: After subtractions (SBC or CMP) carry=set indicates above-or-equal, unlike as for 80x86 and Z80 CPUs.

                    // Interrupts, Exceptions, Breakpoints
                    case 0x00: // 00        ---1--  7   BRK   Force Break B=1,[S]=PC+1,[S]=P,I=1,PC=[FFFE]
                        break;
                }
            }
        }
    }
}