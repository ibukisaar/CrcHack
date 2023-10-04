using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CrcHack;

/* 这是一个GF(2)上的高斯消元，下面给个直观的例子。
 * 我们假设只使用了BitArray32中前10位，构造了一个new GaussianElimination(10)的对象，
 * 
 * 调用AddVector(1..1....11)后，（为了输入和观察方便，1..1....11是[1,0,0,1,0,0,0,0,1,1]向量，下文不再解释）
 *      1 | 1..1....11
 * 
 * 调用AddVector(11111111..)后，
 *      1. | 1..1....11
 *      .1 | 11111111..
 *   (2)+(1)
 *      1. | 1..1....11
 *      11 | .11.111111
 * 
 * 调用AddVector(.....1....)后，
 *      1.. | 1..1....11
 *      11. | .11.111111
 *      ..1 | .....1....
 *   (2)+(3)
 *      1.. | 1..1....11
 *      111 | .11.1.1111
 *      ..1 | .....1....
 *      
 * 调用AddVector(...1.1...1)后，
 *      1... | 1..1....11
 *      11.. | .11.111111
 *      ...1 | ...1.1...1
 *      ..1. | .....1....
 *   (1)+(3)
 *      1..1 | 1....1..1.
 *      11.. | .11.111111
 *      ...1 | ...1.1...1
 *      ..1. | .....1....
 *   (1)+(4), (2)+(4), (3)+(4)
 *      1.11 | 1.......1.
 *      111. | .11.1.1111
 *      ..11 | ...1.....1
 *      ..1. | .....1....
 *      
 * 调用AddVector(...1.1...1)后，返回false，因为(3)+(4)=...1.1...1
 * 
 * 调用AddVector(.....1...1)后，
 *      1.11. | 1.......1.
 *      111.. | .11.1.1111
 *      ..11. | ...1.....1
 *      ..1.. | .....1....
 *      ....1 | .....1...1
 *   (5)+(4)
 *      1.11. | 1.......1.
 *      111.. | .11.1.1111
 *      ..11. | ...1.....1
 *      ..1.. | .....1....
 *      ..1.1 | .........1
 *   (2)+(5), (3)+(5)
 *      1.11. | 1.......1.
 *      11..1 | .11.1.111.
 *      ...11 | ...1......
 *      ..1.. | .....1....
 *      ..1.1 | .........1
 *      
 *  以上，最终GaussianElimination内各字段的值如下：
 *  Count = 5
 *  Left = [1.11., 11..1, ...11, ..1.., ..1.1]
 *  Right = [1.......1., .11.1.111., ...1......, .....1...., .........1]
 *  LinearlyIndependent = [0, 1, 3, 5, 9] // Right对应向量第一个1的下标
 *  
 *  
 *  如果有一个向量1....1..11，我们可以很容易知道(1)+(4)+(5)=1....1..11，同时left向量是1.111 = 1.11. + ..1.. + ..1.1
 *  于是我们回顾之前的输入，很容易得到：
 *  1....1..11 = 1..1....11 (第1个输入)
 *             + .....1.... (第3个输入)
 *             + ...1.1...1 (第4个输入)
 *             + .....1...1 (第5个输入)
 */
/// <summary>
/// GF(2)上的高斯消元。
/// <para>什么是GF(2)？GF(2)是仅包含0、1这两个元素的域，GF(2)的运算规则如下：</para>
/// <code>
/// 0+0=0, 0+1=1, 1+0=1, 1+1=0
/// 0*0=0, 0*1=0, 1*0=0, 1*1=1
/// </code>
/// <para>有加法单位元：0</para>
/// <para>所有元素有加法逆元：-0=0, -1=1</para>
/// <para>有乘法单位元：1</para>
/// <para>非0元素有乘法逆元：1^(-1)=1</para>
/// <para>满足加法/乘法交换律</para>
/// <para>满足加法/乘法结合律</para>
/// <para>满足分配律：a*(b+c)=a*b+a*c</para>
/// </summary>
unsafe public ref struct GaussianElimination {
    private readonly Ref<int> eliminationRecord;
    private readonly int maxCount;

    public int Count { readonly get; private set; }
    public readonly Ref<BitArray32> Left { get; }
    public readonly Ref<BitArray32> Right { get; }
    public readonly Ref<int> LinearlyIndependent { get; }
    public readonly int MaxCount => maxCount;

    public GaussianElimination(int maxCount) {
        if (maxCount is <= 0 or > 32) throw new ArgumentOutOfRangeException(nameof(maxCount));

        this.maxCount = maxCount;
        Count = 0;

        Left = GC.AllocateUninitializedArray<BitArray32>(maxCount);
        Right = GC.AllocateUninitializedArray<BitArray32>(maxCount);
        LinearlyIndependent = GC.AllocateUninitializedArray<int>(maxCount);
        eliminationRecord = GC.AllocateUninitializedArray<int>(maxCount);
    }

    /// <summary>
    /// 将一个<paramref name="vector"/>添加到<see cref="GaussianElimination"/>向量组内。
    /// <para>若<paramref name="vector"/>与向量组线性无关，则更新<see cref="GaussianElimination"/>状态，并返回true。</para>
    /// <para>否则返回false。</para>
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public bool AddVector(BitArray32 vector) {
        if (Count == maxCount) return false;

        int firstOne = vector.FirstOne();
        if (firstOne < 0) return false;

        int firstRow = -1;
        int eliminationRecordCount = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool Elimination(Ref<BitArray32> right, Ref<int> eliminationRecord, ref BitArray32 vector) {
            vector.Xor(right[firstRow]);
            firstOne = vector.FirstOne();
            if (firstOne < 0) return false;
            eliminationRecord[eliminationRecordCount++] = firstRow;
            return true;
        }

        Ref<BitArray32> left = Left;
        Ref<BitArray32> right = Right;
        Ref<int> linearlyIndependent = LinearlyIndependent;
        Ref<int> eliminationRecord = this.eliminationRecord;

        if (Count == 0) goto Success;
        if (firstOne < linearlyIndependent[0]) goto Success;

        for (firstRow++; firstRow < Count - 1;) {
            if (linearlyIndependent[firstRow] < firstOne && firstOne < linearlyIndependent[firstRow + 1]) {
                goto Success;
            } else if (firstOne == linearlyIndependent[firstRow]) {
                if (!Elimination(right, eliminationRecord, ref vector)) return false;
                continue;
            }
            firstRow++;
        }

        if (firstOne == linearlyIndependent[firstRow]) {
            if (!Elimination(right, eliminationRecord, ref vector)) return false;
        }

    Success:
        firstRow++;
        Insert(linearlyIndependent, firstRow, firstOne);
        Insert(right, firstRow, vector);

        BitArray32 newLeftVector = default;
        newLeftVector[Count] = true;
        for (int i = 0; i < eliminationRecordCount; i++) {
            int leftRow = eliminationRecord[i];
            newLeftVector.Xor(left[leftRow]);
        }
        Insert(left, firstRow, newLeftVector);
        Count++;

        ref BitArray32 refVector = ref right[firstRow];

        for (int row = firstRow + 1; row < Count; row++) {
            if (vector[linearlyIndependent[row]]) {
                refVector.Xor(right[row]);
                left[firstRow].Xor(left[row]);
            }
        }
        for (int row = 0; row < firstRow; row++) {
            if (right[row][firstOne]) {
                right[row].Xor(refVector);
                left[row].Xor(left[firstRow]);
            }
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    readonly void Insert<T>(Ref<T> array, int index, in T value) where T : unmanaged {
        for (int i = Count; i > index; i--) {
            array[i] = array[i - 1];
        }
        array[index] = value;
    }

    /// <summary>
    /// 若<paramref name="vector"/>与当前<see cref="GaussianElimination"/>向量组线性相关，则返回关于<paramref name="vector"/>的线性表示。
    /// <para>否则返回零向量。</para>
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public readonly BitArray32 TestVector(BitArray32 vector) {
        if (vector.IsEmpty || Count == 0) return default;

        Ref<BitArray32> left = Left;
        Ref<BitArray32> right = Right;
        Ref<int> linearlyIndependent = LinearlyIndependent;
        int index = 0;
        int count = Count;

        BitArray32 leftResult = default;
        while (!vector.IsEmpty) {
            int first1 = BitOperations.TrailingZeroCount(vector);

            while (index < count && linearlyIndependent[index] < first1) {
                index++;
            }

            if (index == count || linearlyIndependent[index] > first1) {
                return default;
            }

            vector.Xor(right[index]);
            leftResult.Xor(left[index]);
        }
        return leftResult;
    }

    public override readonly string ToString() {
        var sb = new StringBuilder();
        for (int row = 0; row < Count; row++) {
            for (int col = 0; col < Count; col++) {
                sb.Append(Left[row][col] ? '1' : '.');
            }
            sb.Append(" | ");
            for (int col = 0; col < maxCount; col++) {
                sb.Append(Right[row][col] ? '1' : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
