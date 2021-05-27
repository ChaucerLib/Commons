using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
        public S3Streamer(HttpClient reusableHttpClient, IAmazonS3 s3Client, ILogger<IStorageStreamer> logger, int chunkSizeBytes)
        {
            _http = reusableHttpClient ?? throw new ArgumentNullException(nameof(reusableHttpClient));
            _s3 = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _log = logger ?? throw new ArgumentNullException(nameof(logger));
            _chunkSize = chunkSizeBytes < _minChunkSize
                ? throw new ArgumentOutOfRangeException($"Minimum chunk size must be at least 5MB ({_minChunkSize:N0} bytes, was {chunkSizeBytes:N0} bytes)")
                : chunkSizeBytes;
        }


        public Task StreamLocalToS3(string localFile, string bucketName, string fileName, CancellationToken ct)
        {
            throw new System.NotImplementedException();
        }

        public async Task StreamHttpToS3(string remoteUrl, string bucketName, string fileName, CancellationToken ct)
        {
            _log.LogInformation($"Chunking and streaming {remoteUrl} to {bucketName}:{fileName} in {_chunkSize:N0} byte increments");
            var cycleTimer = Stopwatch.StartNew();

            long artifactSize;
            using (var resp = await _http.GetAsync(remoteUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                artifactSize = long.Parse(resp.Content.Headers.First(h => string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase)).Value.First());
                var acceptsRangesHeader = resp.Headers
                    .SingleOrDefault(h => h.Key.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase))
                    .Value
                    .SingleOrDefault(v => v.Equals("bytes", StringComparison.OrdinalIgnoreCase));
                
                if (acceptsRangesHeader is null)
                {
                    throw new NotSupportedException("Remote server does not support partial downloads");
                }
            }

            var dividesEvenly = artifactSize % _chunkSize == 0;
            var iterations = dividesEvenly
                ? artifactSize / _chunkSize
                : artifactSize / _chunkSize + 1;
            
            _log.LogInformation($"{remoteUrl} artifact size = {artifactSize:N0} bytes, which will be chunked into {iterations:N0} chunks");
            
            long offset = 0;
            var chunkStart = 0;
            string uploadId = null;
            var parts = new List<PartETag>();
            
            var existingUploads = await FindIncompleteUploads(bucketName, fileName, ct);
            var keep = existingUploads.FirstOrDefault();
            if (keep is not null)
            {
                _log.LogInformation($"Found {existingUploads.Count:N0} incomplete multipart uploads. The most promising of these has {keep.Parts.Count:N0} / {iterations:N0} chunks uploaded with UploadId = {keep.UploadId}");
                
                uploadId = keep.UploadId;
                chunkStart = keep.NextPartNumberMarker;
                parts.AddRange(keep.Parts);

                var toDelete = existingUploads.Skip(1).ToList();
                if (toDelete.Any())
                {
                    _log.LogInformation($"Cleaning up the {toDelete.Count:N0} less promising incomplete multipart uploads");
                    await CleanUpIncompleteUploads(toDelete, ct);
                }
            }

            for (var i = chunkStart; i <= iterations; i++)
            {
                var isFullChunk = offset + _chunkSize < artifactSize;
                var take = isFullChunk
                    ? _chunkSize
                    : artifactSize - offset;

                var isLastChunk = i == iterations;
                var rangeLimit = isLastChunk
                    ? artifactSize
                    : offset + take - 1;
                _log.LogInformation($"Downloading chunk {i:N0} of {iterations:N0}), Range {offset:N0}-{rangeLimit:N0} bytes, requesting range {offset:N0}-{rangeLimit:N0}, Last chunk? {isLastChunk}");
                
                var req = new HttpRequestMessage(HttpMethod.Get, remoteUrl)
                {
                    Headers = {Range = new RangeHeaderValue(offset, rangeLimit)},
                };

                var timer = Stopwatch.StartNew();
                using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    using (var chunk = new MemoryStream(await resp.Content.ReadAsByteArrayAsync(ct)))
                    {
                        timer.Stop();
                        _log.LogInformation($"Chunk {i:N0} / {iterations:N0} downloaded in {timer.ElapsedMilliseconds:N0}ms");
                        if (uploadId is null)
                        {
                            _log.LogInformation($"Initiating multipart upload request for {bucketName}:{fileName}");
                            timer = Stopwatch.StartNew();
                            var s3UploadResp = await _s3.InitiateMultipartUploadAsync(bucketName, fileName, ct);
                            timer.Stop();
                            s3UploadResp.HttpStatusCode.EnsureSuccessStatusCode();
                            uploadId = s3UploadResp.UploadId;
                            _log.LogInformation($"Multipart upload request initiated for {bucketName}:{fileName} from URL = {remoteUrl} in {timer.ElapsedMilliseconds:N0}ms with UploadId = {uploadId} and status code {s3UploadResp.HttpStatusCode}");
                        }
                        
                        var piece = new UploadPartRequest
                        {
                            BucketName = bucketName,
                            Key = fileName,
                            UploadId = uploadId,
                            PartNumber = i, // PartNumbers start at 1
                            InputStream = chunk,
                            IsLastPart = isLastChunk,
                        };

                        _log.LogInformation($"Uploading chunk {chunk:N0} / {iterations:N0} of {bucketName}:{fileName} which is {take:N0} bytes with UploadId = {uploadId}");
                        timer = Stopwatch.StartNew();
                        var partResp = await _s3.UploadPartAsync(piece, ct);
                        timer.Stop();
                        _log.LogInformation($"Uploaded chunk {chunk:N0} / {iterations:N0} of {bucketName}:{fileName} in {timer.ElapsedMilliseconds:N0} which was {take:N0} bytes with UploadId = {uploadId}");
                        parts.Add(new PartETag(partResp.PartNumber, partResp.ETag));
                    }
                }

                offset += take;
                var memoryUse = GC.GetTotalMemory(forceFullCollection: false) / _mBytes;
                _log.LogInformation($"Chunk {i:N0} / {iterations:N0} completed. Memory usage = {memoryUse:N0)}MB Last chunk? {isLastChunk}");
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
            _log.LogInformation(msg);
            _log.LogInformation(jobMsg);
        }

        /// <summary>
        /// Returns an ordered list of incomplete multipart uploads that match the bucket and file name. List is ordered from most uploaded parts to fewest
        /// uploaded parts
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="fileName"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task<List<ListPartsResponse>> FindIncompleteUploads(string bucketName, string fileName, CancellationToken ct)
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
                .OrderByDescending(u => u.Parts.Count)
                .ToList();
            
            _log.LogInformation($"{successfulParts.Count:N0} upload jobs found in {timer.ElapsedMilliseconds:N0}ms");
            return successfulParts;
        }

        private async Task CleanUpIncompleteUploads(ICollection<ListPartsResponse> incompleteUploads, CancellationToken ct)
        {
            if (incompleteUploads is null || incompleteUploads.Any() == false)
            {
                return;
            }
            
            var abortTasks = incompleteUploads
                .Select(u => new AbortMultipartUploadRequest
                {
                    BucketName = u.BucketName,
                    Key = u.Key,
                    UploadId = u.UploadId,
                })
                .Select(a => _s3.AbortMultipartUploadAsync(a, ct))
                .ToList();

            if (!abortTasks.Any())
            {
                return;
            }

            var abortedJobNames = incompleteUploads.Select(u => $"{u.BucketName}:{u.Key} (UploadId = {u.UploadId})");
            
            _log.LogInformation($"Aborting {abortTasks.Count:N0} multipart uploads: {string.Join(Environment.NewLine, abortedJobNames)}");
            var timer = Stopwatch.StartNew();
            await Task.WhenAll(abortTasks);
            timer.Stop();

            var succeeded = abortTasks
                .Where(a => a.IsCompletedSuccessfully)
                .Select(a => a.Result)
                .ToList();
            
            _log.LogInformation($"{succeeded.Count:N0} multipart uploads successfully aborted in {timer.ElapsedMilliseconds:N0}ms");
        }
    }
}