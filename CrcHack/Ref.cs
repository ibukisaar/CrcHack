using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CrcHack;

[DebuggerDisplay($"{{{nameof(DebugObject)}}}")]
public readonly ref struct Ref<T> {
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public readonly ref T @ref;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public Ref(void* ptr) => @ref = ref Unsafe.AsRef<T>(ptr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ref(ref T @ref) => this.@ref = ref @ref;

    public bool IsNull {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Unsafe.IsNullRef(ref @ref);
    }

    public ref T this[nint i] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref @ref, i);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Ref<T>(ReadOnlySpan<T> span) => new(ref MemoryMarshal.GetReference(span));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Ref<T>(Span<T> span) => new(ref MemoryMarshal.GetReference(span));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Ref<T>(T[] array) => new(ref MemoryMarshal.GetArrayDataReference(array));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    unsafe public static explicit operator void*(Ref<T> @this) => Unsafe.AsPointer(ref @this.@ref);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<T> operator +(Ref<T> @this, int offset) => new(ref Unsafe.Add(ref @this.@ref, offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<T> operator -(Ref<T> @this, int offset) => new(ref Unsafe.Subtract(ref @this.@ref, offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Ref<TOther> As<TOther>() => new(ref Unsafe.As<T, TOther>(ref @ref));

    private object? DebugObject => IsNull ? null : @ref;
}

public static class RefEx {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<byte> AsByteRef<T>(this ReadOnlySpan<T> span) => ((Ref<T>)span).As<byte>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<byte> AsByteRef<T>(this Span<T> span) => ((Ref<T>)span).As<byte>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<byte> AsByteRef<T>(this T[] span) => ((Ref<T>)span).As<byte>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<TTo> AsRef<TFrom, TTo>(this ReadOnlySpan<TFrom> span) => ((Ref<TFrom>)span).As<TTo>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<TTo> AsRef<TFrom, TTo>(this Span<TFrom> span) => ((Ref<TFrom>)span).As<TTo>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Ref<TTo> AsRef<TFrom, TTo>(this TFrom[] span) => ((Ref<TFrom>)span).As<TTo>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T As<T>(this ref byte byteRef) => ref Unsafe.As<byte, T>(ref byteRef);
}