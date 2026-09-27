using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Sim
{
    /// <summary>
    /// Usage:
    ///   sanguo-sim [--players 4] [--seed 1] [--characters none|random] [--viewer 0] [--quiet]
    ///   sanguo-sim --batch 200 [--players 4] [--characters random]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var opts = ParseArgs(args);
            string dataDir = FileContentSource.FindDataDirectory(Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
            if (dataDir == null)
            {
                Console.Error.WriteLine("Cannot find Assets/Resources/Data");
                return 2;
            }
            var content = ContentLoader.Load(new FileContentSource(dataDir));
            int players = Get(opts, "players", 4);
            int seed = Get(opts, "seed", 1);
            bool characters = opts.TryGetValue("characters", out var ch) && ch == "random";
            int batch = Get(opts, "batch", 0);
            return batch > 0 ? RunBatch(content, players, characters, seed, batch) : RunOne(content, players, characters, seed, Get(opts, "viewer", 0), opts.ContainsKey("quiet"));
        }

        private static GameModeConfig Config(int players, bool characters)
        {
            return new GameModeConfig
            {
                ModeId = FreeForAllMode.Id,
                PlayerCount = players,
                StartingHandSize = 4,
                DrawPerTurn = 2,
                DefaultMaxHp = 4,
                CharacterSelection = characters ? CharacterSelectionMode.Random : CharacterSelectionMode.None,
                MaxRounds = 100
            };
        }

        private static GameSession CreateSession(GameContent content, int players, bool characters, int seed)
        {
            var config = Config(players, characters);
            var setups = Enumerable.Range(0, players).Select(i => new PlayerSetup("玩家" + (char)('A' + i), true)).ToList();
            var engine = new GameEngine(content, new FreeForAllMode(config), config, setups, seed);
            return new GameSession(engine, new ManualClock());
        }

        private static int RunOne(GameContent content, int players, bool characters, int seed, int viewer, bool quiet)
        {
            var session = CreateSession(content, players, characters, seed);
            session.Start();
            // The log is rendered from one player's projected events, exactly like a client would.
            var replica = session.GetSnapshot(viewer);
            var names = GameLogNames.ForClient(content, replica);
            int lines = 0;
            session.AddViewer(viewer, e =>
            {
                replica.Apply(e);
                string line = GameLogFormatter.Format(e, names, viewer);
                if (line == null) return;
                lines++;
                if (!quiet) Console.WriteLine(line);
            });
            Console.WriteLine("=== 混战 " + players + " 人，种子 " + seed + "，视角：" + replica.GetPlayer(viewer).Nickname + " ===");
            foreach (var p in replica.Players.OrderBy(p => p.Seat))
                Console.WriteLine("座位" + p.Seat + " " + p.Nickname + " 武将=" + p.CharacterId + " 生命=" + p.Hp + "/" + p.MaxHp + " 手牌=" + p.HandCount);
            var sw = Stopwatch.StartNew();
            session.RunUntilHumanInputOrEnd();
            sw.Stop();
            var s = session.State;
            Console.WriteLine("=== 结束：" + s.Result.Describe() + " ===");
            Console.WriteLine("回合数=" + s.Turn.TurnNumber + " 轮数=" + s.Turn.Round + " 日志行=" + lines + " 事件序号=" + session.Engine.Context.Events.LastSequence + " 耗时=" + sw.ElapsedMilliseconds + "ms");
            Console.WriteLine("客户端副本与服务器快照一致：" + (replica.Dump() == session.GetSnapshot(viewer).Dump()));
            Console.WriteLine("状态不变量：" + (s.ValidateInvariants() ?? "OK"));
            return 0;
        }

        private static int RunBatch(GameContent content, int players, bool characters, int seed, int games)
        {
            int draws = 0;
            long turns = 0, events = 0;
            var winsBySeat = new int[players];
            var sw = Stopwatch.StartNew();
            for (int g = 0; g < games; g++)
            {
                var session = CreateSession(content, players, characters, seed + g);
                session.Start();
                session.RunUntilHumanInputOrEnd();
                var s = session.State;
                turns += s.Turn.TurnNumber;
                events += session.Engine.Context.Events.LastSequence;
                if (s.Result.IsDraw) draws++;
                else foreach (int w in s.Result.WinnerIds) winsBySeat[s.GetPlayer(w).Seat]++;
                var err = s.ValidateInvariants();
                if (err != null)
                {
                    Console.Error.WriteLine("Invariant violation in game " + (seed + g) + ": " + err);
                    return 1;
                }
            }
            sw.Stop();
            Console.WriteLine(games + " games, " + players + " players, characters=" + characters);
            Console.WriteLine("draws=" + draws + " avgTurns=" + (turns / (double)games).ToString("F1") + " avgEvents=" + (events / (double)games).ToString("F0")
                              + " totalMs=" + sw.ElapsedMilliseconds + " msPerGame=" + (sw.ElapsedMilliseconds / (double)games).ToString("F2"));
            Console.WriteLine("wins by seat: " + string.Join(" ", winsBySeat.Select((w, i) => i + ":" + w)));
            return 0;
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string key = args[i].Substring(2);
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
                d[key] = value;
            }
            return d;
        }

        private static int Get(Dictionary<string, string> d, string key, int fallback)
        {
            return d.TryGetValue(key, out var v) && int.TryParse(v, out int n) ? n : fallback;
        }
    }
}
