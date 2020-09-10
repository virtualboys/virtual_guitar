namespace AudioSynthesis
{
    using System.Collections;
    using System.IO;

    public interface IResource
    {
        bool ReadAllowed();
        bool WriteAllowed();
        bool DeleteAllowed();
        string GetName();
        Stream OpenResourceForRead();
        Stream OpenResourceForWrite();
        void DeleteResource();
    }
}
