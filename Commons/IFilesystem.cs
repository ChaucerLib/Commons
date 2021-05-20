using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Commons
{
    public interface IFilesystem
    {
        byte[] FileReadAllBytes(string path);
        string FileReadAllText(string path);
        void FileWriteAllBytes(string path, byte[] contents);
        Task<byte[]> FileReadAllBytesAsync(string path);
        void FileWriteAllLines(string path, IEnumerable<string> lines);
        Task<string[]> FileReadAllLinesAsync(string path);
        bool FileExists(string path);
        IEnumerable<string> FileReadLines(string path);
        void FileDelete(string path);
        void FileWriteAllText(string path, string text, Encoding encoding);
        string[] DirectoryGetDirectories(string path);
        string[] DirectoryGetFiles(string path, string searchPattern, SearchOption searchOption);
        IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern, SearchOption searchOption);
        FileStream FileStream(string path, FileMode fileMode);
        FileStream FileCreate(string path);
        FileStream FileOpenRead(string path);
    }
}