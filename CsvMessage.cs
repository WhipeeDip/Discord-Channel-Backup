using System;
using System.Text.Json;
using CsvHelper.Configuration.Attributes;
using Discord;

namespace Channel_Backup_Discord_Bot
{
    /// <summary>
    /// A Discord message to be written by CsvHelper.
    /// </summary>
    public class CsvMessage
    {
        private const string DATETIME_FMT = "yyyy-MM-dd hh:mm:ss tt zzz";
        private static readonly JsonSerializerOptions JSON_OPTIONS = new JsonSerializerOptions
        {
            IgnoreNullValues = true,
            WriteIndented = false
        };

        [Index(0)]
        public string Time { get; set; }

        [Index(1)]
        public string User { get; set; }

        [Index(2)]
        public string Message { get; set; }

        [Index(3)]
        public string Embed { get; set; }

        [Index(4)]
        public string Attachments { get; set; }

        [Index(5)]
        public string FailedAttachments { get; set; }

        [Index(6)]
        public string LastEdited { get; set; }

        /// <summary>
        /// Note this is a string, not a bool because of empty field errors.
        /// </summary>
        /// <value></value>
        [Index(7)]
        public string Pinned { get; set; }

        /// <summary>
        /// Note this is a string, not a bool because of empty field errors.
        /// <value></value>
        [Index(8)]
        public string Tts { get; set; }

        [Index(9)]
        public ulong Id { get; set; }

        /// <summary>
        /// Exists because CsvHelper throws header validation exceptions without a default constructor.
        /// Do not actually use this to construct this object.
        /// </summary>
        public CsvMessage() {}

        /// <summary>
        /// Calls CsvMessage(IMessage message, DownloadResults attachments, TimeSpan timeZone),
        /// but converting the TimeZone string to a TimeSpan.
        /// </summary>
        /// <param name="message">A Discord.Net IMessage.</param>
        /// <param name="attachments">Result of each Discord attachment download.</param>
        /// <param name="timeZone">The time zone to format the timestamps to, as a string.</param>
        public CsvMessage(IMessage message, DownloadResults attachments, string timeZone) :
            this(message, attachments, TimeSpan.Parse(timeZone.Replace("+", ""))) // time zone should not have a +
        {
            // empty
        }

        /// <summary>
        /// Creates a CsvMessage from a Discord.Net IMessage.
        /// </summary>
        /// <param name="message">A Discord.Net IMessage.</param>
        /// <param name="attachments">Result of each Discord attachment download.</param>
        /// <param name="timeZone">The time zone to format the timestamps to.</param>
        public CsvMessage(IMessage message, DownloadResults attachments, TimeSpan timeZone)
        {
            Time = ConvertTimeAndFormat(message.Timestamp, timeZone);
            User = $"{message.Author.Username}#{message.Author.Discriminator}";
            Message = message.Content;
            Embed = JsonSerializer.Serialize(message.Embeds, JSON_OPTIONS);
            Attachments = JsonSerializer.Serialize(attachments.Success, JSON_OPTIONS);
            FailedAttachments = JsonSerializer.Serialize(attachments.Failed, JSON_OPTIONS);
            LastEdited = ConvertTimeAndFormat(message.EditedTimestamp, timeZone);
            Pinned = message.IsPinned.ToString();
            Tts = message.IsTTS.ToString();
            Id = message.Id;
        }

        /// <summary>
        /// Applies a time zone to a DateTimeOffset and converts it to a string defined by DATETIME_FMT.
        /// </summary>
        /// <param name="time">The original time.</param>
        /// <param name="timeZone">The time zone offset to apply.</param>
        /// <returns>String representation of the time.</returns>
        private static string ConvertTimeAndFormat(DateTimeOffset? time, TimeSpan timeZone)
        {
            if (time == null)
            {
                return string.Empty;
            }

            DateTimeOffset converted = time.Value.ToOffset(timeZone);
            return converted.ToString(DATETIME_FMT);
        }
    }
}
