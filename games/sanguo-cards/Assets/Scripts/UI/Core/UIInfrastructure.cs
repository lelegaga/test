using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sanguo.UI
{
    /// <summary>Keeps a RectTransform inside Screen.safeArea (notches, punch holes, rounded corners, home bar).</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _screen.x || Screen.height != _screen.y) Apply();
        }

        private void Apply()
        {
            var rt = (RectTransform)transform;
            var safe = Screen.safeArea;
            _applied = safe;
            _screen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    public enum Ease : byte
    {
        Linear = 0,
        OutCubic = 1,
        InOutCubic = 2,
        OutBack = 3
    }

    /// <summary>
    /// Minimal allocation-free tween runner (position, scale, alpha, delayed calls). Animations are
    /// presentation only: game logic never waits for them.
    /// </summary>
    public sealed class Tweener : MonoBehaviour
    {
        private enum Kind : byte
        {
            Move,
            Scale,
            Alpha,
            Delay
        }

        private struct Tween
        {
            public Kind Kind;
            public RectTransform Target;
            public CanvasGroup Group;
            public Vector3 From;
            public Vector3 To;
            public float Duration;
            public float Elapsed;
            public Ease Ease;
            public Action Done;
        }

        private readonly List<Tween> _tweens = new List<Tween>(64);

        public static Tweener Instance { get; private set; }

        private void Awake() => Instance = this;

        public void Move(RectTransform rt, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutCubic, Action done = null)
        {
            Cancel(rt, Kind.Move);
            rt.anchoredPosition = from;
            _tweens.Add(new Tween { Kind = Kind.Move, Target = rt, From = from, To = to, Duration = duration, Ease = ease, Done = done });
        }

        public void Scale(RectTransform rt, float from, float to, float duration, Ease ease = Ease.OutBack, Action done = null)
        {
            Cancel(rt, Kind.Scale);
            rt.localScale = Vector3.one * from;
            _tweens.Add(new Tween { Kind = Kind.Scale, Target = rt, From = Vector3.one * from, To = Vector3.one * to, Duration = duration, Ease = ease, Done = done });
        }

        public void Fade(CanvasGroup group, float from, float to, float duration, Action done = null)
        {
            Cancel((RectTransform)group.transform, Kind.Alpha);
            group.alpha = from;
            _tweens.Add(new Tween { Kind = Kind.Alpha, Group = group, Target = (RectTransform)group.transform, From = new Vector3(from, 0, 0), To = new Vector3(to, 0, 0), Duration = duration, Done = done });
        }

        public void Delay(float seconds, Action done)
        {
            _tweens.Add(new Tween { Kind = Kind.Delay, Duration = seconds, Done = done });
        }

        private void Cancel(RectTransform rt, Kind kind)
        {
            for (int i = _tweens.Count - 1; i >= 0; i--)
                if (_tweens[i].Target == rt && _tweens[i].Kind == kind) _tweens.RemoveAt(i);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _tweens.Count - 1; i >= 0; i--)
            {
                var t = _tweens[i];
                t.Elapsed += dt;
                float p = t.Duration <= 0 ? 1f : Mathf.Clamp01(t.Elapsed / t.Duration);
                float e = Evaluate(t.Ease, p);
                if (t.Kind != Kind.Delay && t.Target == null)
                {
                    _tweens.RemoveAt(i);
                    continue;
                }
                switch (t.Kind)
                {
                    case Kind.Move: t.Target.anchoredPosition = Vector3.LerpUnclamped(t.From, t.To, e); break;
                    case Kind.Scale: t.Target.localScale = Vector3.LerpUnclamped(t.From, t.To, e); break;
                    case Kind.Alpha: if (t.Group != null) t.Group.alpha = Mathf.Lerp(t.From.x, t.To.x, e); break;
                }
                if (p >= 1f)
                {
                    _tweens.RemoveAt(i);
                    t.Done?.Invoke();
                }
                else
                {
                    _tweens[i] = t;
                }
            }
        }

        private static float Evaluate(Ease ease, float p)
        {
            switch (ease)
            {
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - p, 3f);
                case Ease.InOutCubic: return p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
                }
                default: return p;
            }
        }
    }

    /// <summary>Reuses UI objects instead of Instantiate/Destroy churn (cards, floating texts, log lines).</summary>
    public sealed class UIPool<T> where T : Component
    {
        private readonly Func<T> _create;
        private readonly Stack<T> _free = new Stack<T>();

        public UIPool(Func<T> create)
        {
            _create = create;
        }

        public T Get()
        {
            T item = null;
            while (_free.Count > 0 && item == null) item = _free.Pop();
            if (item == null) item = _create();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            _free.Push(item);
        }
    }

    /// <summary>A full-screen page (main menu, lobby, table...).</summary>
    public abstract class UIScreen
    {
        public RectTransform Root { get; private set; }
        protected UIRoot UI { get; private set; }

        internal void Create(UIRoot ui, RectTransform parent)
        {
            UI = ui;
            Root = UIFactory.Rect(GetType().Name, parent);
            UIFactory.Stretch(Root);
            Build(Root);
        }

        protected abstract void Build(RectTransform root);

        public virtual void OnShow()
        {
        }

        public virtual void OnHide()
        {
        }

        /// <summary>Called every frame while visible.</summary>
        public virtual void Tick()
        {
        }

        /// <summary>Android back button / Escape.</summary>
        public virtual void OnBack()
        {
        }
    }

    /// <summary>
    /// Owns the canvas (1920×1080 reference, scaled by height for wide phones), the safe area, the
    /// event system and the current screen.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        private readonly Dictionary<Type, UIScreen> _screens = new Dictionary<Type, UIScreen>();
        private UIScreen _current;
        private RectTransform _safe;
        private Text _toast;
        private float _toastUntil;

        public RectTransform Safe => _safe;
        public RectTransform Overlay { get; private set; }
        public Canvas Canvas { get; private set; }

        public static UIRoot Create()
        {
            var go = new GameObject("UIRoot") { layer = UIFactory.UILayer };
            DontDestroyOnLoad(go);
            return go.AddComponent<UIRoot>();
        }

        private void Awake()
        {
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // Landscape phones are wide: match height so text stays readable; iPads get a narrower canvas.
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            gameObject.AddComponent<Tweener>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                DontDestroyOnLoad(es);
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var bg = UIFactory.Image(transform, "Background", UITheme.Background);
            UIFactory.Stretch(bg.rectTransform);
            _safe = UIFactory.Rect("SafeArea", transform);
            UIFactory.Stretch(_safe);
            _safe.gameObject.AddComponent<SafeAreaFitter>();
            Overlay = UIFactory.Rect("Overlay", _safe);
            UIFactory.Stretch(Overlay);

            _toast = UIFactory.Text(Overlay, "Toast", string.Empty, UITheme.FontBody, UITheme.Text);
            UIFactory.Place(_toast.rectTransform, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 80));
            var shadow = _toast.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
        }

        public T Show<T>() where T : UIScreen, new()
        {
            if (!_screens.TryGetValue(typeof(T), out var screen))
            {
                screen = new T();
                screen.Create(this, _safe);
                _screens[typeof(T)] = screen;
            }
            if (_current != null && _current != screen)
            {
                _current.OnHide();
                _current.Root.gameObject.SetActive(false);
            }
            _current = screen;
            screen.Root.gameObject.SetActive(true);
            Overlay.SetAsLastSibling();
            screen.OnShow();
            return (T)screen;
        }

        public T Get<T>() where T : UIScreen => _screens.TryGetValue(typeof(T), out var s) ? (T)s : null;

        public void Toast(string message, float seconds = 2.5f)
        {
            _toast.text = message;
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            _current?.Tick();
            if (Input.GetKeyDown(KeyCode.Escape)) _current?.OnBack();
            if (_toast.text.Length > 0 && Time.unscaledTime > _toastUntil) _toast.text = string.Empty;
        }
    }
}
