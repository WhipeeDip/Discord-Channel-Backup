using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Discord;
using Discord.WebSocket;

namespace Discord_Channel_Backup
{
    public class BackupThread
    {
        private const string TITLE = "Channel Backup";
        private const string TSV_CHRON = "Messages_Chronological.tsv";
        private static readonly Color COLOR_SUCCESS = new Color(0, 0xFF, 0);
        private static readonly Color COLOR_ERR = new Color(0xFF, 0, 0);
        private static readonly ThreadSafeBool Running = new ThreadSafeBool();

        private readonly IMessageChannel _channel;
        private readonly SocketGuild _guild;
        private readonly DiscordSocketClient _client;
        private readonly SocketUser _user;
        private string _path;
        private readonly ThreadSafeBool _error;

        public BackupThread(IMessageChannel channel, DiscordSocketClient client, SocketUser user)
        {
            _channel = channel;
            _guild = ((SocketGuildChannel) _channel).Guild;
            _client = client;
            _user = user;
            _error = new ThreadSafeBool();
            _error.Value = false;
        }

        /// <summary>
        /// Method to call when starting this thread.
        /// Confirms and begins backup process.
        /// </summary>
        /// <param name="startParamsObj">Contains the IMessageChannel and DiscordSocketClient. Must be of type BackupThreadStartParams.</param>
        public async void Run()
        {
            Thread.CurrentThread.IsBackground = false;
            // Console.WriteLine("BackupThread::Start()");

            // only allow one backup thread for now
            if (Running.Value)
            {
                await _channel.SendMessageAsync("", embed: new EmbedBuilder
                {
                    Title = TITLE,
                    Description = "A backup is already running. Only one backup may run at a time.",
                    Color = COLOR_ERR
                }.Build());

                return;
            }

            Running.Value = true;

            await _channel.SendMessageAsync("", embed: new EmbedBuilder
            {
                Title = TITLE,
                Description = "Received backup command, waiting for confirmation in terminal...",
                Color = COLOR_SUCCESS
            }.Build());

            Console.WriteLine($"\nReceived backup command from:\n" +
                              $"\t   User: @{_user.Username}#{_user.Discriminator} ({_user.Id})\n" +
                              $"\t Server: {_guild.Name} ({_guild.Id})\n" +
                              $"\tChannel: #{_channel.Name} ({_channel.Id})");

            // confirm backup in program
            while (true)
            {
                Console.WriteLine("Continue? Y/N:");
                string input = Console.ReadLine().Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (input == "Y")
                {
                    break;
                }
                else if (input == "N")
                {
                    Console.WriteLine("Rejected backup command.");
                    await _channel.SendMessageAsync("", embed: new EmbedBuilder
                    {
                        Title = TITLE,
                        Description = "Backup command was rejected.",
                        Color = COLOR_ERR
                    }.Build());
                    SharedUtils.SetReadyStatus(_client);
                    Running.Value = false;
                    return;
                }
            }

            IUserMessage startingBackupMsg = await _channel.SendMessageAsync("", embed: new EmbedBuilder
            {
                Title = TITLE,
                Description = "Backup command confirmed, continuing.\nNothing will be backed up after this message.",
                Color = COLOR_SUCCESS
            }.Build());

            IDisposable typing = await SharedUtils.SetBackupBusyStatus(_client, _channel);

            // begin backup logic

            _path = PreprocessFolders();
            IMessage existingMsg = null;
            try
            {
                existingMsg = await CheckExisting();
            }
            catch (Exception ex) // couldn't validate existing files
            {
                if (!(ex is ReaderException || ex is ValidationException))
                {
                    throw;
                }

                while (true)
                {
                    Console.WriteLine("********** PLEASE READ CAREFULLY BEFORE ANSWERING **********");
                    Console.WriteLine($"There was an error while validating existing files in directory {_path}.\n" +
                                       "To continue, the program needs to irreversibly delete EVERYTHING in this directory. Continue? (Y/N): ");
                    string input = Console.ReadLine().Trim().ToUpper();
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        continue;
                    }

                    if (input == "Y")
                    {
                        CleanDirectory();
                        break;
                    }
                    else if (input == "N")
                    {
                        Console.WriteLine("Aborting backup.");
                        await _channel.SendMessageAsync("", embed: new EmbedBuilder
                        {
                            Title = TITLE,
                            Description = "Backup failed.\nBot owner should check terminal.",
                            Color = COLOR_ERR
                        }.Build());
                        SharedUtils.SetReadyStatus(_client, typing);
                        Running.Value = false;
                        return;
                    }
                }
            }

            WriterThread writerThread;
            MessageDownloadThread msgDlThread;
            if (existingMsg == null)
            {
                writerThread = new WriterThread(_path, Program.FormatTimeZone, startingBackupMsg, _error, Program.IncludeAttachments);
                msgDlThread = new MessageDownloadThread(_channel, writerThread.Messages, startingBackupMsg, _error);
            }
            else
            {
                Console.WriteLine("Existing message found. Starting from message: " +
                                 $"\tID: {existingMsg.Id}" +
                                 $"\tMessage: {SharedUtils.TruncateString(existingMsg.Content, 80)}");
                writerThread = new WriterThread(_path, Program.FormatTimeZone, null, _error, Program.IncludeAttachments);
                msgDlThread = new MessageDownloadThread(_channel, writerThread.Messages, existingMsg, _error);
            }

            Console.WriteLine("Beginning backup. This can take a while.");

            Thread mThread = new Thread(msgDlThread.Run);
            Thread wThread = new Thread(writerThread.Run);

            mThread.Start();
            wThread.Start();

            mThread.Join();
            wThread.Join();

            if (_error.Value)
            {
                await _channel.SendMessageAsync("", embed: new EmbedBuilder
                {
                    Title = TITLE,
                    Description = "Backup failed.\nBot owner should check terminal.",
                    Color = COLOR_ERR
                }.Build());
                SharedUtils.SetReadyStatus(_client, typing);
                Running.Value = false;
                return;
            }

            Console.WriteLine("\nBackup finished.\n");

            while (true)
            {
                Console.WriteLine("The backup order is in reverse chronological order (newest to oldest).\n" +
                                  "Would you like to put it into chronological order? (Y/N): ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (input == "Y")
                {
                    ReverseMessages();
                    Console.WriteLine("Reversed messages.");
                    break;
                }
                else
                {
                    break;
                }
            }

            Console.WriteLine($"Finished backup! Look at {_path} for backup.");
            await _channel.SendMessageAsync("", embed: new EmbedBuilder
            {
                Title = TITLE,
                Description = "Backup finished!",
                Color = COLOR_SUCCESS
            }.Build());
            SharedUtils.SetReadyStatus(_client, typing);
            Running.Value = false;
            return;
        }

        /// <summary>
        /// Creates necessary directories.
        /// </summary>
        /// <returns>Full path of backup directory.</returns>
        private string PreprocessFolders()
        {
            string guildId = _guild.Id.ToString();
            string channelId = _channel.Id.ToString();
            string channelPath = Path.Join(Program.BackupDir, guildId, channelId);
            string atchPath = Path.Join(channelPath, WriterThread.FILES_DIR);
            DirectoryInfo channelInfo = new DirectoryInfo(channelPath);
            DirectoryInfo atchInfo = new DirectoryInfo(atchPath);

            if (atchInfo.Exists)
            {
                return channelInfo.FullName;
            }

            atchInfo = Directory.CreateDirectory(atchInfo.FullName);
            return channelInfo.FullName;
        }

        /// <summary>
        /// Checks to see if there is an existing tsv file;
        /// and if so, checks if it is valid and the last saved message exists.
        /// (Note the possible thrown ValidationException).
        /// </summary>
        /// <returns>Null if does not exist. The IMessage of the last saved message if exists.</returns>
        /// <exception cref="ValidationException">Thrown upon invalid file (wrong header, message ID invalid, etc).</exception>
        private async Task<IMessage> CheckExisting()
        {
            string tsvPath = Path.Join(_path, WriterThread.TSV_FILE_NAME);
            if (!File.Exists(tsvPath))
            {
                return null;
            }

            using (StreamReader stream = new StreamReader(tsvPath))
            using (CsvReader csv = new CsvReader(stream, WriterThread.CSV_CONFIG))
            {
                string tsvChronPath = Path.Join(_path, TSV_CHRON);
                if (File.Exists(tsvChronPath))
                {
                    throw new FieldValidationException(csv.Context, $"{tsvChronPath} exists.");
                }

                csv.Read(); // advance to beginning of file
                CsvMessage csvMsg = csv.GetRecords<CsvMessage>().Last(); // expensive?
                IMessage msg = await _channel.GetMessageAsync(csvMsg.Id);
                if (msg == null)
                {
                    throw new FieldValidationException(csv.Context, $"Discord message {csvMsg.Id} not found in channel.");
                }

                return msg;
            }
        }

        /// <summary>
        /// Cleans the directory of existing files.
        /// </summary>
        private void CleanDirectory()
        {
            string tsvPath = Path.Join(_path, WriterThread.TSV_FILE_NAME);
            string attachmentPath = Path.Join(_path, WriterThread.FILES_DIR);
            File.Delete(tsvPath);

            DirectoryInfo atchInfo = new DirectoryInfo(attachmentPath);
            foreach (FileInfo file in atchInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in atchInfo.GetDirectories())
            {
                dir.Delete(true);
            }
        }

        /// <summary>
        /// Reverses by renaming the original file, and reversing on the fly to the original file.
        /// </summary>
        private void ReverseMessages()
        {
            string tsvPath = Path.Join(_path, WriterThread.TSV_FILE_NAME); // read from
            string tsvChronPath = Path.Join(_path, TSV_CHRON); // write to

            using (StreamReader streamReader = new StreamReader(tsvPath))
            using (CsvReader csvReader = new CsvReader(streamReader, WriterThread.CSV_CONFIG))
            using (StreamWriter streamWriter = new StreamWriter(tsvChronPath))
            using (CsvWriter csvWriter = new CsvWriter(streamWriter, WriterThread.CSV_CONFIG))
            {
                csvWriter.WriteHeader<CsvMessage>();
                csvWriter.NextRecord();

                foreach (CsvMessage msg in csvReader.GetRecords<CsvMessage>().Reverse())
                {
                    csvWriter.WriteRecord(msg);
                    csvWriter.NextRecord();
                }
            }
        }
    }
}