# Discord-Channel-Backup

A .NET Core program that backs up a Discord channel built on [Discord.Net](https://github.com/discord-net/Discord.Net).

## Setup

1. Acquire a [Discord bot token](https://discordapp.com/developers/applications/).
2. Install [.NET Core 3.1](https://dotnet.microsoft.com/download/dotnet-core).
3. Download this repo and extract somewhere.
4. Update defaults.xml (see below) as needed.
5. Open terminal, `dotnet run`.
6. Follow prompts to finish setup.
7. Type the command `!backup` in a channel and follow terminal prompts to continue.

## defaults.xml Configuration

* `BACKUP_DIR`: Path to create backups, such as `H:\Discord_Backup`. The folder structure will be of `BACKUP_DIR/server_id/channel_id/`.

* `INCLUDE_ATTACHMENTS`: Whether to download attachments or not.

* `FORMAT_TIMEZONE`: Time zone of message timestamps. For example `-07:00`.

* `DISCORD_TOKEN`: Discord token for your bot.

## Libraries

* [CsvHelper](https://www.nuget.org/packages/CsvHelper/)

* [Discord.Net](https://www.nuget.org/packages/Discord.Net/)

* [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/)

* [Microsoft.Extensions.Configuration.Xml](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Xml/)
