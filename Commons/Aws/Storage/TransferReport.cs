using System;

namespace Commons.Aws.Storage
{
    public record TransferReport
    {
        /// <summary>
        /// The file that was downloaded
        /// </summary>
        public string SourceUrl { get; init; }
        
        /// <summary>
        /// The place the downloaded file was transferred to
        /// </summary>
        public string DestinationUrl { get; init; }

        /// <summary>
        /// The size of the transfer
        /// </summary>
        public long Bytes { get; set; }

        /// <summary>
        /// The number of chunks the transfer was broken into in the process of streaming to the remote destination
        /// </summary>
        public int Chunks { get; init; } = 1;

        /// <summary>
        /// The length of time the transfer took. If a transfer was interrupted and then resumed, only the duration of the final upload streams will be reported 
        /// </summary>
        public TimeSpan Duration { get; init; }
    }
}