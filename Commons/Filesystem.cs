using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Commons
{
    public class Filesystem :
        IFilesystem
    {
        public byte[] FileReadAllBytes(string path) => File.ReadAllBytes(path);
        public Task<byte[]> FileReadAllBytesAsync(string path) => File.ReadAllBytesAsync(path);
        public string FileReadAllText(string path) => File.ReadAllText(path);
        public bool FileExists(string path) => File.Exists(path);
        public IEnumerable<string> FileReadLines(string path) => File.ReadLines(path);
        public Task<string[]> FileReadAllLinesAsync(string path) => File.ReadAllLinesAsync(path);
        void IFilesystem.FileDelete(string path) => File.Delete(path);
        public void FileWriteAllText(string path, string text, Encoding encoding) => File.WriteAllText(path, text, encoding);
        public void FileWriteAllBytes(string path, byte[] contents) => File.WriteAllBytes(path, contents);
        public void FileWriteAllLines(string path, IEnumerable<string> lines) => File.WriteAllLines(path, lines);
        public void UpdateTimestamp(string path) => File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        public string[] DirectoryGetDirectories(string path) => Directory.GetDirectories(path);
        public IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            => Directory.EnumerateFiles(path, searchPattern, searchOption);
        public string[] DirectoryGetFiles(string path, string searchPattern, SearchOption searchOption)
            => Directory.GetFiles(path, searchPattern, searchOption);

        public FileStream FileStream(string path, FileMode fileMode) => new FileStream(path, fileMode);
        public FileStream FileCreate(string path) => File.Create(path);
        public FileStream FileOpenRead(string path) => File.OpenRead(path);
    }
}