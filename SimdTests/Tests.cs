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

            var list = Enumerable.Range(0, size).ToArray();
            var expectedResult = list.Sum();

            // Act

            var result = SimdLib.VectorSum.SumLegacy<int, int>(list);

            // Assert

            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(256)]
        [InlineData(1024)]
        public void VectorSum(int size)
        {
            // Arrange

            var list = Enumerable.Range(0, size).ToArray();
            var expectedResult = list.Sum();

            // Act

            var result = SimdLib.VectorSum.Sum<int, int>(list);

            // Assert

            Assert.Equal(expectedResult, result);
        }
    }
}