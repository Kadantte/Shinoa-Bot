using NadekoBot.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Gambling.Common.WheelOfFortune
{
    public class WheelOfFortune
    {
        private static readonly NadekoRandom _rng = new NadekoRandom();

        private static readonly ImmutableArray<string> _emojis = new string[] {
            "⬆",
            "↖",
            "⬅",
            "↙",
            "⬇",
            "↘",
            "➡",
            "↗" }.ToImmutableArray();

        public static readonly ImmutableArray<float> Multipliers = new float[] {
            2.5f,
            2f,
            0.2f,
            0.1f,
            0.3f,
            0.5f,
            1f,
            3f,
        }.ToImmutableArray();

        public int Result { get; }
        public string Emoji => _emojis[Result];
        public float Multiplier => Multipliers[Result];

        public WheelOfFortune()
        {
            this.Result = _rng.Next(0, 8);
        }
    }
}
