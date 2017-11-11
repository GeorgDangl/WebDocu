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
}
