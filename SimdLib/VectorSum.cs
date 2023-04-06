using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
                && span.Length >= Math.Max(32, Vector<T>.Count * 4))
            {
                // Note: While only two vectors of data are a requirement, the advantages are significantly
                // reduced for short lists of larger integers. This is because fewer operations would be required
                // to sum as a scalar, especially given the added costs of vectorized overflow checks. Therefore,
                // vectorization is only performed if there are at least 32 elements regardless of vector size.

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

            Vector<T> accumulator = Vector<T>.Zero;


            // Build a test vector with only the MSB set.
            Vector<T> overflowTestVector = new(T.RotateRight(T.MultiplicativeIdentity, 1));

            // Unroll the loop to sum 4 vectors per iteration
            do
            {
                Vector<T> data = Vector.LoadUnsafe(ref ptr);
                Vector<T> sum = accumulator + data;
                Vector<T> overflowTracking = (sum ^ accumulator) & (sum ^ data);
                accumulator = sum;

                data = Vector.LoadUnsafe(ref ptr, (nuint)Vector<T>.Count * 1);
                sum = accumulator + data;
                overflowTracking |= (sum ^ accumulator) & (sum ^ data);
                accumulator = sum;

                data = Vector.LoadUnsafe(ref ptr, (nuint)Vector<T>.Count * 2);
                sum = accumulator + data;
                overflowTracking |= (sum ^ accumulator) & (sum ^ data);
                accumulator = sum;

                data = Vector.LoadUnsafe(ref ptr, (nuint)Vector<T>.Count * 3);
                sum = accumulator + data;
                overflowTracking |= (sum ^ accumulator) & (sum ^ data);
                accumulator = sum;

                // Test the elements in overflowTracking to see if the MSB is set in any element.
                // If any iteration sets the MSB in any element of overflowTracking then we've overflowed.
                if ((overflowTracking & overflowTestVector) != Vector<T>.Zero)
                {
                    throw new OverflowException();
                }

                ptr = ref Unsafe.Add(ref ptr, Vector<T>.Count * 4);
                length -= Vector<T>.Count * 4;
            } while (length >= Vector<T>.Count * 4);

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