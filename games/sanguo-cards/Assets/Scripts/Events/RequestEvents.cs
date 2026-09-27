using Sanguo.Core;

namespace Sanguo.Events
{
    /// <summary>
    /// The server is waiting for a player. Everyone sees who is being asked (timers, table
    /// highlights); only the asked player receives the private details.
    /// </summary>
    public sealed class RequestOpenedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.RequestOpened;
        public RequestInfo Info;

        public override GameEvent ProjectFor(int viewerId)
        {
            if (Info == null || viewerId == Info.PlayerId || !Info.HasPrivateDetails) return this;
            return new RequestOpenedEvent { Sequence = Sequence, Info = Info.PublicView() };
        }

        public override bool ApplyTo(ClientGameState state)
        {
            if (Info == null) return false;
            state.OpenRequests.RemoveAll(r => r.RequestId == Info.RequestId);
            state.OpenRequests.Add(Info);
            return true;
        }
    }

    public sealed class RequestClosedEvent : GameEvent
    {
        public override GameEventType Type => GameEventType.RequestClosed;
        public int RequestId;
        public int PlayerId;
        public bool TimedOut;
        public bool Passed;

        public override bool ApplyTo(ClientGameState state)
        {
            return state.OpenRequests.RemoveAll(r => r.RequestId == RequestId) == 1;
        }
    }
}
