using System;
using SixFiveOhTwo.Tests;

namespace SixFiveOhTwo.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        var test = new NesLogTest();
        test.VerifyLogState();
    }
}
