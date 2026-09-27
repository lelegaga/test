using System.Collections.Generic;
using Sanguo.Events;

namespace Sanguo.Core
{
    /// <summary>Tracks open requests, their ids and deadlines.</summary>
    public sealed class RequestManager
    {
        private readonly List<PendingRequest> _open = new List<PendingRequest>();
        private int _nextRequestId = 1;

        public IReadOnlyList<PendingRequest> OpenRequests => _open;
        public bool HasOpen => _open.Count > 0;

        public T Open<T>(GameContext ctx, T request) where T : PendingRequest
        {
            request.RequestId = _nextRequestId++;
            request.OpenedAtMs = ctx.NowMs;
            int timeout = request.TimeoutMs > 0 ? request.TimeoutMs : ctx.Config.GetTimeoutMs(request.Kind);
            request.DeadlineMs = ctx.NowMs + timeout;
            request.OnOpened(ctx);
            _open.Add(request);
            if (request.IsResponseWindow) ctx.State.StateMachine.EnterWaitingResponse();
            ctx.Emit(new RequestOpenedEvent { Info = request.ToInfo(ctx) });
            return request;
        }

        public PendingRequest Find(int requestId)
        {
            for (int i = 0; i < _open.Count; i++)
                if (_open[i].RequestId == requestId) return _open[i];
            return null;
        }

        public PendingRequest FindForPlayer(int playerId)
        {
            for (int i = 0; i < _open.Count; i++)
                if (_open[i].PlayerId == playerId) return _open[i];
            return null;
        }

        /// <summary>First open request whose deadline has passed, or null.</summary>
        public PendingRequest FindExpired(long nowMs)
        {
            for (int i = 0; i < _open.Count; i++)
                if (_open[i].DeadlineMs <= nowMs) return _open[i];
            return null;
        }

        internal void Resolve(GameContext ctx, PendingRequest request, GameCommand response, bool timedOut)
        {
            if (request.IsClosed) return;
            request.Response = response;
            request.TimedOut = timedOut;
            request.IsClosed = true;
            _open.Remove(request);
            if (request.IsResponseWindow) ctx.State.StateMachine.ExitWaitingResponse();
            ctx.Emit(new RequestClosedEvent
            {
                RequestId = request.RequestId,
                PlayerId = request.PlayerId,
                TimedOut = timedOut,
                Passed = request.Passed
            });
        }

        /// <summary>Drops every open request without events (game over clears client lists itself).</summary>
        internal void AbortAll()
        {
            foreach (var r in _open) r.IsClosed = true;
            _open.Clear();
        }
    }
}
