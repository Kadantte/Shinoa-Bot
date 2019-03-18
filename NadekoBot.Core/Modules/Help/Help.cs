using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using System.Linq;
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


            if (!CREmbed.TryParse(Bc.BotConfig.HelpString, out var embed))
                return new EmbedBuilder().WithOkColor()
                    .WithDescription(String.Format(Bc.BotConfig.HelpString, _creds.ClientId, Prefix));

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
                .WithDescription($"▫️ Actions\n▫️ Administration\n▫️ CustomReactions\n▫️ Gambling\n▫️ Games\n▫️ Help\n▫️ Music\n▫️ NSFW\n▫️ Permissions\n▫️ Searches\n▫️ Utility\n▫️ Xp\n");
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Invite()
        {
            await Context.Channel.EmbedAsync(
                    new EmbedBuilder().WithOkColor()
                        .WithDescription($"Here are some Invite Links! 💜")
                        .AddField(efb => efb.WithName($"💌Invite me💌").WithValue($"⏩ [Click Here](https://discordapp.com/oauth2/authorize?scope=bot&client_id=464107301285134346&permissions=66186303) ⏪").WithIsInline(true))
                        .AddField(efb => efb.WithName($"💟Join my Server💟").WithValue($"⏩ [Click Here](https://discord.gg/fWDffyn) ⏪").WithIsInline(true))).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [NadekoOptionsAttribute(typeof(CommandsOptions))]
        public async Task Commands(string module = null, params string[] args)
        {
            var channel = Context.Channel;
            var moduleName = module;
            var (opts, _) = OptionsParser.ParseFrom(new CommandsOptions(), args);

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
                    .AddField(efb => efb.WithName("Music Bot").WithValue("```css\n🎶 !!changeprefix\n🎶 !!help\n```").WithIsInline(true)));
	                    break;
                    case "actions":
	                case "Actions":
	            await Context.Channel.EmbedAsync(
                new EmbedBuilder().WithOkColor()
                    .WithAuthor(eab => eab.WithName("Actions"))
                    .AddField(efb => efb.WithName("Good Actions").WithValue("```css\n.pat       []\n.hug       []\n.kiss      []\n.nom       []\n.poke      []\n.lick      []\n.hf        []\n.cuddle    []\n```").WithIsInline(true))
                    .AddField(efb => efb.WithName("Bad Actions").WithValue("```css\n.slap     []\ns.kick    []\n.stab     []\n.shoot    []\n.bite     []\n```").WithIsInline(true)));
	                    break;
	                default:
                if (opts.View != CommandsOptions.ViewType.Hide)
                    await ReplyErrorLocalizedAsync("module_not_found").ConfigureAwait(false);
                else
                    await ReplyErrorLocalizedAsync("module_not_found_or_cant_exec").ConfigureAwait(false);
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
            var isAction = true;
            var isMusic = true;
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
                case ".hug":
                case "hug":
                    title = ".hug";
                    description = "Hug someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.hug @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".kiss":
                case "kiss":
                    title = ".kiss";
                    description = "Kiss someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.kiss @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".pat":
                case "pat":
                    title = ".pat";
                    description = "Pat someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.pat @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".cuddle":
                case "cuddle":
                    title = ".cuddle";
                    description = "Cuddle with someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.cuddle @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".hf":
                case "hf":
                    title = ".hf";
                    description = "Do a high-five with someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.hf @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".lick":
                case "lick":
                    title = ".lick";
                    description = "Lick someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.lick @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;

                    isAction = true;
                    break;
                case ".poke":
                case "poke":
                    title = ".poke";
                    description = "Poke someone, if you need their attention. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.poke @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".bite":
                case "bite":
                    title = ".bite";
                    description = "Bite someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.bite @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".slap":
                case "slap":
                    title = ".slap";
                    description = "Slap someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.slap @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".stab":
                case "stab":
                    title = ".stab";
                    description = "Stab someone. Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.stab @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".shoot":
                case "shoot":
                    title = ".shoot";
                    description = "Shoot someone. But don't worry, it won't hurt. :) Remember: If you change the prefix, it'll be still `.` as prefix.";
                    usage = "`.shoot @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case "s.kick":
                    title = "s.kick";
                    description = "Kick someone. They won't be kicked from the server. :D Remember: If you change the prefix, it'll be still `e.` as prefix.";
                    usage = "`s.kick @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;
                    isAction = true;
                    break;
                case ".nom":
                    title = ".nom";
                    description = "Nom on food with someone. :3 Remember: If you change the prefix, it'll be still `e.` as prefix.";
                    usage = "`.nom @Someone`";
                    footer = "Module: Actions";
                    isMusic = false;                    
                    isAction = true;
                    break;
                case "!!changeprefix":
                case "!!prefix":
                case "changeprefix":
                    title = "!!changeprefix / !!prefix";
                    description = "Set a Prefix of the Musicmodule for your Server.";
                    usage = "`!!changeprefix music s!`";
                    footer = "Module: Music";
                    isMusic = true;
                    break;
                case "!!help":
                    title = "!!help";
                    description = "Lists all music commands.";
                    usage = "`!!help play`";
                    footer = "Module: Music";
                    isMusic = true;
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
                await ReplyErrorLocalizedAsync("command_not_found").ConfigureAwait(false);
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
            Dictionary<string, List<object>> cmdData = new Dictionary<string, List<object>>();
            foreach (var com in _cmds.Commands.OrderBy(com => com.Module.GetTopLevelModule().Name).GroupBy(c => c.Aliases.First()).Select(g => g.First()))
            {
                var module = com.Module.GetTopLevelModule();
                string optHelpStr = null;
                var opt = ((NadekoOptionsAttribute)com.Attributes.FirstOrDefault(x => x is NadekoOptionsAttribute))?.OptionType;
                if (opt != null)
                {
                    optHelpStr = HelpService.GetCommandOptionHelp(opt);
                }
                var obj = new
                {
                    Aliases = com.Aliases.Select(x => Prefix + x).ToArray(),
                    Description = string.Format(com.Summary, Prefix),
                    Usage = JsonConvert.DeserializeObject<string[]>(com.Remarks).Select(x => string.Format(x, Prefix)).ToArray(),
                    Submodule = com.Module.Name,
                    Module = com.Module.GetTopLevelModule().Name,
                    Options = optHelpStr,
                    Requirements = HelpService.GetCommandRequirements(com),
                };
                if (cmdData.TryGetValue(module.Name, out var cmds))
                    cmds.Add(obj);
                else
                    cmdData.Add(module.Name, new List<object>
                    {
                        obj
                    });
            }
            File.WriteAllText("../../docs/cmds_new.json", JsonConvert.SerializeObject(cmdData, Formatting.Indented));
            await ReplyConfirmLocalizedAsync("commandlist_regen").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Guide()
        {
            await ConfirmLocalizedAsync("guide",
                "https://nadekobot.me/commands",
                "http://nadekobot.readthedocs.io/en/latest/").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Donate()
        {
            await ReplyConfirmLocalizedAsync("donate", PatreonUrl, PaypalUrl).ConfigureAwait(false);
        }

        private string GetRemarks(string[] arr)
        {
            return string.Join(" or ", arr.Select(x => Format.Code(x)));
        }
    }

    public class CommandTextEqualityComparer : IEqualityComparer<CommandInfo>
    {
        public bool Equals(CommandInfo x, CommandInfo y) => x.Aliases[0] == y.Aliases[0];

        public int GetHashCode(CommandInfo obj) => obj.Aliases[0].GetHashCode(StringComparison.InvariantCulture);

    }

    public class JsonCommandData
    {
        public string[] Aliases { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
    }
}
