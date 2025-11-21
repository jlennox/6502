using System;
using System.Collections.Generic;
using System.Text;

namespace SixFiveOhTwo
{
    public enum Opcode
    {
    }

    public enum MemoryAddressing
    {
        None = 0,
        Immediate,
        ZeroPage,
        ZeroPageWithX,
        ZeroPageWithY,
        Absolute,
        AbsoluteDereferenced
    }

    public struct Instruction
    {
        public Opcode Opcode;
        public ushort Immediate16;
        public byte Immediate8;

        public int Execute()
        {
            return 0;
        }
    }

    class Cpu2
    {
    }
}
