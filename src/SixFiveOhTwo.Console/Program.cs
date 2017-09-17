using System;
using SixFiveOhTwo.Tests;

namespace SixFiveOhTwo.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var test = new NesLogTest();
            test.VerifyLogState();
        }
    }
}
