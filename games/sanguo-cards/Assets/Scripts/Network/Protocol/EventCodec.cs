using System;
using System.Collections.Generic;
using Sanguo.Core;
using Sanguo.Events;

namespace Sanguo.Network
{
    /// <summary>
    /// Wire format of (already projected) game events. Every concrete event type must be handled here;
    /// a test enumerates all GameEvent subclasses to make sure none is forgotten.
    /// </summary>
    public static class EventCodec
    {
        public static void Write(NetWriter w, GameEvent e)
        {
            w.WriteByte((byte)e.Type);
            w.WriteVarInt(e.Sequence);
            switch (e)
            {
                case GameStartedEvent x:
                    w.WriteString(x.ModeId);
                    w.WriteVarInt(x.PlayerCount);
                    break;
                case TurnStartedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteVarInt(x.TurnNumber);
                    w.WriteVarInt(x.Round);
                    break;
                case PhaseChangedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteByte((byte)x.Phase);
                    break;
                case PhaseSkippedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteByte((byte)x.Phase);
                    break;
                case CardPlayedEvent x:
                    WriteMove(w, x);
                    w.WriteVarInt(x.UserId);
                    w.WriteVarInt(x.UseId);
                    w.WriteString(x.UsedAsCardId);
                    w.WriteString(x.ConversionSkillId);
                    w.WriteIntArray(x.Targets);
                    w.WriteBool(x.IsResponse);
                    break;
                case CardMoveEvent x:
                    WriteMove(w, x);
                    break;
                case DeckReshuffledEvent x:
                    w.WriteVarInt(x.DrawPileCount);
                    break;
                case TargetSelectedEvent x:
                    w.WriteVarInt(x.SourceId);
                    w.WriteVarInt(x.TargetId);
                    w.WriteString(x.CardId);
                    w.WriteVarInt(x.UseId);
                    break;
                case DamageCreatedEvent x:
                    w.WriteVarInt(x.SourceId);
                    w.WriteVarInt(x.TargetId);
                    w.WriteVarInt(x.Amount);
                    w.WriteString(x.CardId);
                    break;
                case DamagePreventedEvent x:
                    w.WriteVarInt(x.SourceId);
                    w.WriteVarInt(x.TargetId);
                    w.WriteString(x.CardId);
                    break;
                case DamageAppliedEvent x:
                    w.WriteVarInt(x.SourceId);
                    w.WriteVarInt(x.TargetId);
                    w.WriteVarInt(x.Amount);
                    w.WriteVarInt(x.NewHp);
                    w.WriteString(x.CardId);
                    break;
                case HpChangedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteVarInt(x.OldHp);
                    w.WriteVarInt(x.NewHp);
                    w.WriteVarInt(x.MaxHp);
                    w.WriteByte((byte)x.Reason);
                    break;
                case HealAppliedEvent x:
                    w.WriteVarInt(x.SourceId);
                    w.WriteVarInt(x.TargetId);
                    w.WriteVarInt(x.Amount);
                    w.WriteVarInt(x.NewHp);
                    break;
                case PlayerDyingEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteBool(x.Entered);
                    break;
                case PlayerDiedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteVarInt(x.KillerId);
                    w.WriteByte((byte)x.RevealedRole);
                    break;
                case RoleRevealedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteByte((byte)x.Role);
                    break;
                case SkillActivatedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteString(x.SkillId);
                    w.WriteIntArray(x.Targets);
                    break;
                case SkillStateChangedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteString(x.SkillId);
                    w.WriteBool(x.UsedUp);
                    w.WriteBool(x.Disabled);
                    break;
                case StatusChangedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteString(x.StatusId);
                    w.WriteVarInt(x.Stacks);
                    w.WriteVarInt(x.RemainingTurns);
                    break;
                case JudgementEvent x:
                    w.WriteVarInt(x.PlayerId);
                    CommonCodec.WriteCard(w, x.Card);
                    w.WriteString(x.ForCardId);
                    w.WriteBool(x.Success);
                    break;
                case RequestOpenedEvent x:
                    CommonCodec.WriteRequest(w, x.Info);
                    break;
                case RequestClosedEvent x:
                    w.WriteVarInt(x.RequestId);
                    w.WriteVarInt(x.PlayerId);
                    w.WriteBool(x.TimedOut);
                    w.WriteBool(x.Passed);
                    break;
                case TurnEndedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    break;
                case GameEndedEvent x:
                    CommonCodec.WriteResult(w, x.Result);
                    w.WriteVarUInt((uint)x.Roles.Count);
                    foreach (var pr in x.Roles)
                    {
                        w.WriteVarInt(pr.PlayerId);
                        w.WriteByte((byte)pr.Role);
                    }
                    break;
                case PlayerStatusChangedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteBool(x.Connected);
                    w.WriteBool(x.AIControlled);
                    break;
                case CharacterAssignedEvent x:
                    w.WriteVarInt(x.PlayerId);
                    w.WriteString(x.CharacterId);
                    w.WriteVarInt(x.Hp);
                    w.WriteVarInt(x.MaxHp);
                    w.WriteStringList(x.SkillIds);
                    break;
                default:
                    throw new NotSupportedException("No wire format for " + e.GetType().Name + ".");
            }
        }

        public static GameEvent Read(NetReader r)
        {
            var type = r.ReadEnum<GameEventType>();
            int sequence = r.ReadVarInt();
            GameEvent e;
            switch (type)
            {
                case GameEventType.GameStarted:
                    e = new GameStartedEvent { ModeId = r.ReadString(), PlayerCount = r.ReadVarInt() };
                    break;
                case GameEventType.TurnStarted:
                    e = new TurnStartedEvent { PlayerId = r.ReadVarInt(), TurnNumber = r.ReadVarInt(), Round = r.ReadVarInt() };
                    break;
                case GameEventType.PhaseChanged:
                    e = new PhaseChangedEvent { PlayerId = r.ReadVarInt(), Phase = r.ReadEnum<GamePhase>() };
                    break;
                case GameEventType.PhaseSkipped:
                    e = new PhaseSkippedEvent { PlayerId = r.ReadVarInt(), Phase = r.ReadEnum<GamePhase>() };
                    break;
                case GameEventType.CardDrawn:
                    e = ReadMove(r, new CardDrawnEvent());
                    break;
                case GameEventType.CardDiscarded:
                    e = ReadMove(r, new CardDiscardedEvent());
                    break;
                case GameEventType.CardMoved:
                    e = ReadMove(r, new CardMovedEvent());
                    break;
                case GameEventType.CardPlayed:
                {
                    var x = ReadMove(r, new CardPlayedEvent());
                    x.UserId = r.ReadVarInt();
                    x.UseId = r.ReadVarInt();
                    x.UsedAsCardId = r.ReadString();
                    x.ConversionSkillId = r.ReadString();
                    x.Targets = r.ReadIntArray();
                    x.IsResponse = r.ReadBool();
                    e = x;
                    break;
                }
                case GameEventType.DeckReshuffled:
                    e = new DeckReshuffledEvent { DrawPileCount = r.ReadVarInt() };
                    break;
                case GameEventType.TargetSelected:
                    e = new TargetSelectedEvent { SourceId = r.ReadVarInt(), TargetId = r.ReadVarInt(), CardId = r.ReadString(), UseId = r.ReadVarInt() };
                    break;
                case GameEventType.DamageCreated:
                    e = new DamageCreatedEvent { SourceId = r.ReadVarInt(), TargetId = r.ReadVarInt(), Amount = r.ReadVarInt(), CardId = r.ReadString() };
                    break;
                case GameEventType.DamagePrevented:
                    e = new DamagePreventedEvent { SourceId = r.ReadVarInt(), TargetId = r.ReadVarInt(), CardId = r.ReadString() };
                    break;
                case GameEventType.DamageApplied:
                    e = new DamageAppliedEvent { SourceId = r.ReadVarInt(), TargetId = r.ReadVarInt(), Amount = r.ReadVarInt(), NewHp = r.ReadVarInt(), CardId = r.ReadString() };
                    break;
                case GameEventType.HpChanged:
                    e = new HpChangedEvent { PlayerId = r.ReadVarInt(), OldHp = r.ReadVarInt(), NewHp = r.ReadVarInt(), MaxHp = r.ReadVarInt(), Reason = r.ReadEnum<HpChangeReason>() };
                    break;
                case GameEventType.HealApplied:
                    e = new HealAppliedEvent { SourceId = r.ReadVarInt(), TargetId = r.ReadVarInt(), Amount = r.ReadVarInt(), NewHp = r.ReadVarInt() };
                    break;
                case GameEventType.PlayerDying:
                    e = new PlayerDyingEvent { PlayerId = r.ReadVarInt(), Entered = r.ReadBool() };
                    break;
                case GameEventType.PlayerDied:
                    e = new PlayerDiedEvent { PlayerId = r.ReadVarInt(), KillerId = r.ReadVarInt(), RevealedRole = r.ReadEnum<Role>() };
                    break;
                case GameEventType.RoleRevealed:
                    e = new RoleRevealedEvent { PlayerId = r.ReadVarInt(), Role = r.ReadEnum<Role>() };
                    break;
                case GameEventType.SkillActivated:
                    e = new SkillActivatedEvent { PlayerId = r.ReadVarInt(), SkillId = r.ReadString(), Targets = r.ReadIntArray() };
                    break;
                case GameEventType.SkillStateChanged:
                    e = new SkillStateChangedEvent { PlayerId = r.ReadVarInt(), SkillId = r.ReadString(), UsedUp = r.ReadBool(), Disabled = r.ReadBool() };
                    break;
                case GameEventType.StatusChanged:
                    e = new StatusChangedEvent { PlayerId = r.ReadVarInt(), StatusId = r.ReadString(), Stacks = r.ReadVarInt(), RemainingTurns = r.ReadVarInt() };
                    break;
                case GameEventType.Judgement:
                    e = new JudgementEvent { PlayerId = r.ReadVarInt(), Card = CommonCodec.ReadCard(r), ForCardId = r.ReadString(), Success = r.ReadBool() };
                    break;
                case GameEventType.RequestOpened:
                    e = new RequestOpenedEvent { Info = CommonCodec.ReadRequest(r) };
                    break;
                case GameEventType.RequestClosed:
                    e = new RequestClosedEvent { RequestId = r.ReadVarInt(), PlayerId = r.ReadVarInt(), TimedOut = r.ReadBool(), Passed = r.ReadBool() };
                    break;
                case GameEventType.TurnEnded:
                    e = new TurnEndedEvent { PlayerId = r.ReadVarInt() };
                    break;
                case GameEventType.GameEnded:
                {
                    var x = new GameEndedEvent { Result = CommonCodec.ReadResult(r) };
                    uint n = r.ReadVarUInt();
                    if (n > NetReader.MaxCollectionLength) throw new ProtocolException("Too many roles.");
                    for (int i = 0; i < n; i++) x.Roles.Add(new PlayerRole(r.ReadVarInt(), r.ReadEnum<Role>()));
                    e = x;
                    break;
                }
                case GameEventType.PlayerStatusChanged:
                    e = new PlayerStatusChangedEvent { PlayerId = r.ReadVarInt(), Connected = r.ReadBool(), AIControlled = r.ReadBool() };
                    break;
                case GameEventType.CharacterAssigned:
                    e = new CharacterAssignedEvent { PlayerId = r.ReadVarInt(), CharacterId = r.ReadString(), Hp = r.ReadVarInt(), MaxHp = r.ReadVarInt(), SkillIds = r.ReadStringList() ?? new List<string>() };
                    break;
                default:
                    throw new ProtocolException("Unsupported event type " + type + ".");
            }
            e.Sequence = sequence;
            return e;
        }

        private static void WriteMove(NetWriter w, CardMoveEvent x)
        {
            w.WriteByte((byte)x.FromZone);
            w.WriteVarInt(x.FromOwner);
            w.WriteByte((byte)x.ToZone);
            w.WriteVarInt(x.ToOwner);
            w.WriteByte((byte)x.Slot);
            w.WriteByte((byte)x.Reason);
            w.WriteVarInt(x.Count);
            CommonCodec.WriteCards(w, x.Cards);
            w.WriteBool(x.IsPublic);
            w.WriteIntArray(x.KnownTo);
        }

        private static T ReadMove<T>(NetReader r, T x) where T : CardMoveEvent
        {
            x.FromZone = r.ReadEnum<ZoneType>();
            x.FromOwner = r.ReadVarInt();
            x.ToZone = r.ReadEnum<ZoneType>();
            x.ToOwner = r.ReadVarInt();
            x.Slot = r.ReadEnum<EquipSlot>();
            x.Reason = r.ReadEnum<MoveReason>();
            x.Count = r.ReadVarInt();
            x.Cards = CommonCodec.ReadCards(r);
            x.IsPublic = r.ReadBool();
            x.KnownTo = r.ReadIntArray();
            return x;
        }

        /// <summary>Batch payload of a GameEvents message.</summary>
        public static byte[] EncodeBatch(IReadOnlyList<GameEvent> events)
        {
            var w = new NetWriter(64 * events.Count + 8);
            w.WriteVarUInt((uint)events.Count);
            foreach (var e in events) Write(w, e);
            return w.ToArray();
        }

        public static List<GameEvent> DecodeBatch(byte[] payload)
        {
            var r = new NetReader(payload);
            uint n = r.ReadVarUInt();
            if (n > NetReader.MaxCollectionLength) throw new ProtocolException("Too many events.");
            var list = new List<GameEvent>((int)n);
            for (int i = 0; i < n; i++) list.Add(Read(r));
            if (!r.AtEnd) throw new ProtocolException("Trailing bytes in event batch.");
            return list;
        }
    }
}
