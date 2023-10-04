using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using CrcHack;
using CrcHack.Example;

MD5_CRC32_Collision(targetCrc32: 0, showMsg: true);
Console.WriteLine("==============================================");
MD5_CRC32_Collision(targetCrc32: 0x23333333);
Console.WriteLine("==============================================");
MD5_CRC32_Collision(targetCrc32: 0x00114514);

static void MD5_CRC32_Collision(uint targetCrc32, bool showMsg = false) {
    // 构造33个config，每个config的data是 MD5Collision.Msg1[i] XOR MD5Collision.Msg2[i]
    // MD5Collision中的数据是事先碰撞好的，可以保证 md5(MD5Collision.Msg1[0] + ... + MD5Collision.Msg1[k]) = md5(MD5Collision.Msg2[0] + ... + MD5Collision.Msg2[k])  (0<=k<33, +是拼接的意思)
    // 而且这33组MD5碰撞数据，每组在MD5Collision.Msg1和MD5Collision.Msg2之间随意选取也不会改变MD5值。
    OverwriteConfig[] configs = new OverwriteConfig[33];
    for (int i = 0; i < configs.Length; i++) {
        var data = new byte[128];
        byte[] m1 = MD5Collision.Msg1[i];
        byte[] m2 = MD5Collision.Msg2[i];
        for (int j = 0; j < 128; j++) {
            data[j] = (byte)(m1[j] ^ m2[j]);
        }

        configs[i] = new OverwriteConfig(i * 128, 128, data);
    }

    // ============================================================

    // 求 crc32(xorMsg) = 0，且xorMsg不全为0，xorMsg是33 * 128字节的数据
    // xorMsg每128字节中，要么等于 MD5Collision.Msg1[i] XOR MD5Collision.Msg2[i]，要么等于全0
    // 于是有：
    //    MD5Collision.Msg1[i] XOR xorMsg[i*128 .. (i+1)*128] = MD5Collision.Msg1[i]
    // 或 MD5Collision.Msg1[i] XOR xorMsg[i*128 .. (i+1)*128] = MD5Collision.Msg2[i]
    // 
    //    MD5Collision.Msg2[i] XOR xorMsg[i*128 .. (i+1)*128] = MD5Collision.Msg1[i]
    // 或 MD5Collision.Msg2[i] XOR xorMsg[i*128 .. (i+1)*128] = MD5Collision.Msg2[i]
    var xorMsg = CRC32Hack.Zero(33 * 128, configs)!;

    // ============================================================

    // 把MD5Collision.Msg2放入configs中，然后MD5Collision.Msg1当成源数据，
    // 这样CRC32Hack.Hack就会在MD5Collision.Msg1和MD5Collision.Msg2之间选取消息，使得最终输出等于目标crc32值，同时不改变MD5值
    for (int i = 0; i < configs.Length; i++) {
        configs[i] = new OverwriteConfig(i * 128, 128, MD5Collision.Msg2[i]);
    }

    var msg1 = new byte[33 * 128];
    for (int i = 0; i < 33; i++) {
        MD5Collision.Msg1[i].CopyTo(msg1.AsSpan(i * 128, 128));
    }

    var outMsg1 = CRC32Hack.Hack(msg1, targetCrc32, configs)!;
    var outMsg2 = new byte[33 * 128];
    for (int i = 0; i < outMsg2.Length; i++) {
        outMsg2[i] = (byte)(outMsg1[i] ^ xorMsg[i]);
    }
    // 令 outMsg2 = outMsg1 XOR xorMsg
    // 因为 crc32(outMsg1 XOR xorMsg) = crc32(outMsg1) XOR crc32(xorMsg)
    // 又因为 crc32(xorMsg) = 0 且 xorMsg != 0
    // 所以 crc32(outMsg2) = crc32(outMsg1) 且 outMsg2 != outMsg1

    if (showMsg) {
        const int Length = 32;

        Console.WriteLine("xorMsg:");
        for (int i = 0; i < 33 * 128 / Length; i++) {
            Console.WriteLine(Convert.ToHexString(xorMsg.AsSpan(i * Length, Length)));
        }
        Console.WriteLine();

        Console.WriteLine("msg1:");
        for (int i = 0; i < 33 * 128 / Length; i++) {
            Console.WriteLine(Convert.ToHexString(outMsg1.AsSpan(i * Length, Length)));
        }
        Console.WriteLine();

        Console.WriteLine("msg2:");
        for (int i = 0; i < 33 * 128 / Length; i++) {
            Console.WriteLine(Convert.ToHexString(outMsg2.AsSpan(i * Length, Length)));
        }
        Console.WriteLine();
    }

    Console.WriteLine($"msg1 sha1 = {NETSHA1(outMsg1)}");
    Console.WriteLine($"msg2 sha1 = {NETSHA1(outMsg2)}");
    Console.WriteLine($"msg1 md5 = {NETMD5(outMsg1)}");
    Console.WriteLine($"msg2 md5 = {NETMD5(outMsg2)}");
    Console.WriteLine($"msg1 crc32 = {NETCrc32(outMsg1):x8}");
    Console.WriteLine($"msg2 crc32 = {NETCrc32(outMsg2):x8}");
}


static uint NETCrc32(ReadOnlySpan<byte> input) {
    uint output = 0;
    Crc32.Hash(input, MemoryMarshal.AsBytes(new Span<uint>(ref output)));
    return output;
}

static string NETMD5(ReadOnlySpan<byte> input) {
    Span<byte> output = stackalloc byte[16];
    MD5.HashData(input, output);
    return Convert.ToHexString(output);
}

static string NETSHA1(ReadOnlySpan<byte> input) {
    Span<byte> output = stackalloc byte[20];
    SHA1.HashData(input, output);
    return Convert.ToHexString(output);
}