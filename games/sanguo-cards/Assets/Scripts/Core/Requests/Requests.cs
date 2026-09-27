using System;
using System.Collections.Generic;
using Sanguo.Cards;

namespace Sanguo.Core
{
    internal static class RequestValidation
    {
        public static bool AllDistinct(int[] ids)
        {
            if (ids == null) return true;
            for (int i = 0; i < ids.Length; i++)
                for (int j = i + 1; j < ids.Length; j++)
                    if (ids[i] == ids[j]) return false;
            return true;
        }

        public static ValidationResult WrongType(GameCommand cmd, string expected)
        {
            return ValidationResult.Fail(RejectReason.WrongCommandType, "Expected " + expected + " but got " + cmd.Type + ".");
        }
    }

    /// <summary>Main play-phase decision of the current player.</summary>
    public sealed class PlayActionRequest : PendingRequest
    {
        public PlayActionRequest(int playerId) : base(playerId)
        {
            Purpose = "play";
        }

        public override RequestKind Kind => RequestKind.PlayAction;
        public override bool IsResponseWindow => false;

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            switch (command)
            {
                case PlayCardCommand play:
                    return ctx.Rules.ValidateCardUse(PlayerId, play);
                case UseSkillCommand skill:
                    return ctx.Rules.ValidateSkillActivation(PlayerId, skill);
                case EndTurnCommand _:
                    return ValidationResult.Ok;
                default:
                    return RequestValidation.WrongType(command, "PlayCard, UseSkill or EndTurn");
            }
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            return new EndTurnCommand { PlayerId = PlayerId, RequestId = RequestId };
        }

        /// <summary>
        /// Private hints for the acting player's UI: which cards/skills are usable right now and their
        /// legal targets, computed by the server's rule engine (the client never needs rule code).
        /// </summary>
        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.HasPrivateDetails = true;
            info.PlayHints = RequestHints.PlayableCards(ctx, PlayerId);
            info.SkillHints = RequestHints.UsableSkills(ctx, PlayerId);
        }
    }

    /// <summary>
    /// Respond with <see cref="Count"/> card(s) that can serve as <see cref="RequiredCardId"/>
    /// (dodge a strike, strike back in a duel, heal a dying player...) or pass.
    /// </summary>
    public sealed class CardResponseRequest : PendingRequest
    {
        public CardResponseRequest(int playerId, string requiredCardId, int sourcePlayerId, string contextCardId, string purpose, int count = 1, bool allowPass = true)
            : base(playerId)
        {
            RequiredCardId = requiredCardId;
            SourcePlayerId = sourcePlayerId;
            ContextCardId = contextCardId;
            Purpose = purpose;
            Count = Math.Max(1, count);
            AllowPass = allowPass;
        }

        public override RequestKind Kind => RequestKind.CardResponse;
        public string RequiredCardId { get; }
        public int SourcePlayerId { get; }
        public string ContextCardId { get; }
        public int Count { get; }
        public bool AllowPass { get; }

        /// <summary>Player the response is for (the dying player for rescues), -1 if not applicable.</summary>
        public int SubjectPlayerId { get; set; } = -1;

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is RespondCommand r)) return RequestValidation.WrongType(command, "Respond");
            if (r.Pass)
                return AllowPass ? ValidationResult.Ok : ValidationResult.Fail(RejectReason.PassNotAllowed);
            if (r.CardIds == null || r.CardIds.Length != Count)
                return ValidationResult.Fail(RejectReason.WrongCardCount, "Exactly " + Count + " card(s) required.");
            if (!RequestValidation.AllDistinct(r.CardIds)) return ValidationResult.Fail(RejectReason.MalformedCommand, "Duplicate cards.");
            var player = ctx.GetPlayer(PlayerId);
            foreach (int id in r.CardIds)
            {
                var card = player.HandCards.FindById(id);
                if (card == null) return ValidationResult.Fail(RejectReason.CardNotOwned, "Card " + id + " is not in hand.");
                if (!ctx.Rules.CanCardServeAs(player, card, RequiredCardId, r.SkillId))
                    return ValidationResult.Fail(RejectReason.InvalidCard, card.CardName + " cannot be used as " + RequiredCardId + ".");
            }
            return ValidationResult.Ok;
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            if (AllowPass) return new RespondCommand { PlayerId = PlayerId, RequestId = RequestId, Pass = true };
            // Forced response: use the first cards that qualify.
            var player = ctx.GetPlayer(PlayerId);
            var ids = new List<int>();
            foreach (var c in player.HandCards)
            {
                if (ids.Count >= Count) break;
                if (ctx.Rules.CanCardServeAs(player, c, RequiredCardId, null)) ids.Add(c.InstanceId);
            }
            return new RespondCommand { PlayerId = PlayerId, RequestId = RequestId, CardIds = ids.ToArray(), Pass = ids.Count < Count };
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.HasPrivateDetails = true;
            info.PlayHints = RequestHints.ResponseCards(ctx, PlayerId, RequiredCardId);
            info.RequiredCardId = RequiredCardId;
            info.SourcePlayerId = SourcePlayerId;
            info.TargetPlayerId = SubjectPlayerId;
            info.ContextCardId = ContextCardId;
            info.Count = Count;
            info.MinCount = Count;
            info.AllowPass = AllowPass;
        }
    }

    /// <summary>Discard between MinCount and Count of own cards.</summary>
    public sealed class DiscardRequest : PendingRequest
    {
        public DiscardRequest(int playerId, int count, string purpose, int minCount = -1, bool includeEquipment = false) : base(playerId)
        {
            Count = Math.Max(0, count);
            MinCount = minCount < 0 ? Count : Math.Min(minCount, Count);
            IncludeEquipment = includeEquipment;
            Purpose = purpose;
            _isResponseWindow = purpose != "discard_phase";
        }

        private readonly bool _isResponseWindow;
        public override RequestKind Kind => RequestKind.Discard;
        public override bool IsResponseWindow => _isResponseWindow;
        public int Count { get; }
        public int MinCount { get; }
        public bool IncludeEquipment { get; }

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is RespondCommand r)) return RequestValidation.WrongType(command, "Respond");
            int n = r.Pass || r.CardIds == null ? 0 : r.CardIds.Length;
            if (n < MinCount || n > Count)
                return ValidationResult.Fail(RejectReason.WrongCardCount, "Discard between " + MinCount + " and " + Count + " cards.");
            if (n == 0) return ValidationResult.Ok;
            if (!RequestValidation.AllDistinct(r.CardIds)) return ValidationResult.Fail(RejectReason.MalformedCommand, "Duplicate cards.");
            var player = ctx.GetPlayer(PlayerId);
            foreach (int id in r.CardIds)
            {
                if (player.FindOwnedCard(id, IncludeEquipment) == null)
                    return ValidationResult.Fail(RejectReason.CardNotOwned, "Card " + id + " cannot be discarded.");
            }
            return ValidationResult.Ok;
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            var player = ctx.GetPlayer(PlayerId);
            var ids = new List<int>();
            for (int i = player.HandCards.Count - 1; i >= 0 && ids.Count < Count; i--) ids.Add(player.HandCards[i].InstanceId);
            if (IncludeEquipment)
            {
                foreach (var c in player.Equipment.Cards)
                {
                    if (ids.Count >= Count) break;
                    ids.Add(c.InstanceId);
                }
            }
            return new RespondCommand { PlayerId = PlayerId, RequestId = RequestId, CardIds = ids.ToArray() };
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.Count = Count;
            info.MinCount = MinCount;
            info.AllowPass = MinCount == 0;
        }
    }

    [Flags]
    public enum ZoneMask
    {
        None = 0,
        Hand = 1,
        Equipment = 2,
        Judge = 4,
        All = Hand | Equipment | Judge
    }

    /// <summary>Pick one card from another player's hand (blind, by position), equipment or judge area.</summary>
    public sealed class ChooseCardFromPlayerRequest : PendingRequest
    {
        public ChooseCardFromPlayerRequest(int chooserId, int targetId, ZoneMask zones, string purpose, string contextCardId) : base(chooserId)
        {
            TargetId = targetId;
            Zones = zones;
            Purpose = purpose;
            ContextCardId = contextCardId;
        }

        public override RequestKind Kind => RequestKind.ChooseCardFromPlayer;
        public int TargetId { get; }
        public ZoneMask Zones { get; }
        public string ContextCardId { get; }

        // Hidden hand cards are picked by position in a server-side shuffled order, so the chooser
        // cannot aim at a card whose position it might have learned earlier.
        private List<CardInstance> _handOrder;

        protected internal override void OnOpened(GameContext ctx)
        {
            var target = ctx.GetPlayer(TargetId);
            if (target == null) return;
            _handOrder = new List<CardInstance>(target.HandCards);
            Utils.RandomExtensions.Shuffle(ctx.Random, _handOrder);
        }

        /// <summary>True if the target has at least one card in the allowed zones.</summary>
        public static bool HasPickableCard(PlayerState target, ZoneMask zones)
        {
            return ((zones & ZoneMask.Hand) != 0 && target.HandCards.Count > 0)
                   || ((zones & ZoneMask.Equipment) != 0 && target.Equipment.Count > 0)
                   || ((zones & ZoneMask.Judge) != 0 && target.JudgeArea.Count > 0);
        }

        /// <summary>Maps a validated answer to the chosen card.</summary>
        public CardInstance ResolvePick(GameContext ctx, RespondCommand r)
        {
            var target = ctx.GetPlayer(TargetId);
            if (target == null || r == null) return null;
            switch (r.PickZone)
            {
                case ZoneType.Hand:
                {
                    if ((Zones & ZoneMask.Hand) == 0 || r.PickIndex < 0 || r.PickIndex >= target.HandCards.Count) return null;
                    if (_handOrder == null || _handOrder.Count != target.HandCards.Count) return target.HandCards[r.PickIndex];
                    var card = _handOrder[r.PickIndex];
                    return target.HandCards.Contains(card) ? card : null;
                }
                case ZoneType.Equipment:
                    if ((Zones & ZoneMask.Equipment) == 0 || r.PickIndex <= 0 || r.PickIndex >= EquipmentArea.SlotCount) return null;
                    return target.Equipment.Get((EquipSlot)r.PickIndex);
                case ZoneType.JudgeArea:
                    return (Zones & ZoneMask.Judge) != 0 && r.PickIndex >= 0 && r.PickIndex < target.JudgeArea.Count ? target.JudgeArea[r.PickIndex] : null;
                default:
                    return null;
            }
        }

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is RespondCommand r)) return RequestValidation.WrongType(command, "Respond");
            if (r.Pass) return ValidationResult.Fail(RejectReason.PassNotAllowed);
            return ResolvePick(ctx, r) != null ? ValidationResult.Ok : ValidationResult.Fail(RejectReason.InvalidOption, "No card at that position.");
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            var target = ctx.GetPlayer(TargetId);
            var r = new RespondCommand { PlayerId = PlayerId, RequestId = RequestId };
            if ((Zones & ZoneMask.Hand) != 0 && target.HandCards.Count > 0)
            {
                r.PickZone = ZoneType.Hand;
                r.PickIndex = ctx.Random.Next(target.HandCards.Count);
            }
            else if ((Zones & ZoneMask.Equipment) != 0 && target.Equipment.Count > 0)
            {
                foreach (var c in target.Equipment.Cards)
                {
                    r.PickZone = ZoneType.Equipment;
                    r.PickIndex = (int)EquipmentArea.SlotOf(c);
                    break;
                }
            }
            else if ((Zones & ZoneMask.Judge) != 0 && target.JudgeArea.Count > 0)
            {
                r.PickZone = ZoneType.JudgeArea;
                r.PickIndex = 0;
            }
            return r;
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.TargetPlayerId = TargetId;
            info.ContextCardId = ContextCardId;
            info.Zones = (int)Zones;
            info.Count = 1;
            info.MinCount = 1;
        }
    }

    /// <summary>Pick between Min and Max players from <see cref="Candidates"/>.</summary>
    public sealed class ChooseTargetsRequest : PendingRequest
    {
        public ChooseTargetsRequest(int playerId, IReadOnlyList<int> candidates, int min, int max, string purpose, string skillId) : base(playerId)
        {
            Candidates = new List<int>(candidates);
            Min = Math.Max(0, min);
            Max = Math.Max(Min, max);
            Purpose = purpose;
            SkillId = skillId;
        }

        public override RequestKind Kind => RequestKind.ChooseTargets;
        public List<int> Candidates { get; }
        public int Min { get; }
        public int Max { get; }
        public string SkillId { get; }

        public int[] Selected => (Response as SelectTargetCommand)?.TargetIds ?? Array.Empty<int>();

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is SelectTargetCommand s)) return RequestValidation.WrongType(command, "SelectTarget");
            int n = s.TargetIds?.Length ?? 0;
            if (n < Min || n > Max) return ValidationResult.Fail(RejectReason.WrongTargetCount);
            if (!RequestValidation.AllDistinct(s.TargetIds)) return ValidationResult.Fail(RejectReason.MalformedCommand, "Duplicate targets.");
            for (int i = 0; i < n; i++)
                if (!Candidates.Contains(s.TargetIds[i])) return ValidationResult.Fail(RejectReason.InvalidTarget);
            return ValidationResult.Ok;
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            var ids = new int[Math.Min(Min, Candidates.Count)];
            for (int i = 0; i < ids.Length; i++) ids[i] = Candidates[i];
            return new SelectTargetCommand { PlayerId = PlayerId, RequestId = RequestId, TargetIds = ids };
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.SkillId = SkillId;
            info.Count = Max;
            info.MinCount = Min;
            info.AllowPass = Min == 0;
            info.HasPrivateDetails = true;
            info.Candidates = new List<int>(Candidates);
        }
    }

    /// <summary>Yes/No question, typically whether to activate an optional skill.</summary>
    public sealed class ConfirmRequest : PendingRequest
    {
        public ConfirmRequest(int playerId, string skillId, string purpose) : base(playerId)
        {
            SkillId = skillId;
            Purpose = purpose;
        }

        public override RequestKind Kind => RequestKind.Confirm;
        public string SkillId { get; }

        public bool Accepted => Response is RespondCommand r && !r.Pass && r.OptionIndex == 1;

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is RespondCommand r)) return RequestValidation.WrongType(command, "Respond");
            if (r.Pass || r.OptionIndex == 0 || r.OptionIndex == 1) return ValidationResult.Ok;
            return ValidationResult.Fail(RejectReason.InvalidOption);
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            return new RespondCommand { PlayerId = PlayerId, RequestId = RequestId, Pass = true };
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.SkillId = SkillId;
            info.AllowPass = true;
        }
    }

    /// <summary>Pick one of several options.</summary>
    public sealed class ChooseOptionRequest : PendingRequest
    {
        public ChooseOptionRequest(int playerId, IReadOnlyList<string> options, string purpose, bool isResponseWindow = true) : base(playerId)
        {
            Options = new List<string>(options);
            Purpose = purpose;
            _isResponseWindow = isResponseWindow;
        }

        private readonly bool _isResponseWindow;
        public override RequestKind Kind => RequestKind.ChooseOption;
        public override bool IsResponseWindow => _isResponseWindow;
        public List<string> Options { get; }

        public int SelectedIndex => Response is RespondCommand r ? r.OptionIndex : -1;

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is RespondCommand r)) return RequestValidation.WrongType(command, "Respond");
            return r.OptionIndex >= 0 && r.OptionIndex < Options.Count ? ValidationResult.Ok : ValidationResult.Fail(RejectReason.InvalidOption);
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            return new RespondCommand { PlayerId = PlayerId, RequestId = RequestId, OptionIndex = 0 };
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.HasPrivateDetails = true;
            info.Options = new List<string>(Options);
        }
    }

    /// <summary>Character selection during preparation (options are private to the chooser).</summary>
    public sealed class ChooseCharacterRequest : PendingRequest
    {
        public ChooseCharacterRequest(int playerId, IReadOnlyList<string> characterIds) : base(playerId)
        {
            CharacterIds = new List<string>(characterIds);
            Purpose = "choose_character";
        }

        public override RequestKind Kind => RequestKind.ChooseCharacter;
        public override bool IsResponseWindow => false;
        public List<string> CharacterIds { get; }

        public string SelectedCharacterId
        {
            get
            {
                int idx = Response is RespondCommand r ? r.OptionIndex : 0;
                return idx >= 0 && idx < CharacterIds.Count ? CharacterIds[idx] : CharacterIds[0];
            }
        }

        public override ValidationResult Validate(GameContext ctx, GameCommand command)
        {
            if (!(command is RespondCommand r)) return RequestValidation.WrongType(command, "Respond");
            return r.OptionIndex >= 0 && r.OptionIndex < CharacterIds.Count ? ValidationResult.Ok : ValidationResult.Fail(RejectReason.InvalidOption);
        }

        public override GameCommand CreateDefaultResponse(GameContext ctx)
        {
            return new RespondCommand { PlayerId = PlayerId, RequestId = RequestId, OptionIndex = 0 };
        }

        protected override void FillInfo(GameContext ctx, RequestInfo info)
        {
            info.HasPrivateDetails = true;
            info.Options = new List<string>(CharacterIds);
        }
    }
}
