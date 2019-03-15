using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dangl.WebDocumentation.Services;
using Xunit;

namespace Dangl.WebDocumentation.Tests.Services
{
    public class SemanticVersionsOrdererTests
    {
        [Fact]
        public void ArgumentNullExceptionForNullInput()
        {
            Assert.Throws<ArgumentNullException>("versions", () => new SemanticVersionsOrderer(null));
        }

        public class IsStableVersion
        {
            [Theory]
            [InlineData(null, false)]
            [InlineData("", false)]
            [InlineData("beta", false)]
            [InlineData("0.0.0", true)]
            [InlineData("1.0.0", true)]
            [InlineData("9999999999.9999999999.9999999999", true)]
            [InlineData("0.1.0", true)]
            [InlineData("0.0.1", true)]
            [InlineData("1.1.0", true)]
            [InlineData("0.999999999999.0", true)]
            [InlineData("1.0.0-beta004", false)]
            [InlineData("1.0.0-beta0004", false)]
            [InlineData("1.0.0-beta.0004", false)]
            [InlineData("1.0.0-beta", false)]
            [InlineData("George", false)]
            [InlineData("1.0.b", false)]
            [InlineData("a.b.c", false)]
            [InlineData("a.1.2", false)]
            [InlineData("1.1", false)]
            [InlineData("1.1.1.", false)]
            [InlineData("1.1.1.0", true)]
            [InlineData("5.0.15.24587", true)]
            [InlineData("1", false)]
            public void DetectPreviewVersion(string version, bool isExpectedPreview)
            {
                var actual = SemanticVersionsOrderer.IsStableVersion(version);
                Assert.Equal(isExpectedPreview, actual);
            }
        }

        public class GetVersionsOrderedBySemanticVersionDescending
        {
            [Fact]
            public void OrdersCorrect_01()
            {
                var input = new[]
                {
                    "1.0.2",
                    "1.0.0",
                    "1.0.1-beta-3",
                    "1.0.2-build-6",
                    "1.0.2-build-5",
                    "1.0.1",
                    "1.0.2-build-7",
                    "1.0.1-build-4"
                };
                var expectedOutput = new[]
                {
                    "1.0.2",
                    "1.0.2-build-7",
                    "1.0.2-build-6",
                    "1.0.2-build-5",
                    "1.0.1",
                    "1.0.1-build-4",
                    "1.0.1-beta-3",
                    "1.0.0"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_02()
            {
                var input = new[]
                {
                    "1.0.3",
                    "1.0.4",
                    "1.0.1",
                    "1.0.2",
                    "1.0.5-build-5",
                    "1.0.5",
                    "1.0.6-build-7",
                    "1.0.7-build-4"
                };
                var expectedOutput = new[]
                {
                    "1.0.7-build-4",
                    "1.0.6-build-7",
                    "1.0.5",
                    "1.0.5-build-5",
                    "1.0.4",
                    "1.0.3",
                    "1.0.2",
                    "1.0.1"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_03()
            {
                var input = new[]
                {
                    "Gamma",
                    "Alpha",
                    "Delta",
                    "Beta"
                };
                var expectedOutput = new[]
                {
                    "Gamma",
                    "Delta",
                    "Beta",
                    "Alpha"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_04()
            {
                var input = new[]
                {
                    "Gamma",
                    "Alpha",
                    "Alpha.02",
                    "Delta",
                    "Beta"
                };
                var expectedOutput = new[]
                {
                    "Gamma",
                    "Delta",
                    "Beta",
                    "Alpha",
                    "Alpha.02"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_05()
            {
                var input = new[]
                {
                    "1.0.7-build-7",
                    "1.0.7-build-5",
                    "1.0.7-build-15",
                    "1.0.7-build-14"
                };
                var expectedOutput = new[]
                {
                    "1.0.7-build-15",
                    "1.0.7-build-14",
                    "1.0.7-build-7",
                    "1.0.7-build-5"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_06()
            {
                var input = new[]
                {
                    "1.0.7-build-15",
                    "1.0.7-build-14",
                    "1.0.7-build-7",
                    "1.0.7-build-5"
                };
                var expectedOutput = new[]
                {
                    "1.0.7-build-15",
                    "1.0.7-build-14",
                    "1.0.7-build-7",
                    "1.0.7-build-5"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_07()
            {
                var input = new[]
                {
                    "1.0.7-build.7",
                    "1.0.7-build.5",
                    "1.0.7-build.15",
                    "1.0.7-build.14"
                };
                var expectedOutput = new[]
                {
                    "1.0.7-build.15",
                    "1.0.7-build.14",
                    "1.0.7-build.7",
                    "1.0.7-build.5"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_08()
            {
                var input = new[]
                {
                    "1.0.0",
                    "1.0.1"
                };
                var expectedOutput = new[]
                {
                    "1.0.1",
                    "1.0.0"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_09()
            {
                var input = new[]
                {
                    "1.0.0",
                    "1.2.0"
                };
                var expectedOutput = new[]
                {
                    "1.2.0",
                    "1.0.0"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_10()
            {
                var input = new[]
                {
                    "a.b.c",
                    "a.b.d"
                };
                var expectedOutput = new[]
                {
                    "a.b.d",
                    "a.b.c"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            [Fact]
            public void OrdersCorrect_WithCommitCounter_01()
            {
                var input = new[]
                {
                    "2.1.0-beta0014",
                    "2.0.1-beta0003",
                    "1.8.11",
                    "2.1.0-version21-0012",
                    "2.0.0",
                    "2.1.0-version21-0009",
                    "2.0.1-beta0005",
                };
                var expectedOutput = new[]
                {
                    "2.1.0-beta0014",
                    "2.1.0-version21-0012",
                    "2.1.0-version21-0009",
                    "2.0.1-beta0005",
                    "2.0.1-beta0003",
                    "2.0.0",
                    "1.8.11"
                };
                AssertOrdersCorrect(input, expectedOutput);
            }

            private void AssertOrdersCorrect(string[] input, string[] expected)
            {
                var orderer = new SemanticVersionsOrderer(input.ToList());
                var actual = orderer.GetVersionsOrderedBySemanticVersionDescending();
                Assert.Equal(input.Length, expected.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    Assert.Equal(expected[i], actual[i]);
                }
            }
        }

        public class GetNextHigherVersionOrNull
        {
            [Fact]
            public void ArgumentNullExceptionForNullBase()
            {
                Assert.Throws<ArgumentNullException>("baseVersion", () => new SemanticVersionsOrderer(new List<string> { "1", "2" }).GetNextHigherVersionOrNull(null));
            }

            [Fact]
            public void ArgumentNullExceptionForEmptyBase()
            {
                Assert.Throws<ArgumentNullException>("baseVersion", () => new SemanticVersionsOrderer(new List<string> { "1", "2" }).GetNextHigherVersionOrNull(string.Empty));
            }

            [Fact]
            public void ReturnsNullForNoHigherVersion01()
            {
                var baseVersion = "1.1.2";
                var availableVersions = new List<string>
                {
                    "1.0.0",
                    "1.0.2",
                    "1.1.1",
                    "1.1.1-build4"
                };
                AssertReturnsCorrectVersion(baseVersion, null, availableVersions);
            }

            [Fact]
            public void ReturnsNullForNoHigherVersion02()
            {
                var baseVersion = "1.1.2";
                var availableVersions = new List<string>
                {
                    "1.0.0",
                    "1.0.2",
                    "1.1.1",
                    "1.1.2-build4"
                };
                AssertReturnsCorrectVersion(baseVersion, null, availableVersions);
            }

            [Fact]
            public void ReturnsCorrectHigherVersion01()
            {
                var baseVersion = "1.1.0";
                var expectedResult = "1.1.1";
                var availableVersions = new List<string>
                {
                    "1.0.0",
                    "1.0.2",
                    "1.1.1",
                    "1.1.2-build4"
                };
                AssertReturnsCorrectVersion(baseVersion, expectedResult, availableVersions);
            }

            [Fact]
            public void ReturnsCorrectHigherVersion02()
            {
                var baseVersion = "0.1.0";
                var expectedResult = "1.0.0";
                var availableVersions = new List<string>
                {
                    "1.0.0",
                    "1.0.2",
                    "1.1.1",
                    "1.1.2-build4"
                };
                AssertReturnsCorrectVersion(baseVersion, expectedResult, availableVersions);
            }

            [Fact]
            public void ReturnsCorrectHigherVersion03()
            {
                var baseVersion = "1.0.3-build4";
                var expectedResult = "1.1.1";
                var availableVersions = new List<string>
                {
                    "1.0.0",
                    "1.0.2",
                    "1.1.1",
                    "1.1.2-build4"
                };
                AssertReturnsCorrectVersion(baseVersion, expectedResult, availableVersions);
            }

            [Fact]
            public void ReturnsCorrectHigherVersion04()
            {
                var baseVersion = "1.1.2-build3";
                var expectedResult = "1.1.2-build4";
                var availableVersions = new List<string>
                {
                    "1.0.0",
                    "1.0.2",
                    "1.1.1",
                    "1.1.2-build4"
                };
                AssertReturnsCorrectVersion(baseVersion, expectedResult, availableVersions);
            }

            private void AssertReturnsCorrectVersion(string baseVersion, string expectedResult, List<string> availableVersions)
            {
                var versionOrderer = new SemanticVersionsOrderer(availableVersions);
                var actualResult = versionOrderer.GetNextHigherVersionOrNull(baseVersion);
                Assert.Equal(expectedResult, actualResult);
            }
        }
    }
}
