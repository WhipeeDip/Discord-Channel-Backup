using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Channel_Backup_Discord_Bot
{
    /// <summary>
    /// Shared utils across the program.
    /// </summary>
    public static class SharedUtils
    {
        /// <summary>
        /// Set the bot's statuses to ready for backup.
        /// </summary>
        /// <param name="client">The Discord client.</param>
        /// <param name="typing">To stop the typing status, if applicable.</param>
        public static async void SetReadyStatus(DiscordSocketClient client, IDisposable typing = null)
        {
            await client.SetStatusAsync(UserStatus.Online);
            await client.SetGameAsync("!backup", type: ActivityType.Listening);
            typing?.Dispose();
        }

        /// <summary>
        /// Set the bot's statuses to backup in process.
        /// </summary>
        /// <param name="client">The Discord client</param>
        /// <param name="channel">The channel that is being backed up.</param>
        /// <returns>The IDisposable for typing status.</returns>
        public static async Task<IDisposable> SetBackupBusyStatus(DiscordSocketClient client, IMessageChannel channel)
        {
            await client.SetStatusAsync(UserStatus.DoNotDisturb);
            await client.SetGameAsync($"#{channel.Name}", type: ActivityType.Playing);
            return channel.EnterTypingState();
        }

        /// <summary>
        /// Truncates a string and adds an ellipsis if needed.
        /// </summary>
        /// <param name="str">The string to truncate.</param>
        /// <returns>The truncated string.</returns>
        public static string TruncateString(string str, int length)
        {
            if (str.Length > length)
            {
                return str.Substring(0, length) + "...";
            }

            return str;
        }
    }
}