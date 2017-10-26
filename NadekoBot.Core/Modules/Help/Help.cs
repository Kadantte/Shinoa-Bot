using Discord.Commands;
using NadekoBot.Extensions;
using System.Linq;
using Discord;
using NadekoBot.Core.Services;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Help.Services;
using NadekoBot.Modules.Permissions.Services;

namespace NadekoBot.Modules.Help
{
    public class Help : NadekoTopLevelModule<HelpService>
    {
        public const string PatreonUrl = "https://patreon.com/nadekobot";
        public const string PaypalUrl = "https://paypal.me/Kwoth";
        private readonly IBotCredentials _creds;
        private readonly IBotConfigProvider _config;
        private readonly CommandService _cmds;
        private readonly GlobalPermissionService _perms;

        public string HelpString => String.Format(_config.BotConfig.HelpString, _creds.ClientId, Prefix);
        public string DMHelpString => _config.BotConfig.DMHelpString;

        public Help(IBotCredentials creds, GlobalPermissionService perms, IBotConfigProvider config, CommandService cmds)
        {
            _creds = creds;
            _config = config;
            _cmds = cmds;
            _perms = perms;
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Modules()
        {
            var embed = new EmbedBuilder().WithOkColor()
                .WithFooter(efb => efb.WithText("ℹ️" + GetText("modules_footer", Prefix)))
                .WithTitle(GetText("list_of_modules"))
                .WithDescription($"▫️ Administration\n▫️ CustomReactions\n▫️ Gambling\n▫️ Games\n▫️ Help\n▫️ Music\n▫️ NSFW\n▫️ Permissions\n▫️ Pokemon\n▫️ Searches\n▫️ Utility\n▫️ Xp\n");
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Commands([Remainder] string module = null)
        {
            var channel = Context.Channel;
            var moduleName = module;

            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
                return;
            var cmds = _cmds.Commands.Where(c => c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(module))
                                                .Where(c => !_perms.BlockedCommands.Contains(c.Aliases.First().ToLowerInvariant()))
                                                  .OrderBy(c => c.Aliases.First())
                                                  .Distinct(new CommandTextEqualityComparer())
                                                  .AsEnumerable();

            var cmdsArray = cmds as CommandInfo[] ?? cmds.ToArray();
            var musicCmds = ".settings       []      .nowplaying     [np]    .play           []\n.playlists      []      .queue          [list]  .remove         [delete] \n.search         []      .scsearch       []      .shuffle        []\n.skip           [voteskip] .forceskip   []      .pause          []\n.skipto         []      .stop           []      .volume         [vol]\n.setdj          []      .settc          []      .setvc          []";

            if (!cmdsArray.Any())
            {
	            switch (moduleName)
	            {
	                case "Music":
	                case "music":
	                    await channel.SendMessageAsync($"📃 **{GetText("list_of_commands")}**\n```css\n{musicCmds}\n```").ConfigureAwait(false);
	                    break;
	                default:
		                await ReplyErrorLocalized("module_not_found").ConfigureAwait(false);
		                return;
	            }
            }
            var j = 0;
            var groups = cmdsArray.GroupBy(x => j++ / 48).ToArray();

            for (int i = 0; i < groups.Count(); i++)
            {
                await channel.SendTableAsync(i == 0 ? $"📃 **{GetText("list_of_commands")}**\n" : "", groups.ElementAt(i), el => $"{Prefix + el.Aliases.First(),-15} {"[" + el.Aliases.Skip(1).FirstOrDefault() + "]",-8}").ConfigureAwait(false);
            }

            await ConfirmLocalized("commands_instr", Prefix).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(0)]
        public async Task H([Remainder] string fail)
        {
            var isMusic = true;
            var title = fail;
            var description = "";
            var usage = $"`{title}`";
            var image = "";

            switch (fail)
            {

                case ".settings":
                    description = "Shows the settings for the current server. This includes Text Channel, Voice Channel, DJ Role, and Default Playlist. This command also shows the number of servers the bot is on, and how many audio connections there currently are.";
                    break;
                case ".nowplaying":
                case ".np":
                case ".current":
                    title = ".nowplaying / .np / .current";
                    description = "Shows information about the song that is currently playing (name, user that added it, current timestamp, and song URL)";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".play":
                    description = "▫️ With no parameters shows the play commands. If the player is paused, it resumes the player.\n▫️ If name is provided, plays the top YouTube result for the specified song name.\n▫️ If URL is provided, plays the corresponding stream. Supported locations include (but are not limited to): YouTube (and playlists), SoundCloud, BandCamp, Vimeo, and Twitch. Local files or URLs of the following formats are also supported: MP3, FLAC, WAV, Matroska/WebM (AAC, Opus or Vorbis codecs), MP4/M4A (AAC codec), OGG streams (Opus, Vorbis and FLAC codecs), AAC streams, Stream playlists (M3U and PLS)\n▫️ If name of playlist is provided, plays all songs in the specified list. There must already be a playlist of the specified name in the Playlists folder.";
                    usage = "`.play`\n`.play <song title>`\n`.play <URL>`\n`.play playlist <name>` or `.play pl <name>`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".playlists":
                    description = "Shows available playlists. These playlists must be inside the Playlists folder.";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".queue":
                case ".list":
                    title = ".queue / .list";
                    description = "Shows songs in the queue. If no page number is provided, it defaults to the first page.";
                    usage = "`.queue [pagenum]` or `.list [pagenum]`";
                    break;
                case ".remove":
                case ".delete":
                    title = ".remove / .delete";
                    description = "Removes the song at the provided position in the queue, or all songs if `all` option is used. You can only remove songs that you added, unless you are an Admin or have the specified DJ role.";
                    usage = "`.remove <songnum>` or `.delete <songnum>`\n`.remove all` or `.delete all`";
                    break;
                case ".search":
                    description = "Shows the top YouTube results for a search and allows you to select one to add to the queue.";
                    usage = "`.search <query>`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".scsearch":
                    description = "Shows the top SoundCloud results for a search and allows you to select one to add to the queue.";
                    usage = "`.scsearch <query>`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".shuffle":
                    description = "Shuffles (changes the order, randomly) of songs that you have added to the queue.";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".skip":
                case ".voteskip":
                    title = ".skip / .voteskip";
                    description = "Skips a song if you added it. If you didn't add it, it adds your vote to skip it. Approximately 60% of active listeners need to vote to skip a song for it to be skipped.";
                    usage = "`.skip` or `.voteskip`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".forceskip":
                    description = "Forcibly skips the current song, regardless of who added it and how many votes there are to skip it.";
                    break;
                case ".pause":
                    description = "Pauses the player. The player remains paused until a DJ or Admin uses the play command.";
                    break;
                case ".skipto":
                    description = "Skips forward in the queue to the provided song number, playing that song and removing any songs before that from the queue.";
                    usage = "`.skipto <position>`";
                    break;
                case ".stop":
                    description = "Clears the queue, ends the current song, and leaves the voice channel.";
                    break;
                case ".volume":
                case ".vol":
                    description = "Shows or sets the current volume. For best performance, it is recommended to leave this at 100 and adjust volume on an individual basis within Discord.";
                    usage = "`.volume [0-150]` or `.vol [0-150]`";
                    break;
                case ".setdj":
                    description = "Sets or clears the DJ role. Users with this role will be able to use DJ commands.";
                    usage = "`.setdj <rolename>`\n`.setdj none`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".settc":
                    description = "Sets or clears the text channel for music commands. Using music commands in other channels will result in them being deleted (if possible), and a warning sent via DMs to use the correct channel. Additionally, if the bot has the Manage Channel permission in the set channel, it will adjust the topic to show the current track.";
                    usage = "`.settc <channel>`\n`.settc none`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                case ".setvc":
                    description = "Sets or clears the voice channel for playing music. When set, the bot will only connect to the specified channel when users attempt to play music. When cleared, users can play music from any channel that the bot can connect to (if the bot is not already in a different channel).";
                    usage = "`.setvc <channel>`\n`.setvc none`";
                    image = "https://thumbs.gfycat.com/OpenAlertBoaconstrictor-size_restricted.gif";
                    break;
                default:
                    isMusic = false;
                    break;
            }

            switch (isMusic)
            {
                case true:
                    await Context.Channel.EmbedAsync(
                        new EmbedBuilder().WithOkColor()
                            .WithTitle(title)
                            .WithDescription(description)
                            .AddField(efb => efb.WithName($"Usage").WithValue(usage))
                            .WithImageUrl($"{image}")
                            .WithFooter($"Module: Music")).ConfigureAwait(false);
                    break;
                case false:
                    await ReplyErrorLocalized("command_not_found").ConfigureAwait(false);
                    break;
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(1)]
        public async Task H([Remainder] CommandInfo com = null)
        {

            var channel = Context.Channel;
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

            if (com == null)
            {
                IMessageChannel ch = channel is ITextChannel ? await ((IGuildUser)Context.User).GetOrCreateDMChannelAsync() : channel;
                await ch.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithTitle($"What can I help you with? :)")
                        .WithDescription($"Heeey. My name is Ene. I am Gremagol-sama's Bot. Also, a custom bot that offers a wide variety of features and high quality music. I'd be happy to help you improve your server!")
                        .AddField(efb => efb.WithName($"💌Invite Links💌").WithValue($"🔗 [**CLICK HERE TO INVITE ME**](http://www.gremagol.com/inv-ene)\n🔗 [**JOIN MY SERVER IF YOU STILL NEED HELP**](https://gremagol.com/discord)").WithIsInline(false))
                        .AddField(efb => efb.WithName($"⚙️Features🎶").WithValue($"✅ Moderation\n✅ Games and gambling\n✅ Xp and leveling\n✅ Multiple utility commands\n✅ And more!\n\n**Extra:**\n\n🎶 High quality music\n💰 Currency generation (`.gc`)\n⚙️ Logs\n🆒 Many funny custom reactions preloaded\n(type  `.cmds custom` in your server to see a list, type `.lcr` here for full list)").WithIsInline(false))
                        .AddField(efb => efb.WithName($"Commands").WithValue($"▫️ [A list of all commands](http://www.gremagol.com/ene-commandlist)\n\n▶️ Type `.modules` to get the list of modules.\n▶️ Type `.cmds <module>` to get the list of a module's\n▶️commands.\n▶️ Type `.h <command>` to get help for a specific command.").WithIsInline(false))
                        .WithImageUrl("https://i.imgur.com/UPAvj1i.png")).ConfigureAwait(false);
                    await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription($"" + Context.User.Mention + " Okaay! Check your DMs! 	(＾◡＾)")).ConfigureAwait(false);
                return;
            }

            //if (com == null)
            //{
            //    await ReplyErrorLocalized("command_not_found").ConfigureAwait(false);
            //    return;
            //}

            var embed = _service.GetCommandHelp(com, Context.Guild);
            await channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        [OwnerOnly]
        public async Task Hgit()
        {
            var helpstr = new StringBuilder();
            helpstr.AppendLine(GetText("cmdlist_donate", PatreonUrl, PaypalUrl) + "\n");
            helpstr.AppendLine("##"+ GetText("table_of_contents"));
            helpstr.AppendLine(string.Join("\n", _cmds.Modules.Where(m => m.GetTopLevelModule().Name.ToLowerInvariant() != "help")
                .Select(m => m.GetTopLevelModule().Name)
                .Distinct()
                .OrderBy(m => m)
                .Prepend("Help")
                .Select(m => string.Format("- [{0}](#{1})", m, m.ToLowerInvariant()))));
            helpstr.AppendLine();
            string lastModule = null;
            foreach (var com in _cmds.Commands.OrderBy(com => com.Module.GetTopLevelModule().Name).GroupBy(c => c.Aliases.First()).Select(g => g.First()))
            {
                var module = com.Module.GetTopLevelModule();
                if (module.Name != lastModule)
                {
                    if (lastModule != null)
                    {
                        helpstr.AppendLine();
                        helpstr.AppendLine($"###### [{GetText("back_to_toc")}](#{GetText("table_of_contents").ToLowerInvariant().Replace(' ', '-')})");
                    }
                    helpstr.AppendLine();
                    helpstr.AppendLine("### " + module.Name + "  ");
                    helpstr.AppendLine($"{GetText("cmd_and_alias")} | {GetText("desc")} | {GetText("usage")}");
                    helpstr.AppendLine("----------------|--------------|-------");
                    lastModule = module.Name;
                }
                helpstr.AppendLine($"{string.Join(" ", com.Aliases.Select(a => "`" + Prefix + a + "`"))} |" +
                                   $" {string.Format(com.Summary, Prefix)} {_service.GetCommandRequirements(com, Context.Guild)} |" +
                                   $" {string.Format(com.Remarks, Prefix)}");
            }
            File.WriteAllText("../../docs/Commands List.md", helpstr.ToString());
            await ReplyConfirmLocalized("commandlist_regen").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Guide()
        {
            await ConfirmLocalized("guide", 
                "http://nadekobot.readthedocs.io/en/latest/Commands%20List/",
                "http://nadekobot.readthedocs.io/en/latest/").ConfigureAwait(false);
        }
        
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Donate()
        {
            await ReplyConfirmLocalized("donate", PatreonUrl, PaypalUrl).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Invite()
        {
            await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription($"Here are some Invite Links! 💙")
                        .AddField(efb => efb.WithName($"💌Invite me💌").WithValue($"⏩ [Click Here](http://www.gremagol.com/inv-ene) ⏪").WithIsInline(true))
                        .AddField(efb => efb.WithName($"💟Join my Server💟").WithValue($"⏩ [Click Here](https://gremagol.com/discord) ⏪").WithIsInline(true))).ConfigureAwait(false);
        }
    }

    public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
    {
        public bool Equals(CommandInfo x, CommandInfo y) => x.Aliases.First() == y.Aliases.First();

        public int GetHashCode(CommandInfo obj) => obj.Aliases.First().GetHashCode();

    }
}
