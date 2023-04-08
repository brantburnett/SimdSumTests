using BenchmarkDotNet.Attributes;
using SimdLib;

namespace Simd
{
    public class SimdInt
    {
        private int[] _array;

        [Params(/*16,*/ 32, 128, 1024)]
        public int Size { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _array = Enumerable.Range(0, Size).ToArray();
        }

        [Benchmark(Baseline = true)]
        public int Basic()
        {
            return VectorSum.SumLegacy<int, int>(_array);
        }

        [Benchmark]
        public int Vectorized()
        {
            return VectorSum.Sum<int, int>(_array);
        }
    }
}
