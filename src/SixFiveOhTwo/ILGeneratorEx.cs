using System.Reflection.Emit;

namespace SixFiveOhTwo
{
    public static class ILGeneratorEx
    {
        public static void EmitI4Load(this ILGenerator gen, int i)
        {
            switch (i)
            {
                case -1: gen.Emit(OpCodes.Ldc_I4_M1); return;
                case 0: gen.Emit(OpCodes.Ldc_I4_0); return;
                case 1: gen.Emit(OpCodes.Ldc_I4_1); return;
                case 2: gen.Emit(OpCodes.Ldc_I4_2); return;
                case 3: gen.Emit(OpCodes.Ldc_I4_3); return;
                case 4: gen.Emit(OpCodes.Ldc_I4_4); return;
                case 5: gen.Emit(OpCodes.Ldc_I4_5); return;
                case 6: gen.Emit(OpCodes.Ldc_I4_6); return;
                case 7: gen.Emit(OpCodes.Ldc_I4_7); return;
                case 8: gen.Emit(OpCodes.Ldc_I4_8); return;
            }

            if (i <= 255)
            {
                gen.Emit(OpCodes.Ldc_I4_S, i);
            }

            gen.Emit(OpCodes.Ldc_I4, i);
        }

        public static void EmitStloc(this ILGenerator gen, int i)
        {
            switch (i)
            {
                case 0: gen.Emit(OpCodes.Stloc_0); return;
                case 1: gen.Emit(OpCodes.Stloc_1); return;
                case 2: gen.Emit(OpCodes.Stloc_2); return;
                case 3: gen.Emit(OpCodes.Stloc_3); return;
            }

            if (i <= 255)
            {
                gen.Emit(OpCodes.Stloc_S, (byte)i);
                return;
            }

            gen.Emit(OpCodes.Stloc, (ushort)i);
        }

        public static void EmitStloc(this ILGenerator gen, LocalBuilder local)
        {
            EmitStloc(gen, local.LocalIndex);
        }

        public static void EmitLdloc(this ILGenerator gen, int i)
        {
            switch (i)
            {
                case 0: gen.Emit(OpCodes.Ldloc_0); return;
                case 1: gen.Emit(OpCodes.Ldloc_1); return;
                case 2: gen.Emit(OpCodes.Ldloc_2); return;
                case 3: gen.Emit(OpCodes.Ldloc_3); return;
            }

            if (i <= 255)
            {
                gen.Emit(OpCodes.Ldloc_S, (byte)i);
                return;
            }

            gen.Emit(OpCodes.Ldloc, (ushort)i);
        }

        public static void EmitLdloc(this ILGenerator gen, LocalBuilder local)
        {
            EmitLdloc(gen, local.LocalIndex);
        }

        public static void Emitldloca(this ILGenerator gen, int i)
        {
            if (i <= 255)
            {
                gen.Emit(OpCodes.Ldloca_S, (byte)i);
                return;
            }

            gen.Emit(OpCodes.Ldloca, (ushort)i);
        }

        public static void EmitLdarg(this ILGenerator gen, int i)
        {
            switch (i)
            {
                case 0: gen.Emit(OpCodes.Ldarg_0); return;
                case 1: gen.Emit(OpCodes.Ldarg_1); return;
                case 2: gen.Emit(OpCodes.Ldarg_2); return;
                case 3: gen.Emit(OpCodes.Ldarg_3); return;
            }

            if (i <= 255)
            {
                gen.Emit(OpCodes.Ldarg_S, (byte)i);
                return;
            }

            gen.Emit(OpCodes.Ldarg, (ushort)i);
        }

        public static void EmitLdarga(this ILGenerator gen, int i)
        {
            if (i <= 255)
            {
                gen.Emit(OpCodes.Ldarga_S, (byte)i);
                return;
            }

            gen.Emit(OpCodes.Ldarga, (ushort)i);
        }
    }
}