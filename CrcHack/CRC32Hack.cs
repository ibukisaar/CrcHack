using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CrcHack;

public static partial class CRC32Hack {
    private static bool Overlap(in OverwriteConfig config1, in OverwriteConfig config2) {
        int start = Math.Max(config1.Offset, config2.Offset);
        int end = Math.Min(config1.Offset + config1.Length, config2.Offset + config2.Length);
        if (start >= end) {
            return false;
        }

        switch ((config1.Mask, config2.Mask)) {
        case (not null, not null):
            int length = end - start;
            Ref<byte> mask1 = config1.Mask.AsSpan(start - config1.Offset, length);
            Ref<byte> mask2 = config2.Mask.AsSpan(start - config2.Offset, length);

            for (int i = 0; i < length; i++) {
                if ((mask1[i] & mask2[i]) != 0) return true;
            }

            return false;
        default:
            return true;
        }
    }

    /// <summary>
    /// 根据<paramref name="configs"/>重写<paramref name="source"/>，使得重写后的数据crc32值等于<paramref name="targetCrc32"/>。
    /// </summary>
    /// <param name="source">要重写的数据</param>
    /// <param name="targetCrc32">目标crc32</param>
    /// <param name="configs">重写配置。两两之间不允许重叠</param>
    /// <returns>返回重写后的数据。如果无解则返回null。</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static byte[]? Hack(ReadOnlySpan<byte> source, uint targetCrc32, params OverwriteConfig[] configs)
        => Hack(source, targetCrc32, (IEnumerable<OverwriteConfig>)configs);

    /// <summary>
    /// 根据<paramref name="configs"/>重写<paramref name="source"/>，使得重写后的数据crc32值等于<paramref name="targetCrc32"/>。
    /// </summary>
    /// <param name="source">要重写的数据</param>
    /// <param name="targetCrc32">目标crc32</param>
    /// <param name="configs">重写配置。两两之间不允许重叠</param>
    /// <returns>返回重写后的数据。如果无解则返回null。</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static byte[]? Hack(ReadOnlySpan<byte> source, uint targetCrc32, IEnumerable<OverwriteConfig> configs) {
        targetCrc32 = ~targetCrc32;
        targetCrc32 ^= CRC32.Hash(source);
        if (targetCrc32 == 0) return source.ToArray();

        var vaildConfigs = new List<OverwriteConfig>();
        var operations = new List<IOperation>();
        var handler = new HackHandler {
            source = source,
            targetCrc32 = targetCrc32,
            gaussianElimination = new GaussianElimination(32),
            ops = operations,
        };

        foreach (var config in configs) {
            if (config.Offset + config.Length > source.Length) {
                throw new InvalidOperationException($"{config}超出源数据[length={source.Length}]范围");
            }

            if (config.Invalid) continue;

            if (vaildConfigs.Any(c => Overlap(c, config))) {
                throw new InvalidOperationException($"{nameof(configs)}两两之间不允许重叠");
            }

            bool ret = config.Data switch {
                null => handler.BitHack(in config),
                not null => handler.DataHack(in config),
            };
            if (!ret) continue;

            if (handler.result is not null) {
                return handler.result;
            }

            vaildConfigs.Add(config);
        }

        return null;
    }

    static void Copy(Span<byte> result, ReadOnlySpan<byte> data, byte[]? mask) {
        if (mask is null) {
            data.CopyTo(result);
        } else {
            Ref<byte> r = result;
            Ref<byte> d = data;
            Ref<byte> m = mask;
            nint length = result.Length;
            nint i = 0;

            for (; i + 8 <= length; i += 8) {
                r[i].As<long>() ^= m[i].As<long>() & (d[i].As<long>() ^ r[i].As<long>());
            }

            for (; i < length; i++) {
                r[i] ^= (byte)(m[i] & (d[i] ^ r[i]));
            }
        }
    }

    interface IOperation {
        void Operate(byte[] result);
    }

    record BitOperation(int BitOffset) : IOperation {
        public void Operate(byte[] result) {
            int bitOffset = BitOffset;
            result[bitOffset >> 3] ^= (byte)(1 << (bitOffset & 7));
        }
    }

    record DataOperation(OverwriteConfig Config) : IOperation {
        public void Operate(byte[] result) {
            Copy(result.AsSpan(Config.Offset, Config.Length), Config.Data, Config.Mask);
        }
    }

    ref struct HackHandler {
        public ReadOnlySpan<byte> source;
        public uint targetCrc32;
        public GaussianElimination gaussianElimination;
        public List<IOperation> ops;
        public byte[]? result;

        private bool TryBuildResult() {
            uint opVector = gaussianElimination.TestVector(targetCrc32);
            if (opVector == 0) return false;

            var result = GC.AllocateUninitializedArray<byte>(source.Length);
            source.CopyTo(result);

            for (int i = 0; i < ops.Count; i++) {
                if (((opVector >> i) & 1) != 0) {
                    ops[i].Operate(result);
                }
            }

            this.result = result;
            return true;
        }

        public bool BitHack(in OverwriteConfig config) {
            int oldCount = ops.Count;
            byte[]? mask = config.Mask;
            int length = config.Length;
            for (int i = 0; i < length; i++) {
                byte m = mask?[i] ?? 0xff;
                if (m == 0) continue;

                for (int bitOffset = 0; bitOffset < 8; bitOffset++) {
                    byte msg = (byte)(1 << bitOffset);
                    if ((m & msg) == 0) continue;

                    uint crc32 = CRC32.Hash(msg, 0);
                    crc32 = CRC32.Shift(crc32, source.Length - config.Offset - i - 1);
                    if (crc32 == 0) continue;

                    if (gaussianElimination.AddVector(crc32)) {
                        ops.Add(new BitOperation((config.Offset + i) * 8 + bitOffset));

                        if (TryBuildResult()) {
                            return true;
                        }
                    }
                }
            }

            return oldCount != ops.Count;
        }

        public bool DataHack(in OverwriteConfig config) {
            uint crc32 = XorCrc32(source.Slice(config.Offset, config.Length), config.Data, config.Mask);
            crc32 = CRC32.Shift(crc32, source.Length - (config.Offset + config.Length));
            if (crc32 == 0) return false;

            if (gaussianElimination.AddVector(crc32)) {
                ops.Add(new DataOperation(config));
                TryBuildResult();
                return true;
            }

            return false;
        }

        static uint XorCrc32(ReadOnlySpan<byte> data1, ReadOnlySpan<byte> data2, byte[]? mask) {
            Ref<uint> table = CRC32.table;
            Ref<byte> d1 = data1;
            Ref<byte> d2 = data2;
            nint length = data1.Length;
            uint hash = 0;

            if (mask is null) {
                for (nint i = 0; i < length; i++) {
                    nint idx = (byte)hash ^ d1[i] ^ d2[i];
                    hash = table[idx] ^ (hash >> 8);
                }
            } else {
                Ref<byte> m = mask;
                for (nint i = 0; i < length; i++) {
                    nint idx = (byte)hash ^ ((d1[i] ^ d2[i]) & m[i]);
                    hash = table[idx] ^ (hash >> 8);
                }
            }

            return hash;
        }
    }
}
