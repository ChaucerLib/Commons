using System.Threading;
using System.Threading.Tasks;

namespace Commons.Aws.Storage
{
    public interface IStorageStreamer
    {
        /// <summary>
        /// Streams a file from a mounted filesystem to S3 via multi-part upload.
        ///
        /// If a file upload is interrupted, the multipart upload associated with the bucket name + key name that has the most uploaded parts will be resumed.
        /// Once the upload is completed, any dangling multipart uploads that match the bucket name + key name will be deleted. 
        /// </summary>
        /// <param name="localFile">Full path to the file to stream</param>
        /// <param name="bucketName">S3 bucket to stream to</param>
        /// <param name="fileName">S3 key name</param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StreamLocalToS3(string localFile, string bucketName, string fileName, CancellationToken ct);
        
        /// <summary>
        /// Streams a file from a remote URL to S3 by downloading sequential chunks from the remote server, and uploading them to S3.
        /// 1) Chunk is downloaded via HTTP
        /// 2) Chunk is uploaded to S3 as a multipart upload
        /// 3) Repeat until the file is fully downloaded and fully uploaded
        ///
        /// If a file stream is interrupted, the multipart upload associated with the bucket name + key name that has the most uploaded parts will be resumed.
        /// Once the stream is completed, any dangling multipart uploads that match the bucket name + key name will be deleted. 
        /// </summary>
        /// <param name="remoteUrl"></param>
        /// <param name="bucketName"></param>
        /// <param name="fileName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task StreamHttpToS3(string remoteUrl, string bucketName, string fileName, CancellationToken ct);
    }
}