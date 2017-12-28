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
using NadekoBot.Common;
using NadekoBot.Common.Replacements;

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

        public EmbedBuilder GetHelpStringEmbed()
        {
            var r = new ReplacementBuilder()
                .WithDefault(Context)
                .WithOverride("{0}", () => _creds.ClientId.ToString())
                .WithOverride("{1}", () => Prefix)
                .Build();


            if (!CREmbed.TryParse(_config.BotConfig.HelpString, out var embed))
                return new EmbedBuilder().WithOkColor()
                    .WithDescription(String.Format(_config.BotConfig.HelpString, _creds.ClientId, Prefix));

            r.Replace(embed);

            return embed.ToEmbed();
        }

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
                .WithDescription($"▫️ Actions\n▫️ Administration\n▫️ CustomReactions\n▫️ Gambling\n▫️ Games\n▫️ Help\n▫️ Music\n▫️ NSFW\n▫️ Permissions\n▫️ Pokemon\n▫️ Searches\n▫️ Utility\n▫️ Xp\n");
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
                                                  .GroupBy(c => c.Module.Name.Replace("Commands", ""));
            cmds = cmds.OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());
            if (!cmds.Any())
            {
	            switch (moduleName)
	            {
	                case "Music":
	                case "music":
	            await Context.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName("Music"))
                    .AddField(efb => efb.WithName("Player").WithValue("```css\n.play      [p]\n.export    []\n.skip      [s]\n.stop      []\n.pause     []\n.unpause   []\n.resume    []\n.forward   []\n.rewind    []\n.seek      []\n.split     []\n.join      []\n.disconnect[]\n.destroy   [d]\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Controlls").WithValue("```css\n.select    []\n.nowplaying[np]\n.list      [lq]\n.songrepeat[srp]\n.shuffle   []\n.reshuffle []\n.volume    [vol]\n.restart   []\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Other").WithValue("```css\n.gensokyo  []\n.history   []\n.config    []\n.mprefix   [smp]\n```").WithIsInline(true)));
	                    break;
                    case "actions":
	                case "Actions":
	            await Context.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName("Actions"))
                    .AddField(efb => efb.WithName("Good Actions").WithValue("```css\n.pat         []\n.hug         []\n.kiss        []\n.poke        []\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Bad Actions").WithValue("```css\n.slap        []\n.kick        []\n.stab        []\n.shoot       []\n.bite        []\n```").WithIsInline(true)));
	                    break;
	                default:
		                await ReplyErrorLocalized("module_not_found").ConfigureAwait(false);
		                return;
                }
            }
            var i = 0;
            var groups = cmds.GroupBy(x => i++ / 48).ToArray();
            var embed = new EmbedBuilder().WithOkColor();
            foreach (var g in groups)
            {
                var last = g.Count();
                for (i = 0; i < last; i++)
                {
                    var transformed = g.ElementAt(i).Select(x =>
                    {
                        return $"{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                        var str = $"{Prefix + x.Aliases.First(),-18}";
                        var al = x.Aliases.Skip(1).FirstOrDefault();
                        if (al != null)
                            str += $" {"(" + Prefix + al + ")", -9}";
                        return str;
                    });

                    if (i == last - 1 && (i + 1) % 2 != 0)
                    {
                        var grp = 0;
                        var count = transformed.Count();
                        transformed = transformed
                            .GroupBy(x => grp++ % count / 2)
                            .Select(x => {
                                if (x.Count() == 1)
                                    return $"{x.First()}";
                                else
                                    return String.Concat(x);
                            });                        
                    }
                    embed.AddField(g.ElementAt(i).Key, "```css\n" + string.Join("\n", transformed) + "\n```", true);
                }
            }
            embed.WithFooter(GetText("commands_instr", Prefix));
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [Priority(0)]
        public async Task H([Remainder] string fail)
        {
            var isMusic = true;
            var isAction = true;
            var title = fail;
            var description = "";
            var usage = $"`{title}`";
            var image = "";

            switch (fail)
            {

                case ".config":
                    title = ".config";
                    description = "Gives you various options like announce the song that's currently playing in the selected channel.";
                    usage = "`.config topic_channel = music`\n`.config auto_resume = true`";
                    break;
                case ".nowplaying":
                case ".np":
                    title = ".nowplaying / .np";
                    description = "Shows information about the song that is currently playing (name, user that added it, current timestamp, and song URL)";
                    break;
                case ".play":
                    description = "▫️ With no parameters shows the play commands.\n▫️ If name is provided, plays the top YouTube result for the specified song name.\n▫️ If URL is provided, plays the corresponding stream. Supported locations include (but are not limited to): YouTube (and playlists), SoundCloud, BandCamp, Vimeo, and Twitch. Local files or URLs of the following formats are also supported: MP3, FLAC, WAV, Matroska/WebM (AAC, Opus or Vorbis codecs), MP4 (AAC codec), OGG streams (Opus, Vorbis and FLAC codecs), AAC streams, Stream playlists (M3U and PLS)\n▫️ If name of playlist is provided, plays all songs in the specified list. There must already be a playlist of the specified name in the Playlists folder.";
                    usage = "`.play`\n`.play <song title>`\n`.play <URL>`\n`.play <hastebin-link>`";
                    image = "https://thumbs.gfycat.com/RadiantGrandCow-size_restricted.gif";
                    break;
                case ".join":
                    title = ".join";
                    description = "Makes Ene join your current voice channel.";
                    usage = "`.join`";
                    break;
                case ".disconnect":
                    title = ".join";
                    description = "Make Ene leave the current voice channel.";
                    usage = "`.disconnect` `.lv`";
                    break;
                case ".srp":
                case ".songrepeat":
                    title = ".songrepeat";
                    description = "Make Ene leave the current voice channel.";
                    usage = "`.songrepeat all`\n`.srp single`\n`.srp off`";
                    break;
                case ".restart":
                    title = ".restart";
                    description = "Restart the currently playing track because why not?";
                    usage = "`.restart`";
                    break;
                case ".split":
                    title = ".split";
                    description = "Split a YouTube video into a tracklist provided in its description.";
                    usage = "`.split <url>`";
                    break;
                case ".gensokyo":
                    title = ".gensokyo";
                    description = "Show the current song played on gensokyoradio.net";
                    usage = "`.gensokyo`";
                    break;
                case ".export":
                    title = ".export";
                    description = "Export the current queue to a hastebin link, can be later used as a playlist.";
                    usage = "`.export`";
                    image = "https://thumbs.gfycat.com/OptimalUnhappyCats-size_restricted.gif";
                    break;
                case ".lq":
                case ".list":
                    title = ".lq / .list";
                    description = "Shows songs in the queue. If no page number is provided, it defaults to the first page.";
                    usage = "`.lq [pagenum]`\n`.list [pagenum]`";
                    break;
                case ".mprefix":
                case ".smp":
                    title = ".mprefix / .smp";
                    description = "Set a Prefix of the Musicmodule for your Server. \n**Note:** If you forgot the prefix, you can do `@Ene mprefix`";
                    usage = "`.mprefix +`\n`+smp .`";
                    break;
                case ".shuffle":
                    description = "Shuffles (changes the order, randomly) of songs that you have added to the queue.";
                    break;
                case ".reshuffle":
                    description = "Reshuffles (changes the order, randomly) of songs that you have added to the queue again.";
                    break;
                case ".skip":
                case ".voteskip":
                    title = ".skip";
                    description = "Skips a song if you added it. If you didn't add it, it adds your vote to skip it. Approximately 60% of active listeners need to vote to skip a song for it to be skipped.";
                    usage = "`.skip`\n`.skip @User`";
                    break;
                case ".pause":
                    description = "Pauses the player. The player remains paused until a DJ or Admin uses the play command.";
                    break;
                case ".forward":
                    description = "Skips forward in the queue to the provided song number, playing that song and removing any songs before that from the queue.";
                    usage = "`.forward 2:30`";
                    break;
                case ".rewind":
                    description = "Rewind the track by a given amount of time.";
                    usage = "`.rewind 30`";
                    break;
                case ".seek":
                    description = "Set the position of the track to the given time.";
                    usage = "`.seek 2:45:00`";
                    break;
                case ".stop":
                    description = "Clears the queue, ends the current song, and leaves the voice channel.";
                    break;
                case ".volume":
                case ".vol":
                    description = "Shows or sets the current volume. For best performance, it is recommended to leave this at 100 and adjust volume on an individual basis within Discord.";
                    usage = "`.volume [0-150]`\n`.vol [0-150]`";
                    break;
                case ".unpause":
                case ".resume":
                    description = "Unpauses the player.";
                    usage = "`.unpause`\n`.resume`";
                    break;
                case ".hug":
                    title = ".hug";
                    description = "Hug someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.hug @Someone`";
                    isAction = true;
                    break;
                case ".kiss":
                    title = ".kiss";
                    description = "Kiss someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.kiss @Someone`";
                    isAction = true;
                    break;
                case ".pat":
                    title = ".pat";
                    description = "Pat someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.pat @Someone`";
                    isAction = true;
                    break;
                default:
                    isMusic = false;
                    break;
            }

            if (isAction == false && isMusic == true)
            {
                await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithTitle(title)
                        .WithDescription(description)
                        .AddField(efb => efb.WithName($"Usage").WithValue(usage))
                        .WithImageUrl($"{image}")
                        .WithFooter($"Module: Music")).ConfigureAwait(false);
            } else if (isAction == true)
            {
                await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithTitle(title)
                        .WithDescription(description)
                        .AddField(efb => efb.WithName($"Usage").WithValue(usage))
                        .WithImageUrl($"{image}")
                        .WithFooter($"Module: Actions")).ConfigureAwait(false);
            } else
            {
                await ReplyErrorLocalized("command_not_found").ConfigureAwait(false);
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
