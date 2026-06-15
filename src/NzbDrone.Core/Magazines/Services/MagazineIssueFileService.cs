using System.Collections.Generic;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineIssueFileService
    {
        void Add(MagazineIssueFile file);
        void Update(MagazineIssueFile file);
        void Delete(int id);
        MagazineIssueFile Get(int id);
        List<MagazineIssueFile> GetFilesByMagazine(int magazineId);
        List<MagazineIssueFile> GetFilesForIssue(int issueId);
    }

    public class MagazineIssueFileService : IMagazineIssueFileService
    {
        private readonly IMagazineIssueFileRepository _magazineIssueFileRepository;

        public MagazineIssueFileService(IMagazineIssueFileRepository magazineIssueFileRepository)
        {
            _magazineIssueFileRepository = magazineIssueFileRepository;
        }

        public void Add(MagazineIssueFile file)
        {
            _magazineIssueFileRepository.Insert(file);
        }

        public void Update(MagazineIssueFile file)
        {
            _magazineIssueFileRepository.Update(file);
        }

        public void Delete(int id)
        {
            _magazineIssueFileRepository.Delete(id);
        }

        public MagazineIssueFile Get(int id)
        {
            return _magazineIssueFileRepository.Get(id);
        }

        public List<MagazineIssueFile> GetFilesByMagazine(int magazineId)
        {
            return _magazineIssueFileRepository.GetFilesByMagazine(magazineId);
        }

        public List<MagazineIssueFile> GetFilesForIssue(int issueId)
        {
            return _magazineIssueFileRepository.GetFilesByIssue(issueId);
        }
    }
}
