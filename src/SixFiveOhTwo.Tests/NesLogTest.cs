using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace SixFiveOhTwo.Tests;

internal partial class LogState
{
    //                  1       2                      3                           4+
    [GeneratedRegex(@"^(....)  (.. .. ..) [\s\*].+?\$?([\dA-Fa-f]{2,4} = ..)?\s+A:(..) X:(..) Y:(..) P:(..) SP:(..) CYC:\s*(\d+)  .+$", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    private static partial Regex _loglineExpr();

    public byte A { get; init; }
    public byte X { get; init; }
    public byte Y { get; init; }
    public byte P { get; init; }
    public byte[] Bytes { get; init; }
    public ushort PC { get; init; }
    public byte SP { get; init; }
    public int CYC { get; init; }
    public ushort? MemTestAddr { get; set; }
    public byte MemTestValue { get; set; }
    public string FullText { get; init; }

    public void AssertState(Cpu cpu, string? fault)
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

    public static ImmutableArray<LogState> ReadStatesFromLog(Cpu cpu)
    {
        var logText = TestResources.ReadLog();
        var stateList = logText.Select(ParseLine).ToArray();

        cpu.ProgramCounter = 0xC000;
        cpu.StackPointer = 0xFD;
        cpu.IrqDisableFlag = true;

        return stateList.ToImmutableArray();
    }

    private static LogState ParseLine(string line)
    {
        var exp = _loglineExpr();
        var match = exp.Match(line);

        Assert.IsTrue(match.Success, "failed to match: " + line);

        var addr = (ushort)Convert.ToInt16(match.Groups[1].Value, 16);

        var bytes = match.Groups[2].Value
            .Split([' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(b => Convert.ToByte(b, 16)).ToArray();

        var stateStart = 4;
        var state = new LogState
        {
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
            var split = match.Groups[3].Value.Split([" = "], StringSplitOptions.None);

            state.MemTestAddr = Convert.ToUInt16(split[0], 16);
            state.MemTestValue = Convert.ToByte(split[1], 16);
        }

        return state;
    }
}

[TestClass]
public class NesLogTest
{
    private static void SetupNLog()
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

    [TestMethod]
    public void VerifyLogState()
    {
        SetupNLog();

        // Cpu.Trace = true;

        using var cpu = new Cpu(TestResources.ReadRom());
        var states = LogState.ReadStatesFromLog(cpu);

        foreach (var state in states)
        {
            if (state.PC < 0x8000) continue;

            var addr = state.PC;
            foreach (var b in state.Bytes)
            {
                var b2 = cpu.ReadMemoryUnlogged(addr);
                Assert.AreEqual(b, b2, $"Mismatched memory at {addr:X4}");
                addr++;
            }
        }

        Assert.AreEqual(8991, states.Length);

        var count = 0;
        LogState? lastState = null;

        // NOTE: This is expected to execute successfully until it hits NES specific hardware.
        // The first of which is: STA $4015 = FF
        foreach (var state in states)
        {
            state.AssertState(cpu, lastState?.FullText);
            cpu.Execute(out _);
            ++count;
            lastState = state;
        }

        Assert.AreEqual(8991, count);
    }
}