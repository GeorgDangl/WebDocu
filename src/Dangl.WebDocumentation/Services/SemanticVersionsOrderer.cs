using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dangl.WebDocumentation.Services
{
    public class SemanticVersionsOrderer
    {
        private readonly List<string> _versions;
        private List<string> _orderedVersions;

        public SemanticVersionsOrderer(List<string> versions)
        {
            _versions = versions ?? throw new ArgumentNullException(nameof(versions));
        }

        public List<string> GetVersionsOrderedBySemanticVersionDescending()
        {
            if (_orderedVersions != null)
            {
                return _orderedVersions;
            }

            var orderedVersions = _versions
                .Select(v => new Version(v))
                .OrderByDescending(s => s)
                .Select(v => v.Value)
                .ToList();
            _orderedVersions = orderedVersions;
            return _orderedVersions;
        }

        public string GetNextHigherVersionOrNull(string baseVersion)
        {
            if (string.IsNullOrWhiteSpace(baseVersion))
            {
                throw new ArgumentNullException(nameof(baseVersion));
            }

            var versions = _versions.Concat(new[] {baseVersion}).ToList();
            var innerOrderer = new SemanticVersionsOrderer(versions);
            var allVersions = Enumerable.Reverse(innerOrderer.GetVersionsOrderedBySemanticVersionDescending());

            var nextHigherVersion = allVersions
                .SkipWhile(version => version != baseVersion)
                .Skip(1)
                .FirstOrDefault();
            return nextHigherVersion;
        }

        public static bool IsStableVersion(string version)
        {
            const string stableSemverRegex = @"^\d+\.\d+\.\d+$";
            return Regex.IsMatch(version ?? string.Empty, stableSemverRegex);
        }
    }
}
