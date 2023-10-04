using System.Text;
using CrcHack;

Example1();
Console.WriteLine("=================================");
Example2();

static void Example1() {
    var input = new byte[] { 0x12, 0x23, 0x34, 0x45, 0, 0, 0, 0, 0x99, 0x88, 0x77 };
    Console.WriteLine($" input: {Convert.ToHexString(input)}");
    Console.WriteLine($" crc32: {CRC32(input):x8}");

    var config = new OverwriteConfig(offset: 4, length: 4); // input[4..8]可以随意赋值
    byte[]? output = CRC32Hack.Hack(input, targetCrc32: 0, config); // 目标crc32值设为0
    if (output != null) {
        Console.WriteLine($"output: {Convert.ToHexString(output)}");
        Console.WriteLine($" crc32: {CRC32(output):x8}");
    } else {
        Console.WriteLine("无解");
    }
}

static void Example2() {
    ReadOnlySpan<byte> input = """
        function sum(a, b) {
            return a + b;
        }
        """u8;
    Span<byte> template = """
        function sum(a, b) {
            return a - b; // aaaaaaaaa
        }
        """u8.ToArray();

    var config = new OverwriteConfig(offset: 43, length: 9, mask: new byte[] { 0xf, 0xf, 0xf, 0xf, 0xf, 0xf, 0xf, 0xf, 0xf });
    uint targetCrc32 = CRC32(input);
    byte[]? output = CRC32Hack.Hack(template, targetCrc32, config);
    if (output != null) {
        Console.WriteLine(Encoding.UTF8.GetString(input));
        Console.WriteLine($"crc32: {CRC32(input):x8}");
        Console.WriteLine();
        Console.WriteLine(Encoding.UTF8.GetString(output));
        Console.WriteLine($"crc32: {CRC32(output):x8}");
    }
}


static uint CRC32(ReadOnlySpan<byte> input) {
    return ~CrcHack.CRC32.Hash(input);
}