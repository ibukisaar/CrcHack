using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CrcHack;

/// <summary>
/// 表示最长为32bits向量，可以当成<see cref="uint"/>。
/// <para>下标0到31分别是低位到高位。例如0b1110001 = [1,0,0,0,1,1,1]</para>
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct BitArray32 {
    private uint v0;

    public BitArray32(ReadOnlySpan<char> bits) {
        this = default;

        int length = Math.Min(bits.Length, 32);
        Ref<char> p = bits;
        for (int i = 0; i < length; i++) {
            this[i] = p[i] == '1';
        }
    }

    public readonly bool IsEmpty => v0 == 0;

    public bool this[int i] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get => (v0 & (1U << i)) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            if (value) {
                v0 |= 1U << i;
            } else {
                v0 &= ~(1U << i);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Xor(BitArray32 other) {
        v0 ^= other.v0;
    }

    /// <summary>
    /// 返回向量第一个1的下标。
    /// <para>如果是零向量，则返回-1。</para>
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int FirstOne() {
        if (v0 != 0) return BitOperations.TrailingZeroCount(v0);
        return -1;
    }

    /// <summary>
    /// 将向量转成字符串，其中元素0用'.'表示，因为便于观察。
    /// <para>例如：[1,1,0,0,0] = "11..."。</para>
    /// <para>该方法总是将向量当成32bits。</para>
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public readonly override string ToString() {
        return string.Create(32, this, static (span, @this) => {
            ref char p = ref MemoryMarshal.GetReference(span);
            for (int i = 0; i < 32; i++) {
                Unsafe.Add(ref p, i) = @this[i] ? '1' : '.';
            }
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BitArray32(uint bits) => Unsafe.As<uint, BitArray32>(ref bits);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(BitArray32 @this) => Unsafe.As<BitArray32, uint>(ref @this);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BitArray32(ReadOnlySpan<char> bits) => new BitArray32(bits);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator BitArray32(string bits) => new BitArray32(bits);
}
