using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SixFiveOhTwo
{
    public enum Opcode
    {
    }

    // 96 100_101_10 nn     ------  4   STX nn,Y    MOV [nn+Y],X        ;[nn+Y]=X
    // B6 101_101_10 nn     nz----  4   LDX nn,Y    MOV X,[nn+Y]        ;X=[nn+Y]
    // BE 101_111_10 nn nn  nz----  4*  LDX nnnn,Y  MOV X,[nnnn+Y]      ;X=[nnnn+Y]
    // 97 100_101_11 nn     ------  4   SAX nn,Y    STA+STX  [nn+Y]=A AND X
    // i == X, except in the above it's Y.
    public enum MemoryAddressing
    {
        None = 0,
        // 000_01, 000_11
        ZeroPageIndexedPtr, // [WORD[nn+X]]
        // 001_*
        Immediate, // nn
        // 011_*
        Absolute, // [nnnn]
        // 100_01, 100_11
        ZeroPagePtrWithX, // [WORD[nn]+Y]
        // 101_*
        ZeroPageIndexed, // [nn+i]
        // 110_01, 110_11
        AbsoluteYIndexed, // [nnnn+Y]
        // 111_*
        AbsoluteIndexed  // [nnnn+i]
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Instruction
    {
        [FieldOffset(0)]
        public Opcode Opcode;
        [FieldOffset(4)]
        public ushort Immediate16;
        [FieldOffset(4)]
        public byte Immediate8;
        [FieldOffset(6)]
        public MemoryAddressing Addressing;



        public int Execute()
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    struct CpuRegisters
    {
        [FieldOffset(0)]
        public byte Accumulator;
        [FieldOffset(1)]
        public byte IndexX;
        [FieldOffset(2)]
        public byte IndexY;
    }

    class Memory
    {
        private byte[] _memory;

        public byte this[ushort addr]
        {
            get => Read(addr);
            set => Write(addr, value);
        }

        public byte Read(ushort addr)
        {
            return _memory[addr];
        }

        public void Write(ushort addr, byte value)
        {
            _memory[addr] = value;
        }
    }

    enum InstructionState
    {
        Opcode, Immediate8, ArgumentDecode, Execute
    }

    class Cpu2
    {
        private Memory _memory;
        private ushort _pc;

        private void Tick()
        {
        }

        private byte ReadAtProgramCounter()
        {
            var value = _memory.Read(_pc);
            Tick();
            _pc++;
            return value;
        }

        public void Execute()
        {
            byte opcode;
            byte immediate8;

            var state = InstructionState.Opcode;

            while (true)
            {
                switch (state)
                {
                    case InstructionState.Opcode:
                        opcode = ReadAtProgramCounter();
                        break;
                    case InstructionState.Immediate8:
                        immediate8 = ReadAtProgramCounter();
                        break;
                    case InstructionState.ArgumentDecode:
                        break;
                }

                ++state;
            }
        }
    }
}
