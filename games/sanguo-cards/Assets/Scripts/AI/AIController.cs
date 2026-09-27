using Sanguo.Core;
using Sanguo.Utils;

namespace Sanguo.AI
{
    /// <summary>
    /// Drives one AI-controlled seat: builds the player's restricted perspective and asks its
    /// decision maker for a command answering the open request.
    /// </summary>
    public sealed class AIController
    {
        private readonly IRandom _random;

        public AIController(int playerId, IAIDecisionMaker brain, int seed, IRelationEstimator estimator = null)
        {
            PlayerId = playerId;
            Brain = brain ?? new HeuristicAI();
            Estimator = estimator;
            _random = new XorShiftRandom(seed ^ (playerId * 7919 + 17));
        }

        public int PlayerId { get; }
        public IAIDecisionMaker Brain { get; set; }
        public IRelationEstimator Estimator { get; set; }

        public GameCommand Decide(GameContext ctx, PendingRequest request)
        {
            if (request == null || request.PlayerId != PlayerId) return null;
            var view = new PlayerPerspective(ctx, PlayerId, Estimator);
            var cmd = Brain.Decide(new AIDecisionContext(view, request, _random));
            if (cmd == null) return null;
            cmd.PlayerId = PlayerId;
            cmd.RequestId = request.RequestId;
            return cmd;
        }
    }
}
