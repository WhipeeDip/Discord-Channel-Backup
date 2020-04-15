using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace Channel_Backup_Discord_Bot
{
    public class Program
    {
        private const string TZ_REGEX = @"^(\+?|-)(\d\d:\d\d)$";

        private static string _token;
        private static string _backupDir;
        private static string _formatTimeZone;
        private static bool _includeAttachments;

        private DiscordSocketClient _client;
        private CommandHandler _handler;

        public static string BackupDir
        {
            get
            {
                return _backupDir;
            }
        }

        public static string FormatTimeZone
        {
            get
            {
                return _formatTimeZone;
            }
        }

        public static bool IncludeAttachments
        {
            get
            {
                return _includeAttachments;
            }
        }

        /// <summary>
        /// Main program entry point.
        /// Calls configuration then goes into async main.
        /// </summary>
        /// <param name="args">Command line args (unused).</param>
        public static void Main(string[] args)
        {
            try
            {
                ConfigureApp();
                new Program().MainAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured. Try restarting the program in a bit.");
                Console.WriteLine($"\nException:\n{ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Inits the bot and waits forever.
        /// </summary>
        /// <returns>Should never return during the lifetime of the program.</returns>
        public async Task MainAsync()
        {
            Console.WriteLine("Creating Discord client and logging in...");

            _client = new DiscordSocketClient();
            _client.Log += Log; // TODO: keep Log?

            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();
            Console.WriteLine($"Logged in as {_client.Rest.CurrentUser.Username}#{_client.Rest.CurrentUser.Discriminator}");

            Console.WriteLine("Init CommandHandler...");
            _handler = new CommandHandler(_client);

            Console.WriteLine("Installing command modules...");
            await _handler.InstallCommandsAsync();

            SharedUtils.SetReadyStatus(_client);
            Console.WriteLine("Ready! Waiting for !backup command...");
            await Task.Delay(-1);
        }

        /// <summary>
        /// Calls Console.WriteLine()
        /// </summary>
        /// <param name="msg">The LogMessage to log.</param>
        /// <returns>Always returns Task.CompletedTask</returns>
        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        /// <summary>
        /// Prompts for app configuration.
        /// </summary>
        private static void ConfigureApp()
        {
            Console.WriteLine("Starting configuration...");

            IConfigurationRoot configBuilder =
                new ConfigurationBuilder()
                .AddXmlFile("defaults.xml", true, false) // path, optional, reload
                .Build();

            string dir = configBuilder["BACKUP_DIR"];
            string attachments = configBuilder["INCLUDE_ATTACHMENTS"];
            string timezone = configBuilder["FORMAT_TIMEZONE"];
            string token = configBuilder["DISCORD_TOKEN"];
            string channel = configBuilder["CHANNEL_NAME"];

            // configure backup directory
            while (true)
            {
                Console.WriteLine($"Enter the backup directory path ({dir}):");
                string input = Console.ReadLine();
                input = string.IsNullOrEmpty(input) ? dir : input;
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                try
                {
                    DirectoryInfo info = Directory.CreateDirectory(input);

                    // create a file that does not yet exist to test fs permissions
                    while (true)
                    {
                        Guid guid = Guid.NewGuid();
                        string testFile = Path.Join(input, guid.ToString());
                        if (File.Exists(testFile))
                        {
                            continue;
                        }

                        File.Create(testFile, 1, FileOptions.DeleteOnClose);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not create directory or did not have proper permissions.\n{ex}");
                    continue;
                }

                _backupDir = input;
                break;
            }

            // confgiure include files
            while (true)
            {
                Console.WriteLine($"Do you want to download attached files? true/false ({attachments}):");
                string input = Console.ReadLine();
                input = string.IsNullOrEmpty(input) ? attachments : input;
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                bool parsed;
                _includeAttachments = bool.TryParse(input, out parsed);

                if (!parsed)
                {
                    continue;
                }

                break;
            }

            // configure timezone
            while (true)
            {
                Console.WriteLine($"Enter the timezone for formatted time output ({timezone}):");
                string input = Console.ReadLine();
                input = string.IsNullOrEmpty(input) ? timezone : input;
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                Match match = Regex.Match(input, TZ_REGEX);
                if (!match.Success)
                {
                    continue;
                }

                _formatTimeZone = input;
                break;
            }

            while (true)
            {
                Console.WriteLine($"Enter your Discord token ({token}):");
                string input = Console.ReadLine();
                input = string.IsNullOrEmpty(input) ? token : input;
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                _token = input;
                break;
            }

            Console.WriteLine($"Using the following settings:\n" +
                              $"\tBackup directory: {_backupDir}\n" +
                              $"\tDownload files: {_includeAttachments}\n" +
                              $"\tFormat timezone: {_formatTimeZone}\n" +
                              $"\tDiscord token: {_token}\n");

            while (true)
            {
                Console.WriteLine("Continue? (Y)/N:");
                string input = Console.ReadLine().Trim().ToUpper();
                input = string.IsNullOrEmpty(input) ? "Y" : input;
                if (input == "Y")
                {
                    break;
                }
                else if (input == "N")
                {
                    Environment.Exit(1);
                }
            }
        }
    }
}
