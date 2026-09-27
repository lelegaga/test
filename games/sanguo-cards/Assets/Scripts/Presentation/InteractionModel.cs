using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Events;

namespace Sanguo.Presentation
{
    public enum InteractionMode : byte
    {
        /// <summary>Nothing open at all (e.g. between phases).</summary>
        Idle = 0,
        /// <summary>Someone else is being asked something.</summary>
        Waiting = 1,
        PlayPhase = 2,
        /// <summary>Play phase, building an active skill use (cost cards + targets).</summary>
        SkillSelect = 3,
        Response = 4,
        Discard = 5,
        ChooseCardFromPlayer = 6,
        ChooseTargets = 7,
        Confirm = 8,
        ChooseOption = 9,
        ChooseCharacter = 10
    }

    /// <summary>
    /// Engine-free "UI brain" for the local player: turns taps on cards, players and skills into
    /// a selection, answers "is this selectable / can I confirm", and builds the intent command.
    /// Everything it offers comes from the server's request hints, so illegal choices are simply
    /// not selectable (the server still validates whatever is sent).
    /// </summary>
    public sealed class InteractionModel
    {
        private readonly int _viewerId;
        private readonly IGameLogNames _names;
        private readonly List<int> _selectedCards = new List<int>();
        private readonly List<int> _selectedTargets = new List<int>();
        private ClientGameState _state;
        private RequestInfo _request;
        private PlayHint _activeHint;
        private SkillHint _activeSkill;
        private string _conversionSkill;

        public InteractionModel(int viewerId, IGameLogNames names)
        {
            _viewerId = viewerId;
            _names = names;
        }

        public InteractionMode Mode { get; private set; }
        public RequestInfo Request => _request;

        /// <summary>The request someone else is answering (for "waiting for X" display).</summary>
        public RequestInfo OtherRequest { get; private set; }

        /// <summary>True after a command was produced for the current request (buttons disabled until the next one).</summary>
        public bool Submitted { get; private set; }

        public IReadOnlyList<int> SelectedCards => _selectedCards;
        public IReadOnlyList<int> SelectedTargets => _selectedTargets;
        public string ActiveSkillId => _activeSkill?.SkillId;
        public string ConversionSkillId => _conversionSkill;

        /// <summary>Bumps whenever something the UI shows may have changed.</summary>
        public int Version { get; private set; }

        /// <summary>Re-reads the replica; resets the selection when a new request arrives. Returns true if anything changed.</summary>
        public bool Refresh(ClientGameState state)
        {
            _state = state;
            var mine = state != null && _viewerId >= 0 ? state.FindRequestFor(_viewerId) : null;
            RequestInfo other = null;
            if (mine == null && state != null && state.OpenRequests.Count > 0) other = state.OpenRequests[0];
            bool changed = !ReferenceEquals(other, OtherRequest);
            OtherRequest = other;
            if ((mine?.RequestId ?? 0) != (_request?.RequestId ?? 0))
            {
                _request = mine;
                ResetSelection();
                Submitted = false;
                changed = true;
            }
            var mode = ComputeMode();
            if (mode != Mode)
            {
                Mode = mode;
                changed = true;
            }
            if (changed) Version++;
            return changed;
        }

        private InteractionMode ComputeMode()
        {
            if (_request == null) return OtherRequest != null ? InteractionMode.Waiting : InteractionMode.Idle;
            switch (_request.Kind)
            {
                case RequestKind.PlayAction: return _activeSkill != null ? InteractionMode.SkillSelect : InteractionMode.PlayPhase;
                case RequestKind.CardResponse: return InteractionMode.Response;
                case RequestKind.Discard: return InteractionMode.Discard;
                case RequestKind.ChooseCardFromPlayer: return InteractionMode.ChooseCardFromPlayer;
                case RequestKind.ChooseTargets: return InteractionMode.ChooseTargets;
                case RequestKind.Confirm: return InteractionMode.Confirm;
                case RequestKind.ChooseCharacter: return InteractionMode.ChooseCharacter;
                default: return InteractionMode.ChooseOption;
            }
        }

        private void ResetSelection()
        {
            _selectedCards.Clear();
            _selectedTargets.Clear();
            _activeHint = null;
            _activeSkill = null;
        }

        private void Touch() => Version++;

        // ================================================================== queries

        public bool IsCardSelected(int cardId) => _selectedCards.Contains(cardId);
        public bool IsTargetSelected(int playerId) => _selectedTargets.Contains(playerId);

        public bool IsCardSelectable(int cardId)
        {
            if (_request == null || Submitted) return false;
            switch (Mode)
            {
                case InteractionMode.PlayPhase:
                case InteractionMode.Response:
                    return FindHint(cardId) != null;
                case InteractionMode.SkillSelect:
                    return _activeSkill.MaxCards > 0 && OwnsCard(cardId);
                case InteractionMode.Discard:
                    return OwnsCard(cardId);
                default:
                    return false;
            }
        }

        public bool IsTargetSelectable(int playerId)
        {
            if (_request == null || Submitted) return false;
            switch (Mode)
            {
                case InteractionMode.PlayPhase:
                    return _activeHint != null && _activeHint.NeedsTargets && _activeHint.LegalTargets.Contains(playerId);
                case InteractionMode.SkillSelect:
                    return _activeSkill.MaxTargets > 0 && _activeSkill.LegalTargets.Contains(playerId);
                case InteractionMode.ChooseTargets:
                    return _request.Candidates != null && _request.Candidates.Contains(playerId);
                default:
                    return false;
            }
        }

        /// <summary>Active skills usable now (skill buttons light up).</summary>
        public bool IsSkillUsable(string skillId)
        {
            if (_request == null || Submitted) return false;
            if ((Mode == InteractionMode.PlayPhase || Mode == InteractionMode.SkillSelect) && FindSkillHint(skillId) != null) return true;
            return HasConversionHints(skillId);
        }

        public bool CanConfirm
        {
            get
            {
                if (_request == null || Submitted) return false;
                switch (Mode)
                {
                    case InteractionMode.PlayPhase:
                        return _activeHint != null && (!_activeHint.NeedsTargets
                            || (_selectedTargets.Count >= Math.Max(1, _activeHint.MinTargets) && _selectedTargets.Count <= _activeHint.MaxTargets));
                    case InteractionMode.SkillSelect:
                        return _selectedCards.Count >= _activeSkill.MinCards && _selectedCards.Count <= _activeSkill.MaxCards
                               && _selectedTargets.Count >= _activeSkill.MinTargets && _selectedTargets.Count <= _activeSkill.MaxTargets;
                    case InteractionMode.Response:
                        return _selectedCards.Count == Math.Max(1, _request.Count);
                    case InteractionMode.Discard:
                        return _selectedCards.Count >= _request.MinCount && _selectedCards.Count <= _request.Count;
                    case InteractionMode.ChooseTargets:
                        return _selectedTargets.Count >= _request.MinCount && _selectedTargets.Count <= Math.Max(_request.Count, _request.MinCount);
                    case InteractionMode.Confirm:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>"Don't respond / don't use" is available.</summary>
        public bool CanPass
        {
            get
            {
                if (_request == null || Submitted) return false;
                switch (Mode)
                {
                    case InteractionMode.Response:
                    case InteractionMode.Confirm:
                        return true;
                    case InteractionMode.ChooseTargets:
                        return _request.MinCount == 0;
                    default:
                        return false;
                }
            }
        }

        public bool CanCancelSelection => !Submitted && (_selectedCards.Count > 0 || _selectedTargets.Count > 0 || _activeSkill != null);
        public bool CanEndTurn => !Submitted && _request != null && _request.Kind == RequestKind.PlayAction;

        public string ConfirmLabel
        {
            get
            {
                switch (Mode)
                {
                    case InteractionMode.PlayPhase: return "出牌";
                    case InteractionMode.SkillSelect: return "发动";
                    case InteractionMode.Response: return "打出";
                    case InteractionMode.Discard: return "弃牌";
                    case InteractionMode.Confirm: return "发动";
                    default: return "确定";
                }
            }
        }

        public string PassLabel => Mode == InteractionMode.Confirm ? "不发动" : Mode == InteractionMode.ChooseTargets ? "跳过" : "不出";

        public string Prompt
        {
            get
            {
                if (_request == null)
                {
                    if (OtherRequest == null) return string.Empty;
                    return "等待 " + _names.PlayerName(OtherRequest.PlayerId) + " " + DescribeKind(OtherRequest);
                }
                switch (Mode)
                {
                    case InteractionMode.PlayPhase:
                        if (_activeHint == null) return "出牌阶段：请选择一张牌，或结束出牌";
                        string card = "【" + _names.CardName(_activeHint.AsCardId) + "】";
                        if (!_activeHint.NeedsTargets) return "点击「出牌」使用" + card;
                        if (_activeHint.LegalTargets.Count == 0) return card + "没有合法目标";
                        return "请选择" + card + "的目标（" + _selectedTargets.Count + "/" + _activeHint.MaxTargets + "）";
                    case InteractionMode.SkillSelect:
                        return "发动【" + _names.SkillName(_activeSkill.SkillId) + "】：选择 " + _activeSkill.MinCards + " 张牌"
                               + (_activeSkill.MaxTargets > 0 ? "和 " + _activeSkill.MinTargets + " 名目标" : "");
                    case InteractionMode.Response:
                        if (_request.Purpose == "rescue")
                            return _names.PlayerName(_request.TargetPlayerId) + " 濒死，是否使用【" + _names.CardName(_request.RequiredCardId) + "】？";
                        string source = _request.SourcePlayerId >= 0 ? _names.PlayerName(_request.SourcePlayerId) : "";
                        string ctxCard = _request.ContextCardId != null ? "【" + _names.CardName(_request.ContextCardId) + "】" : "";
                        return (source.Length > 0 ? source + " 使用了" + ctxCard + "，" : "") + "是否打出【" + _names.CardName(_request.RequiredCardId) + "】？";
                    case InteractionMode.Discard:
                        return "请弃置 " + _request.Count + " 张牌（已选 " + _selectedCards.Count + "）";
                    case InteractionMode.ChooseTargets:
                        return "请选择 " + (_request.MinCount == _request.Count ? _request.Count.ToString() : _request.MinCount + "-" + _request.Count) + " 名角色";
                    case InteractionMode.Confirm:
                        return "是否发动【" + _names.SkillName(_request.SkillId) + "】？";
                    case InteractionMode.ChooseCardFromPlayer:
                        return "请选择 " + _names.PlayerName(_request.TargetPlayerId) + " 的一张牌";
                    case InteractionMode.ChooseCharacter:
                        return "请选择你的武将";
                    default:
                        return "请选择";
                }
            }
        }

        private string DescribeKind(RequestInfo r)
        {
            switch (r.Kind)
            {
                case RequestKind.PlayAction: return "出牌";
                case RequestKind.CardResponse: return r.RequiredCardId != null ? "打出【" + _names.CardName(r.RequiredCardId) + "】" : "响应";
                case RequestKind.Discard: return "弃牌";
                case RequestKind.ChooseCharacter: return "选择武将";
                case RequestKind.Confirm: return "考虑是否发动技能";
                default: return "做出选择";
            }
        }

        // ================================================================== input

        public void ClickCard(int cardId)
        {
            if (!IsCardSelectable(cardId)) return;
            switch (Mode)
            {
                case InteractionMode.PlayPhase:
                    if (_selectedCards.Contains(cardId))
                    {
                        ResetSelection();
                        break;
                    }
                    _activeHint = FindHint(cardId);
                    _selectedCards.Clear();
                    _selectedCards.Add(cardId);
                    _selectedTargets.Clear();
                    // A single legal target is chosen for the player.
                    if (_activeHint.NeedsTargets && _activeHint.LegalTargets.Count == 1 && _activeHint.MaxTargets == 1)
                        _selectedTargets.Add(_activeHint.LegalTargets[0]);
                    break;
                case InteractionMode.Response:
                    if (_selectedCards.Contains(cardId)) _selectedCards.Remove(cardId);
                    else
                    {
                        if (_selectedCards.Count >= Math.Max(1, _request.Count)) _selectedCards.RemoveAt(0);
                        _selectedCards.Add(cardId);
                    }
                    break;
                case InteractionMode.SkillSelect:
                    Toggle(_selectedCards, cardId, _activeSkill.MaxCards);
                    break;
                case InteractionMode.Discard:
                    Toggle(_selectedCards, cardId, _request.Count);
                    break;
            }
            Touch();
        }

        public void ClickPlayer(int playerId)
        {
            if (!IsTargetSelectable(playerId)) return;
            int max;
            switch (Mode)
            {
                case InteractionMode.PlayPhase: max = _activeHint.MaxTargets; break;
                case InteractionMode.SkillSelect: max = _activeSkill.MaxTargets; break;
                default: max = Math.Max(_request.Count, _request.MinCount); break;
            }
            Toggle(_selectedTargets, playerId, max);
            Touch();
        }

        /// <summary>Skill button: enter/leave active skill mode, or toggle a conversion skill.</summary>
        public void ClickSkill(string skillId)
        {
            if (!IsSkillUsable(skillId)) return;
            if ((Mode == InteractionMode.PlayPhase || Mode == InteractionMode.SkillSelect) && FindSkillHint(skillId) != null)
            {
                bool leaving = _activeSkill != null && _activeSkill.SkillId == skillId;
                ResetSelection();
                if (!leaving) _activeSkill = FindSkillHint(skillId);
                Mode = ComputeMode();
            }
            else
            {
                _conversionSkill = _conversionSkill == skillId ? null : skillId;
                _selectedCards.Clear();
                _selectedTargets.Clear();
                _activeHint = null;
            }
            Touch();
        }

        public void CancelSelection()
        {
            ResetSelection();
            _conversionSkill = null;
            Mode = ComputeMode();
            Touch();
        }

        private static void Toggle(List<int> list, int id, int max)
        {
            if (list.Remove(id)) return;
            if (max <= 0) return;
            if (list.Count >= max)
            {
                if (max == 1) list.Clear();
                else return;
            }
            list.Add(id);
        }

        // ================================================================== commands

        /// <summary>Builds the command for the "confirm" button, or null if not possible.</summary>
        public GameCommand Confirm()
        {
            if (!CanConfirm) return null;
            GameCommand cmd;
            switch (Mode)
            {
                case InteractionMode.PlayPhase:
                    cmd = new PlayCardCommand
                    {
                        CardInstanceId = _activeHint.CardInstanceId,
                        TargetIds = _activeHint.NeedsTargets ? _selectedTargets.ToArray() : Array.Empty<int>(),
                        SkillId = _activeHint.SkillId,
                        AsCardId = _activeHint.SkillId != null ? _activeHint.AsCardId : null
                    };
                    break;
                case InteractionMode.SkillSelect:
                    cmd = new UseSkillCommand { SkillId = _activeSkill.SkillId, CardIds = _selectedCards.ToArray(), TargetIds = _selectedTargets.ToArray() };
                    break;
                case InteractionMode.Response:
                {
                    var hint = FindHint(_selectedCards[0]);
                    cmd = new RespondCommand { CardIds = _selectedCards.ToArray(), SkillId = hint?.SkillId };
                    break;
                }
                case InteractionMode.Discard:
                    cmd = new RespondCommand { CardIds = _selectedCards.ToArray() };
                    break;
                case InteractionMode.ChooseTargets:
                    cmd = new SelectTargetCommand { TargetIds = _selectedTargets.ToArray() };
                    break;
                case InteractionMode.Confirm:
                    cmd = new RespondCommand { OptionIndex = 1 };
                    break;
                default:
                    return null;
            }
            return Finish(cmd);
        }

        public GameCommand Pass()
        {
            if (!CanPass) return null;
            if (Mode == InteractionMode.ChooseTargets) return Finish(new SelectTargetCommand { TargetIds = Array.Empty<int>() });
            if (Mode == InteractionMode.Confirm) return Finish(new RespondCommand { OptionIndex = 0 });
            return Finish(new RespondCommand { Pass = true });
        }

        public GameCommand EndTurn() => CanEndTurn ? Finish(new EndTurnCommand()) : null;

        public GameCommand ChooseOption(int index)
        {
            if (_request == null || Submitted) return null;
            if (Mode != InteractionMode.ChooseOption && Mode != InteractionMode.ChooseCharacter) return null;
            if (_request.Options == null || index < 0 || index >= _request.Options.Count) return null;
            return Finish(new RespondCommand { OptionIndex = index });
        }

        public GameCommand PickFromPlayer(ZoneType zone, int index)
        {
            if (_request == null || Submitted || Mode != InteractionMode.ChooseCardFromPlayer) return null;
            return Finish(new RespondCommand { PickZone = zone, PickIndex = index });
        }

        private GameCommand Finish(GameCommand cmd)
        {
            cmd.RequestId = _request.RequestId;
            cmd.PlayerId = _viewerId;
            Submitted = true;
            Touch();
            return cmd;
        }

        /// <summary>The server rejected the last command: allow the player to try again.</summary>
        public void OnCommandRejected()
        {
            Submitted = false;
            Touch();
        }

        // ================================================================== hint lookup

        private PlayHint FindHint(int cardId)
        {
            if (_request?.PlayHints == null) return null;
            PlayHint own = null, converted = null;
            foreach (var h in _request.PlayHints)
            {
                if (h.CardInstanceId != cardId) continue;
                if (h.SkillId == null) own = own ?? h;
                else if (_conversionSkill == null || h.SkillId == _conversionSkill) converted = converted ?? h;
            }
            if (_conversionSkill != null) return converted;
            return own ?? converted;
        }

        private SkillHint FindSkillHint(string skillId)
        {
            if (_request?.SkillHints == null) return null;
            foreach (var h in _request.SkillHints)
                if (h.SkillId == skillId) return h;
            return null;
        }

        private bool HasConversionHints(string skillId)
        {
            if (_request?.PlayHints == null) return false;
            foreach (var h in _request.PlayHints)
                if (h.SkillId == skillId) return true;
            return false;
        }

        private bool OwnsCard(int cardId)
        {
            var hand = _state?.Self?.HandCards;
            if (hand == null) return false;
            foreach (var c in hand)
                if (c.InstanceId == cardId) return true;
            return false;
        }
    }
}
