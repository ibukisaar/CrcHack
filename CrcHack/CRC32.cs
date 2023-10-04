using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CrcHack;

unsafe public static class CRC32 {
    /*
     return crc(index)
     */
    internal static readonly uint[] table = new uint[256];

    static CRC32() {
        const uint P = 0xEDB88320;

        for (int i = 0; i < table.Length; i++) {
            uint hash = (uint)i;
            for (int j = 0; j < 8; j++) {
                if ((hash & 1) != 0) {
                    hash = (hash >> 1) ^ P;
                } else {
                    hash >>= 1;
                }
            }
            table[i] = hash;
        }
    }

    public static uint Hash(ReadOnlySpan<byte> data, uint initValue = 0xffffffff) {
        /* 
         * crc(a) = a*x32 mod p
         * crc(c*x16 + b*x8 + a) = (c*x16 + b*x8 + a)*x32 mod p
         *                       = (((c*x8 + b)*x32 mod p)*x8 mod p) + (a*x32 mod p)
         *                       = (crc(c*x8 + b)*x8 mod p) + crc(a)
         * 令 crc(c*x8 + b) = h3*x24 + h2*x16 + h1*x8 + h0 = <hash>
         *                       = ((h3*x24 + h2*x16 + h1*x8 + h0)*x8 mod p) + crc(a)
         *                       = (h2*x16 + h1*x8 + h0)*x8 + crc(h3) + crc(a)
         *                       = (h2*x16 + h1*x8 + h0)*x8 + crc(h3 + a)
         *                       
         *                       = (hash >> 8) ^ table[(byte)hash ^ data[i]]
         */

        Ref<uint> table = CRC32.table;
        uint hash = initValue;
        for (int i = 0; i < data.Length; i++) {
            byte idx = data[i];
            idx ^= (byte)hash;
            hash = table[idx] ^ (hash >> 8);
        }
        return hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Hash(byte data, uint hash) {
        Ref<uint> table = CRC32.table;
        return table[(byte)hash ^ data] ^ (hash >> 8);
    }

    /// <summary>
    /// <paramref name="crc32"/> shift n，n具体是多少取决于<paramref name="shiftTable"/>
    /// </summary>
    /// <param name="shiftTable"></param>
    /// <param name="crc32"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Shift(Ref<Crc32Shift> shiftTable, uint crc32) {
        return shiftTable[(byte)crc32].v0
            ^ shiftTable[(byte)(crc32 >> 8)].v1
            ^ shiftTable[(byte)(crc32 >> 16)].v2
            ^ shiftTable[(byte)(crc32 >> 24)].v3;
    }

    /// <summary>
    /// 使用查表法，在给定crc32值后添加<paramref name="shift"/>个零字节，并返回计算后的crc32值。
    /// <para>等价于：<code><see cref="CRC32"/>.Hash(new <see cref="byte"/>[<paramref name="shift"/>], <paramref name="crc32"/>)</code></para>
    /// </summary>
    /// <param name="crc32"></param>
    /// <param name="shift">&gt;=0</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static uint Shift(uint crc32, int shift) {
        if (shift < 0) throw new ArgumentOutOfRangeException(nameof(shift));

        Ref<Crc32Shift> table = Crc32Shift.table;

        uint hash = crc32;
        for (; shift != 0; shift >>>= 1) {
            if ((shift & 1) != 0) {
                hash = Shift(table, hash);
            }
            table += 256;
        }
        return hash;
    }


    struct Crc32Shift {
        public uint v0, v1, v2, v3;

        private Crc32Shift(uint v0, uint v1, uint v2, uint v3) {
            this.v0 = v0;
            this.v1 = v1;
            this.v2 = v2;
            this.v3 = v3;
        }

        public static readonly Crc32Shift[] table = new Crc32Shift[256 * 31];

        static Crc32Shift() {
            /*
            table 是 shift = 2**i的表，一共有31组，每组长度256，第i组表示shift = 2**i，每组256个元素表示byte的所有取值。
            例如想计算shift = 0b1001 = 9后的crc32值，
                crc32 shift 9 = crc32 shift 1 shift 8 = Shift(ref table[3*256], Shift(ref table[0*256], crc32))
                              = crc32 shift 8 shift 1 = Shift(ref table[0*256], Shift(ref table[3*256], crc32))
                Shift = static uint CRC32::Shift(ref Crc32Shift shiftTable, uint crc32)


            Crc32Shift中的v0,v1,v2,v3分别表示负责记录从低到高每8bit shift之后的crc32值。
            crc32值是4 byte组成的，假设h0=0x000000aa,h1=0x0000bb00,h2=0x00cc0000,h3=0xdd000000，crc32=h0^h1^h2^h3，那么可以得到以下性质：
                crc32 shift n = (h0 shift n) ^ (h1 shift n) ^ (h2 shift n) ^ (h3 shift n)
            对于table[i * 256 + m]：
                v0 = m shift 2**i, v1 = (m<<8) shift 2**i, v2 = (m<<16) shift 2**i, v3 = (m<<24) shift 2**i。(0<=i<=30, 0<=m<=255)
             

            在计算table之前，prevTable 是 shift = 2**i - 1的表，
            这样在计算table的时候，只需要把prevTable shift 1就能得到table。
            然后prevTable可以利用当前table，让prevTable shift 2**i得到下一个prevTable，
            prevTable会变成shift = 2**(i+1) - 1的表，于是下一个shift = 2**(i+1)的表(table)也很好求，以此类推。
            */
            Crc32Shift* prevTable = stackalloc Crc32Shift[256];
            Ref<uint> crcTable = CRC32.table;
            Ref<Crc32Shift> table = Crc32Shift.table;

            for (uint msg = 0; msg < 256; msg++) {
                ref Crc32Shift prev = ref prevTable[msg];
                ref Crc32Shift curr = ref table[(nint)msg];
                uint v0 = ZeroByteCrc32(crcTable, msg);
                uint v1 = ZeroByteCrc32(crcTable, msg << 8);
                uint v2 = ZeroByteCrc32(crcTable, msg << 16);
                uint v3 = ZeroByteCrc32(crcTable, msg << 24);
                prev = curr = new Crc32Shift(v0, v1, v2, v3);
            }
            table += 256;

            for (int i = 1; i < 31; i++) {
                for (int msg = 0; msg < 256; msg++) {
                    ref Crc32Shift prev = ref prevTable[msg];
                    ref Crc32Shift curr = ref table[msg];
                    curr.v0 = ZeroByteCrc32(crcTable, prev.v0);
                    curr.v1 = ZeroByteCrc32(crcTable, prev.v1);
                    curr.v2 = ZeroByteCrc32(crcTable, prev.v2);
                    curr.v3 = ZeroByteCrc32(crcTable, prev.v3);
                }

                for (int msg = 0; msg < 256; msg++) {
                    ref Crc32Shift prev = ref prevTable[msg];
                    prev.v0 = Shift(table, prev.v0);
                    prev.v1 = Shift(table, prev.v1);
                    prev.v2 = Shift(table, prev.v2);
                    prev.v3 = Shift(table, prev.v3);
                }

                table += 256;
            }

            /// 等价于shift 1
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static uint ZeroByteCrc32(Ref<uint> crcTable, uint crc32) {
                return crcTable[(byte)crc32] ^ (crc32 >> 8);
            }
        }
    }
}

