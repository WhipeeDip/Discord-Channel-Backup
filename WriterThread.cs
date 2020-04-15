using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using Discord;

namespace Channel_Backup_Discord_Bot
{
    /// <summary>
    /// Thread that handles writing messages and downloading attachments.
    /// Must create this before a MessageDownloadThread and pass the Messages reference to it.
    /// </summary>
    public class WriterThread
    {
        public const string TSV_FILE_NAME = "Messages.tsv";
        public const string FILES_DIR = "attachments";
        public static readonly CsvConfiguration CSV_CONFIG = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = true,
            IncludePrivateMembers = false,
            MissingFieldFound = null,
        };

        /// <summary>
        /// The writer thread will take from this queue.
        /// It is a queue of lists because downloading multiple messages at once reduces the request count.
        /// Must pass this to MessageDownloadThread.
        /// </summary>
        /// <value></value>
        public BlockingCollection<List<IMessage>> Messages { get; }

        private readonly string _timeZone;
        private readonly string _path;
        private readonly FileStream _fs;
        private readonly StreamWriter _sw;
        private readonly CsvWriter _csv;
        private readonly IMessage _firstMsg;
        private int _msgWritten;
        private readonly ThreadSafeBool _error;

        /// <summary>
        /// Initializes the streams in append mode for the path.
        /// Header should have already been written elsewhere.
        /// </summary>
        /// <param name="path">Path to the directory for backup.</param>
        /// <param name="firstMsg">An additional message to write that will not be in the queue
        /// (should be the backup confirmation message).</param>
        /// <param name="channel">The Discord channel being backed up.</param>
        /// <param name="msgWritten">To optionally override the message written counter.</param>
        public WriterThread(string path, string timeZone, IMessage firstMsg, ThreadSafeBool error)
        {
            _msgWritten = 1;
            _timeZone = timeZone;
            _path = path;
            _firstMsg = firstMsg;
            _error = error;

            Messages = new BlockingCollection<List<IMessage>>(new ConcurrentQueue<List<IMessage>>());

            if (firstMsg == null) // existing file, don't overwrite
            {
                _fs = new FileStream(Path.Join(_path, TSV_FILE_NAME), FileMode.Append);
            }
            else // non-existing file, create new
            {
                _fs = new FileStream(Path.Join(_path, TSV_FILE_NAME), FileMode.CreateNew);
            }

            _sw = new StreamWriter(_fs);
            _csv = new CsvWriter(_sw, CSV_CONFIG);
        }

        /// <summary>
        /// Call to start the main writing loop.
        /// Note that this should not be called again after a Run(); create a new thread instead.
        /// </summary>
        public void Run()
        {
            Thread.CurrentThread.IsBackground = false;

            try
            {
                if (_firstMsg != null)
                {
                    _csv.WriteHeader<CsvMessage>();
                    _csv.NextRecord();
                    ConvertMessageAndWrite(_firstMsg);
                }

                while (!Messages.IsCompleted)
                {
                    List<IMessage> msgList;

                    try
                    {
                        msgList = Messages.Take();
                    }
                    catch (Exception ex)
                    {
                        // Messages.IsCompleted isn't true when it should be?
                        if (ex is ObjectDisposedException || ex is InvalidOperationException || ex is OperationCanceledException)
                        {
                            break;
                        }

                        throw;
                    }

                    foreach(IMessage msg in msgList)
                    {
                        ConvertMessageAndWrite(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured while writing messages. Try restarting the program in a bit.");
                Console.WriteLine($"\tException: {ex}");
                _error.Value = true;
                return;
            }
            finally
            {
                Messages.Dispose();
                _csv.Dispose();
            }

            Console.WriteLine("Done writing all messages!");
        }

        /// <summary>
        /// Converts the Discord.Net IMessage, downloads atatchments, and writes.
        /// </summary>
        /// <param name="message">The Discord.Net IMessage to be written.</param>
        /// <returns>The CsvMessage version of the Discord.Net IMessage.</returns>
        private CsvMessage ConvertMessageAndWrite(IMessage message)
        {
            DownloadResults attachments = DownloadAttachments(message);
            CsvMessage msg = new CsvMessage(message, attachments, _timeZone);

            _csv.WriteRecord(msg);
            _csv.NextRecord();
            Console.WriteLine($"Wrote message #{_msgWritten}");

            _msgWritten++;
            return msg;
        }

        /// <summary>
        /// Downloads the attachment(s) that are part of the message.
        /// Note the downloads are not async.
        /// </summary>
        /// <param name="message">The message with attachments.</param>
        /// <returns>Results of all attachments in this message.</returns>
        private DownloadResults DownloadAttachments(IMessage message)
        {
            DownloadResults results = new DownloadResults();
            if (message.Attachments.Count == 0)
            {
                return results;
            }

            string path = Path.Join(_path, FILES_DIR, message.Id.ToString());
            Directory.CreateDirectory(path);

            uint count = 0;
            foreach (IAttachment attachment in message.Attachments)
            {
                string filePath = Path.Join(path, attachment.Filename);
                string subdir = Path.Join(FILES_DIR, attachment.Filename); // for writing attachments/msgId/filename for easier reading in tsv

                // basic duplicate checking/renaming just in case...
                // but i think shouldn't be possible to have attachments with duplicate file names
                while (File.Exists(filePath))
                {
                    count++;
                    filePath += $"_{count}";
                    subdir += $"_{count}";
                }

                // attempt proxy url download first
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(attachment.ProxyUrl, filePath);
                        results.Success.Add(attachment.Filename);
                    }
                    continue;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while downloading {attachment.ProxyUrl}, trying another URL.");
                }

                // fallback to original url if cdn failed, which does seem to happen
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(attachment.Url, filePath);
                        results.Success.Add(attachment.Filename);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while downloading {attachment.Url}. This attachment will be skipped.");
                    results.Failed.Add(attachment.Filename);
                }
            }

            return results;
        }
    }
}
