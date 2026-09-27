namespace Sanguo.Core
{
    public enum ActionResult : byte
    {
        /// <summary>The action finished; it is removed from the stack.</summary>
        Done = 0,
        /// <summary>
        /// The action made progress and wants to be stepped again, after any child actions it
        /// pushed have fully resolved.
        /// </summary>
        Continue = 1,
        /// <summary>The action opened a request and waits for player input (or timeout).</summary>
        Wait = 2
    }

    /// <summary>
    /// One unit of rule resolution (use a card, deal damage, run a phase...). Actions live on the
    /// <see cref="ActionStack"/> and are stepped by the engine loop. An action never calls another
    /// action directly: it pushes children and returns <see cref="ActionResult.Continue"/>; the
    /// children resolve first, then the parent is stepped again. This keeps resolution iterative
    /// (no C# recursion), makes nested skill triggers bounded and lets any action pause for player
    /// input at any depth.
    ///
    /// Implementations are typically small state machines driven by a private stage counter.
    /// </summary>
    public abstract class GameAction
    {
        /// <summary>Advances the action. Must not block; must push children via ctx.Push.</summary>
        public abstract ActionResult Step(GameContext ctx);

        /// <summary>Called when the action is discarded without finishing (for example on game over).</summary>
        public virtual void OnAborted(GameContext ctx)
        {
        }

        public override string ToString() => GetType().Name;
    }
}
