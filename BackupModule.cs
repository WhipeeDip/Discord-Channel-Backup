using System;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;

namespace Channel_Backup_Discord_Bot
{
    public class BackupModule : ModuleBase<SocketCommandContext>
    {
        /// <summary>
        /// The backup command. Creates a new thread to handle backup logic and returns.
        /// </summary>
        /// <returns>Always returns Task.CompletedTask. Any errors will be handled by the backup thread.</returns>
        [Command("backup")]
        [Summary("Invoke this command to select for backup.")]
        public Task Backup()
        {
            Console.WriteLine("Got backup command");

            BackupThread bkupThread = new BackupThread(Context.Channel, Context.Client, Context.User);
            Thread backupThread = new Thread(bkupThread.Run);
            backupThread.Start();

            // Console.WriteLine("Returning Task.CompletedTask in BackupModule::Backup()");
            return Task.CompletedTask;
        }
    }
}
