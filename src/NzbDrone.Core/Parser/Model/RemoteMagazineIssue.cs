using NzbDrone.Core.Magazines;

namespace NzbDrone.Core.Parser.Model
{
    public class RemoteMagazineIssue : RemoteBook
    {
        public Magazine Magazine { get; set; }
        public MagazineIssue Issue { get; set; }
        public ParsedMagazineIssueInfo ParsedMagazineIssueInfo { get; set; }
    }
}
