using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CrcHack;

partial class CRC32Hack {
    /// <summary>
    /// 求一个非零的x，使得crc32(x) = 0。
    /// <para>可以让crc32(data ^ x) = crc32(data)。</para>
    /// </summary>
    /// <param name="sourceLength">源数据长度</param>
    /// <param name="configs">重写配置。两两之间不允许重叠</param>
    /// <returns>如果无解，则返回null</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static byte[]? Zero(int sourceLength, params OverwriteConfig[] configs) => Zero(sourceLength, (IEnumerable<OverwriteConfig>)configs);

    /// <summary>
    /// 求一个非零的x，使得crc32(x) = 0。
    /// <para>可以让crc32(data ^ x) = crc32(data)。</para>
    /// </summary>
    /// <param name="sourceLength">源数据长度</param>
    /// <param name="configs">重写配置。两两之间不允许重叠</param>
    /// <returns>如果无解，则返回null</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static byte[]? Zero(int sourceLength, IEnumerable<OverwriteConfig> configs) {
        if (sourceLength <= 0) return null;

        var vaildConfigs = new List<OverwriteConfig>();
        var operations = new List<IOperation>();
        var handler = new ZeroHandler {
            sourceLength = sourceLength,
            gaussianElimination = new GaussianElimination(32),
            ops = operations,
        };

        foreach (var config in configs) {
            if (config.Offset + config.Length > sourceLength) {
                throw new InvalidOperationException($"{config}超出源数据[length={sourceLength}]范围");
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

    ref struct ZeroHandler {
        public int sourceLength;
        public GaussianElimination gaussianElimination;
        public List<IOperation> ops;
        public byte[]? result;

        private void BuildResult(uint crc32) {
            uint opVector = gaussianElimination.TestVector(crc32);

            var result = new byte[sourceLength];

            for (int i = 0; i < gaussianElimination.Count; i++) {
                if (((opVector >> i) & 1) != 0) {
                    ops[i].Operate(result);
                }
            }
            ops[^1].Operate(result);

            this.result = result;
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
                    crc32 = CRC32.Shift(crc32, sourceLength - config.Offset - i - 1);
                    if (crc32 == 0) continue;

                    ops.Add(new BitOperation((config.Offset + i) * 8 + bitOffset));
                    if (!gaussianElimination.AddVector(crc32)) {
                        // 当前crc32与之前所有crc32线性相关，可以保证存在一组向量xor后可以等于该crc32向量，因此直接返回结果
                        BuildResult(crc32);
                        return true;
                    }
                }
            }

            return oldCount != ops.Count;
        }

        public bool DataHack(in OverwriteConfig config) {
            uint crc32 = MaskCrc32(config.Data, config.Mask);
            crc32 = CRC32.Shift(crc32, sourceLength - (config.Offset + config.Length));
            if (crc32 == 0) return false;

            ops.Add(new DataOperation(config));
            if (!gaussianElimination.AddVector(crc32)) {
                BuildResult(crc32);
            }

            return true;
        }

        static uint MaskCrc32(ReadOnlySpan<byte> data, byte[]? mask) {
            Ref<uint> table = CRC32.table;
            Ref<byte> d = data;
            nint length = data.Length;
            uint hash = 0;

            if (mask is null) {
                for (nint i = 0; i < length; i++) {
                    nint idx = (byte)hash ^ d[i];
                    hash = table[idx] ^ (hash >> 8);
                }
            } else {
                Ref<byte> m = mask;
                for (nint i = 0; i < length; i++) {
                    nint idx = (byte)hash ^ (d[i] & m[i]);
                    hash = table[idx] ^ (hash >> 8);
                }
            }

            return hash;
        }
    }
}

