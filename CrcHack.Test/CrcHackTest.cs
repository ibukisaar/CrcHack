
namespace CrcHack.Test;

[TestClass]
public class CrcHackTest {
    [TestMethod]
    public void TestCrcHack_Hack1() {
        Span<byte> data = new byte[4];
        var newData = CRC32Hack.Hack(data, 0x88888888u, new OverwriteConfig(0, 4));

        Assert.AreEqual(NETCrc32(newData), 0x88888888u);
    }

    [TestMethod]
    public void TestCrcHack_Hack2() {
        Span<byte> data = new byte[32];
        OverwriteConfig[] configs = new OverwriteConfig[32];
        for (int i = 0; i < configs.Length; i++) {
            configs[i] = new OverwriteConfig(i, 1, new byte[] { (byte)(i + 1) });
        }
        var newData = CRC32Hack.Hack(data, 0x12345678u, configs);

        Assert.AreEqual(NETCrc32(newData), 0x12345678u);
    }

    [TestMethod]
    public void TestCrcHack_Hack3() {
        Span<byte> data = new byte[100];
        Random.Shared.NextBytes(data);
        var newData = CRC32Hack.Hack(data, 0xccccccccu, new OverwriteConfig(0, 4));

        if (newData != null) {
            Assert.AreEqual(NETCrc32(newData), 0xccccccccu);
        }
    }

    [TestMethod]
    public void TestCrcHack_Hack4() {
        Span<byte> data = new byte[100];
        Random.Shared.NextBytes(data);

        OverwriteConfig[] configs = {
            new OverwriteConfig(0, 1, new byte[] { 0 }),
            new OverwriteConfig(1, 1, new byte[] { 1 }),
            new OverwriteConfig(2, 1, new byte[] { 2 }),
            new OverwriteConfig(3, 1, new byte[] { 3 }),
            new OverwriteConfig(4, 1, new byte[] { 4 }),
            new OverwriteConfig(5, 1, new byte[] { 5 }),
            new OverwriteConfig(6, 1, new byte[] { 6 }),
            new OverwriteConfig(7, 1, new byte[] { 7 }),
            new OverwriteConfig(8, 4, mask: new byte[] { 0b1111_1100, 0b1111_1100, 0b1111_1100, 0b1111_1100 })
        };

        var newData = CRC32Hack.Hack(data, 0x23333333u, configs);

        if (newData != null) {
            Assert.AreEqual(NETCrc32(newData), 0x23333333u);
        }
    }

    [TestMethod]
    public void TestCrcHack_Hack5() {
        Span<byte> data = new byte[100];
        var newData = CRC32Hack.Hack(data, 0x23333333u);

        Assert.IsNull(newData);
    }

    [TestMethod]
    public void TestCrcHack_Zero1() {
        var xorData = CRC32Hack.Zero(5, new OverwriteConfig(0, 5));

        Assert.AreEqual(CRC32.Hash(xorData, 0), 0u);
    }

    [TestMethod]
    public void TestCrcHack_Zero2() {
        var xorData = CRC32Hack.Zero(5, new OverwriteConfig(0, 5));
        Assert.IsNotNull(xorData);
        Assert.AreEqual(CRC32.Hash(xorData, 0), 0u);

        Span<byte> data1 = new byte[5];
        Span<byte> data2 = new byte[5];
        Random.Shared.NextBytes(data1);

        for (int i = 0; i < 5; i++) {
            data2[i] = (byte)(data1[i] ^ xorData[i]);
        }

        Assert.IsFalse(data1.SequenceEqual(data2));
        Assert.AreEqual(NETCrc32(data1), NETCrc32(data2));
    }

    [TestMethod]
    public void TestCrcHack_Zero3() {
        OverwriteConfig[] configs = {
            new OverwriteConfig(0, 1, new byte[] { 1 }),
            new OverwriteConfig(1, 1, new byte[] { 2 }),
            new OverwriteConfig(2, 1, new byte[] { 3 }),
            new OverwriteConfig(3, 1, new byte[] { 4 }),
            new OverwriteConfig(4, 1, new byte[] { 5 }),
            new OverwriteConfig(5, 1, new byte[] { 6 }),
            new OverwriteConfig(6, 1, new byte[] { 7 }),
            new OverwriteConfig(7, 1, new byte[] { 8 }),
            new OverwriteConfig(8, 1, new byte[] { 9 }),
            new OverwriteConfig(9, 4, mask: new byte[] { 0b1111_1100, 0b1111_1100, 0b1111_1100, 0b1111_1100 })
        };

        var xorData = CRC32Hack.Zero(20, configs);
        Assert.IsNotNull(xorData);
        Assert.AreEqual(CRC32.Hash(xorData, 0), 0u);

        Span<byte> data1 = new byte[20];
        Span<byte> data2 = new byte[20];
        Random.Shared.NextBytes(data1);

        for (int i = 0; i < 20; i++) {
            data2[i] = (byte)(data1[i] ^ xorData[i]);
        }

        Assert.IsFalse(data1.SequenceEqual(data2));
        Assert.AreEqual(NETCrc32(data1), NETCrc32(data2));
    }
}
