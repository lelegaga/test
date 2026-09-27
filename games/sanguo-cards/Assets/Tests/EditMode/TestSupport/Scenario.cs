using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Sanguo.Cards;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    /// <summary>Loads the real content files (Assets/Resources/Data) once per test run.</summary>
    public static class TestContent
    {
        private static GameContent _shared;

        public static string DataDirectory
        {
            get
            {
                string dir = FileContentSource.FindDataDirectory(
                    TestContext.CurrentContext.TestDirectory,
                    Directory.GetCurrentDirectory(),
                    AppContext.BaseDirectory);
                Assert.That(dir, Is.Not.Null, "Could not locate Assets/Resources/Data");
                return dir;
            }
        }

        /// <summary>Shared read-only content. Do not register extra skills on it.</summary>
        public static GameContent Shared => _shared ?? (_shared = LoadFresh());

        /// <summary>A private copy that a test may extend (custom skills, cards).</summary>
        public static GameContent LoadFresh() => ContentLoader.Load(new FileContentSource(DataDirectory));
    }

    /// <summary>
    /// Arranges precise game situations: empty opening hands and no draws by default, cards handed out
    /// explicitly, HP set directly, commands numbered automatically.
    /// </summary>
    public sealed class Scenario
    {
        public GameEngine Engine { get; private set; }
        public GameContext Ctx => Engine.Context;
        public GameState State => Engine.State;
        public ManualClock Clock { get; } = new ManualClock { NowMs = 1000 };
        public List<GameEvent> Events { get; } = new List<GameEvent>();

        public static GameModeConfig DefaultConfig(int players)
        {
            return new GameModeConfig
            {
                ModeId = FreeForAllMode.Id,
                PlayerCount = players,
                ShuffleSeats = false,
                StartingHandSize = 0,
                DrawPerTurn = 0,
                DefaultMaxHp = 4,
                CharacterSelection = CharacterSelectionMode.None,
                // Scenarios hand out cards explicitly; skipping hopeless requests keeps them short.
                AutoSkipImpossibleResponses = true
            };
        }

        public static Scenario Create(int players = 4, Action<GameModeConfig> configure = null, int seed = 1,
            GameContent content = null, Func<GameModeConfig, IGameMode> mode = null, List<PlayerSetup> setups = null, bool start = true)
        {
            var config = DefaultConfig(players);
            configure?.Invoke(config);
            var s = new Scenario();
            if (setups == null)
            {
                setups = new List<PlayerSetup>();
                for (int i = 0; i < config.PlayerCount; i++) setups.Add(new PlayerSetup("P" + i));
            }
            var m = mode != null ? mode(config) : new FreeForAllMode(config);
            s.Engine = new GameEngine(content ?? TestContent.Shared, m, config, setups, seed, "room-test", keepEventHistory: true);
            if (start) s.Start();
            return s;
        }

        public void Start()
        {
            Engine.Start(Clock.NowMs);
            Drain();
            AssertInvariants();
        }

        public PlayerState P(int id) => State.GetPlayer(id);

        public void Drain() => Engine.DrainEvents(Events);

        public List<T> EventsOf<T>() where T : GameEvent
        {
            var list = new List<T>();
            foreach (var e in Events)
                if (e is T t) list.Add(t);
            return list;
        }

        public void ClearEvents() => Events.Clear();

        public void AssertInvariants()
        {
            Assert.That(State.ValidateInvariants(), Is.Null);
        }

        /// <summary>Moves a card with the given id (and optional suit) from the draw/discard pile into a hand.</summary>
        public CardInstance Give(int playerId, string cardId, Suit suit = Suit.None)
        {
            var card = FindFree(cardId, suit);
            Ctx.Mutator.MoveCards(new[] { card }, ZoneType.Hand, playerId, MoveReason.Skill);
            Drain();
            return card;
        }

        /// <summary>Puts a card straight into a player's equipment slot.</summary>
        public CardInstance Equip(int playerId, string cardId)
        {
            var card = FindFree(cardId, Suit.None);
            Ctx.Mutator.Equip(P(playerId), card);
            Drain();
            return card;
        }

        /// <summary>Moves a matching card to the top of the draw pile.</summary>
        public CardInstance PutOnTop(Func<CardInstance, bool> match)
        {
            CardInstance found = null;
            foreach (var c in State.DrawPile)
                if (match(c)) found = c;
            if (found == null)
                foreach (var c in State.DiscardPile)
                    if (match(c)) found = c;
            Assert.That(found, Is.Not.Null, "No matching free card");
            Ctx.Mutator.MoveCards(new[] { found }, ZoneType.DrawPile, -1, MoveReason.Skill);
            Drain();
            Assert.That(State.DrawPile.Top, Is.SameAs(found));
            return found;
        }

        private CardInstance FindFree(string cardId, Suit suit)
        {
            foreach (var zone in new[] { State.DrawPile, State.DiscardPile })
            {
                for (int i = zone.Count - 1; i >= 0; i--)
                {
                    var c = zone[i];
                    if (c.CardId == cardId && (suit == Suit.None || c.Suit == suit)) return c;
                }
            }
            Assert.Fail("No free card '" + cardId + "' (" + suit + ") left in the piles.");
            return null;
        }

        public void SetHp(int playerId, int hp)
        {
            var p = P(playerId);
            Ctx.Mutator.ChangeHp(p, hp - p.Hp, HpChangeReason.LoseHp);
            Drain();
        }

        public PendingRequest Request => Engine.OpenRequests.Count > 0 ? Engine.OpenRequests[0] : null;

        public PendingRequest RequestFor(int playerId) => Ctx.Requests.FindForPlayer(playerId);

        public T ExpectRequest<T>(int playerId) where T : PendingRequest
        {
            var r = RequestFor(playerId);
            Assert.That(r, Is.InstanceOf<T>(), "Expected " + typeof(T).Name + " for player " + playerId + " but open requests are: " + DescribeRequests());
            return (T)r;
        }

        public string DescribeRequests()
        {
            var parts = new List<string>();
            foreach (var r in Engine.OpenRequests) parts.Add(r.ToString());
            return parts.Count == 0 ? "(none)" : string.Join(", ", parts);
        }

        /// <summary>Submits with automatic sequence number and request id (unless already set).</summary>
        public CommandResult Submit(GameCommand cmd)
        {
            if (cmd.SequenceNumber == 0) cmd.SequenceNumber = Engine.GetLastSequence(cmd.PlayerId) + 1;
            if (cmd.RequestId == 0)
            {
                var r = RequestFor(cmd.PlayerId);
                if (r != null) cmd.RequestId = r.RequestId;
            }
            var result = Engine.Submit(cmd, Clock.NowMs);
            Drain();
            AssertInvariants();
            return result;
        }

        public CommandResult Play(int playerId, CardInstance card, params int[] targets)
        {
            return Submit(new PlayCardCommand { PlayerId = playerId, CardInstanceId = card.InstanceId, TargetIds = targets });
        }

        public CommandResult PlayAs(int playerId, CardInstance card, string skillId, string asCardId, params int[] targets)
        {
            return Submit(new PlayCardCommand { PlayerId = playerId, CardInstanceId = card.InstanceId, TargetIds = targets, SkillId = skillId, AsCardId = asCardId });
        }

        public CommandResult Pass(int playerId) => Submit(new RespondCommand { PlayerId = playerId, Pass = true });

        public CommandResult Respond(int playerId, params CardInstance[] cards)
        {
            var ids = new int[cards.Length];
            for (int i = 0; i < cards.Length; i++) ids[i] = cards[i].InstanceId;
            return Submit(new RespondCommand { PlayerId = playerId, CardIds = ids });
        }

        public CommandResult Confirm(int playerId, bool yes) => Submit(new RespondCommand { PlayerId = playerId, OptionIndex = yes ? 1 : 0 });

        public CommandResult EndTurn(int playerId) => Submit(new EndTurnCommand { PlayerId = playerId });

        public CommandResult UseSkill(int playerId, string skillId, int[] cards = null, int[] targets = null)
        {
            return Submit(new UseSkillCommand { PlayerId = playerId, SkillId = skillId, CardIds = cards ?? new int[0], TargetIds = targets ?? new int[0] });
        }

        public void AdvanceTime(long ms)
        {
            Clock.Advance(ms);
            Engine.Tick(Clock.NowMs);
            Drain();
            AssertInvariants();
        }

        /// <summary>Adds a skill to a player mid-game (test setup).</summary>
        public void GrantSkill(int playerId, string skillId)
        {
            Ctx.Mutator.AddSkill(P(playerId), Ctx.Content.Skills.Get(skillId));
            Drain();
        }

        public static void AssertAccepted(CommandResult r)
        {
            Assert.That(r.Accepted, Is.True, r.ToString());
        }

        public static void AssertRejected(CommandResult r, RejectReason reason)
        {
            Assert.That(r.Accepted, Is.False, "Command unexpectedly accepted");
            Assert.That(r.Reason, Is.EqualTo(reason), r.ToString());
        }
    }
}
