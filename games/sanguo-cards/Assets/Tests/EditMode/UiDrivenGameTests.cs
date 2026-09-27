using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sanguo.Core;
using Sanguo.Events;
using Sanguo.Game;
using Sanguo.GameModes;
using Sanguo.Presentation;
using Sanguo.Utils;

namespace Sanguo.Tests
{
    /// <summary>
    /// Whole games in which seat 0 is played exactly the way the table screen plays it: only the
    /// client replica (snapshot + projected events) and <see cref="InteractionModel"/> clicks. This
    /// proves every request the server can open is answerable from the UI, and that what the UI
    /// offers as selectable is never rejected by the authoritative rules.
    /// </summary>
    public class UiDrivenGameTests
    {
        /// <summary>A "tapping" player: random choices among what the model says is selectable.</summary>
        private sealed class TappingPlayer
        {
            private readonly Random _rng;
            private readonly InteractionModel _model;
            private int _playTurn = -1;
            private int _playsThisTurn;

            public TappingPlayer(InteractionModel model, int seed)
            {
                _model = model;
                _rng = new Random(seed);
            }

            public readonly HashSet<InteractionMode> ModesSeen = new HashSet<InteractionMode>();

            public GameCommand Decide(ClientGameState state)
            {
                var m = _model;
                ModesSeen.Add(m.Mode);
                switch (m.Mode)
                {
                    case InteractionMode.PlayPhase:
                        if (state.TurnNumber != _playTurn)
                        {
                            _playTurn = state.TurnNumber;
                            _playsThisTurn = 0;
                        }
                        if (_playsThisTurn < 6 && (TryUseSkill(state) || TryPlayCard(state)))
                        {
                            _playsThisTurn++;
                            return m.Confirm();
                        }
                        m.CancelSelection();
                        return m.EndTurn();
                    case InteractionMode.Response:
                    {
                        var usable = Selectable(state);
                        if (usable.Count > 0 && _rng.NextDouble() < 0.7)
                        {
                            foreach (var id in usable)
                            {
                                if (m.CanConfirm) break;
                                m.ClickCard(id);
                            }
                            if (m.CanConfirm) return m.Confirm();
                        }
                        return m.Pass();
                    }
                    case InteractionMode.Discard:
                        foreach (var id in Shuffled(Selectable(state)))
                        {
                            if (m.CanConfirm && m.SelectedCards.Count >= m.Request.Count) break;
                            m.ClickCard(id);
                        }
                        return m.Confirm();
                    case InteractionMode.ChooseTargets:
                        foreach (var id in Shuffled(Targets(state)))
                        {
                            if (m.CanConfirm) break;
                            m.ClickPlayer(id);
                        }
                        return m.CanConfirm ? m.Confirm() : m.Pass();
                    case InteractionMode.Confirm:
                        return _rng.NextDouble() < 0.6 ? m.Confirm() : m.Pass();
                    case InteractionMode.ChooseOption:
                    case InteractionMode.ChooseCharacter:
                        return m.ChooseOption(_rng.Next(m.Request.Options.Count));
                    case InteractionMode.ChooseCardFromPlayer:
                        return PickFromPlayer(state);
                    default:
                        return null;
                }
            }

            private bool TryUseSkill(ClientGameState state)
            {
                if (_rng.NextDouble() > 0.35) return false;
                foreach (var skill in state.Self.Skills)
                {
                    if (!_model.IsSkillUsable(skill.SkillId)) continue;
                    _model.ClickSkill(skill.SkillId);
                    if (_model.Mode == InteractionMode.SkillSelect)
                    {
                        foreach (var id in Shuffled(Selectable(state)))
                        {
                            if (_model.CanConfirm) break;
                            _model.ClickCard(id);
                        }
                        foreach (var id in Shuffled(Targets(state)))
                        {
                            if (_model.CanConfirm) break;
                            _model.ClickPlayer(id);
                        }
                        if (_model.CanConfirm) return true;
                    }
                    else if (TryPlayCard(state))
                    {
                        return true; // conversion skill: play a converted card
                    }
                    _model.CancelSelection();
                }
                return false;
            }

            private bool TryPlayCard(ClientGameState state)
            {
                foreach (var id in Shuffled(Selectable(state)))
                {
                    _model.ClickCard(id);
                    foreach (var target in Shuffled(Targets(state)))
                    {
                        if (_model.CanConfirm) break;
                        _model.ClickPlayer(target);
                    }
                    if (_model.CanConfirm) return true;
                    _model.CancelSelection();
                }
                return false;
            }

            private GameCommand PickFromPlayer(ClientGameState state)
            {
                var r = _model.Request;
                var target = state.GetPlayer(r.TargetPlayerId);
                int zones = r.Zones == 0 ? (int)ZoneMask.All : r.Zones;
                if ((zones & (int)ZoneMask.Hand) != 0 && target.HandCount > 0) return _model.PickFromPlayer(ZoneType.Hand, _rng.Next(target.HandCount));
                if ((zones & (int)ZoneMask.Equipment) != 0)
                    for (int slot = 1; slot < target.Equipment.Length; slot++)
                        if (target.Equipment[slot] != null) return _model.PickFromPlayer(ZoneType.Equipment, slot);
                if ((zones & (int)ZoneMask.Judge) != 0 && target.JudgeArea.Count > 0) return _model.PickFromPlayer(ZoneType.JudgeArea, 0);
                return null;
            }

            private List<int> Selectable(ClientGameState state)
            {
                return state.Self.HandCards.Select(c => c.InstanceId).Where(_model.IsCardSelectable).ToList();
            }

            private List<int> Targets(ClientGameState state)
            {
                return state.Players.Select(p => p.PlayerId).Where(id => _model.IsTargetSelectable(id) && !_model.IsTargetSelected(id)).ToList();
            }

            private List<int> Shuffled(List<int> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
                return list;
            }
        }

        private static GameModeConfig Config(string modeId, int players, CharacterSelectionMode selection)
        {
            var c = new GameModeConfig { ModeId = modeId, PlayerCount = players, CharacterSelection = selection };
            if (modeId == Team3v3Mode.Id || modeId == Team5v5Mode.Id || modeId == Team10v10Mode.Id) c.TeamSize = players / 2;
            return c;
        }

        [TestCase("identity", 5, CharacterSelectionMode.Choose, 101)]
        [TestCase("identity", 8, CharacterSelectionMode.Choose, 102)]
        [TestCase("identity", 5, CharacterSelectionMode.Random, 103)]
        [TestCase("ffa", 4, CharacterSelectionMode.Choose, 104)]
        [TestCase("ffa", 3, CharacterSelectionMode.None, 105)]
        [TestCase("team3v3", 6, CharacterSelectionMode.Choose, 106)]
        [TestCase("team5v5", 10, CharacterSelectionMode.Random, 107)]
        public void SeatPlayedOnlyThroughInteractionModelFinishesWithoutRejections(string modeId, int players, CharacterSelectionMode selection, int seed)
        {
            var config = Config(modeId, players, selection);
            var setups = Enumerable.Range(0, players).Select(i => new PlayerSetup(i == 0 ? "Human" : "Bot" + i, i != 0)).ToList();
            var engine = new GameEngine(TestContent.Shared, GameModeRegistry.CreateDefault().Create(config), config, setups, seed, "ui-test")
            {
                RethrowInternalErrors = true
            };
            var session = new GameSession(engine, new ManualClock());
            session.Start();

            // Exactly what LocalGameView does: snapshot, then projected events (resync on a gap).
            var state = session.GetSnapshot(0);
            int resyncs = 0;
            session.AddViewer(0, e =>
            {
                if (state.Apply(e)) return;
                state = session.GetSnapshot(0);
                resyncs++;
            });
            var names = new GameLogNames(TestContent.Shared, id => state.GetPlayer(id)?.Nickname);
            var model = new InteractionModel(0, names);
            var player = new TappingPlayer(model, seed);
            var rejections = new List<string>();
            int commands = 0;

            for (int step = 0; step < 5000 && !engine.IsGameOver; step++)
            {
                session.RunUntilHumanInputOrEnd();
                if (engine.IsGameOver) break;
                model.Refresh(state);
                Assert.That(model.Request, Is.Not.Null, "the game waits, but not for the human: " + state.Dump());
                string prompt = model.Prompt;
                Assert.That(prompt, Is.Not.Empty);
                var cmd = player.Decide(state);
                Assert.That(cmd, Is.Not.Null, "the UI offers no way to answer " + model.Request.Describe());
                cmd.SequenceNumber = engine.GetLastSequence(0) + 1;
                var result = session.Submit(cmd);
                commands++;
                if (!result.Accepted)
                {
                    rejections.Add(cmd + " -> " + result + " (prompt: " + prompt + ")");
                    model.OnCommandRejected();
                    engine.ResolveWithDefault(engine.OpenRequests.First(r => r.PlayerId == 0), session.Now);
                }
            }

            Assert.That(engine.IsGameOver, Is.True, "game did not finish");
            Assert.That(state.IsGameOver, Is.True, "the replica saw the end");
            Assert.That(rejections, Is.Empty, string.Join("\n", rejections));
            Assert.That(resyncs, Is.Zero, "the incremental replica never needed a resync");
            Assert.That(commands, Is.GreaterThan(0));
            if (selection == CharacterSelectionMode.Choose) Assert.That(player.ModesSeen, Does.Contain(InteractionMode.ChooseCharacter));
        }
    }
}
