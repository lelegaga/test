using NUnit.Framework;
using Sanguo.Core;

namespace Sanguo.Tests
{
    /// <summary>Malicious or buggy clients: every illegal command is rejected and changes nothing.</summary>
    public class ValidationTests
    {
        private static string Fingerprint(Scenario s) => s.Engine.CreateSnapshot(-1).Dump() + "|" + s.State.ValidateInvariants();

        [Test]
        public void DuplicateSequenceIsRejected()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            var cmd = new PlayCardCommand { PlayerId = 0, CardInstanceId = strike.InstanceId, TargetIds = new[] { 1 } };
            Scenario.AssertAccepted(s.Submit(cmd));
            var replay = new PlayCardCommand { PlayerId = 0, CardInstanceId = strike.InstanceId, TargetIds = new[] { 1 }, SequenceNumber = cmd.SequenceNumber, RequestId = cmd.RequestId };
            Scenario.AssertRejected(s.Submit(replay), RejectReason.DuplicateSequence);
        }

        [Test]
        public void OutOfOrderSequenceIsRejected()
        {
            var s = Scenario.Create(4);
            var r = s.Submit(new EndTurnCommand { PlayerId = 0, SequenceNumber = 5 });
            Scenario.AssertRejected(r, RejectReason.OutOfOrderSequence);
            Assert.That(s.State.Turn.CurrentPlayerId, Is.EqualTo(0));
        }

        [Test]
        public void RejectedCommandStillConsumesItsSequenceNumber()
        {
            var s = Scenario.Create(4);
            var dodge = s.Give(0, "dodge");
            var bad = new PlayCardCommand { PlayerId = 0, CardInstanceId = dodge.InstanceId, SequenceNumber = 1 };
            Scenario.AssertRejected(s.Submit(bad), RejectReason.CardNotUsable);
            Assert.That(s.Engine.GetLastSequence(0), Is.EqualTo(1));
            Scenario.AssertAccepted(s.Submit(new EndTurnCommand { PlayerId = 0, SequenceNumber = 2 }));
        }

        [Test]
        public void StaleRequestIdIsRejected()
        {
            var s = Scenario.Create(4, c => c.AutoSkipImpossibleResponses = false);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            int oldRequest = s.RequestFor(1).RequestId;
            s.Pass(1);
            var r = s.Submit(new RespondCommand { PlayerId = 1, Pass = true, RequestId = oldRequest });
            Scenario.AssertRejected(r, RejectReason.NoPendingRequest);
        }

        [Test]
        public void CannotUseAnotherPlayersCard()
        {
            var s = Scenario.Create(4);
            var foreign = s.Give(2, "strike");
            string before = Fingerprint(s);
            Scenario.AssertRejected(s.Play(0, foreign, 1), RejectReason.CardNotOwned);
            Assert.That(foreign.OwnerId, Is.EqualTo(2));
            Assert.That(Fingerprint(s), Is.EqualTo(before));
        }

        [Test]
        public void CannotUseNonexistentCard()
        {
            var s = Scenario.Create(4);
            var r = s.Submit(new PlayCardCommand { PlayerId = 0, CardInstanceId = 99999, TargetIds = new[] { 1 } });
            Scenario.AssertRejected(r, RejectReason.CardNotOwned);
        }

        [Test]
        public void RejectsMalformedTargets()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            Scenario.AssertRejected(s.Play(0, strike), RejectReason.WrongTargetCount);
            Scenario.AssertRejected(s.Play(0, strike, 1, 3), RejectReason.WrongTargetCount);
            Scenario.AssertRejected(s.Play(0, strike, 42), RejectReason.InvalidTarget);
            var supply = s.Give(0, "supply");
            Scenario.AssertRejected(s.Play(0, supply, 1), RejectReason.WrongTargetCount);
        }

        [Test]
        public void RejectsUnknownPlayer()
        {
            var s = Scenario.Create(4);
            Scenario.AssertRejected(s.Submit(new EndTurnCommand { PlayerId = 17, SequenceNumber = 1 }), RejectReason.UnknownPlayer);
        }

        [Test]
        public void RejectsWrongCommandTypeForRequest()
        {
            var s = Scenario.Create(4);
            Scenario.AssertRejected(s.Pass(0), RejectReason.WrongCommandType);
            Scenario.AssertRejected(s.Submit(new SelectTargetCommand { PlayerId = 0, TargetIds = new[] { 1 } }), RejectReason.WrongCommandType);
        }

        [Test]
        public void DeadPlayerCannotAct()
        {
            var s = Scenario.Create(4);
            s.SetHp(1, 1);
            var strike = s.Give(0, "strike");
            s.Play(0, strike, 1);
            Assert.That(s.P(1).Alive, Is.False);
            Scenario.AssertRejected(s.Submit(new EndTurnCommand { PlayerId = 1 }), RejectReason.PlayerDead);
        }

        [Test]
        public void CannotRespondWithCardsNotInHand()
        {
            var s = Scenario.Create(4, c => c.AutoSkipImpossibleResponses = false);
            var strike = s.Give(0, "strike");
            var someoneElsesDodge = s.Give(2, "dodge");
            s.Play(0, strike, 1);
            Scenario.AssertRejected(s.Respond(1, someoneElsesDodge), RejectReason.CardNotOwned);
            var dup = s.Give(1, "dodge");
            var r = s.Submit(new RespondCommand { PlayerId = 1, CardIds = new[] { dup.InstanceId, dup.InstanceId } });
            Scenario.AssertRejected(r, RejectReason.WrongCardCount);
        }

        [Test]
        public void RejectedCommandsLeaveStateUntouched()
        {
            var s = Scenario.Create(4);
            var strike = s.Give(0, "strike");
            var dodge = s.Give(0, "dodge");
            string before = Fingerprint(s);
            s.Play(0, strike, 0);
            s.Play(0, dodge);
            s.Submit(new UseSkillCommand { PlayerId = 0, SkillId = "valor" });
            s.Submit(new RespondCommand { PlayerId = 0, Pass = true });
            Assert.That(Fingerprint(s), Is.EqualTo(before));
            Assert.That(s.P(1).Hp, Is.EqualTo(4));
        }
    }
}
