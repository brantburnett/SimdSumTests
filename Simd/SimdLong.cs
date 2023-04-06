using BenchmarkDotNet.Attributes;
using SimdLib;

namespace Simd
{
    [DisassemblyDiagnoser]
    [ShortRunJob]
    public class SimdLong
    {
        private long[] _array;

        [Params(/*16,*/ 32, 128/*, 1024*/)]
        public int Size { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _array = Enumerable.Range(0, Size).Select(p => (long)p).ToArray();
        }

        [Benchmark(Baseline = true)]
        public long Basic()
        {
            return VectorSum.SumLegacy<long, long>(_array);
        }

        [Benchmark]
        public long Vectorized()
        {
            return VectorSum.Sum<long, long>(_array);
        }
    }
}
