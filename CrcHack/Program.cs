
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using CrcHack;

//ReadOnlySpan<byte> source = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 11, 12, 13, 0, 0, 0, 0, 0 };
//var configs = new OverwriteConfig[10];
//configs[0] = new OverwriteConfig(4, 3);
//for (int i = 1; i < 10; i++) {
//    configs[i] = new OverwriteConfig(6 + i, 1, new byte[] { (byte)i });
//}

//var newData = CRC32Hack.Zero(8, new OverwriteConfig(0, 5, mask: new byte[] { 0xff, 0xff, 0xff, 0xff, 0x01 }));
//if (newData != null) {
//    Console.WriteLine(Convert.ToHexString(source));
//    Console.WriteLine(Convert.ToHexString(newData));
//    var crc32_1 = CRC32.Hash(source, 0);
//    var crc32_2 = CRC32.Hash(newData, 0);
//    Console.WriteLine($"{crc32_1:x8}");
//    Console.WriteLine($"{crc32_2:x8}");
//}

////long v = MemoryMarshal.Read<long>(newData);
////int s = 0;
////for (int i = 0; i < 32; i++) {
////    if (((v >> i) & 1) != 0) {
////        s += i + 32;
////    }
////}
////Console.WriteLine(s % 255);

//for (int c = 1; c < 256; c++) {
//    uint aValue = CRC32.Hash((byte)c, 0); 

//    const uint P = 0xEDB88320;

//    uint hash = 0x80000000;
//    long count = 0;
//    do {
//        if ((hash & 1) == 0) {
//            hash >>= 1;
//        } else {
//            hash = (hash >> 1) ^ P;
//        }
//        count++;
//    } while (hash != aValue);
//    Console.WriteLine($"{c:x2}, {count}");
//}

GaussianElimination ge = new GaussianElimination(10);
ge.AddVector("1..1....11");
Console.WriteLine(ge.ToString());
ge.AddVector("11111111..");
Console.WriteLine(ge.ToString());
ge.AddVector(".....1....");
Console.WriteLine(ge.ToString());
ge.AddVector("...1.1...1");
Console.WriteLine(ge.ToString());
ge.AddVector("...1.1...1");
Console.WriteLine(ge.ToString());
ge.AddVector(".....1...1");
Console.WriteLine(ge.ToString());