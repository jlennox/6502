A 6502 emulator written in C# with no intended use in mind.

Verifies execution against an execution log of nestest.nes from Nintendulator.

There is an out of bounds memory access issue when executing code at 0xFFFD.
There's also memory allocated by the Cpu class that's never deallocated.