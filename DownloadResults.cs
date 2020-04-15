using System.Collections.Generic;

namespace Discord_Channel_Backup
{
    /// <summary>
    /// Represents the result of downloading Discord attachments.
    /// </summary>
    public class DownloadResults
    {
        /// <summary>
        /// List of file names of each success.
        /// </summary>
        /// <value></value>
        public List<string> Success { get; }

        /// <summary>
        /// List of file names of each failure.
        /// </summary>
        /// <value></value>
        public List<string> Failed { get; }

        /// <summary>
        /// Creates an empty list for Success and Failed.
        /// </summary>
        public DownloadResults()
        {
            Success = new List<string>();
            Failed = new List<string>();
        }
    }
}