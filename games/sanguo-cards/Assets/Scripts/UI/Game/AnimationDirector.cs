using System;
using Sanguo.Core;
using Sanguo.Data;
using Sanguo.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>
    /// Presentation-only effects driven by the (projected) event stream: cards flying between
    /// seats, the table and the hand, floating damage/heal/skill text, hit bursts and banners.
    /// It never changes game state and nothing waits for it; effects are pooled and capped so a
    /// 20-player deal does not flood the screen or the garbage collector.
    /// </summary>
    public sealed class AnimationDirector : MonoBehaviour
    {
        private const int MaxFlyingCards = 10;
        private const int MaxFloatingTexts = 16;

        private sealed class Floating : MonoBehaviour
        {
            public Text Text;
            public CanvasGroup Group;
        }

        private sealed class Burst : MonoBehaviour
        {
            public Image Image;
            public CanvasGroup Group;
        }

        private RectTransform _layer;
        private GameContent _content;
        private IGameLogNames _names;
        private Func<int, Vector3?> _seatWorld;
        private Func<Vector3> _tableWorld;
        private Func<Vector3> _pileWorld;
        private Func<Vector3> _handWorld;
        private int _viewerId;
        private UIPool<CardWidget> _cards;
        private UIPool<Floating> _texts;
        private UIPool<Burst> _bursts;
        private Text _banner;
        private CanvasGroup _bannerGroup;
        private int _flyingCards;
        private int _floatingTexts;
        private int _bannerToken;

        public static AnimationDirector Create(RectTransform layer)
        {
            var d = layer.gameObject.AddComponent<AnimationDirector>();
            d._layer = layer;
            d._cards = new UIPool<CardWidget>(() => CardWidget.Create(layer, CardWidget.DefaultSize * 0.8f, false));
            d._texts = new UIPool<Floating>(d.CreateFloating);
            d._bursts = new UIPool<Burst>(d.CreateBurst);
            d._banner = UIFactory.Text(layer, "Banner", string.Empty, UITheme.FontTitle, UITheme.Gold);
            UIFactory.Place(d._banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1400, 140));
            d._banner.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            d._bannerGroup = d._banner.gameObject.AddComponent<CanvasGroup>();
            d._bannerGroup.alpha = 0;
            d._bannerGroup.blocksRaycasts = false;
            return d;
        }

        /// <summary>Connects the director to a table (called again after a full rebuild).</summary>
        public void Bind(GameContent content, IGameLogNames names, int viewerId, Func<int, Vector3?> seatWorld, Func<Vector3> tableWorld,
            Func<Vector3> pileWorld, Func<Vector3> handWorld)
        {
            _content = content;
            _names = names;
            _viewerId = viewerId;
            _seatWorld = seatWorld;
            _tableWorld = tableWorld;
            _pileWorld = pileWorld;
            _handWorld = handWorld;
        }

        private Floating CreateFloating()
        {
            var t = UIFactory.Text(_layer, "Float", string.Empty, UITheme.FontLarge + 6, UITheme.Text);
            t.rectTransform.sizeDelta = new Vector2(420, 70);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.9f);
            var f = t.gameObject.AddComponent<Floating>();
            f.Text = t;
            f.Group = t.gameObject.AddComponent<CanvasGroup>();
            f.Group.blocksRaycasts = false;
            return f;
        }

        private Burst CreateBurst()
        {
            var img = UIFactory.Image(_layer, "Burst", Color.white, UIAssets.Circle);
            img.rectTransform.sizeDelta = new Vector2(160, 160);
            var b = img.gameObject.AddComponent<Burst>();
            b.Image = img;
            b.Group = img.gameObject.AddComponent<CanvasGroup>();
            b.Group.blocksRaycasts = false;
            return b;
        }

        private Vector2 Local(Vector3 world) => _layer.InverseTransformPoint(world);

        /// <summary>Seat position in layer space; the viewer's own cards come from and go to the hand.</summary>
        private Vector2? SeatLocal(int playerId, bool viewerHand = false)
        {
            if (viewerHand && playerId == _viewerId && _viewerId >= 0) return Local(_handWorld());
            var w = _seatWorld?.Invoke(playerId);
            return w.HasValue ? Local(w.Value) : (Vector2?)null;
        }

        public void OnEvent(GameEvent e)
        {
            if (_layer == null || _seatWorld == null || !isActiveAndEnabled) return;
            switch (e)
            {
                case CardPlayedEvent played:
                    OnCardPlayed(played);
                    break;
                case CardDrawnEvent drawn:
                {
                    var to = SeatLocal(drawn.ToOwner, true);
                    if (to.HasValue) FlyCard(null, Local(_pileWorld()), to.Value, drawn.Count > 1 ? "×" + drawn.Count : null, 0.4f, 0.05f);
                    break;
                }
                case CardMovedEvent moved when moved.Reason == MoveReason.Steal || moved.Reason == MoveReason.Give:
                {
                    var from = SeatLocal(moved.FromOwner, true);
                    var to = SeatLocal(moved.ToOwner, true);
                    var card = moved.Cards != null && moved.Cards.Count > 0 ? moved.Cards[0] : null;
                    if (from.HasValue && to.HasValue) FlyCard(card, from.Value, to.Value, null, 0.45f, 0.1f);
                    break;
                }
                case CardDiscardedEvent discarded when discarded.Cards != null && discarded.Cards.Count > 0 && discarded.FromOwner >= 0:
                {
                    var from = SeatLocal(discarded.FromOwner, true);
                    if (from.HasValue) FlyCard(discarded.Cards[0], from.Value, Local(_tableWorld()), discarded.Cards.Count > 1 ? "×" + discarded.Cards.Count : null, 0.35f, 0.25f);
                    break;
                }
                case DamageAppliedEvent damage:
                    Hit(damage.TargetId, "-" + damage.Amount, UITheme.HpLow, new Color(1f, 0.25f, 0.15f, 0.8f));
                    break;
                case HealAppliedEvent heal:
                    Hit(heal.TargetId, "+" + heal.Amount, UITheme.HpHigh, new Color(0.3f, 1f, 0.4f, 0.6f));
                    break;
                case DamagePreventedEvent prevented:
                    FloatAt(prevented.TargetId, "免伤", UITheme.Gold);
                    break;
                case SkillActivatedEvent skill:
                    FloatAt(skill.PlayerId, "【" + (_names?.SkillName(skill.SkillId) ?? skill.SkillId) + "】", UITheme.Gold);
                    break;
                case PlayerDyingEvent dying when dying.Entered:
                    FloatAt(dying.PlayerId, "濒死", UITheme.HpLow);
                    break;
                case JudgementEvent judge:
                {
                    string caption = "判定【" + (_names?.CardName(judge.ForCardId) ?? judge.ForCardId) + "】" + (judge.Success ? "✓" : "✗");
                    var from = Local(_pileWorld());
                    FlyCard(judge.Card, from, Local(_tableWorld()), caption, 0.35f, 0.9f);
                    break;
                }
                case PlayerDiedEvent died:
                {
                    string role = died.RevealedRole != Role.None && died.RevealedRole != Role.Unknown ? "（" + GameLogFormatter.RoleName(died.RevealedRole) + "）" : "";
                    Banner((_names?.PlayerName(died.PlayerId) ?? "") + " 阵亡" + role, UITheme.HpLow);
                    break;
                }
                case TurnStartedEvent turn when turn.PlayerId == _viewerId:
                    Banner("你的回合", UITheme.Gold);
                    break;
            }
        }

        private void OnCardPlayed(CardPlayedEvent played)
        {
            var from = SeatLocal(played.UserId, true) ?? Local(_tableWorld());
            string caption = _names?.PlayerName(played.UserId);
            if (played.UsedAsCardId != null && (played.Cards == null || played.Cards.Count == 0 || played.Cards[0].CardId != played.UsedAsCardId))
                caption += " 当作【" + (_names?.CardName(played.UsedAsCardId) ?? played.UsedAsCardId) + "】";
            var card = played.Cards != null && played.Cards.Count > 0 ? played.Cards[0] : null;
            var to = Local(_tableWorld()) + new Vector2(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-20f, 20f));
            FlyCard(card, from, to, caption, 0.32f, 0.55f);
            if (played.Targets == null) return;
            foreach (var target in played.Targets)
            {
                var t = SeatLocal(target);
                if (t.HasValue) SpawnBurst(t.Value, new Color(1f, 0.85f, 0.3f, 0.5f), 0.4f);
            }
        }

        private void FlyCard(CardInfo card, Vector2 from, Vector2 to, string caption, float duration, float hold)
        {
            if (_flyingCards >= MaxFlyingCards) return;
            var tween = Tweener.Instance;
            if (tween == null) return;
            _flyingCards++;
            var w = _cards.Get();
            w.Rect.SetAsLastSibling();
            w.Rect.anchorMin = w.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            if (card != null && card.CardId != null) w.SetCard(card, _content);
            else w.SetHidden();
            w.SetCaption(caption);
            w.SetHighlight(false, false);
            w.SetAlpha(1f);
            tween.Scale(w.Rect, 0.7f, 1f, duration, Ease.OutCubic);
            tween.Move(w.Rect, from, to, duration, Ease.OutCubic, () =>
                tween.Delay(hold, () =>
                    tween.Fade(w.Group, 1f, 0f, 0.25f, () =>
                    {
                        _cards.Release(w);
                        _flyingCards--;
                    })));
        }

        private void Hit(int playerId, string text, Color textColor, Color burstColor)
        {
            var pos = SeatLocal(playerId);
            if (!pos.HasValue) return;
            SpawnBurst(pos.Value, burstColor, 0.35f);
            SpawnText(pos.Value, text, textColor, UITheme.FontTitle);
        }

        private void FloatAt(int playerId, string text, Color color)
        {
            var pos = SeatLocal(playerId);
            if (pos.HasValue) SpawnText(pos.Value + new Vector2(0, 40), text, color, UITheme.FontLarge + 4);
        }

        private void SpawnText(Vector2 at, string text, Color color, int size)
        {
            if (_floatingTexts >= MaxFloatingTexts) return;
            var tween = Tweener.Instance;
            if (tween == null) return;
            _floatingTexts++;
            var f = _texts.Get();
            f.transform.SetAsLastSibling();
            var rt = (RectTransform)f.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            f.Text.text = text;
            f.Text.color = color;
            f.Text.fontSize = size;
            f.Group.alpha = 1f;
            tween.Scale(rt, 0.6f, 1f, 0.25f);
            tween.Move(rt, at, at + new Vector2(0, 90), 1.1f, Ease.OutCubic, () =>
            {
                _texts.Release(f);
                _floatingTexts--;
            });
            tween.Delay(0.6f, () =>
            {
                if (f != null && f.gameObject.activeSelf) tween.Fade(f.Group, 1f, 0f, 0.45f);
            });
        }

        private void SpawnBurst(Vector2 at, Color color, float duration)
        {
            var tween = Tweener.Instance;
            if (tween == null) return;
            var b = _bursts.Get();
            b.transform.SetAsFirstSibling();
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            b.Image.color = color;
            tween.Scale(rt, 0.3f, 1.4f, duration, Ease.OutCubic);
            tween.Fade(b.Group, 1f, 0f, duration, () => _bursts.Release(b));
        }

        public void Banner(string text, Color color)
        {
            var tween = Tweener.Instance;
            if (tween == null) return;
            _banner.text = text;
            _banner.color = color;
            _banner.transform.SetAsLastSibling();
            tween.Scale(_banner.rectTransform, 0.8f, 1f, 0.3f);
            int token = ++_bannerToken;
            tween.Fade(_bannerGroup, 0f, 1f, 0.2f, () => tween.Delay(0.9f, () =>
            {
                // A newer banner owns the label now; let it fade on its own schedule.
                if (token == _bannerToken) tween.Fade(_bannerGroup, 1f, 0f, 0.4f);
            }));
        }
    }
}
