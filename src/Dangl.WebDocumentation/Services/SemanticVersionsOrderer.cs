using System;
using System.Collections.Generic;
using System.Linq;

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
    }
}
