using System;
using System.Collections.Generic;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    public class JsonTests
    {
        [Test]
        public void ParsesNestedDocument()
        {
            var j = JsonValue.Parse("{\"a\":1,\"b\":[true,null,\"x\\u0041\"],\"c\":{\"d\":-2.5e1}}");
            Assert.That(j.GetInt("a"), Is.EqualTo(1));
            Assert.That(j["b"].Count, Is.EqualTo(3));
            Assert.That(j["b"][0].AsBool(), Is.True);
            Assert.That(j["b"][1].IsNull, Is.True);
            Assert.That(j["b"][2].AsString(), Is.EqualTo("xA"));
            Assert.That(j["c"].GetDouble("d"), Is.EqualTo(-25.0));
            Assert.That(j["missing"].IsNull, Is.True);
        }

        [Test]
        public void RoundTripsThroughText()
        {
            var o = JsonValue.NewObject().Set("name", "攻击\n\"q\"").Set("n", 3).Set("ok", true);
            o.Set("list", JsonValue.NewArray().Add(JsonValue.From(1)).Add(JsonValue.From("two")));
            var back = JsonValue.Parse(o.ToJson(pretty: true));
            Assert.That(back.GetString("name"), Is.EqualTo("攻击\n\"q\""));
            Assert.That(back.GetInt("n"), Is.EqualTo(3));
            Assert.That(back.GetBool("ok"), Is.True);
            Assert.That(back["list"][1].AsString(), Is.EqualTo("two"));
        }

        [Test]
        public void RejectsMalformedInput()
        {
            Assert.Throws<FormatException>(() => JsonValue.Parse("{\"a\":}"));
            Assert.Throws<FormatException>(() => JsonValue.Parse("[1,2"));
            Assert.Throws<FormatException>(() => JsonValue.Parse("{} trailing"));
        }
    }

    public class RandomTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new XorShiftRandom(42);
            var b = new XorShiftRandom(42);
            for (int i = 0; i < 1000; i++) Assert.That(a.Next(100), Is.EqualTo(b.Next(100)));
        }

        [Test]
        public void StaysInRangeAndCoversValues()
        {
            var r = new XorShiftRandom(7);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int v = r.Next(10);
                Assert.That(v, Is.InRange(0, 9));
                seen.Add(v);
            }
            Assert.That(seen.Count, Is.EqualTo(10));
        }

        [Test]
        public void ShuffleKeepsElements()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            new XorShiftRandom(3).Shuffle(list);
            list.Sort();
            Assert.That(list, Is.EqualTo(new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 }));
        }
    }

    public class GameStateMachineTests
    {
        [Test]
        public void FollowsStandardTurnOrder()
        {
            var sm = new GameStateMachine();
            var order = new[]
            {
                GamePhase.Preparing, GamePhase.GameStart, GamePhase.TurnStart, GamePhase.JudgePhase, GamePhase.DrawPhase,
                GamePhase.PlayPhase, GamePhase.DiscardPhase, GamePhase.TurnEnd, GamePhase.TurnStart
            };
            foreach (var p in order)
            {
                sm.TransitionTo(p);
                Assert.That(sm.Phase, Is.EqualTo(p));
            }
        }

        [Test]
        public void RejectsIllegalTransitions()
        {
            var sm = new GameStateMachine();
            Assert.Throws<InvalidOperationException>(() => sm.TransitionTo(GamePhase.PlayPhase));
            sm.TransitionTo(GamePhase.Preparing);
            sm.TransitionTo(GamePhase.GameStart);
            sm.TransitionTo(GamePhase.TurnStart);
            // Phases cannot be jumped over, even by skills.
            Assert.Throws<InvalidOperationException>(() => sm.TransitionTo(GamePhase.PlayPhase));
            Assert.Throws<InvalidOperationException>(() => sm.TransitionTo(GamePhase.TurnStart));
        }

        [Test]
        public void WaitingResponseIsAnOverlay()
        {
            var sm = new GameStateMachine();
            sm.TransitionTo(GamePhase.Preparing);
            sm.TransitionTo(GamePhase.GameStart);
            sm.TransitionTo(GamePhase.TurnStart);
            sm.TransitionTo(GamePhase.JudgePhase);
            sm.TransitionTo(GamePhase.DrawPhase);
            sm.TransitionTo(GamePhase.PlayPhase);
            sm.EnterWaitingResponse();
            Assert.That(sm.Current, Is.EqualTo(GamePhase.WaitingResponse));
            Assert.That(sm.Phase, Is.EqualTo(GamePhase.PlayPhase));
            sm.ExitWaitingResponse();
            Assert.That(sm.Current, Is.EqualTo(GamePhase.PlayPhase));
            Assert.Throws<InvalidOperationException>(() => sm.ExitWaitingResponse());
        }

        [Test]
        public void GameOverReachableFromAnyInGamePhase()
        {
            var sm = new GameStateMachine();
            sm.TransitionTo(GamePhase.Preparing);
            sm.TransitionTo(GamePhase.GameOver);
            Assert.That(sm.Current, Is.EqualTo(GamePhase.GameOver));
            Assert.Throws<InvalidOperationException>(() => sm.TransitionTo(GamePhase.TurnStart));
        }
    }

    public class ContentTests
    {
        [Test]
        public void LoadsBaseContent()
        {
            var c = TestContent.Shared;
            Assert.That(c.Cards.Count, Is.GreaterThanOrEqualTo(15));
            Assert.That(c.Characters.Count, Is.GreaterThanOrEqualTo(10));
            Assert.That(c.GetDeck("standard").Entries.Count, Is.EqualTo(84));
            var strike = c.Cards.Get("strike");
            Assert.That(strike.CardType, Is.EqualTo(Cards.CardType.Basic));
            Assert.That(strike.UsageLimitKey, Is.EqualTo("strike"));
            Assert.That(strike.Effects.Count, Is.EqualTo(1));
            Assert.That(c.Cards.Get("dodge").CanUseActively, Is.False);
            Assert.That(c.Cards.Get("long_spear"), Is.InstanceOf<Cards.EquipmentCard>());
            foreach (var ch in c.Characters.All)
                foreach (var s in ch.SkillIds)
                    Assert.That(c.Skills.TryGet(s, out _), Is.True, ch.Id + " -> " + s);
        }

        [Test]
        public void RejectsUnknownEffectType()
        {
            var src = new DictionaryContentSource()
                .Set(ContentLoader.CardsFile, "{\"cards\":[{\"id\":\"x\",\"type\":\"basic\",\"effects\":[{\"type\":\"Nope\"}]}],\"decks\":[]}")
                .Set(ContentLoader.SkillsFile, "{\"skills\":[]}")
                .Set(ContentLoader.CharactersFile, "{\"characters\":[]}");
            Assert.Throws<FormatException>(() => ContentLoader.Load(src));
        }

        [Test]
        public void RejectsCharacterWithUnknownSkill()
        {
            var src = new DictionaryContentSource()
                .Set(ContentLoader.CardsFile, "{\"cards\":[],\"decks\":[]}")
                .Set(ContentLoader.SkillsFile, "{\"skills\":[]}")
                .Set(ContentLoader.CharactersFile, "{\"characters\":[{\"id\":\"a\",\"skills\":[\"ghost\"]}]}");
            Assert.Throws<FormatException>(() => ContentLoader.Load(src));
        }

        [Test]
        public void ConfigRoundTripsThroughJson()
        {
            var c = new GameModes.GameModeConfig { ModeId = "identity", PlayerCount = 8, TeamSize = 3, UseCaptain = true };
            c.Roles.Add(new GameModes.RoleCount(Role.Rebel, 4));
            var back = GameModes.GameModeConfig.FromJson(JsonValue.Parse(c.ToJson().ToJson()));
            Assert.That(back.ModeId, Is.EqualTo("identity"));
            Assert.That(back.PlayerCount, Is.EqualTo(8));
            Assert.That(back.UseCaptain, Is.True);
            Assert.That(back.GetRoleCount(Role.Rebel), Is.EqualTo(4));
        }
    }
}
