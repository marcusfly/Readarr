namespace NzbDrone.Core.MediaFiles.TagExtraction
{
    public interface ITagExtractionService
    {
        FileTagResult GetTags(string path);
        void Evict(string path);
        void Clear();
    }
}
