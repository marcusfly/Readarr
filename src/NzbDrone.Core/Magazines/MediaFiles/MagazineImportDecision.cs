using System.Collections.Generic;
using NzbDrone.Core.DecisionEngine;

namespace NzbDrone.Core.Magazines.MediaFiles
{
    public class MagazineImportDecision
    {
        public LocalMagazineIssue LocalIssue { get; set; }
        public List<Rejection> Rejections { get; set; } = new List<Rejection>();
        public bool Approved => Rejections.Count == 0;
    }
}
