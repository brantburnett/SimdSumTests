using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace SimdLib
{
    public static class VectorSum
    {
        public static TResult SumLegacy<T, TResult>(ReadOnlySpan<T> span)
            where T : struct, INumber<T>, IMinMaxValue<T>
            where TResult : struct, INumber<TResult>, IMinMaxValue<T>
        {
            TResult sum = TResult.Zero;
            foreach (T value in span)
            {
                checked { sum += TResult.CreateChecked(value); }
            }

            return sum;
        }

        public static TResult Sum<T, TResult>(ReadOnlySpan<T> span)
            where T : struct, INumber<T>
            where TResult : struct, INumber<TResult>
        {
            if (typeof(T) == typeof(TResult)
                && Vector<T>.IsSupported
                && Vector.IsHardwareAccelerated
                && Vector<T>.Count > 2
                && span.Length >= Vector<T>.Count * 4)
            {
                // For cases where the vector may only contain two elements vectorization doesn't add any benefit
                // due to the expense of overflow checking. This means that architectures where Vector<T> is 128 bit,
                // such as ARM or Intel without AVX, will only vectorize spans of ints and not longs.

                if (typeof(T) == typeof(long))
                {
                    return (TResult) (object) SumSignedIntegersVectorized(MemoryMarshal.Cast<T, long>(span));
                }
                if (typeof(T) == typeof(int))
                {
                    return (TResult) (object) SumSignedIntegersVectorized(MemoryMarshal.Cast<T, int>(span));
                }
            }

            TResult sum = TResult.Zero;
            foreach (T value in span)
            {
                checked { sum += TResult.CreateChecked(value); }
            }

            return sum;
        }

        // Note: The overflow checking in this algorithm is only correct for signed integers.
        // If support is ever added for unsigned integers then the overflow check should be
        // overflowTracking |= (accumulator & data) | Vector.AndNot(accumulator | data, sum);
        private static T SumSignedIntegersVectorized<T>(ReadOnlySpan<T> span)
            where T : struct, IBinaryInteger<T>
        {
            Debug.Assert(span.Length >= Vector<T>.Count * 4);

            ref T ptr = ref MemoryMarshal.GetReference(span);
            int length = span.Length;

            // Overflow testing for vectors is based on setting the sign bit of the overflowTracking
            // vector for an element if the following are all true:
            //   - The two elements being summed have the same sign bit. If one element is positive
            //     and the other is negative then an overflow is not possible.
            //   - The sign bit of the sum is not the same as the sign bit of the previous accumulator.
            //     This indicates that the new sum wrapped around to the opposite sign.
            //
            // By bitwise or-ing the overflowTracking vector for each step we can save cycles by testing
            // the sign bits less often. If any iteration has the sign bit set in any element it indicates
            // there was an overflow.

            Vector<T> accumulator = Vector<T>.Zero;

            // Build a test vector with only the sign bit set in each element. JIT will fold this into a constant.
            Vector<T> overflowTestVector = new(T.RotateRight(T.MultiplicativeIdentity, 1));

            // Unroll the loop to sum 4 vectors per iteration
            do
            {
                // Switch accumulators with each step to avoid an additional move operation
                Vector<T> data = Vector.LoadUnsafe(ref ptr);
                Vector<T> accumulator2 = accumulator + data;
                Vector<T> overflowTracking = (accumulator2 ^ accumulator) & (accumulator2 ^ data);

                data = Vector.LoadUnsafe(ref ptr, (nuint)Vector<T>.Count);
                accumulator = accumulator2 + data;
                overflowTracking |= (accumulator ^ accumulator2) & (accumulator ^ data);

                data = Vector.LoadUnsafe(ref ptr, (nuint)Vector<T>.Count * 2);
                accumulator2 = accumulator + data;
                overflowTracking |= (accumulator2 ^ accumulator) & (accumulator2 ^ data);

                data = Vector.LoadUnsafe(ref ptr, (nuint)Vector<T>.Count * 3);
                accumulator = accumulator2 + data;
                overflowTracking |= (accumulator ^ accumulator2) & (accumulator ^ data);

                if ((overflowTracking & overflowTestVector) != Vector<T>.Zero)
                {
                    throw new OverflowException();
                }

                ptr = ref Unsafe.Add(ref ptr, Vector<T>.Count * 4);
                length -= Vector<T>.Count * 4;
            } while (length >= Vector<T>.Count * 4);

            // Process remaining vectors, if any, without unrolling
            if (length >= Vector<T>.Count)
            {
                Vector<T> overflowTracking = Vector<T>.Zero;

                do
                {
                    Vector<T> data = Vector.LoadUnsafe(ref ptr);
                    Vector<T> accumulator2 = accumulator + data;
                    overflowTracking |= (accumulator2 ^ accumulator) & (accumulator2 ^ data);
                    accumulator = accumulator2;
                } while (length >= Vector<T>.Count);

                if ((overflowTracking & overflowTestVector) != Vector<T>.Zero)
                {
                    throw new OverflowException();
                }
            }

            // Add the elements in the vector horizontally.
            // Vector.Sum doesn't perform overflow checking, instead add elements individually.
            T result = T.Zero;
            for (int i = 0; i < Vector<T>.Count; i++)
            {
                checked { result += accumulator[i]; }
            }

            // Add any remaining elements
            for (int i = 0; i < length; i++)
            {
                checked { result += T.CreateChecked(Unsafe.Add(ref ptr, i)); }
            }

            return result;
        }
    }
}