using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace SixFiveOhTwo.Tests
{
    [TestClass]
    public unsafe class NesLogTest
    {
        private static string[] ReadLog()
        {
            using (var stream = EmbeddedResource("nestest.log"))
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd().Split(new[] { "\r\n", "\n" },
                    StringSplitOptions.RemoveEmptyEntries);
            }
        }
        private static NesRom ReadRom()
        {
            using (var stream = EmbeddedResource("nestest.nes"))
            {
                return NesRom.FromFile(stream, CancellationToken.None).Result;
            }
        }

        private static Stream EmbeddedResource(string filename)
        {
            var assembly = typeof(NesLogTest).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(t => t.EndsWith(filename));

            return assembly.GetManifestResourceStream(resourceName);
        }

        class State
        {
            public byte A { get; set; }
            public byte X { get; set; }
            public byte Y { get; set; }
            public byte P { get; set; }
            public byte[] Bytes { get; set; }
            public ushort PC { get; set; }
            public byte SP { get; set; }
            public int CYC { get; set; }
            public ushort? MemTestAddr { get; set; }
            public byte MemTestValue { get; set; }
            public string FullText { get; set; }

            public void AssertState(Cpu cpu, string fault)
            {
                if (MemTestAddr.HasValue)
                {
                    var val = cpu.ReadMemoryUnlogged(MemTestAddr.Value);
                    Assert.AreEqual(MemTestValue, val, "mem failed: " + FullText);
                }

                Assert.AreEqual(A, cpu.Accumulator, "A failed: " + fault);
                Assert.AreEqual(X, cpu.IndexX, "X failed: " + fault);
                Assert.AreEqual(Y, cpu.IndexY, "Y failed: " + fault);
                Assert.AreEqual(
                    Cpu.ProcessStatusDescription(P),
                    Cpu.ProcessStatusDescription(cpu.ProcessorStatus),
                    "P failed: " + fault);
                Assert.AreEqual(P, cpu.ProcessorStatus, "P failed: " + fault);
                Assert.AreEqual(PC, cpu.ProgramCounter, "PC failed: " + fault);
                Assert.AreEqual(SP, cpu.StackPointer, "SP failed: " + fault);
            }
        }

        private static void SetupLogging()
        {
            var config = new LoggingConfiguration();

            foreach (var target in new TargetWithLayout[] {
                new MemoryTarget("mem-target"),
                new ConsoleTarget("console-target")
            })
            {
                target.Layout = "${longdate} ${message}";
                var rule = new LoggingRule("*", LogLevel.Trace, target);
                config.LoggingRules.Add(rule);
            }

            LogManager.Configuration = config;
        }

        private static State[] LoadLog(Cpu cpu)
        {
            var stateList = new List<State>();
            var logText = ReadLog();

            //                      1       2                   3                             4+
            var exp = new Regex(@"^(....)  (.. .. ..) [\s\*].+?\$?([\dA-Fa-f]{2,4} = ..)?\s+A:(..) X:(..) Y:(..) P:(..) SP:(..) CYC:\s*(\d+)  .+$", RegexOptions.Multiline);

            foreach (var line in logText)
            {
                var match = exp.Match(line);

                Assert.IsTrue(match.Success, "failed to match: " + line);

                var addr = (ushort)Convert.ToInt16(match.Groups[1].Value, 16);

                var bytes = match.Groups[2].Value.Split(
                        new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(b => Convert.ToByte(b, 16)).ToArray();

                var stateStart = 4;
                var state = new State {
                    A = Convert.ToByte(match.Groups[stateStart++].Value, 16),
                    X = Convert.ToByte(match.Groups[stateStart++].Value, 16),
                    Y = Convert.ToByte(match.Groups[stateStart++].Value, 16),
                    P = Convert.ToByte(match.Groups[stateStart++].Value, 16),
                    PC = addr,
                    Bytes = bytes,
                    SP = Convert.ToByte(match.Groups[stateStart++].Value, 16),
                    CYC = Convert.ToInt32(match.Groups[stateStart++].Value),
                    FullText = match.Value
                };

                if (match.Groups[3].Success)
                {
                    var split = match.Groups[3].Value
                        .Split(new[] { " = " }, StringSplitOptions.None);

                    state.MemTestAddr = Convert.ToUInt16(split[0], 16);
                    state.MemTestValue = Convert.ToByte(split[1], 16);
                }

                stateList.Add(state);
            }

            cpu.ProgramCounter = 0xC000;
            cpu.StackPointer = 0xFD;
            cpu.IrqDisableFlag = true;

            return stateList.ToArray();
        }

        [TestMethod]
        public void VerifyLogState()
        {
            SetupLogging();

            Cpu.Trace = true;

            var cpu = new Cpu();
            cpu.Rom = ReadRom();
            var states = LoadLog(cpu);

            foreach (var state in states)
            {
                if (state.PC < 0x8000)
                {
                    continue;
                }

                var addr = state.PC;
                foreach (var b in state.Bytes)
                {
                    var b2 = cpu.ReadMemoryUnlogged(addr++);
                    Assert.AreEqual(b, b2, $"Mismatched bytes at {addr:X4}");
                }
            }

            Assert.AreEqual(8991, states.Length);

            var count = 0;
            State lastState = null;

            foreach (var state in states)
            {
                state.AssertState(cpu, lastState?.FullText);
                cpu.Execute(out int ticks);
                ++count;
                lastState = state;
            }

            Assert.AreEqual(8991, count);
        }
    }
}