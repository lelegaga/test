using Sanguo.Data;
using Sanguo.Events;
using Sanguo.GameModes;
using Sanguo.Skills;
using Sanguo.Utils;

namespace Sanguo.Core
{
    /// <summary>
    /// Server-side facade handed to actions, effects, skills, requests and modes. It exists only on
    /// the host: clients never have a GameContext, so client code has no path to rule execution or
    /// state mutation.
    /// </summary>
    public sealed class GameContext
    {
        internal GameContext(GameState state, GameContent content, IGameMode mode, IRandom random, bool keepEventHistory)
        {
            State = state;
            Content = content;
            Mode = mode;
            Random = random;
            Events = new EventLog(keepEventHistory);
            Stack = new ActionStack();
            Requests = new RequestManager();
            Modifiers = new ModifierSystem(this);
            Rules = new GameRuleEngine(this);
            Mutator = new GameStateMutator(this);
            Triggers = new SkillTriggerManager();
        }

        public GameState State { get; }
        public GameContent Content { get; }
        public IGameMode Mode { get; }
        public GameModeConfig Config => State.Config;
        public IRandom Random { get; }
        public EventLog Events { get; }
        public ActionStack Stack { get; }
        public RequestManager Requests { get; }
        public ModifierSystem Modifiers { get; }
        public GameRuleEngine Rules { get; }
        public GameStateMutator Mutator { get; }
        public SkillTriggerManager Triggers { get; }

        /// <summary>Host time of the command/tick currently being processed.</summary>
        public long NowMs { get; internal set; }

        public void Push(GameAction action) => Stack.Push(action);

        public void Emit(GameEvent e) => Events.Append(e);

        public PlayerState GetPlayer(int playerId) => State.GetPlayer(playerId);
    }
}
