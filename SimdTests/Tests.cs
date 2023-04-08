namespace SimdTests
{
    public class Tests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(256)]
        [InlineData(1024)]
        public void BasicSum(int size)
        {
            // Arrange

            var list = Enumerable.Range(1, size).ToArray();
            var expectedResult = list.Sum();

            // Act

            var result = SimdLib.VectorSum.SumLegacy<int, int>(list);

            // Assert

            Assert.Equal(expectedResult, result);
        }

        public static IEnumerable<object[]> Lengths()
        {
            for (int i = 256; i < 512; i++)
            {
                yield return new object[] {i};
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(128)]
        [MemberData(nameof(Lengths))]
        public void VectorSum(int size)
        {
            // Arrange

            var list = Enumerable.Range(1, size).ToArray();
            var expectedResult = list.Sum();

            // Act

            var result = SimdLib.VectorSum.Sum<int, int>(list);

            // Assert

            Assert.Equal(expectedResult, result);
        }
    }
}