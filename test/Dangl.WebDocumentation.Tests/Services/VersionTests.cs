using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class VersionTests
    {
        [Theory]
        [InlineData("", "", 0)]
        [InlineData("1.0.0", "1.0.0", 0)]
        [InlineData("1.0.0", "1.0.1", -1)]
        [InlineData("1.0.2", "1.0.1", 1)]
        [InlineData("1.0.1-beta0001", "1.0.1-beta0002", -1)]
        [InlineData("1.0.1-be-ta0001", "1.0.1-be-ta0002", -1)]
        [InlineData("1.0.1-be-ta0002", "1.0.1-be-ta0001", 1)]
        [InlineData("1.0.1-be-ta0002", "1.0.1-be-ta0002", 0)]
        [InlineData("1.0.1-beta0001", "1.0.1-alpha0002", -1)]
        [InlineData("1.0.1-alpha0001", "1.0.1-beta0002", -1)]
        [InlineData("1.0.1-beta0002", "1.0.1-alpha0001", 1)]
        [InlineData("1.0.1-alpha0002", "1.0.1-beta0001", 1)]
        [InlineData("1.0.1-be-ta0001", "1.0.1-al-pha0002", -1)]
        [InlineData("1.0.1-al-pha0001", "1.0.1-be-ta0002", -1)]
        [InlineData("1.0.1-be-ta0002", "1.0.1-al-pha0001", 1)]
        [InlineData("1.0.1-al-pha0002", "1.0.1-be-ta0001", 1)]
        public void ComparesCorrect(string source, string dest, int expectedComparison)
        {
            var sourceVer = new WebDocumentation.Services.Version(source);
            var destVer = new WebDocumentation.Services.Version(dest);
            var actual = sourceVer.CompareTo(destVer);
            Assert.Equal(expectedComparison, actual);
        }
    }
}
