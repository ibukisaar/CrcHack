
namespace CrcHack.Test;

[TestClass]
public class CRC32Test {
    [TestMethod]
    public void TestCRC32Hash_ZeroData1() {
        Assert.AreEqual(CRC32.Hash(0, 0), 0u);
    }

    [TestMethod]
    public void TestCRC32Hash_ZeroData2() {
        Assert.AreEqual(CRC32.Hash(new byte[5], 0), 0u);
    }

    [TestMethod]
    public void TestCRC32Hash_ZeroData3() {
        Assert.AreEqual(CRC32.Hash(new byte[13], 0), 0u);
    }

    [TestMethod]
    public void TestCRC32Hash_ByteData() {
        Assert.AreEqual(~CRC32.Hash(0x23, 0xffffffff), NETCrc32(new byte[] { 0x23 }));
    }

    [TestMethod]
    public void TestCRC32Hash_RandomData1() {
        ReadOnlySpan<byte> data = new byte[] { 1, 2, 3, 4, 5, 6 };
        Assert.AreEqual(~CRC32.Hash(data), NETCrc32(data));
    }

    [TestMethod]
    public void TestCRC32Hash_RandomData2() {
        Span<byte> data = new byte[100];
        Random.Shared.NextBytes(data);
        Assert.AreEqual(~CRC32.Hash(data), NETCrc32(data));
    }

    [TestMethod]
    public void TestCRC32Shift_ZeroShift1() {
        uint hash = CRC32.Shift(0, 1);
        Assert.AreEqual(hash, 0u);
    }

    [TestMethod]
    public void TestCRC32Shift_ZeroShift2() {
        uint hash = CRC32.Shift(0, 100);
        Assert.AreEqual(hash, 0u);
    }


    [TestMethod]
    public void TestCRC32Shift_Shift1() {
        Span<byte> data = new byte[100];
        Random.Shared.NextBytes(data[0..99]); // 最后一个字节为0

        uint hash = CRC32.Hash(data[0..99]);
        hash = CRC32.Shift(hash, 1);
        Assert.AreEqual(~hash, NETCrc32(data));
    }

    [TestMethod]
    public void TestCRC32Shift_Shift2() {
        Span<byte> data = new byte[100];
        Random.Shared.NextBytes(data[0..50]);

        uint hash = CRC32.Hash(data[0..50]);
        hash = CRC32.Shift(hash, 50);
        Assert.AreEqual(~hash, NETCrc32(data));
    }
}
