using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace CrcHack.Test;

internal static class Tools {
    public static uint NETCrc32(ReadOnlySpan<byte> input) {
        uint output = 0;
        Crc32.Hash(input, MemoryMarshal.AsBytes(new Span<uint>(ref output)));
        return output;
    }
}

