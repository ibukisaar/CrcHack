namespace CrcHack;

/// <summary>
/// 配置需要重写的位置、长度、数据和位掩码。
/// </summary>
public readonly struct OverwriteConfig {
    /// <summary>
    /// 字节偏移量（必须）
    /// </summary>
    public readonly int Offset;
    /// <summary>
    /// 字节长度（必须）
    /// </summary>
    public readonly int Length;
    /// <summary>
    /// 覆盖数据（可选）
    /// <para>如果为null，则表示该区域可以随意填充数据，否则将使用指定的数据来覆盖源数据</para>
    /// </summary>
    public readonly byte[]? Data;
    /// <summary>
    /// 位掩码（可选）
    /// <para>位掩码为1的位置表示可以被修改，位掩码为0的位置不受影响。如果为null，则默认位掩码全为1。</para>
    /// </summary>
    public readonly byte[]? Mask;
    /// <summary>
    /// 如果<see cref="Mask"/>全为0，那么<see cref="Invalid"/>为true，则不会处理。
    /// </summary>
    internal readonly bool Invalid;

    /// <param name="offset">
    /// 字节偏移量（必须）
    /// </param>
    /// <param name="length">
    /// 字节长度（必须）
    /// </param>
    /// <param name="data">
    /// 覆盖数据（可选）
    /// <para>如果为null，则表示该区域可以随意填充数据，否则将使用指定的数据来覆盖源数据</para>
    /// </param>
    /// <param name="mask">
    /// 位掩码（可选）
    /// <para>位掩码为1的位置表示可以被修改，位掩码为0的位置不受影响。如果为null，则默认位掩码全为1。</para>
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public OverwriteConfig(int offset, int length, byte[]? data = null, byte[]? mask = null) {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)}必须>=0");
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), $"{nameof(length)}必须>0");

        if (data != null && data.Length != length)
            throw new ArgumentOutOfRangeException(nameof(data), $"如果指定了{nameof(data)}，那么{nameof(data)}.Length必须等于{nameof(length)}");

        if (mask != null && mask.Length != length)
            throw new ArgumentOutOfRangeException(nameof(mask), $"如果指定了{nameof(mask)}，那么{nameof(mask)}.Length必须等于{nameof(length)}");

        if (mask != null) {
            Invalid = AllZero(mask);
        } else {
            Invalid = false;
        }


        Offset = offset;
        Length = length;
        Data = data;
        Mask = mask;


        static bool AllZero(byte[] mask) {
            nint i = 0;
            nint length = mask.Length;
            Ref<byte> m = mask;

            for (; i + 8 <= length; i += 8) {
                if (m[i].As<long>() != 0) {
                    return false;
                }
            }

            for (; i < length; i++) {
                if (m[i] != 0) {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly override string ToString() {
        return $"{nameof(OverwriteConfig)}[{nameof(Offset)}={Offset}, {nameof(Length)}={Length}]";
    }
}
