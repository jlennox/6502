using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NLog;

namespace SixFiveOhTwo
{
    public unsafe class Cpu3
    {
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
        private bool GetFlag(byte flag)
        {
            return (ProcessorStatus & flag) == flag;
        }

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

        #region ROM
        private NesRom _rom;
        private byte* _prgRom;
        private int _prgRomSize;

        public NesRom Rom
        {
            get => _rom;
            set
            {
                _prgRom = Unsafe.Allocate(value.PrgRom);
                _prgRomSize = value.PrgRom.Length;
                _rom = value;
            }
        }
        #endregion

        public Cpu3()
        {
            UnusedFlag = true;

            Memory = Unsafe.AllocateZero(0xFFFF);

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

        private readonly byte* _constantValues;

        public byte* Memory;

        private const int _opcodeMask = 0b111_000_00;

        private void ArratyTesT()
        {
            var x = new byte[10];
            var m = x[5];
        }

        enum MemoryType
        {
            Main,
            Constants
        }

        class ProgramGenerator
        {
            private readonly AssemblyName _assemblyName;
            private readonly AssemblyBuilder _assemblyBuilder;
            private readonly ModuleBuilder _moduleBuilder;
            public readonly TypeBuilder TypeBuilder;

            public FieldBuilder Accumulator { get; set; }
            public FieldBuilder IndexX { get; set; }
            public FieldBuilder IndexY { get; set; }
            public FieldBuilder Memory { get; set; }
            public FieldBuilder PrgRom { get; set; }
            public FieldBuilder Constants { get; set; }
            public FieldBuilder PC { get; set; }
            public FieldBuilder SP { get; set; }
            public FieldBuilder Flags { get; set; }

            public ProgramGenerator()
            {
                var name = $"{GetType().Assembly.GetName().Name}.{nameof(ProgramGenerator)}.Product";
                _assemblyName = new AssemblyName(name);
                _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                    _assemblyName, AssemblyBuilderAccess.Run);
                _moduleBuilder = _assemblyBuilder.DefineDynamicModule("Main");

                TypeBuilder = _moduleBuilder.DefineType(
                    "Program", TypeAttributes.Class | TypeAttributes.Public);

                Accumulator = TypeBuilder.DefineField(
                    nameof(Accumulator), typeof(byte), FieldAttributes.Public);

                IndexX = TypeBuilder.DefineField(
                    nameof(IndexX), typeof(byte), FieldAttributes.Public);

                IndexY = TypeBuilder.DefineField(
                    nameof(IndexY), typeof(byte), FieldAttributes.Public);

                Memory = TypeBuilder.DefineField(
                    nameof(Memory), typeof(byte*), FieldAttributes.Public);

                Constants = TypeBuilder.DefineField(
                    nameof(Constants), typeof(byte*), FieldAttributes.Public);

                PC = TypeBuilder.DefineField(
                    nameof(PC), typeof(ushort), FieldAttributes.Public);

                SP = TypeBuilder.DefineField(
                    nameof(SP), typeof(ushort), FieldAttributes.Public);

                Flags = TypeBuilder.DefineField(
                    nameof(Flags), typeof(ushort), FieldAttributes.Public);
            }

            private readonly Dictionary<int, ProgramMethod> _methods =
                new Dictionary<int, ProgramMethod>();

            public ProgramMethod StartMethod(int start)
            {
                var method = new ProgramMethod(this, start);

                _methods[start] = method;

                return method;
            }
        }

        enum Flags
        {
            CarryFlag = 1 << 0,
            ZeroFlag = 1 << 1,
            IrqDisableFlag = 1 << 2,
            DecimalModeFlag = 1 << 3,
            BreakFlag = 1 << 4,
            UnusedFlag = 1 << 5,
            OverflowFlag = 1 << 6,
            NegativeFlag = 1 << 7
        }

        class ProgramMethod
        {
            private const int _tempCount = 4;

            public ProgramGenerator Program { get; }
            public MethodBuilder MethodBuilder { get; }
            public ILGenerator IL { get; }
            public LocalBuilder[] TempI4Local { get; }

            public ProgramMethod(ProgramGenerator program, int start)
            {
                Program = program;
                MethodBuilder = program.TypeBuilder.DefineMethod(
                    "m_" + start, MethodAttributes.Public);
                IL = MethodBuilder.GetILGenerator();

                for (var i = 0; i < _tempCount; ++i)
                {
                    TempI4Local[i] = IL.DeclareLocal(typeof(int));
                }
            }

            private void Dup(bool dup)
            {
                if (dup)
                {
                    IL.Emit(OpCodes.Dup);
                }
            }

            public void IncrementPC(int amount)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.PC);
                IL.EmitI4Load(amount);
                IL.Emit(OpCodes.Stfld, Program.PC);
            }

            public void LoadA()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.Accumulator);
            }

            public void LoadX()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.IndexX);
            }

            public void LoadY()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.IndexY);
            }

            public void LoadTemp()
            {
                IL.EmitLdloc(0);
            }

            public void LoadTemp(int index)
            {
                IL.EmitLdloc(TempI4Local[index]);
            }

            public void StoreA()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Stfld, Program.Accumulator);
            }

            public void StoreX()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Stfld, Program.IndexX);
            }

            public void StoreY()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Stfld, Program.IndexY);
            }

            public void StoreTemp(bool dup = true)
            {
                StoreTemp(0, dup);
            }

            public void StoreTemp(int index, bool dup = true)
            {
                Dup(dup);

                IL.EmitStloc(TempI4Local[index]);
            }

            public void StoreFlags()
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, Program.Flags);
            }

            public void GetFlag(Flags flag)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.Flags);
                IL.EmitI4Load((int)flag);
                IL.Emit(OpCodes.And);
                IL.EmitI4Load((int)flag);
                IL.Emit(OpCodes.Ceq);
            }

            public void GetFlagHigh(Flags flag)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.Flags);
                IL.EmitI4Load((int)flag);
                IL.Emit(OpCodes.And);

                var isSetLabel = IL.DefineLabel();
                var endLabel = IL.DefineLabel();

                IL.Emit(OpCodes.Brtrue_S, isSetLabel);

                IL.EmitI4Load(0);
                IL.Emit(OpCodes.Br, endLabel);

                IL.MarkLabel(isSetLabel);
                IL.EmitI4Load(0b1000_0000);

                IL.MarkLabel(endLabel);
            }

            public void SetFlag(Flags flag, bool set)
            {
                SetFlagNoStore(flag, set);
                StoreFlags();
            }

            public void SetFlagNoStore(Flags flag, bool set)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, Program.Flags);

                if (set)
                {
                    IL.EmitI4Load((int)flag);
                    IL.Emit(OpCodes.Or);
                }
                else
                {
                    IL.EmitI4Load(~(int)flag);
                    IL.Emit(OpCodes.And);
                }
            }

            public void EmitInc(FieldBuilder field, OpCode addOrSub)
            {
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, field);
                IL.Emit(OpCodes.Ldc_I4_1);
                IL.Emit(addOrSub);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, field);
            }

            public void ReadMemory(MemoryType type)
            {
                GetMemoryPtr(type);
                IL.Emit(OpCodes.Ldind_U1);
            }

            public void GetMemoryPtr(MemoryType type)
            {
                var notPrgRomAndPopLabel = IL.DefineLabel();
                var notPrgRomLabel = IL.DefineLabel();
                var endLabel = IL.DefineLabel();

                IL.Emit(OpCodes.Ldarg_0);

                switch (type)
                {
                    case MemoryType.Main:
                        IL.Emit(OpCodes.Dup);
                        IL.EmitI4Load(0x8000);
                        IL.Emit(OpCodes.Blt_S, notPrgRomAndPopLabel);
                        IL.EmitI4Load(0xFFFF);
                        IL.Emit(OpCodes.Bgt_S, notPrgRomLabel);

                        IL.Emit(OpCodes.Ldfld, Program.PrgRom);
                        IL.Emit(OpCodes.Br_S, endLabel);

                        IL.MarkLabel(notPrgRomAndPopLabel);
                        IL.Emit(OpCodes.Pop);
                        IL.MarkLabel(notPrgRomLabel);

                        IL.Emit(OpCodes.Ldfld, Program.Memory);
                        break;
                    case MemoryType.Constants:
                        IL.Emit(OpCodes.Ldfld, Program.Constants);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(type), type, "Unsupported memory type.");
                }

                IL.MarkLabel(endLabel);
                IL.Emit(OpCodes.Add);
            }

            public void ReadMemory(MemoryType type, ushort addr)
            {
                GetMemoryPtr(type, addr);
                IL.Emit(OpCodes.Ldind_U1);
            }

            public void GetMemoryPtr(MemoryType type, ushort addr)
            {
                FieldBuilder memoryField;
                switch (type)
                {
                    case MemoryType.Main:
                        if (addr >= 0x8000 && addr <= 0xFFFF)
                        {
                            memoryField = Program.PrgRom;
                            break;
                        }

                        memoryField = Program.Memory;
                        break;
                    case MemoryType.Constants:
                        memoryField = Program.Constants;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(type), type, "Unsupported memory type.");
                }

                IL.Emit(OpCodes.Ldfld, memoryField);
                IL.EmitI4Load(addr);
                IL.Emit(OpCodes.Add);
            }


            // NOTE TO SELF, optimize sequential flag access.
            public void SetZeroFlag(bool dup = true)
            {
                Dup(dup);

                var wasset = IL.DefineLabel();
                var end = IL.DefineLabel();

                IL.Emit(OpCodes.Brtrue, wasset);
                SetFlagNoStore(Flags.ZeroFlag, false);
                IL.MarkLabel(wasset);
                SetFlagNoStore(Flags.ZeroFlag, true);

                IL.MarkLabel(end);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, Program.Flags);
            }

            public void SetNegativeFlag(bool dup = true)
            {
                Dup(dup);

                //NegativeFlag = (diff & 0b1000_0000) != 0;

                var wasset = IL.DefineLabel();
                var end = IL.DefineLabel();

                IL.Emit(OpCodes.And, 0b1000_0000);
                IL.Emit(OpCodes.Brtrue, wasset);
                SetFlagNoStore(Flags.NegativeFlag, false);
                IL.MarkLabel(wasset);
                SetFlagNoStore(Flags.NegativeFlag, true);

                IL.MarkLabel(end);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, Program.Flags);
            }

            public void SetCarryFlag(bool dup = true)
            {
                Dup(dup);

                //CarryFlag = (val & 0x80) != 0;

                var wasset = IL.DefineLabel();
                var end = IL.DefineLabel();

                IL.Emit(OpCodes.And, 0b1000_0000);
                IL.Emit(OpCodes.Brtrue, wasset);
                SetFlagNoStore(Flags.CarryFlag, false);
                IL.MarkLabel(wasset);
                SetFlagNoStore(Flags.CarryFlag, true);

                IL.MarkLabel(end);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, Program.Flags);
            }

            public void SetOverflowFlag(bool dup = true)
            {
                Dup(dup);

                //OverflowFlag = (diff & 0b0100_0000) != 0;

                var wasset = IL.DefineLabel();
                var end = IL.DefineLabel();

                IL.Emit(OpCodes.And, 0b0100_0000);
                IL.Emit(OpCodes.Brtrue, wasset);
                SetFlagNoStore(Flags.NegativeFlag, false);
                IL.MarkLabel(wasset);
                SetFlagNoStore(Flags.NegativeFlag, true);

                IL.MarkLabel(end);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, Program.Flags);
            }

            public void Compare(FieldBuilder register)
            {
                IL.Emit(OpCodes.Ldind_U1);
                IL.Emit(OpCodes.Ldarg_0);
                IL.Emit(OpCodes.Ldfld, register);
                IL.Emit(OpCodes.Sub);
                IL.Emit(OpCodes.Dup);
                IL.Emit(OpCodes.Conv_U1);
                SetNegativeFlag(false);
                IL.Emit(OpCodes.Dup);
                IL.Emit(OpCodes.Conv_U1);
                SetZeroFlag(false);

                var carryNotSetLabel = IL.DefineLabel();
                var endLabel = IL.DefineLabel();
                IL.EmitI4Load(0);
                IL.Emit(OpCodes.Bge_S, carryNotSetLabel);
                SetFlagNoStore(Flags.CarryFlag, true);
                IL.Emit(OpCodes.Br_S, endLabel);
                IL.MarkLabel(carryNotSetLabel);
                SetFlagNoStore(Flags.CarryFlag, false);
                IL.MarkLabel(endLabel);
                IL.Emit(OpCodes.Conv_U1);
                IL.Emit(OpCodes.Stfld, Program.Flags);

                //var val = ReadMemory(memory, ptr);
                //var diff = register - val;
                //SetNegativeFlag((byte)diff);
                //SetZeroFlag((byte)diff);
                //CarryFlag = diff >= 0;
            }
        }

        // Pushes a ptr to the appropriate source/dest on the stack.
        private bool EmitOperandPtrToStack(
            byte opcode, ProgramMethod method, bool writeAccess)
        {
            var program = method.Program;
            var il = method.IL;
            // These opcodes index based on Y instead of X.
            // 96 100_101_10 nn     ------  4   STX nn,Y    MOV [nn+Y],X        ;[nn+Y]=X
            // B6 101_101_10 nn     nz----  4   LDX nn,Y    MOV X,[nn+Y]        ;X=[nn+Y]
            // BE 101_111_10 nn nn  nz----  4*  LDX nnnn,Y  MOV X,[nnnn+Y]      ;X=[nnnn+Y]
            // 97 100_101_11 nn     ------  4   SAX nn,Y    STA+STX  [nn+Y]=A AND X
            var switchingIndex = program.IndexX;

            switch (opcode >> 1)
            {
                case 0b100_101_1:
                case 0b101_101_1:
                case 0b101_111_1:
                    switchingIndex = program.IndexY;
                    break;
            }

            switch (opcode & 0b000_111_11)
            {
                case 0b000_000_01: // [WORD[nn+X]]
                case 0b000_000_11: // [WORD[nn+X]]
                    // push (GetImmediate8() + IndexX) & 0XFF
                    il.EmitI4Load(GetImmediate8());
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, program.IndexX);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Conv_U1);

                    // push push (pop() + memory)
                    method.ReadMemory(MemoryType.Main);
                    il.Emit(OpCodes.Dup);

                    il.EmitI4Load(1);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Ldind_U1);
                    il.Emit(OpCodes.Shl, 8);
                    method.StoreTemp(false);
                    il.Emit(OpCodes.Ldind_U1);
                    method.LoadTemp();
                    il.Emit(OpCodes.Or);

                    method.GetMemoryPtr(MemoryType.Main);

                    method.IncrementPC(2);
                    //memory = Memory;
                    //var srcPtr = (ushort)((GetImmediate8() + IndexX) & 0xFF);
                    //ptr = ReadMemory(srcPtr);
                    //ptr |= (ushort)(ReadMemory((byte)(srcPtr + 1)) << 8);
                    //_pc = 2;
                    break;
                case 0b000_001_00: // [nn]
                case 0b000_001_01: // [nn]
                case 0b000_001_10: // [nn]
                case 0b000_001_11: // [nn]
                    method.ReadMemory(MemoryType.Main, GetImmediate8());
                    method.IncrementPC(2);
                    //memory = Memory;
                    //ptr = GetImmediate8();
                    //_pc += 2;
                    break;
                case 0b000_000_00: // nn
                case 0b000_010_01: // nn
                case 0b000_000_10: // nn
                case 0b000_010_11: // nn
                    if (writeAccess)
                    {
                        throw new Exception();
                    }

                    method.ReadMemory(MemoryType.Constants, GetImmediate8());
                    method.IncrementPC(2);

                    //memory = _constantValues;
                    //ptr = GetImmediate8();
                    //_pc += 2;
                    break;
                case 0b000_011_00: // [nnnn]
                case 0b000_011_01: // [nnnn]
                case 0b000_011_10: // [nnnn]
                case 0b000_011_11: // [nnnn]
                    method.ReadMemory(MemoryType.Main, GetImmediate16());
                    method.IncrementPC(3);
                    //memory = Memory;
                    //ptr = GetImmediate16();
                    //_pc += 3;
                    break;
                case 0b000_100_01:
                case 0b000_100_11:
                { // [WORD[nn]+Y]
                    method.ReadMemory(MemoryType.Main, GetImmediate8());
                    method.LoadY();
                    il.Emit(OpCodes.Add);
                    method.IncrementPC(3);
                    //memory = Memory;
                    //var srcPtr = GetImmediate8();
                    //ptr = ReadMemory(srcPtr);
                    //ptr |= (ushort)(ReadMemory((byte)(srcPtr + 1)) << 8);
                    //ptr += IndexY;
                    //_pc += 2;
                    break;
                }
                case 0b000_101_00: // [nn+X]
                case 0b000_101_01: // [nn+X]
                case 0b000_101_10: // [nn+X]
                case 0b000_101_11: // [nn+X]
                    il.EmitI4Load(GetImmediate8());
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, switchingIndex);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Conv_U1);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, program.Memory);
                    il.Emit(OpCodes.Add);

                    method.IncrementPC(2);
                    //memory = Memory;
                    //ptr = (byte)((GetImmediate8() + switchingIndex) & 0xFF);
                    //_pc += 2;
                    break;
                case 0b000_110_01:
                case 0b000_110_11:// [nnnn+Y]
                    il.EmitI4Load(GetImmediate16());
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, program.IndexY);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Conv_U2);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, program.Memory);
                    il.Emit(OpCodes.Add);
                    method.IncrementPC(3);
                    //memory = Memory;
                    //ptr = GetImmediate16();
                    //ptr = (ushort)((ptr + IndexY) & 0xFFFF);
                    //_pc += 3;
                    break;
                case 0b000_111_00: // [nnnn+X]
                case 0b000_111_01: // [nnnn+X]
                case 0b000_111_10:
                case 0b000_111_11:// [nnnn+X]
                    il.EmitI4Load(GetImmediate16());
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, switchingIndex);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Conv_U2);

                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, program.Memory);
                    il.Emit(OpCodes.Add);
                    method.IncrementPC(3);
                    //memory = Memory;
                    //ptr = GetImmediate16();
                    //ptr = (ushort)((ptr + switchingIndex) & 0xFFFF);
                    //_pc += 3;
                    break;
                case 0b000_010_10: // A
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldflda, program.Accumulator);
                    //ptr = 0;
                    //memory = _aPtr;
                    ++_pc;
                    break;
                case 0b000_010_00:
                case 0b000_100_00:
                case 0b000_110_00:
                case 0b000_100_10:
                case 0b000_110_10:
                default:
                    //memory = null;
                    //ptr = 0;
                    return false;
            }
            
            return true;
        }

        private void EmitDecode(ProgramMethod method)
        {
            var program = method.Program;
            var il = method.IL;

            var opcode = ReadMemory(ProgramCounter);

            if (Trace)
            {
                Log($"Execute({opcode:X2})");
            }

            switch (opcode)
            {
                case 0b010_011_00:
                    // 4C 010_011_00 nn nn  ------  3   JMP nnnn     JMP nnnn                 ;PC=nnnn
                    il.EmitI4Load(GetImmediate16());
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Stfld, program.PC);
                    //ProgramCounter = GetImmediate16();
                    return;
                case 0b011_011_00:
                    // 6C 011_011_00 nn nn  ------  5   JMP (nnnn)   JMP [nnnn]               ;PC=WORD[nnnn]
                    // Glitch: For JMP [nnnn] the operand word cannot cross page boundaries, ie. JMP [03FFh] would fetch the MSB from [0300h] instead of [0400h]. Very simple workaround would be to place a ALIGN 2 before the data word.

                    il.EmitI4Load(GetImmediate16());

                    //var operand = GetImmediate16();
                    //ushort addr = ReadMemory(operand);
                    //var highaddr = (operand & 0xFF00) | ((operand + 1) & 0xFF);
                    //addr |= (ushort)(ReadMemory((ushort)highaddr) << 8);
                    //ProgramCounter = addr;
                    return;
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
                case 0b111_010_00:
                    // E8 111_010_00        nz----  2   INX         INC X               ;X=X+1
                    method.EmitInc(program.IndexX, OpCodes.Add);
                    method.IncrementPC(1);
                    //++IndexX;
                    //++_pc;
                    return;
                case 0b110_010_00:
                    // C8 110_010_00        nz----  2   INY         INC Y               ;Y=Y+1
                    method.EmitInc(program.IndexY, OpCodes.Add);
                    method.IncrementPC(1);
                    //++IndexY;
                    //++_pc;
                    return;
                case 0b110_010_10:
                    // CA 110_010_10        nz----  2   DEX         DEC X               ;X=X-1
                    method.EmitInc(program.IndexX, OpCodes.Sub);
                    method.IncrementPC(1);
                    //--IndexX;
                    //++_pc;
                    return;
                case 0b100_010_00:
                    // 88 100_010_00        nz----  2   DEY         DEC Y               ;Y=Y-1
                    method.EmitInc(program.IndexY, OpCodes.Sub);
                    method.IncrementPC(1);
                    //--IndexY;
                    //++_pc;
                    return;
                case 0b111_010_10:
                    // EA 111_010_10        ------  2   NOP       NOP    ;No operation
                    method.IncrementPC(1);
                    //++_pc;
                    return;
                case 0b000_110_00:
                case 0b001_110_00:
                    // 18        --0---  2   CLC       CLC    ;Clear carry flag            C=0
                    // 38        --1---  2   SEC       STC    ;Set carry flag              C=1
                    method.SetFlag(Flags.CarryFlag, (opcode & 0b001_000_00) != 0);
                    method.IncrementPC(1);
                    //CarryFlag = (opcode & 0b001_000_00) != 0;
                    //++_pc;
                    return;
                case 0b010_110_00:
                case 0b011_110_00:
                    // 58        ---0--  2   CLI       EI     ;Clear interrupt disable bit I=0
                    // 78        ---1--  2   SEI       DI     ;Set interrupt disable bit   I=1
                    method.SetFlag(Flags.IrqDisableFlag, (opcode & 0b001_000_00) != 0);
                    method.IncrementPC(1);
                    //IrqDisableFlag = (opcode & 0b001_000_00) != 0;
                    //++_pc;
                    return;
                case 0b110_110_00:
                case 0b111_110_00:
                    // D8        ----0-  2   CLD       CLD    ;Clear decimal mode          D=0
                    // F8        ----1-  2   SED       STD    ;Set decimal mode            D=1
                    method.SetFlag(Flags.DecimalModeFlag, (opcode & 0b001_000_00) != 0);
                    method.IncrementPC(1);
                    //DecimalModeFlag = (opcode & 0b001_000_00) != 0;
                    //++_pc;
                    return;
                case 0b101_110_00:
                    // B8        -----0  2   CLV       CLV    ;Clear overflow flag         V=0
                    method.SetFlag(Flags.OverflowFlag, false);
                    //OverflowFlag = false;
                    //++_pc;
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
                    method.LoadA();
                    method.StoreY();
                    method.IncrementPC(1);
                    //IndexY = Accumulator;
                    //++_pc;
                    return;
                case 0b101_010_10:
                    method.LoadA();
                    method.StoreX();
                    method.IncrementPC(1);
                    //IndexX = Accumulator;
                    //++_pc;
                    return;
                case 0b101_110_10:
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldfld, program.SP);
                    method.StoreX();
                    method.IncrementPC(1);
                    //IndexX = StackPointer;
                    //++_pc;
                    return;
                case 0b100_110_00:
                    method.LoadY();
                    method.StoreA();
                    method.IncrementPC(1);
                    //Accumulator = IndexY;
                    //++_pc;
                    return;
                case 0b100_010_10:
                    method.LoadX();
                    method.StoreA();
                    method.IncrementPC(1);
                    //Accumulator = IndexX;
                    //++_pc;
                    return;
                case 0b100_110_10:
                    method.LoadX();
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Stfld, program.SP);
                    method.IncrementPC(1);
                    //StackPointer = IndexX;
                    //++_pc;
                    return;
                case 0b101_000_00:
                    il.EmitI4Load(GetImmediate8());
                    method.StoreY();
                    method.IncrementPC(2);
                    //IndexY = GetImmediate8();
                    //_pc += 2;
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
                case 0x0B:
                    InvalidOpcode(opcode);
                    _pc += 2;
                    return;
                case 0x2B:
                    InvalidOpcode(opcode);
                    _pc += 2;
                    return;
                case 0x4B:
                    InvalidOpcode(opcode);
                    _pc += 2;
                    return;
                case 0x6B:
                    InvalidOpcode(opcode);
                    _pc += 2;
                    return;
                case 0xCB:
                    InvalidOpcode(opcode);
                    _pc += 2;
                    return;
                //case 0xEB: InvalidOpcode(opcode); _pc += 2; return;
                case 0xBB:
                    InvalidOpcode(opcode);
                    _pc += 3;
                    return;
            }

            // TODO: Fix write access stuff.
            var valid = EmitOperandPtrToStack(opcode, method, false);

            if (!valid)
            {
                method.IncrementPC(1);
                //++_pc;
                return;
            }

            var operandInformation = (opcode >> 2) & 0b111;

            if ((opcode & 0b11) == 0)
            {
                // operandInformation patterns 0b010, 0b100, 0b110 are filtered
                // out by the return result of GetPtr.
                switch (opcode & _opcodeMask)
                {
                    case 0b001_000_00:
                        // Bit Test
                        // 24 001_001_00 nn     xz---x  3   BIT nn      TEST A,[nn]         ;test and set flags
                        // 2C 001_011_00 nn nn  xz---x  4   BIT nnnn    TEST A,[nnnn]       ;test and set flags
                        // Flags are set as so: Z=((A AND [addr])=00h), N=[addr].Bit7, V=[addr].Bit6. Note that N and V are affected only by [addr] (not by A).
                        switch (operandInformation)
                        {
                            case 0b000:
                            case 0b101:
                            case 0b111:
                                goto endOf00;
                        }

                        il.Emit(OpCodes.Ldind_U1);
                        method.SetZeroFlag(true);
                        method.SetNegativeFlag(true);
                        method.SetOverflowFlag(false);

                        //var val = ReadMemory(memory, ptr);
                        //ZeroFlag = (Accumulator & val) == 0;
                        //SetNegativeFlag(val);
                        //SetOverflowFlag(val);
                        break;
                    case 0b100_000_00:
                        // 84 100_001_00 nn     ------  3   STY nn      MOV [nn],Y          ;[nn]=Y
                        // 94 100_101_00 nn     ------  4   STY nn,X    MOV [nn+X],Y        ;[nn+X]=Y
                        // 8C 100_011_00 nn nn  ------  4   STY nnnn    MOV [nnnn],Y        ;[nnnn]=Y
                        switch (operandInformation)
                        {
                            case 0b000:
                            case 0b111:
                                goto endOf00;
                        }

                        method.LoadY();
                        il.Emit(OpCodes.Stind_I1);

                        //WriteMemory(memory, ptr, IndexY);
                        break;
                    case 0b101_000_00:
                        // Load Register from Memory
                        // A4 101_001_00 nn     nz----  3   LDY nn      MOV Y,[nn]          ;Y=[nn]
                        // B4 101_101_00 nn     nz----  4   LDY nn,X    MOV Y,[nn+X]     g   ;Y=[nn+X]
                        // AC 101_011_00 nn nn  nz----  4   LDY nnnn    MOV Y,[nnnn]        ;Y=[nnnn]
                        // BC 101_111_00 nn nn  nz----  4*  LDY nnnn,X  MOV Y,[nnnn+X]      ;Y=[nnnn+X]
                        // * Add one cycle if indexing crosses a page boundary.
                        switch (operandInformation)
                        {
                            case 0b000:
                                goto endOf00;
                        }

                        il.Emit(OpCodes.Ldind_U1);
                        method.StoreY();

                        //var val = ReadMemory(memory, ptr);
                        //IndexY = val;
                        break;
                    case 0b110_000_00:
                        // C0 110_000_00 nn     nzc---  2   CPY #nn     CMP Y,nn            ;Y-nn
                        // C4 110_001_00 nn     nzc---  3   CPY nn      CMP Y,[nn]          ;Y-[nn]
                        // CC 110_011_00 nn nn  nzc---  4   CPY nnnn    CMP Y,[nnnn]        ;Y-[nnnn]
                        switch (operandInformation)
                        {
                            case 0b101:
                            case 0b111:
                                goto endOf00;
                        }

                        method.Compare(program.IndexY);

                        //Compare(memory, ptr, IndexY);
                        break;
                    case 0b111_000_00:
                        // E0 111_000_00 nn     nzc---  2   CPX #nn     CMP X,nn            ;X-nn
                        // E4 111_001_00 nn     nzc---  3   CPX nn      CMP X,[nn]          ;X-[nn]
                        // EC 111_011_00 nn nn  nzc---  4   CPX nnnn    CMP X,[nnnn]        ;X-[nnnn]
                        switch (operandInformation)
                        {
                            case 0b101:
                            case 0b111:
                                goto endOf00;
                        }

                        method.Compare(program.IndexX);

                        //Compare(memory, ptr, IndexX);
                        break;
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
                    case 0b100_000_00:
                        // CPU Illegal Opcodes
                        // SAX
                        // 87 nn     ------  3   SAX nn      STA+STX  [nn]=A AND X
                        // 97 nn     ------  4   SAX nn,Y    STA+STX  [nn+Y]=A AND X
                        // 8F nn nn  ------  4   SAX nnnn    STA+STX  [nnnn]=A AND X
                        // 83 nn     ------  6   SAX (nn,X)  STA+STX  [WORD[nn+X]]=A AND X

                        method.LoadA();
                        method.LoadX();
                        il.Emit(OpCodes.And);
                        il.Emit(OpCodes.Conv_U1);
                        il.Emit(OpCodes.Stind_I1);

                        //WriteMemory(memory, ptr, (byte)(Accumulator & IndexX));
                        return;
                }
            }

            if ((opcode & 0b10) != 0)
            {
                switch (opcode & _opcodeMask)
                {
                    case 0b000_000_00:
                        // Shift Left Logical/Arithmetic
                        // 0A 000_010_10        nzc---  2   ASL A       SHL A               ;SHL A
                        // 06 000_001_10 nn     nzc---  5   ASL nn      SHL [nn]            ;SHL [nn]
                        // 16 000_101_10 nn     nzc---  6   ASL nn,X    SHL [nn+X]          ;SHL [nn+X]
                        // 0E 000_011_10 nn nn  nzc---  6   ASL nnnn    SHL [nnnn]          ;SHL [nnnn]
                        // 1E 000_111_10 nn nn  nzc---  7   ASL nnnn,X  SHL [nnnn+X]        ;SHL [nnnn+X]

                        il.Emit(OpCodes.Dup); // Stind_I1
                        il.Emit(OpCodes.Ldind_U1);
                        method.SetCarryFlag();
                        il.EmitI4Load(1);
                        il.Emit(OpCodes.Shl);
                        il.Emit(OpCodes.Conv_U1);
                        method.SetZeroFlag();
                        method.SetNegativeFlag();
                        il.Emit(OpCodes.Stind_I1);

                        //var val = ReadMemory(memory, ptr);
                        //var sum = (byte)(val << 1 & 0xFF);
                        //WriteMemory(memory, ptr, sum);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = (sum & 0x80) != 0;
                        //CarryFlag = (val & 0x80) != 0;

                        //var val = ReadMemory(memory, ptr);
                        //CarryFlag = (val & 0x80) != 0;
                        //var sum = (byte)(val << 1 & 0xFF);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = (sum & 0x80) != 0;
                        //WriteMemory(memory, ptr, sum);
                        break;
                    case 0b001_000_00:
                        // Rotate Left through Carry
                        // 2A 001_010_10        nzc---  2   ROL A        RCL A              ;RCL A
                        // 26 001_001_10 nn     nzc---  5   ROL nn       RCL [nn]           ;RCL [nn]
                        // 36 001_101_10 nn     nzc---  6   ROL nn,X     RCL [nn+X]         ;RCL [nn+X]
                        // 2E 001_011_10 nn nn  nzc---  6   ROL nnnn     RCL [nnnn]         ;RCL [nnnn]
                        // 3E 001_111_10 nn nn  nzc---  7   ROL nnnn,X   RCL [nnnn+X]       ;RCL [nnnn+X]

                        il.Emit(OpCodes.Dup); // Stind_I1
                        il.Emit(OpCodes.Ldind_U1);
                        method.SetCarryFlag();
                        il.EmitI4Load(1);
                        il.Emit(OpCodes.Shl);
                        method.GetFlag(Flags.CarryFlag);
                        il.Emit(OpCodes.Or);
                        il.Emit(OpCodes.Conv_U1);
                        method.SetZeroFlag();
                        method.SetNegativeFlag();
                        il.Emit(OpCodes.Stind_I1);

                        //var val = ReadMemory(memory, ptr);
                        //var sum = (byte)((val << 1 | CarryValue) & 0xFF);
                        //WriteMemory(memory, ptr, sum);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = (sum & 0x80) != 0;
                        //CarryFlag = (val & 0x80) != 0;

                        //var val = ReadMemory(memory, ptr);
                        //CarryFlag = (val & 0x80) != 0;
                        //var sum = (byte)((val << 1 | CarryValue) & 0xFF);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = (sum & 0x80) != 0;
                        //WriteMemory(memory, ptr, sum);
                        break;
                    case 0b010_000_00:
                        // Shift Right Logical
                        // 4A 010_010_10        0zc---  2   LSR A       SHR A               ;SHR A
                        // 46 010_001_10 nn     0zc---  5   LSR nn      SHR [nn]            ;SHR [nn]
                        // 56 010_101_10 nn     0zc---  6   LSR nn,X    SHR [nn+X]          ;SHR [nn+X]
                        // 4E 010_011_10 nn nn  0zc---  6   LSR nnnn    SHR [nnnn]          ;SHR [nnnn]
                        // 5E 010_111_10 nn nn  0zc---  7   LSR nnnn,X  SHR [nnnn+X]        ;SHR [nnnn+X]

                        il.Emit(OpCodes.Dup); // Stind_I1
                        il.Emit(OpCodes.Ldind_U1);
                        method.SetCarryFlag();
                        il.EmitI4Load(1);
                        il.Emit(OpCodes.Shr);
                        il.Emit(OpCodes.Conv_U1);
                        method.SetZeroFlag();
                        method.SetFlag(Flags.NegativeFlag, false);
                        il.Emit(OpCodes.Stind_I1);

                        //var val = ReadMemory(memory, ptr);
                        //var sum = (byte)(val >> 1);
                        //WriteMemory(memory, ptr, sum);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = false;
                        //CarryFlag = (val & 0x01) != 0;

                        //var val = ReadMemory(memory, ptr);
                        //CarryFlag = (val & 0x01) != 0;
                        //var sum = (byte)(val >> 1);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = false;
                        //WriteMemory(memory, ptr, sum);
                        break;
                    case 0b011_000_00:
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

                        il.Emit(OpCodes.Dup); // Stind_I1
                        il.Emit(OpCodes.Ldind_U1);
                        method.SetCarryFlag();
                        il.EmitI4Load(1);
                        il.Emit(OpCodes.Shr);
                        method.GetFlagHigh(Flags.CarryFlag);
                        il.Emit(OpCodes.Or);
                        il.Emit(OpCodes.Conv_U1);
                        method.SetZeroFlag();
                        method.SetNegativeFlag();
                        il.Emit(OpCodes.Stind_I1);

                        //var val = ReadMemory(memory, ptr);
                        //var sum = (byte)((val >> 1) | CarryValueHigh);
                        //WriteMemory(memory, ptr, sum);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = (sum & 0x80) != 0;
                        //CarryFlag = (val & 0x01) != 0;

                        //var val = ReadMemory(memory, ptr);
                        //CarryFlag = (val & 0x01) != 0;
                        //var sum = (byte)((val >> 1) | CarryValueHigh);
                        //ZeroFlag = sum == 0;
                        //NegativeFlag = (sum & 0x80) != 0;
                        //WriteMemory(memory, ptr, sum);
                        break;
                    case 0b100_000_00:
                        // Store Register in Memory
                        // 86 100_001_10 nn     ------  3   STX nn      MOV [nn],X          ;[nn]=X
                        // 96 100_101_10 nn     ------  4   STX nn,Y    MOV [nn+Y],X        ;[nn+Y]=X
                        // 8E 100_011_10 nn nn  ------  4   STX nnnn    MOV [nnnn],X        ;[nnnn]=X

                        method.LoadX();
                        il.Emit(OpCodes.Stind_I1);

                        //WriteMemory(memory, ptr, IndexX);
                        break;
                    case 0b101_000_00:
                        // Load Register from Memory
                        // A2 101_000_10 nn     nz----  2   LDX #nn     MOV X,nn            ;X=nn
                        // A6 101_001_10 nn     nz----  3   LDX nn      MOV X,[nn]          ;X=[nn]
                        // B6 101_101_10 nn     nz----  4   LDX nn,Y    MOV X,[nn+Y]        ;X=[nn+Y]
                        // AE 101_011_10 nn nn  nz----  4   LDX nnnn    MOV X,[nnnn]        ;X=[nnnn]
                        // BE 101_111_10 nn nn  nz----  4*  LDX nnnn,Y  MOV X,[nnnn+Y]      ;X=[nnnn+Y]

                        il.Emit(OpCodes.Ldind_U1);
                        method.StoreX();

                        //IndexX = ReadMemory(memory, ptr);
                        break;
                    case 0b110_000_00:
                        // Decrement by one
                        // C6 110_001_10 nn     nz----  5   DEC nn      DEC [nn]            ;[nn]=[nn]-1
                        // D6 110_101_10 nn     nz----  6   DEC nn,X    DEC [nn+X]          ;[nn+X]=[nn+X]-1
                        // CE 110_011_10 nn nn  nz----  6   DEC nnnn    DEC [nnnn]          ;[nnnn]=[nnnn]-1
                        // DE 110_111_10 nn nn  nz----  7   DEC nnnn,X  DEC [nnnn+X]        ;[nnnn+X]=[nnnn+X]-1

                        switch (operandInformation)
                        {
                            case 0b010:
                                goto endOf10;
                        }

                        il.Emit(OpCodes.Dup); // Stind_I1
                        il.Emit(OpCodes.Ldind_U1);
                        il.EmitI4Load(1);
                        il.Emit(OpCodes.Sub);
                        method.SetNegativeFlag();
                        method.SetZeroFlag();
                        il.Emit(OpCodes.Stind_I1);

                        //var val = ReadMemory(memory, ptr);
                        //var diff = --val;
                        //SetNegativeFlag(diff);
                        //SetZeroFlag(diff);
                        //WriteMemory(memory, ptr, diff);
                        break;
                    case 0b111_000_00:
                        // Increment by one
                        // E6 111_001_10 nn     nz----  5   INC nn      INC [nn]            ;[nn]=[nn]+1
                        // F6 111_101_10 nn     nz----  6   INC nn,X    INC [nn+X]          ;[nn+X]=[nn+X]+1
                        // EE 111_011_10 nn nn  nz----  6   INC nnnn    INC [nnnn]          ;[nnnn]=[nnnn]+1
                        // FE 111_111_10 nn nn  nz----  7   INC nnnn,X  INC [nnnn+X]        ;[nnnn+X]=[nnnn+X]+1

                        // EA 111_010_10 is nop. This flow through happens with
                        // the illegal SBC call EB 111_010_11
                        switch (operandInformation)
                        {
                            case 0b010:
                                goto endOf10;
                        }

                        il.Emit(OpCodes.Dup); // Stind_I1
                        il.Emit(OpCodes.Ldind_U1);
                        il.EmitI4Load(1);
                        il.Emit(OpCodes.Add);
                        method.SetNegativeFlag();
                        method.SetZeroFlag();
                        il.Emit(OpCodes.Stind_I1);

                        //var val = ReadMemory(memory, ptr);
                        //var diff = ++val;
                        //SetNegativeFlag(diff);
                        //SetZeroFlag(diff);
                        //WriteMemory(memory, ptr, diff);
                        break;
                }

                endOf10:
                ;
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

                        il.Emit(OpCodes.Ldind_U1);
                        method.LoadA();
                        il.Emit(OpCodes.Or);
                        il.Emit(OpCodes.Conv_U1);
                        method.StoreA();

                        //Accumulator = (byte)(Accumulator | ReadMemory(memory, ptr));
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

                        il.Emit(OpCodes.Ldind_U1);
                        method.LoadA();
                        il.Emit(OpCodes.And);
                        il.Emit(OpCodes.Conv_U1);
                        method.StoreA();

                        //Accumulator = (byte)(Accumulator & ReadMemory(memory, ptr));
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

                        il.Emit(OpCodes.Ldind_U1);
                        method.LoadA();
                        il.Emit(OpCodes.Xor);
                        il.Emit(OpCodes.Conv_U1);
                        method.StoreA();

                        //Accumulator = (byte)(Accumulator ^ ReadMemory(memory, ptr));
                        break;
                    case 0b011_000_00:
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

                        il.Emit(OpCodes.Ldind_U1);
                        il.Emit(OpCodes.Dup); // val, val
                        il.Emit(OpCodes.Dup); // val, val, val
                        method.LoadA(); // val, val, val, A
                        method.StoreTemp(0); // val, val, val, A
                        method.GetFlag(Flags.CarryFlag); // val, val, val, A, CarryFlag
                        il.Emit(OpCodes.Add); // val, val, val, (A + CarryFlag)
                        il.Emit(OpCodes.Add); // val, val, sum
                        il.Emit(OpCodes.Dup); // val, val, sum, sum
                        il.Emit(OpCodes.Conv_U1); // val, val, sum, sum_U1
                        method.StoreA(); // val, val, sum

                        var carryflagSetLabel = il.DefineLabel();
                        var endCarryFlagLabel = il.DefineLabel();

                        il.EmitI4Load(0xFF); // val, val, sum, 0xFF
                        il.Emit(OpCodes.Bgt_S, carryflagSetLabel); // val, val
                        method.SetFlagNoStore(Flags.CarryFlag, false);
                        il.Emit(OpCodes.Br_S, endCarryFlagLabel);
                        il.MarkLabel(carryflagSetLabel);
                        method.SetFlagNoStore(Flags.CarryFlag, true);
                        il.MarkLabel(endCarryFlagLabel);
                        method.StoreFlags(); // val, val
                        il.Emit(OpCodes.Dup);
                        method.LoadTemp(0); // val, val, val, acc
                        il.Emit(OpCodes.Xor); // val, val, (val ^ acc)
                        il.EmitI4Load(7); // val, val, (val ^ acc), 7
                        il.Emit(OpCodes.Shr); // val, val, signage

                        var signageNotZeroLabel = il.DefineLabel();
                        var signageTopBitSetLabel = il.DefineLabel();
                        var signageEndLabel = il.DefineLabel();
                        il.Emit(OpCodes.Brfalse_S, signageNotZeroLabel); // val
                        il.Emit(OpCodes.Pop); // empty
                        method.SetFlagNoStore(Flags.OverflowFlag, false);
                        il.Emit(OpCodes.Br_S, signageEndLabel);

                        il.MarkLabel(signageNotZeroLabel); // val
                        method.LoadA(); // val, A
                        il.Emit(OpCodes.Xor); // (val ^ A)
                        il.EmitI4Load(0x80); // (val ^ A), 0x80
                        il.Emit(OpCodes.And); // (val ^ A) & 0x80
                        il.EmitI4Load(0x80); // (val ^ A) & 0x80, 0x80
                        il.Emit(OpCodes.Beq_S, signageTopBitSetLabel);
                        method.SetFlagNoStore(Flags.OverflowFlag, false);
                        il.Emit(OpCodes.Br_S, signageEndLabel);

                        il.MarkLabel(signageTopBitSetLabel);
                        method.SetFlagNoStore(Flags.OverflowFlag, true);

                        il.MarkLabel(signageEndLabel);
                        method.StoreFlags();

                        //var val = ReadMemory(memory, ptr);
                        //var acc = Accumulator;
                        //var sum = acc + val + CarryValue;
                        //Accumulator = (byte)(sum & 0xFF);
                        //CarryFlag = sum > 0xFF;
                        //var signage = (val ^ acc) >> 7;
                        //OverflowFlag = signage == 0 && ((val ^ Accumulator) & 0x80) == 0x80;
                        break;
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

                        method.LoadA();
                        il.Emit(OpCodes.Stind_I1);

                        //WriteMemory(memory, ptr, Accumulator);
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

                        il.Emit(OpCodes.Ldind_U1);
                        method.StoreA();

                        //Accumulator = ReadMemory(memory, ptr);
                        break;
                    case 0b110_000_00:
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


                        method.Compare(program.Accumulator);

                        //Compare(memory, ptr, Accumulator);
                        break;
                    case 0b111_000_00:
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
                        //var val = ReadMemory(memory, ptr);
                        //var acc = Accumulator;
                        //var diff = AccumulatorWithCarry - 1 - val;
                        //Accumulator = (byte)(diff & 0xFF);
                        //CarryFlag = diff >= 0;
                        //var signage = (val ^ acc) >> 7;
                        //OverflowFlag = signage == 1 && ((val ^ Accumulator) & 0x80) == 0x00;
                        break;
                }
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetPtr(
            byte opcode,
            out byte* memory, out ushort ptr,
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
                    memory = Memory;
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
                    memory = Memory;
                    ptr = GetImmediate8();
                    _pc += 2;
                    break;
                case 0b000_000_00: // nn
                case 0b000_010_01: // nn
                case 0b000_000_10: // nn
                case 0b000_010_11: // nn
                    if (writeAccess)
                    {
                        throw new Exception();
                    }

                    memory = _constantValues;
                    ptr = GetImmediate8();
                    _pc += 2;
                    break;
                case 0b000_011_00: // [nnnn]
                case 0b000_011_01: // [nnnn]
                case 0b000_011_10: // [nnnn]
                case 0b000_011_11: // [nnnn]
                    memory = Memory;
                    ptr = GetImmediate16();
                    _pc += 3;
                    break;
                case 0b000_100_01:
                case 0b000_100_11: { // [WORD[nn]+Y]
                    memory = Memory;
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
                    memory = Memory;
                    ptr = (byte)((GetImmediate8() + switchingIndex) & 0xFF);
                    _pc += 2;
                    break;
                case 0b000_110_01:
                case 0b000_110_11:// [nnnn+Y]
                    memory = Memory;
                    ptr = GetImmediate16();
                    ptr = (ushort)((ptr + IndexY) & 0xFFFF);
                    _pc += 3;
                    break;
                case 0b000_111_00: // [nnnn+X]
                case 0b000_111_01: // [nnnn+X]
                case 0b000_111_10:
                case 0b000_111_11:// [nnnn+X]
                    memory = Memory;
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
        private byte GetImmediate8()
        {
            return ReadMemory(_pc + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetImmediate16()
        {
            return (ushort)(ReadMemory(_pc + 1) | (ReadMemory(_pc + 2) << 8));
        }

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
        private string ProcessStatusDescription()
        {
            return ProcessStatusDescription(_ps);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Log(string s)
        {
            _log.Debug($"{ProgramCounter:X4} A:{Accumulator:X2} X:{IndexX:X2} Y:{IndexY:X2} P:{_ps:X2} ({ProcessStatusDescription()}) SP:{_sp:X2} " + s);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadMemoryUnlogged(byte* memory, ushort addr)
        {
            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                var prgMemory = _prgRom;
                if (prgMemory == null)
                {

                }

                // There's a race on unsafe memory access here.
                addr = (ushort)((addr - 0x8000) % _prgRomSize);
                memory = _prgRom;
            }

            return memory[addr];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadMemory(byte* memory, ushort addr)
        {
            if (Trace && memory == Memory)
            {
                Log($"ReadMemory({addr:X4}) = {memory[addr]:X2}");
            }

            return ReadMemoryUnlogged(memory, addr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadMemory(ushort addr)
        {
            return ReadMemory(Memory, addr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadMemoryUnlogged(ushort addr)
        {
            return ReadMemoryUnlogged(Memory, addr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte ReadMemory(int addr)
        {
            return ReadMemory((ushort)addr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMemory(byte* memory, ushort addr, byte value)
        {
            if (memory == _constantValues)
            {
                throw new Exception();
            }

            if (Trace && memory == Memory)
            {
                Log($"WriteMemory({addr:X4}, {value:X2}) (was {memory[addr]:X2})");
            }

            memory[addr] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteMemory(ushort addr, byte value)
        {
            WriteMemory(Memory, addr, value);
        }

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
            if (Trace)
            {
                Log($"InvalidOpcode({opcode:X2})");
            }
        }
    }
}