using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;

namespace Sanguo.Tests
{
    public class DrawTests
    {
        [Test]
        public void OpeningHandsAndDrawPhaseDealCards()
        {
            var s = Scenario.Create(4, c =>
            {
                c.StartingHandSize = 4;
                c.DrawPerTurn = 2;
            });
            // Player 0 got 4 opening cards + 2 in the draw phase; the others 4.
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(6));
            for (int i = 1; i < 4; i++) Assert.That(s.P(i).HandCardCount, Is.EqualTo(4));
            Assert.That(s.State.DrawPile.Count, Is.EqualTo(84 - 18));
            Assert.That(s.State.Phase, Is.EqualTo(GamePhase.PlayPhase));
            s.ExpectRequest<PlayActionRequest>(0);
        }

        [Test]
        public void DrawingFromEmptyPileReshufflesDiscards()
        {
            var s = Scenario.Create(2);
            var p = s.P(0);
            // Move everything except 1 card into the discard pile.
            var cards = s.State.DrawPile.ToList();
            s.Ctx.Mutator.Discard(cards.Take(cards.Count - 1).ToList(), MoveReason.Discard);
            s.Drain();
            Assert.That(s.State.DrawPile.Count, Is.EqualTo(1));
            var drawn = s.Ctx.Mutator.DrawCards(p, 3);
            s.Drain();
            Assert.That(drawn.Count, Is.EqualTo(3));
            Assert.That(s.State.DiscardPile.Count, Is.EqualTo(0));
            Assert.That(s.State.DrawPile.Count, Is.EqualTo(84 - 3));
            Assert.That(s.EventsOf<DeckReshuffledEvent>().Count, Is.EqualTo(1));
            s.AssertInvariants();
        }

        [Test]
        public void DrawEventHidesCardsFromOtherViewers()
        {
            var s = Scenario.Create(3);
            s.ClearEvents();
            s.Ctx.Mutator.DrawCards(s.P(1), 2);
            s.Drain();
            var e = s.EventsOf<CardDrawnEvent>().Single();
            var own = (CardDrawnEvent)e.ProjectFor(1);
            var other = (CardDrawnEvent)e.ProjectFor(2);
            var spectator = (CardDrawnEvent)e.ProjectFor(-1);
            Assert.That(own.Cards, Has.Count.EqualTo(2));
            Assert.That(other.Cards, Is.Null);
            Assert.That(other.Count, Is.EqualTo(2));
            Assert.That(spectator.Cards, Is.Null);
            // The server event itself is untouched by projection.
            Assert.That(e.Cards, Has.Count.EqualTo(2));
        }
    }

    public class AttackTests
    {
        [Test]
        public void StrikeWithoutDodgeDealsOneDamage()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            Scenario.AssertAccepted(s.Play(0, strike, 1));
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
            Assert.That(strike.Zone, Is.EqualTo(ZoneType.DiscardPile));
            s.ExpectRequest<PlayActionRequest>(0);
        }

        [Test]
        public void TargetIsAskedForDodgeAndMayPass()
        {
            var s = Scenario.Create(4, c => c.AutoSkipImpossibleResponses = false);
            var strike = s.Give(0, "strike");
            Scenario.AssertAccepted(s.Play(0, strike, 1));
            var req = s.ExpectRequest<CardResponseRequest>(1);
            Assert.That(req.RequiredCardId, Is.EqualTo("dodge"));
            Assert.That(s.State.StateMachine.Current, Is.EqualTo(GamePhase.WaitingResponse));
            Scenario.AssertAccepted(s.Pass(1));
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
            Assert.That(strike.Zone, Is.EqualTo(ZoneType.DiscardPile));
            Assert.That(s.State.StateMachine.Current, Is.EqualTo(GamePhase.PlayPhase));
            s.ExpectRequest<PlayActionRequest>(0);
            var dmg = s.EventsOf<DamageAppliedEvent>().Single();
            Assert.That(dmg.SourceId, Is.EqualTo(0));
            Assert.That(dmg.TargetId, Is.EqualTo(1));
            Assert.That(dmg.NewHp, Is.EqualTo(3));
        }

        [Test]
        public void DodgeCancelsStrike()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            var dodge = s.Give(1, "dodge");
            s.Play(0, strike, 1);
            Scenario.AssertAccepted(s.Respond(1, dodge));
            Assert.That(s.P(1).Hp, Is.EqualTo(4));
            Assert.That(dodge.Zone, Is.EqualTo(ZoneType.DiscardPile));
            Assert.That(s.EventsOf<CardPlayedEvent>().Any(e => e.IsResponse && e.UserId == 1 && e.UsedAsCardId == "dodge"), Is.True);
        }

        [Test]
        public void StrikeIsLimitedToOncePerTurn()
        {
            var s = Scenario.Create(4);
            var a = s.Give(0, "strike");
            var b = s.Give(0, "strike");
            s.Play(0, a, 1);
            s.Pass(1);
            Scenario.AssertRejected(s.Play(0, b, 1), RejectReason.UsageLimitReached);
            Assert.That(b.Zone, Is.EqualTo(ZoneType.Hand));
        }

        [Test]
        public void StrikeRequiresAttackRange()
        {
            var s = Scenario.Create(5);
            var strike = s.Give(0, "strike");
            // Seat distance 0->2 is 2 in a 5 player table; base attack range is 1.
            Scenario.AssertRejected(s.Play(0, strike, 2), RejectReason.TargetOutOfRange);
            s.Equip(0, "long_spear");
            Scenario.AssertAccepted(s.Play(0, strike, 2));
        }

        [Test]
        public void CannotStrikeSelfOrUseDodgeActively()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            var dodge = s.Give(0, "dodge");
            Scenario.AssertRejected(s.Play(0, strike, 0), RejectReason.InvalidTarget);
            Scenario.AssertRejected(s.Play(0, dodge), RejectReason.CardNotUsable);
        }

        [Test]
        public void OnlyCurrentPlayerMayPlayCards()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(1, "strike");
            var r = s.Submit(new PlayCardCommand { PlayerId = 1, CardInstanceId = strike.InstanceId, TargetIds = new[] { 2 }, RequestId = s.RequestFor(0).RequestId });
            Scenario.AssertRejected(r, RejectReason.NoPendingRequest);
        }

        [Test]
        public void WrongResponseCardIsRejected()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            var notDodge = s.Give(1, "heal");
            s.Give(1, "dodge");
            s.Play(0, strike, 1);
            Scenario.AssertRejected(s.Respond(1, notDodge), RejectReason.InvalidCard);
            // The request is still open and can be answered properly.
            Scenario.AssertAccepted(s.Pass(1));
        }
    }

    public class HealTests
    {
        [Test]
        public void HealRestoresOneHp()
        {
            var s = Scenario.Create(4);
            s.SetHp(0, 2);
            var heal = s.Give(0, "heal");
            Scenario.AssertAccepted(s.Play(0, heal));
            Assert.That(s.P(0).Hp, Is.EqualTo(3));
            Assert.That(s.EventsOf<HealAppliedEvent>().Single().Amount, Is.EqualTo(1));
        }

        [Test]
        public void HealAtFullHpIsNotUsable()
        {
            var s = Scenario.Create(4);
            var heal = s.Give(0, "heal");
            Scenario.AssertRejected(s.Play(0, heal), RejectReason.CardNotUsable);
        }

        [Test]
        public void FeastHealsEveryWoundedPlayer()
        {
            var s = Scenario.Create(4);
            s.SetHp(1, 2);
            s.SetHp(3, 1);
            var feast = s.Give(0, "feast");
            Scenario.AssertAccepted(s.Play(0, feast));
            Assert.That(s.P(0).Hp, Is.EqualTo(4));
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
            Assert.That(s.P(2).Hp, Is.EqualTo(4));
            Assert.That(s.P(3).Hp, Is.EqualTo(2));
        }
    }

    public class DeathTests
    {
        [Test]
        public void LethalDamageWithoutRescueKills()
        {
            var s = Scenario.Create(4);
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            var victimCard = s.Give(1, "supply");
            s.Play(0, strike, 1);
            s.Pass(1);
            // Nobody holds a heal, so rescue requests are skipped automatically.
            Assert.That(s.P(1).Alive, Is.False);
            Assert.That(s.P(1).IsDying, Is.False);
            Assert.That(victimCard.Zone, Is.EqualTo(ZoneType.DiscardPile));
            var died = s.EventsOf<PlayerDiedEvent>().Single();
            Assert.That(died.PlayerId, Is.EqualTo(1));
            Assert.That(died.KillerId, Is.EqualTo(0));
            Assert.That(s.EventsOf<PlayerDyingEvent>().First().Entered, Is.True);
            Assert.That(s.State.IsGameOver, Is.False);
        }

        [Test]
        public void DyingPlayerCanBeRescuedWithHeal()
        {
            var s = Scenario.Create(4);
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            var heal = s.Give(2, "heal");
            s.Play(0, strike, 1);
            s.Pass(1);
            Assert.That(s.P(1).IsDying, Is.True);
            var rescue = s.ExpectRequest<CardResponseRequest>(2);
            Assert.That(rescue.Purpose, Is.EqualTo("rescue"));
            Assert.That(rescue.SubjectPlayerId, Is.EqualTo(1));
            Scenario.AssertAccepted(s.Respond(2, heal));
            Assert.That(s.P(1).Alive, Is.True);
            Assert.That(s.P(1).IsDying, Is.False);
            Assert.That(s.P(1).Hp, Is.EqualTo(1));
            s.ExpectRequest<PlayActionRequest>(0);
        }

        [Test]
        public void RescueRequestsFollowSeatOrderFromCurrentPlayer()
        {
            var s = Scenario.Create(4);
            s.SetHp(2, 1);
            s.Equip(0, "long_spear");
            var strike = s.Give(0, "strike");
            s.Give(0, "heal");
            s.Give(3, "heal");
            s.Play(0, strike, 2);
            s.Pass(2);
            s.ExpectRequest<CardResponseRequest>(0);
            s.Pass(0);
            s.ExpectRequest<CardResponseRequest>(3);
            s.Pass(3);
            Assert.That(s.P(2).Alive, Is.False);
        }

        [Test]
        public void DeadPlayersTurnIsSkipped()
        {
            var s = Scenario.Create(3);
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            s.Pass(1);
            s.EndTurn(0);
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(2));
            s.ExpectRequest<PlayActionRequest>(2);
        }
    }

    public class VictoryTests
    {
        [Test]
        public void LastPlayerStandingWinsFreeForAll()
        {
            var s = Scenario.Create(2);
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            s.Pass(1);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Phase, Is.EqualTo(GamePhase.GameOver));
            Assert.That(s.State.Result.WinnerIds, Is.EqualTo(new[] { 0 }));
            var ended = s.EventsOf<GameEndedEvent>().Single();
            Assert.That(ended.Result.Reason, Is.EqualTo("last_standing"));
            Assert.That(s.Engine.OpenRequests.Count, Is.EqualTo(0));
            var late = s.Submit(new EndTurnCommand { PlayerId = 0, RequestId = 999 });
            Scenario.AssertRejected(late, RejectReason.GameOver);
        }

        [Test]
        public void RoundLimitEndsGame()
        {
            var s = Scenario.Create(2, c => c.MaxRounds = 2);
            s.SetHp(1, 3);
            for (int i = 0; i < 4 && !s.State.IsGameOver; i++) s.EndTurn(s.State.Turn.CurrentPlayerId);
            Assert.That(s.State.IsGameOver, Is.True);
            Assert.That(s.State.Result.Reason, Is.EqualTo("round_limit"));
            Assert.That(s.State.Result.WinnerIds, Is.EqualTo(new[] { 0 }));
        }
    }

    public class TurnTests
    {
        [Test]
        public void EndTurnPassesToNextPlayerThroughAllPhases()
        {
            var s = Scenario.Create(3);
            s.ClearEvents();
            Scenario.AssertAccepted(s.EndTurn(0));
            var phases = s.EventsOf<PhaseChangedEvent>().Select(e => e.Phase).ToList();
            Assert.That(phases, Is.EqualTo(new[]
            {
                GamePhase.DiscardPhase, GamePhase.TurnEnd, GamePhase.TurnStart, GamePhase.JudgePhase, GamePhase.DrawPhase, GamePhase.PlayPhase
            }));
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(1));
            Assert.That(s.State.Turn.TurnNumber, Is.EqualTo(2));
        }

        [Test]
        public void RoundIncrementsWhenOrderWraps()
        {
            var s = Scenario.Create(2);
            Assert.That(s.State.Turn.Round, Is.EqualTo(1));
            s.EndTurn(0);
            Assert.That(s.State.Turn.Round, Is.EqualTo(1));
            s.EndTurn(1);
            Assert.That(s.State.Turn.Round, Is.EqualTo(2));
        }

        [Test]
        public void DiscardPhaseForcesDiscardDownToHp()
        {
            var s = Scenario.Create(3);
            s.SetHp(0, 2);
            var cards = new[] { s.Give(0, "strike"), s.Give(0, "dodge"), s.Give(0, "supply"), s.Give(0, "duel") };
            s.EndTurn(0);
            var req = s.ExpectRequest<DiscardRequest>(0);
            Assert.That(req.Count, Is.EqualTo(2));
            Scenario.AssertRejected(s.Respond(0, cards[0]), RejectReason.WrongCardCount);
            Scenario.AssertRejected(s.Pass(0), RejectReason.WrongCardCount);
            Scenario.AssertAccepted(s.Respond(0, cards[0], cards[1]));
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(2));
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void TimeoutsEndPlayPhaseAndAutoDiscard()
        {
            var s = Scenario.Create(3, c =>
            {
                c.PlayTimeoutMs = 1000;
                c.DiscardTimeoutMs = 1000;
            });
            s.SetHp(0, 1);
            s.Give(0, "strike");
            s.Give(0, "dodge");
            s.AdvanceTime(999);
            s.ExpectRequest<PlayActionRequest>(0);
            s.AdvanceTime(1);
            s.ExpectRequest<DiscardRequest>(0);
            s.AdvanceTime(1000);
            Assert.That(s.P(0).HandCardCount, Is.EqualTo(1));
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(1));
            Assert.That(s.EventsOf<RequestClosedEvent>().Count(e => e.TimedOut), Is.EqualTo(2));
        }

        [Test]
        public void ResponseTimeoutCountsAsPass()
        {
            var s = Scenario.Create(3, c => c.ResponseTimeoutMs = 500);
            var strike = s.Give(0, "strike");
            s.Give(1, "dodge");
            s.Play(0, strike, 1);
            s.ExpectRequest<CardResponseRequest>(1);
            s.AdvanceTime(500);
            Assert.That(s.P(1).Hp, Is.EqualTo(3));
        }
    }
}
