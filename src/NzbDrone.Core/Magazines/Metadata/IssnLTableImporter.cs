using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Magazines.Metadata
{
    public interface IIssnLTableImporter
    {
        string GetIssnL(string issn);
        IReadOnlyList<string> GetSiblings(string issn);
    }

    public class IssnLTableImporter : IIssnLTableImporter
    {
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;
        private readonly Lazy<IssnLTableIndex> _index;

        public IssnLTableImporter(IConfigService configService, IDiskProvider diskProvider, Logger logger)
        {
            _configService = configService;
            _diskProvider = diskProvider;
            _logger = logger;
            _index = new Lazy<IssnLTableIndex>(LoadIndex, true);
        }

        public string GetIssnL(string issn)
        {
            return _index.Value.GetIssnL(issn);
        }

        public IReadOnlyList<string> GetSiblings(string issn)
        {
            return _index.Value.GetSiblings(issn);
        }

        private IssnLTableIndex LoadIndex()
        {
            var path = _configService.IssnLTablePath;
            if (path.IsNullOrWhiteSpace())
            {
                _logger.Info("ISSN-L table importer is disabled because IssnLTablePath is empty.");
                return IssnLTableIndex.Empty;
            }

            if (!_diskProvider.FileExists(path))
            {
                _logger.Info("ISSN-L table importer is disabled because {0} does not exist.", path);
                return IssnLTableIndex.Empty;
            }

            try
            {
                var contents = _diskProvider.ReadAllText(path);
                return IssnLTableIndex.Load(contents.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries));
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to load ISSN-L table from {0}", path);
                return IssnLTableIndex.Empty;
            }
        }

        private class IssnLTableIndex
        {
            public static IssnLTableIndex Empty { get; } = new IssnLTableIndex(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                                                                 new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase));

            private readonly IReadOnlyDictionary<string, string> _issnToIssnL;
            private readonly IReadOnlyDictionary<string, List<string>> _issnLToIssns;

            private IssnLTableIndex(IReadOnlyDictionary<string, string> issnToIssnL, IReadOnlyDictionary<string, List<string>> issnLToIssns)
            {
                _issnToIssnL = issnToIssnL;
                _issnLToIssns = issnLToIssns;
            }

            public static IssnLTableIndex Load(IEnumerable<string> lines)
            {
                var issnToIssnL = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var issnLToIssns = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in lines ?? Array.Empty<string>())
                {
                    if (line.IsNullOrWhiteSpace() || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var parts = line.Split('\t');
                    if (parts.Length < 2)
                    {
                        continue;
                    }

                    var issn = Issn.Normalize(parts[0]);
                    var issnL = Issn.Normalize(parts[1]);
                    if (issn.IsNullOrWhiteSpace() || issnL.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    issnToIssnL[issn] = issnL;
                    if (!issnLToIssns.TryGetValue(issnL, out var siblings))
                    {
                        siblings = new List<string>();
                        issnLToIssns[issnL] = siblings;
                    }

                    if (!siblings.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x, issn)))
                    {
                        siblings.Add(issn);
                    }
                }

                return new IssnLTableIndex(issnToIssnL, issnLToIssns);
            }

            public string GetIssnL(string issn)
            {
                var normalized = Issn.Normalize(issn);
                if (normalized.IsNullOrWhiteSpace())
                {
                    return null;
                }

                return _issnToIssnL.TryGetValue(normalized, out var issnL) ? issnL : null;
            }

            public IReadOnlyList<string> GetSiblings(string issn)
            {
                var normalized = Issn.Normalize(issn);
                if (normalized.IsNullOrWhiteSpace())
                {
                    return Array.Empty<string>();
                }

                var issnL = GetIssnL(normalized);
                if (issnL.IsNullOrWhiteSpace())
                {
                    return Array.Empty<string>();
                }

                if (!_issnLToIssns.TryGetValue(issnL, out var siblings))
                {
                    return Array.Empty<string>();
                }

                var result = new List<string>(siblings);
                if (!result.Any(x => StringComparer.OrdinalIgnoreCase.Equals(x, normalized)))
                {
                    result.Add(normalized);
                }

                return result;
            }
        }
    }
}
