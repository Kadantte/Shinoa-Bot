using Discord.Commands;
using NadekoBot.Extensions;
using System.Linq;
using Discord;
using NadekoBot.Core.Services;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Collections.Generic;
using NadekoBot.Common.Attributes;
using NadekoBot.Modules.Help.Services;
using NadekoBot.Modules.Permissions.Services;
using NadekoBot.Common;
using NadekoBot.Common.Replacements;
using Newtonsoft.Json;
using NadekoBot.Core.Common;
using NadekoBot.Core.Modules.Help.Common;
using System.Text;

namespace NadekoBot.Modules.Help
{
    public class Help : NadekoTopLevelModule<HelpService>
    {
        public const string PatreonUrl = "https://patreon.com/nadekobot";
        public const string PaypalUrl = "https://paypal.me/Kwoth";
        private readonly IBotCredentials _creds;
        private readonly CommandService _cmds;
        private readonly GlobalPermissionService _perms;
        private readonly IServiceProvider _services;

        public EmbedBuilder GetHelpStringEmbed()
        {
            var r = new ReplacementBuilder()
                .WithDefault(Context)
                .WithOverride("{0}", () => _creds.ClientId.ToString())
                .WithOverride("{1}", () => Prefix)
                .Build();


            if (!CREmbed.TryParse(_bc.BotConfig.HelpString, out var embed))
                return new EmbedBuilder().WithOkColor()
                    .WithDescription(String.Format(_bc.BotConfig.HelpString, _creds.ClientId, Prefix));

            r.Replace(embed);

            return embed.ToEmbed();
        }

        public Help(IBotCredentials creds, GlobalPermissionService perms, CommandService cmds,
            IServiceProvider services)
        {
            _creds = creds;
            _cmds = cmds;
            _perms = perms;
            _services = services;
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
        [NadekoOptions(typeof(CommandsOptions))]
        public async Task Commands(string module = null, params string[] args)
        {
            var channel = Context.Channel;
            var moduleName = module;
            var (opts, _) = OptionsParser.Default.ParseFrom(new CommandsOptions(), args);

            module = module?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(module))
                return;

            // Find commands for that module
            // don't show commands which are blocked
            // order by name
            var cmds = _cmds.Commands.Where(c => c.Module.GetTopLevelModule().Name.ToUpperInvariant().StartsWith(module))
                                                .Where(c => !_perms.BlockedCommands.Contains(c.Aliases.First().ToLowerInvariant()))
                                                  .OrderBy(c => c.Aliases.First())
                                                  .Distinct(new CommandTextEqualityComparer());


            // check preconditions for all commands, but only if it's not 'all'
            // because all will show all commands anyway, no need to check
            HashSet<CommandInfo> succ = new HashSet<CommandInfo>();
            if (opts.View != CommandsOptions.ViewType.All)
            {
                succ = new HashSet<CommandInfo>((await Task.WhenAll(cmds.Select(async x =>
                {
                    var pre = (await x.CheckPreconditionsAsync(Context, _services));
                    return (Cmd: x, Succ: pre.IsSuccess);
                })))
                    .Where(x => x.Succ)
                    .Select(x => x.Cmd));

                if (opts.View == CommandsOptions.ViewType.Hide)
                {
                    // if hidden is specified, completely remove these commands from the list
                    cmds = cmds.Where(x => succ.Contains(x));
                }
            }

            var cmdsWithGroup = cmds.GroupBy(c => c.Module.Name.Replace("Commands", ""))
                .OrderBy(x => x.Key == x.First().Module.Name ? int.MaxValue : x.Count());
            if (!cmds.Any())
            {
	            switch (moduleName)
	            {
	                case "Music":
	                case "music":
	            await Context.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName("Music"))
                    .AddField(efb => efb.WithName("Config").WithValue("```css\n.config        [.cfg]\n.mprefix       [.smp]\n.mlanguage     [.mlang]\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Perms").WithValue("```css\n.admin       []\n.dj          []\n.user        []\n```").WithIsInline(true))                    
                    .AddField(efb => efb.WithName("Info").WithValue("```css\n.nowplaying    [.np]\n.list          [.lq]\n.export        [.ex]\n.gensokyo      [.gr]\n.history       []\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Seeking").WithValue("```css\n.forward             [.fwd]\n.rewind              [.rew]\n.restart             [.replay]\n.seek                []\n```").WithIsInline(true))                    
                    .AddField(efb => efb.WithName("Control").WithValue("```css\n.stop                [.s]\n.join                [.j]\n.disconnect          [.lv]\n.play                [.q]\n.pause               []\n.unpause             [.resume]\n.split               []\n.select              [.sel]\n.songrepeat          [.srp]\n.shuffle             [.sh]\n.reshuffle           [.resh]\n.skip                [.n]\n.volume              [.vol]\n.destroy             [.d]\n```").WithIsInline(true)));
	                    break;
                    case "actions":
	                case "Actions":
	            await Context.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName("Actions"))
                    .AddField(efb => efb.WithName("Good Actions").WithValue("```css\n.pat         []\n.hug         []\n.kiss        []\n.poke        []\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Bad Actions").WithValue("```css\n .slap        []\ne.kick        []\n .stab        []\n .shoot       []\n .bite        []\n```").WithIsInline(true)));
	                    break;
	                default:
                if (opts.View != CommandsOptions.ViewType.Hide)
                    await ReplyErrorLocalized("module_not_found").ConfigureAwait(false);
                else
                    await ReplyErrorLocalized("module_not_found_or_cant_exec").ConfigureAwait(false);
		                return;
                }
            }
           var i = 0;
            var groups = cmdsWithGroup.GroupBy(x => i++ / 48).ToArray();
            var embed = new EmbedBuilder().WithOkColor();
            foreach (var g in groups)
            {
                var last = g.Count();
                for (i = 0; i < last; i++)
                {
                    var transformed = g.ElementAt(i).Select(x =>
                    {
                        //if cross is specified, and the command doesn't satisfy the requirements, cross it out
                        if (opts.View == CommandsOptions.ViewType.Cross)
                        {
                            return $"{(succ.Contains(x) ? "✅" : "❌")}{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                        }
                        return $"{Prefix + x.Aliases.First(),-15} {"[" + x.Aliases.Skip(1).FirstOrDefault() + "]",-8}";
                    });

                    if (i == last - 1 && (i + 1) % 2 != 0)
                    {
                        var grp = 0;
                        var count = transformed.Count();
                        transformed = transformed
                            .GroupBy(x => grp++ % count / 2)
                            .Select(x =>
                            {
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
            var footer = "";
            var prefixless = _cmds.Commands.FirstOrDefault(x => x.Name.ToLowerInvariant() == fail);
            if(prefixless!= null)
            {
                await H(prefixless);
                return;
            }

            switch (fail)
            {

                case ".config":
                case "config":
                case "cfg":
                case ".cfg":
                    title = ".config";
                    description = "Gives you various options like announce the song that's currently playing in the selected channel.";
                    usage = "`.config topic_channel = music`\n`.config auto_resume = true`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".mlanguage":
                case "mlanguage":
                case "mlang":
                case ".mlang":
                    title = ".mlanguage";
                    description = "Ene supports several user-contributed languages that you can select with this command.";
                    usage = "`.mlang`\n`.mlang de_DE`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".admin":
                case "admin":
                    title = ".admin";
                    description = "Allows whitelisting members and roles for the Admin rank. Guide here: http://enecmdlist.readthedocs.io/en/latest/Permissions/";
                    usage = "`.admin add Staff`\n`.admin del DjRole`\n`.admin list`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".dj":
                case "dj":
                    title = ".dj";
                    description = "Allows whitelisting members and roles for the DJ rank. Guide here: http://enecmdlist.readthedocs.io/en/latest/Permissions/";
                    usage = "`.dj add @DjRole`\n`.dj del everyone`\n`.dj list`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".user":
                case "user":
                    title = ".user";
                    description = "Allows whitelisting members and roles for the User rank. Guide here: http://enecmdlist.readthedocs.io/en/latest/Permissions/";
                    usage = "`.user add @everyone`\n`.user del everyone`\n`.user list`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".nowplaying":
                case ".np":
                case "nowplaying":
                case "np":
                    title = ".nowplaying / .np";
                    description = "Shows information about the song that is currently playing (name, user that added it, current timestamp, and song URL)";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".play":
                case "play":
                case "q":
                case ".q":
                    description = "▫️ With no parameters shows the play commands.\n▫️ If name is provided, plays the top YouTube result for the specified song name.\n▫️ If URL is provided, plays the corresponding stream. Supported locations include (but are not limited to): YouTube (and playlists), SoundCloud, BandCamp, Vimeo, and Twitch. Local files or URLs of the following formats are also supported: MP3, FLAC, WAV, Matroska/WebM (AAC, Opus or Vorbis codecs), MP4 (AAC codec), OGG streams (Opus, Vorbis and FLAC codecs), AAC streams, Stream playlists (M3U and PLS)\n▫️ If name of playlist is provided, plays all songs in the specified list. There must already be a playlist of the specified name in the Playlists folder.";
                    usage = "`.play`\n`.play <song title>`\n`.play <URL>`\n`.play <hastebin-link>`";
                    image = "https://thumbs.gfycat.com/RadiantGrandCow-size_restricted.gif";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".select":
                case "select":
                case "sel":
                case ".sel":
                    description = "Select one of the offered tracks after a search to play.";
                    usage = "`.select n`\n`.play n`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".join":
                case "join":
                case "j":
                case ".j":
                    title = ".join";
                    description = "Makes Ene join your current voice channel.";
                    usage = "`.join`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".disconnect":
                case "disconnect":
                case "lv":
                case ".lv":
                    title = ".disconnect";
                    description = "Make Ene leave the current voice channel.";
                    usage = "`.disconnect` `.lv`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".s":
                case ".stop":
                case "stop":
                case "s":
                    description = "Clears the queue, ends the current song, and stops the player.";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".srp":
                case ".songrepeat":
                case "songrepeat":
                case "srp":
                    title = ".songrepeat";
                    description = "Make Ene leave the current voice channel.";
                    usage = "`.songrepeat all`\n`.srp single`\n`.srp off`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".restart":
                case "restart":
                    title = ".restart";
                    description = "Restart the currently playing track because why not?";
                    usage = "`.restart`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".split":
                case "split":
                    title = ".split";
                    description = "Split a YouTube video into a tracklist provided in its description.";
                    usage = "`.split <url>`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".gensokyo":
                case "gensokyo":
                    title = ".gensokyo";
                    description = "Show the current song played on gensokyoradio.net";
                    usage = "`.gensokyo`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".export":
                case "export":
                    title = ".export";
                    description = "Export the current queue to a hastebin link, can be later used as a playlist.";
                    usage = "`.export`";
                    image = "https://thumbs.gfycat.com/OptimalUnhappyCats-size_restricted.gif";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".lq":
                case ".list":
                case "list":
                case "lq":
                    title = ".lq / .list";
                    description = "Shows songs in the queue. If no page number is provided, it defaults to the first page.";
                    usage = "`.lq [pagenum]`\n`.list [pagenum]`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".mprefix":
                case ".smp":
                case "mprefix":
                case "smp":
                    title = ".mprefix / .smp";
                    description = "Set a Prefix of the Musicmodule for your Server. \n**Note:** If you forgot the prefix, you can do `@Ene mprefix`";
                    usage = "`.mprefix +`\n`+smp .`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".shuffle":
                case ".sh":
                case "sh":
                case "shuffle":
                    description = "Shuffles (changes the order, randomly) of songs that you have added to the queue.";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".reshuffle":
                case "reshuffle":
                    description = "Reshuffles (changes the order, randomly) of songs that you have added to the queue again.";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".skip":
                case "skip":
                case ".n":
                case "n":
                    title = ".skip";
                    description = "Skips a song if you added it. If you didn't add it, it adds your vote to skip it. Approximately 60% of active listeners need to vote to skip a song for it to be skipped.";
                    usage = "`.skip`\n`.skip @User`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".pause":
                case "pause":
                    description = "Pauses the player. The player remains paused until a DJ or Admin uses the play command.";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".forward":
                case "forward":
                    description = "Skips forward in the queue to the provided song number, playing that song and removing any songs before that from the queue.";
                    usage = "`.forward 2:30`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".rewind":
                case "rewind":
                    description = "Rewind the track by a given amount of time.";
                    usage = "`.rewind 30`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".seek":
                case "seek":
                    description = "Set the position of the track to the given time.";
                    usage = "`.seek 2:45:00`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".d":
                case "d":
                case ".destroy":
                case "destroy":
                    description = "Clears the queue, ends the current song, and leaves the voice channel.";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".volume":
                case "volume":
                case ".vol":
                case "vol":
                    description = "Shows or sets the current volume. For best performance, it is recommended to leave this at 100 and adjust volume on an individual basis within Discord.";
                    usage = "`.volume [0-150]`\n`.vol [0-150]`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".unpause":
                case "unpause":
                case ".resume":
                case "resume":
                    description = "Unpauses the player.";
                    usage = "`.unpause`\n`.resume`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case ".hug":
                case "hug":
                    title = ".hug";
                    description = "Hug someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.hug @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".kiss":
                case "kiss":
                    title = ".kiss";
                    description = "Kiss someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.kiss @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".pat":
                case "pat":
                    title = ".pat";
                    description = "Pat someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.pat @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".poke":
                case "poke":
                    title = ".poke";
                    description = "Poke someone, if you need their attention. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.poke @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".bite":
                case "bite":
                    title = ".bite";
                    description = "Bite someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.bite @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".slap":
                case "slap":
                    title = ".slap";
                    description = "Slap someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.slap @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".stab":
                case "stab":
                    title = ".stab";
                    description = "Stab someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.stab @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case ".shoot":
                case "shoot":
                    title = ".shoot";
                    description = "Shoot someone. But don't worry, it won't hurt. :) Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.shoot @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                case "e.kick":
                    title = "e.kick";
                    description = "Kick someone. They won't be kicked from the server. :D Remember: If you change the prefix, it'll be still `e.` as prefix.";
                    usage = "`e.kick @Someone`";
                    footer = "Module: Actions";
                    isAction = true;
                    break;
                default:
                    isMusic = false;
                    isAction = false;
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
                        .WithFooter($"{footer}")).ConfigureAwait(false);
            } else if (isAction == true)
            {
                await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithTitle(title)
                        .WithDescription(description)
                        .AddField(efb => efb.WithName($"Usage").WithValue(usage))
                        .WithImageUrl($"{image}")
                        .WithFooter($"{footer}")).ConfigureAwait(false);
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
                IMessageChannel ch = channel is ITextChannel 
                    ? await ((IGuildUser)Context.User).GetOrCreateDMChannelAsync() 
                    : channel;
                await ch.EmbedAsync(GetHelpStringEmbed()).ConfigureAwait(false);
                await Context.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithDescription($"" + Context.User.Mention + " Okaaay! Check your new message!~ 	(＾◡＾)")).ConfigureAwait(false);
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
            helpstr.AppendLine("##"+ GetText("table_of_contents"));
            helpstr.AppendLine(string.Join("\n", _cmds.Modules.Where(m => m.GetTopLevelModule().Name.ToLowerInvariant() != "help")
                .Select(m => m.GetTopLevelModule().Name)
                .Distinct()
                .OrderBy(m => m)
                .Prepend("Help")
                .Select(m => string.Format("- [{0}](#{1})", m, m.ToLowerInvariant()))));
            helpstr.AppendLine();
            string lastModule = null;
            Dictionary<string, List<object>> cmdData = new Dictionary<string, List<object>>();
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
                    helpstr.AppendLine($"Submodule | {GetText("cmd_and_alias")} | {GetText("desc")} | {GetText("usage")}");
                    helpstr.AppendLine("----------|----------------|--------------|-------");
                    lastModule = module.Name;
                }
                helpstr.AppendLine($" {com.Module.Name} |" + 
                                   $" {string.Join(" ", com.Aliases.Select(a => "`" + Prefix + a + "`"))} |" +
                                   $" {string.Format(com.Summary, Prefix)} {_service.GetCommandRequirements(com, Context.Guild)} |" +
                                   $" {com.RealRemarks(Prefix)}");
                var obj = new
                {
                    Aliases = com.Aliases.Select(x => Prefix + x).ToArray(),
                    Description = string.Format(com.Summary, Prefix), 
                    Requirements = _service.GetCommandRequirements(com, Context.Guild),
                    Usage = JsonConvert.DeserializeObject<string[]>(com.Remarks).Select(x => string.Format(x, Prefix)).ToArray(),
                    Submodule = com.Module.Name,
                    Module = com.Module.GetTopLevelModule().Name,
                };
                if (cmdData.TryGetValue(module.Name, out var cmds))
                    cmds.Add(obj);
                else
                    cmdData.Add(module.Name, new List<object>
                    {
                        obj
                    });
            }
            File.WriteAllText("../../docs/Commands List.md", helpstr.ToString());
            File.WriteAllText("../../docs/cmds.json", JsonConvert.SerializeObject(cmdData));

            await ReplyConfirmLocalized("commandlist_regen").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Guide()
        {
            await ConfirmLocalized("guide",
                "https://nadekobot.me/commands",
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

        private string GetRemarks(string[] arr)
        {
            return string.Join(" or ", arr.Select(x => Format.Code(x)));
        }
    }

    public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
    {
        public bool Equals(CommandInfo x, CommandInfo y) => x.Aliases.First() == y.Aliases.First();

        public int GetHashCode(CommandInfo obj) => obj.Aliases.First().GetHashCode();

    }

    public class JsonCommandData
    {
        public string[] Aliases { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
    }
}
