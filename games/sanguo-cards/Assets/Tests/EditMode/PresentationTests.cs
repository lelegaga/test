using System.IO;
using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Presentation;
using Sanguo.Save;

namespace Sanguo.Tests
{
    /// <summary>The UI brain driven by real server snapshots (what a client would hold).</summary>
    public class InteractionModelTests
    {
        private static InteractionModel ModelFor(Scenario s, int viewer, out ClientGameState view)
        {
            view = s.Engine.CreateSnapshot(viewer);
            var names = new GameLogNames(s.Ctx.Content, id => s.P(id).Nickname);
            var model = new InteractionModel(viewer, names);
            model.Refresh(view);
            return model;
        }

        [Test]
        public void PlayPhaseSelectsCardThenLegalTargetThenBuildsCommand()
        {
            var s = Scenario.Create(5);
            var strike = s.Give(0, "strike");
            var dodge = s.Give(0, "dodge");
            var model = ModelFor(s, 0, out _);
            Assert.That(model.Mode, Is.EqualTo(InteractionMode.PlayPhase));
            Assert.That(model.IsCardSelectable(strike.InstanceId), Is.True);
            Assert.That(model.IsCardSelectable(dodge.InstanceId), Is.False, "dodge cannot be used actively");
            Assert.That(model.CanConfirm, Is.False);

            model.ClickCard(strike.InstanceId);
            Assert.That(model.IsCardSelected(strike.InstanceId), Is.True);
            Assert.That(model.IsTargetSelectable(1), Is.True);
            Assert.That(model.IsTargetSelectable(4), Is.True);
            Assert.That(model.IsTargetSelectable(2), Is.False, "out of attack range");
            Assert.That(model.IsTargetSelectable(0), Is.False, "cannot target self");
            model.ClickPlayer(2);
            Assert.That(model.SelectedTargets, Is.Empty);
            model.ClickPlayer(4);
            Assert.That(model.CanConfirm, Is.True);
            Assert.That(model.Prompt, Does.Contain("攻击"));

            var cmd = (PlayCardCommand)model.Confirm();
            Assert.That(cmd.CardInstanceId, Is.EqualTo(strike.InstanceId));
            Assert.That(cmd.TargetIds, Is.EqualTo(new[] { 4 }));
            Assert.That(model.Submitted, Is.True);
            Assert.That(model.Confirm(), Is.Null, "no double submit");
            Scenario.AssertAccepted(s.Submit(cmd));
            Assert.That(s.P(4).Hp, Is.EqualTo(3));
        }

        [Test]
        public void ClickingSelectedCardAgainDeselects()
        {
            var s = Scenario.Create(3);
            var supply = s.Give(0, "supply");
            var model = ModelFor(s, 0, out _);
            model.ClickCard(supply.InstanceId);
            Assert.That(model.CanConfirm, Is.True, "no targets needed");
            model.ClickCard(supply.InstanceId);
            Assert.That(model.SelectedCards, Is.Empty);
            Assert.That(model.CanConfirm, Is.False);
        }

        [Test]
        public void ResponseOffersOnlyUsableCardsAndPass()
        {
            var s = Scenario.Create(3, c => c.AutoSkipImpossibleResponses = false);
            var strike = s.Give(0, "strike");
            var dodge = s.Give(1, "dodge");
            var heal = s.Give(1, "heal");
            s.Play(0, strike, 1);
            var model = ModelFor(s, 1, out _);
            Assert.That(model.Mode, Is.EqualTo(InteractionMode.Response));
            Assert.That(model.IsCardSelectable(dodge.InstanceId), Is.True);
            Assert.That(model.IsCardSelectable(heal.InstanceId), Is.False);
            Assert.That(model.CanPass, Is.True);
            Assert.That(model.Prompt, Does.Contain("闪避"));
            model.ClickCard(dodge.InstanceId);
            var cmd = (RespondCommand)model.Confirm();
            Assert.That(cmd.CardIds, Is.EqualTo(new[] { dodge.InstanceId }));
            Scenario.AssertAccepted(s.Submit(cmd));
            Assert.That(s.P(1).Hp, Is.EqualTo(4));
        }

        [Test]
        public void OtherPlayersSeeWaitingWithoutPrivateHints()
        {
            var s = Scenario.Create(3, c => c.AutoSkipImpossibleResponses = false);
            var strike = s.Give(0, "strike");
            s.Give(1, "dodge");
            s.Play(0, strike, 1);
            var model = ModelFor(s, 2, out var view);
            Assert.That(model.Mode, Is.EqualTo(InteractionMode.Waiting));
            Assert.That(model.Prompt, Does.StartWith("等待 P1"));
            Assert.That(view.OpenRequests[0].PlayHints, Is.Null);
        }

        [Test]
        public void DiscardNeedsExactCount()
        {
            var s = Scenario.Create(3);
            s.SetHp(0, 1);
            var a = s.Give(0, "strike");
            var b = s.Give(0, "dodge");
            var c = s.Give(0, "supply");
            s.EndTurn(0);
            var model = ModelFor(s, 0, out _);
            Assert.That(model.Mode, Is.EqualTo(InteractionMode.Discard));
            model.ClickCard(a.InstanceId);
            Assert.That(model.CanConfirm, Is.False);
            model.ClickCard(b.InstanceId);
            model.ClickCard(c.InstanceId);
            Assert.That(model.SelectedCards.Count, Is.EqualTo(2), "cannot exceed the discard count");
            Scenario.AssertAccepted(s.Submit(model.Confirm()));
        }

        [Test]
        public void ActiveSkillFlowBuildsUseSkillCommand()
        {
            var s = Scenario.Create(3);
            s.GrantSkill(0, "raid");
            var c1 = s.Give(0, "dodge");
            var c2 = s.Give(0, "dodge");
            var model = ModelFor(s, 0, out _);
            Assert.That(model.IsSkillUsable("raid"), Is.True);
            model.ClickSkill("raid");
            Assert.That(model.Mode, Is.EqualTo(InteractionMode.SkillSelect));
            model.ClickCard(c1.InstanceId);
            model.ClickCard(c2.InstanceId);
            model.ClickPlayer(2);
            var cmd = (UseSkillCommand)model.Confirm();
            Assert.That(cmd.SkillId, Is.EqualTo("raid"));
            Scenario.AssertAccepted(s.Submit(cmd));
            Assert.That(s.P(2).Hp, Is.EqualTo(3));
        }

        [Test]
        public void ConversionSkillLetsDodgeBePlayedAsStrike()
        {
            var s = Scenario.Create(3, c => c.AutoSkipImpossibleResponses = false);
            s.GrantSkill(0, "dragon_dance");
            var dodge = s.Give(0, "dodge");
            var model = ModelFor(s, 0, out _);
            Assert.That(model.IsCardSelectable(dodge.InstanceId), Is.True, "usable through the conversion");
            model.ClickCard(dodge.InstanceId);
            model.ClickPlayer(1);
            var cmd = (PlayCardCommand)model.Confirm();
            Assert.That(cmd.SkillId, Is.EqualTo("dragon_dance"));
            Assert.That(cmd.AsCardId, Is.EqualTo("strike"));
            Scenario.AssertAccepted(s.Submit(cmd));
        }

        [Test]
        public void ConfirmDialogAndCharacterChoice()
        {
            var s = Scenario.Create(3, c => c.CharacterSelection = CharacterSelectionMode.Choose);
            var model = ModelFor(s, 1, out _);
            Assert.That(model.Mode, Is.EqualTo(InteractionMode.ChooseCharacter));
            Assert.That(model.ChooseOption(99), Is.Null);
            Scenario.AssertAccepted(s.Submit(model.ChooseOption(1)));
        }
    }

    public class SeatLayoutTests
    {
        private static ClientGameState Table(int players, int current)
        {
            var s = new ClientGameState { CurrentPlayerId = current };
            for (int i = 0; i < players; i++) s.Players.Add(new ClientPlayerState { PlayerId = i, Seat = i });
            return s;
        }

        [Test]
        public void SmallTableShowsEveryoneMediumAndCurrentLarge()
        {
            var slots = SeatLayout.Arrange(Table(5, 3), 0);
            Assert.That(slots.Select(x => x.PlayerId), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(slots.Single(x => x.PlayerId == 3).Tier, Is.EqualTo(SeatTier.Large));
            Assert.That(slots.Where(x => x.PlayerId != 3).All(x => x.Tier == SeatTier.Medium), Is.True);
        }

        [Test]
        public void TwentyPlayersEmphasiseNeighboursAndActivePlayer()
        {
            var slots = SeatLayout.Arrange(Table(20, 10), 0);
            Assert.That(slots.Count, Is.EqualTo(19));
            Assert.That(slots[0].PlayerId, Is.EqualTo(1));
            Assert.That(slots[18].PlayerId, Is.EqualTo(19));
            Assert.That(slots.Single(x => x.PlayerId == 10).Tier, Is.EqualTo(SeatTier.Large));
            foreach (int neighbour in new[] { 1, 2, 18, 19, 9, 11 })
                Assert.That(slots.Single(x => x.PlayerId == neighbour).Tier, Is.EqualTo(SeatTier.Medium), "player " + neighbour);
            Assert.That(slots.Single(x => x.PlayerId == 5).Tier, Is.EqualTo(SeatTier.Small));
            Assert.That(slots.Count(x => x.Tier == SeatTier.Small), Is.EqualTo(12));
        }

        [Test]
        public void SpectatorStartsFromSeatZero()
        {
            var slots = SeatLayout.Arrange(Table(4, 0), -1);
            Assert.That(slots.Select(x => x.PlayerId), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void LogFeedKeepsNewestLines()
        {
            var feed = new LogFeed(10);
            for (int i = 0; i < 25; i++) feed.Add("line " + i);
            Assert.That(feed.Lines.Count, Is.EqualTo(10));
            Assert.That(feed.Lines[0], Is.EqualTo("line 15"));
            Assert.That(feed.Lines[9], Is.EqualTo("line 24"));
        }

        [Test]
        public void CardTextFormatting()
        {
            Assert.That(CardText.SuitSymbol(Suit.Heart) + CardText.NumberText(12), Is.EqualTo("♥Q"));
            Assert.That(CardText.IsRed(Suit.Diamond), Is.True);
            Assert.That(CardText.IsRed(Suit.Club), Is.False);
        }
    }

    public class ProfileTests
    {
        [Test]
        public void ProfileRoundTripsAndSurvivesCorruption()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sanguo-profile-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var fresh = ProfileStore.Load(dir);
                Assert.That(fresh.Nickname, Is.EqualTo("玩家"));
                Assert.That(fresh.DeviceId, Is.Not.Empty);
                fresh.Nickname = "诸葛";
                fresh.SfxVolume = 0.25f;
                fresh.LastServerAddress = "192.168.1.8";
                fresh.LastRoomConfig = new GameModeConfig { ModeId = Team5v5Mode.Id, PlayerCount = 10 };
                ProfileStore.Save(dir, fresh);
                var back = ProfileStore.Load(dir);
                Assert.That(back.Nickname, Is.EqualTo("诸葛"));
                Assert.That(back.SfxVolume, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(back.LastServerAddress, Is.EqualTo("192.168.1.8"));
                Assert.That(back.LastRoomConfig.ModeId, Is.EqualTo(Team5v5Mode.Id));
                Assert.That(back.DeviceId, Is.EqualTo(fresh.DeviceId));

                File.WriteAllText(Path.Combine(dir, ProfileStore.FileName), "{ not json");
                Assert.That(ProfileStore.Load(dir).Nickname, Is.EqualTo("玩家"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
