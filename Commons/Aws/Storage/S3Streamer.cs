using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Commons.Extensions;
using Microsoft.Extensions.Logging;

namespace Commons.Aws.Storage
{
    public class S3Streamer :
        IStorageStreamer
    {
        private const int _mBytes = 1024 * 1024;
        private const int _minChunkSize = 5 * _mBytes;
        private readonly HttpClient _http;
        private readonly IAmazonS3 _s3;
        private readonly int _chunkSize;
        private readonly ILogger<IStorageStreamer> _log;

        /// <summary>
        /// A wrapper for S3 to support basic streaming and resumption for large file transfers
        /// </summary>
        /// <param name="reusableHttpClient">A vanilla HTTP client that will live for the lifetime of the application, so make sure it supports DNS refreshes</param>
        /// <param name="s3Client"></param>
        /// <param name="logger"></param>
        /// <param name="chunkSizeBytes">The size of each multipart upload chunk. Must be at least 5MB (5,242,880 bytes)</param>
        public S3Streamer(HttpClient reusableHttpClient, IAmazonS3 s3Client, int chunkSizeBytes, ILogger<IStorageStreamer> logger)
        {
            _http = reusableHttpClient ?? throw new ArgumentNullException(nameof(reusableHttpClient));
            _s3 = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _log = logger ?? throw new ArgumentNullException(nameof(logger));
            _chunkSize = chunkSizeBytes < _minChunkSize
                ? throw new ArgumentOutOfRangeException($"Minimum chunk size must be at least 5MB ({_minChunkSize:N0} bytes, was {chunkSizeBytes:N0} bytes)")
                : chunkSizeBytes;
        }


        public Task<TransferReport> StreamLocalToS3(string localFile, string bucketName, string fileName, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task<TransferReport> StreamHttpToS3(string remoteUrl, string bucketName, string fileName, CancellationToken ct)
        {
            _log.LogInformation($"Chunking and streaming {remoteUrl} to {bucketName}:{fileName} in {_chunkSize:N0} byte increments");
            var cycleTimer = Stopwatch.StartNew();

            var artifactSize = await GetArtifactSize(remoteUrl, ct);
            var totalPartNumbers = GetChunkCount(artifactSize);

            _log.LogInformation($"{remoteUrl} artifact size = {artifactSize:N0} bytes, which will be chunked into {totalPartNumbers:N0} chunks");
            
            var initializedDownload = await InitializeDownload(bucketName, fileName, ct);
            var offset = initializedDownload.Offset;
            var uploadId = initializedDownload.UploadId;
            var parts = new List<PartETag>(initializedDownload.Parts);

            for (var i = initializedDownload.PartNumber; i <= totalPartNumbers; i++)
            {
                var isFullChunk = offset + _chunkSize < artifactSize;
                var take = isFullChunk
                    ? _chunkSize
                    : artifactSize - offset;

                var isLastChunk = i == totalPartNumbers;
                var rangeLimit = isLastChunk
                    ? artifactSize
                    : offset + take - 1;
                _log.LogInformation($"Streaming chunk {i:N0} of {totalPartNumbers:N0}, Range {offset:N0}-{rangeLimit:N0} bytes, requesting range {offset:N0}-{rangeLimit:N0}, Last chunk? {isLastChunk}");
                
                var streamReq = new StreamRequest
                {
                    UploadId = uploadId,
                    BucketName = bucketName,
                    FileName = fileName,
                    RemoteUrl = remoteUrl,
                    Offset = offset,
                    RangeLimit = rangeLimit,
                    PartNumber = i,
                };

                var streamReport = await StreamChunk(streamReq, ct);
                parts.Add(streamReport.PartETag);
                offset += take;
                var memoryUse = GC.GetTotalMemory(forceFullCollection: false) / _mBytes;
                _log.LogInformation($"Streamed chunk {i:N0} of {totalPartNumbers:N0}. Memory usage = {memoryUse:N2}MB:{Environment.NewLine}{streamReport}");
            }
            
            _log.LogInformation($"Last part of {bucketName}:{fileName} uploaded. Initiating completion request for UploadId = {uploadId}");
            var completionReq = new CompleteMultipartUploadRequest
            {
                BucketName = bucketName,
                Key = fileName,
                UploadId = uploadId,
                PartETags = parts,
            };
            
            var completionTimer = Stopwatch.StartNew();
            var completionResp = await _s3.CompleteMultipartUploadAsync(completionReq, ct);
            completionTimer.Stop();
            cycleTimer.Stop();
            
            var msg = $"Completion request for {bucketName}:{fileName} from URL = {remoteUrl} with UploadId = {uploadId} completed in {completionTimer.ElapsedMilliseconds:N0}ms with UploadId = {uploadId} and status code {completionResp.HttpStatusCode}";
            var jobMsg = $"{remoteUrl} was chunked and streamed to {bucketName}:{fileName} in {_chunkSize:N0} byte increments in {cycleTimer.Elapsed.TotalSeconds} seconds";
            if (!completionResp.HttpStatusCode.IsSuccessStatusCode())
            {
                _log.LogError(msg);
                _log.LogError(jobMsg);
                throw new ApplicationException(msg);
            }

            await PruneIncompleteUploads(bucketName, fileName, ct);
            
            _log.LogInformation(msg);
            _log.LogInformation(jobMsg);
            return new TransferReport
            {
                SourceUrl = remoteUrl,
                DestinationUrl = completionResp.Location,
                Chunks = parts.Count,
                Bytes = artifactSize,
                Duration = completionTimer.Elapsed,
            };
        }

        private long GetChunkCount(long artifactSize)
        {
            var dividesEvenly = artifactSize % _chunkSize == 0;
            var totalPartNumbers = dividesEvenly
                ? artifactSize / _chunkSize
                : artifactSize / _chunkSize + 1;
            return totalPartNumbers;
        }

        /// <summary>
        /// Returns the size in bytes of the remote URL.
        /// </summary>
        /// <param name="remoteUrl"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">Thrown if the remote server doesn't support the Range header for this URL</exception>
        private async Task<long> GetArtifactSize(string remoteUrl, CancellationToken ct)
        {
            using (var resp = await _http.GetAsync(remoteUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                var acceptsRangesHeader = resp.Headers
                    .SingleOrDefault(h => h.Key.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase))
                    .Value
                    .SingleOrDefault(v => v.Equals("bytes", StringComparison.OrdinalIgnoreCase));

                if (acceptsRangesHeader is null)
                {
                    throw new NotSupportedException($"Remote server does not support partial downloads for this URL ' {remoteUrl} '");
                }
                
                return long.Parse(resp.Content.Headers.First(h => string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)).Value.First());
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ListPartsResponse>> FindIncompleteUploads(string bucketName, string fileName, CancellationToken ct)
        {
            var listReq = new ListMultipartUploadsRequest
            {
                BucketName = bucketName,
                Prefix = fileName,
            };
            
            _log.LogInformation($"Searching for incomplete uploads for {bucketName}:{fileName}");
            var timer = Stopwatch.StartNew();
            var partialMatches = await _s3.ListMultipartUploadsAsync(listReq, ct);
            timer.Stop();
            _log.LogInformation($"Found {partialMatches.MultipartUploads.Count:N0} possible matches for {bucketName}:{fileName} in {timer.ElapsedMilliseconds:N0}ms");
 
            var partialUploadTasks = partialMatches.MultipartUploads
                .Where(u => u.Key.Equals(fileName, StringComparison.Ordinal))
                .Select(m => new ListPartsRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    UploadId = m.UploadId,
                })
                .Select(lpr => _s3.ListPartsAsync(lpr, ct))
                .ToList();

            if (!partialUploadTasks.Any())
            {
                return new List<ListPartsResponse>();
            }
            
            _log.LogInformation($"{partialUploadTasks.Count:N0} complete matches found. Getting the associated upload details for {bucketName}:{fileName}");
            timer = Stopwatch.StartNew();
            await Task.WhenAll(partialUploadTasks);
            timer.Stop();

            var successfulParts = partialUploadTasks
                .Where(t => t.IsCompletedSuccessfully)
                .Select(t => t.Result)
                .OrderByDescending(u => u.AbortDate)
                .ToList();
            
            _log.LogInformation($"{successfulParts.Count:N0} upload jobs found in {timer.ElapsedMilliseconds:N0}ms");
            return successfulParts;
        }

        public async Task PruneIncompleteUploads(string bucketName, string fileName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(bucketName)) throw new ArgumentNullException(nameof(bucketName));
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

            var timer = Stopwatch.StartNew();

            var incompleteUploads = new List<MultipartUpload>();
            string nextKeyMarker = null;
            string uploadIdMarker = null;
            do
            {
                var paginatedReq = new ListMultipartUploadsRequest
                {
                    BucketName = bucketName,
                    Prefix = fileName,
                    KeyMarker = nextKeyMarker,
                    UploadIdMarker = uploadIdMarker,
                };
                var partialMatches = await _s3.ListMultipartUploadsAsync(paginatedReq, ct);
                nextKeyMarker = partialMatches.NextKeyMarker;
                uploadIdMarker = partialMatches.UploadIdMarker;
                
                var matchQuery = partialMatches.MultipartUploads.Where(pm => string.Equals(fileName, pm.Key, StringComparison.Ordinal));
                incompleteUploads.AddRange(matchQuery);
            } while (nextKeyMarker is null);

            if (!incompleteUploads.Any())
            {
                return;
            }
            
            var abortTasks = incompleteUploads
                .Select(u => new AbortMultipartUploadRequest
                {
                    BucketName = bucketName,
                    Key = u.Key,
                    UploadId = u.UploadId,
                })
                .Select(a => _s3.AbortMultipartUploadAsync(a, ct))
                .ToList();
            await Task.WhenAll(abortTasks);
            timer.Stop();

            var failures = abortTasks.Where(t => !t.IsCompletedSuccessfully);
            foreach (var failure in failures)
            {
                _log.LogError("Unable to delete partial upload", failure.Exception?.Flatten());
            }

            var successes = abortTasks
                .Where(t => t.IsCompletedSuccessfully)
                .ToList();
            
            _log.LogInformation($"{successes.Count:N0} partial uploads deleted for {bucketName}:{fileName} in {timer.ElapsedMilliseconds:N0}ms");
        }

        private async Task<(List<PartDetail> parts, int nextUploadPartNumber)> LoadPriorUploadParts(ListPartsResponse piece, CancellationToken ct)
        {
            var parts = new List<PartDetail>(piece.Parts);
            while (piece.IsTruncated)
            {
                var listPartsReq = new ListPartsRequest
                {
                    BucketName = piece.BucketName,
                    Key = piece.Key,
                    UploadId = piece.UploadId,
                    PartNumberMarker = piece.NextPartNumberMarker.ToString(),
                };

                piece = await _s3.ListPartsAsync(listPartsReq, ct);
                parts.AddRange(piece.Parts);
            }

            // NextPartNumber really means PartNumber of the last successfully-uploaded chunk, so increment by 1
            return (parts, piece.NextPartNumberMarker + 1);
        }

        private record MultipartUploadTarget
        {
            public long Offset { get; init; }
            public int PartNumber { get; init; } = 1;
            public string UploadId { get; init; }
            public IReadOnlyList<PartETag> Parts { get; init; }
        }

        /// <summary>
        /// Creates a multipart upload placeholder if none exists, or returns the multipart upload information if there is a pre-existing partial upload that
        /// can be resumed.
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="fileName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task<MultipartUploadTarget> InitializeDownload(string bucketName, string fileName, CancellationToken ct)
        {
            var existingUploads = await FindIncompleteUploads(bucketName, fileName, ct);
            var keep = existingUploads.FirstOrDefault();
            if (keep is null)
            {
                _log.LogInformation($"No multipart upload found for {bucketName}:{fileName} so one will be created");
                var s3UploadResp = await _s3.InitiateMultipartUploadAsync(bucketName, fileName, ct);
                s3UploadResp.HttpStatusCode.EnsureSuccessStatusCode();
                var emptyMultipartUpload = new MultipartUploadTarget
                {
                    UploadId = s3UploadResp.UploadId,
                    Parts = new List<PartETag>(),
                };
                _log.LogInformation($"Multipart upload created for {bucketName}:{fileName} with UploadId = {s3UploadResp.UploadId}");
                return emptyMultipartUpload;
            }
            
            _log.LogInformation($"Found {existingUploads.Count:N0} incomplete multipart uploads. The most recent of these was abandoned on {keep.AbortDate} with UploadId = {keep.UploadId}");

            var (priorPieces, nextPartNbr) = await LoadPriorUploadParts(keep, ct);
            return new MultipartUploadTarget
            {
                UploadId = keep.UploadId,
                PartNumber = nextPartNbr,
                Parts = new List<PartETag>(priorPieces),
                Offset = (nextPartNbr - 1) * _chunkSize,
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="streamRequest"></param>
        /// <param name="ct"></param>
        /// <returns>The UploadId of the multipart upload</returns>
        private async Task<StreamReport> StreamChunk(StreamRequest streamRequest, CancellationToken ct)
        {
            if (streamRequest is null) throw new ArgumentNullException(nameof(streamRequest));
            
            var req = new HttpRequestMessage(HttpMethod.Get, streamRequest.RemoteUrl)
            {
                Headers = {Range = new RangeHeaderValue(streamRequest.Offset, streamRequest.RangeLimit)},
            };

            var downloadTimer = Stopwatch.StartNew();
            using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                using (var chunk = new MemoryStream(await resp.Content.ReadAsByteArrayAsync(ct)))
                {
                    downloadTimer.Stop();

                    var piece = new UploadPartRequest
                    {
                        BucketName = streamRequest.BucketName,
                        Key = streamRequest.FileName,
                        UploadId = streamRequest.UploadId,
                        PartNumber = streamRequest.PartNumber,
                        InputStream = chunk,
                    };

                    var uploadTimer = Stopwatch.StartNew();
                    var partResp = await _s3.UploadPartAsync(piece, ct);
                    uploadTimer.Stop();
                    var report = new StreamReport
                    {
                        UploadId = streamRequest.UploadId,
                        DownloadTime = downloadTimer.Elapsed,
                        ChunkSize = chunk.Length,
                        UploadTime = uploadTimer.Elapsed,
                        PartETag = new PartETag(partResp.PartNumber, partResp.ETag),
                    };
                    return report;
                }
            }
        }

        /// <summary>
        /// A multipart upload request must already have been initiated, which means the UplaodId will be known
        /// </summary>
        private record StreamRequest
        {
            /// <summary>
            /// Must not be null. Must be a value associated with an existing multipart upload 
            /// </summary>
            public string UploadId { get; init; }
            public string BucketName { get; init; }
            public string FileName { get; init; }
            public int PartNumber { get; init; }
            public string RemoteUrl { get; init; }
            public long Offset { get; init; }
            public long RangeLimit { get; init; }
        }

        private record StreamReport
        {
            public string UploadId { get; init; }
            public TimeSpan DownloadTime { get; init; }
            public TimeSpan UploadTime { get; init; }
            public long ChunkSize { get; init; }
            public PartETag PartETag { get; init; }

            public override string ToString()
            {
                var mBytes = ChunkSize / _mBytes;
                var sb = new StringBuilder();
                sb.AppendLine($"UploadId = {UploadId}");
                sb.AppendLine($"Downloaded {mBytes:N2}MB in {DownloadTime.TotalMilliseconds:N0} ({mBytes / DownloadTime.TotalSeconds:N2} MB/sec)");
                sb.AppendLine($"Uploaded {mBytes:N2}MB in {UploadTime.TotalMilliseconds:N0} ({mBytes / UploadTime.TotalSeconds:N2} MB/sec)");
                return sb.ToString();
            }
        }
    }
}