using System;

namespace SixFiveOhTwo.Tests;

internal static class TestResources
{
    public static IEnumerable<string> ReadLog()
    {
        using var stream = EmbeddedResource("nestest.log");
        using var sr = new StreamReader(stream);
        while (true)
        {
            var line = sr.ReadLine();
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            yield return line;
        }
    }

    public static NesRom ReadRom()
    {
        using var stream = EmbeddedResource("nestest.nes");
        return NesRom.FromFile(stream, CancellationToken.None).Result;
    }

    public static Stream EmbeddedResource(string filename)
    {
        var assembly = typeof(NesLogTest).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(t => t.EndsWith(filename))
            ?? throw new Exception($"Unable to locate EmbeddedResource by name \"{filename}\"");

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new Exception($"Unable to locate EmbeddedResource \"{filename}\"");
    }
}