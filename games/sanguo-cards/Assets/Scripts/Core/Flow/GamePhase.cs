namespace Sanguo.Core
{
    /// <summary>States of <see cref="GameStateMachine"/>.</summary>
    public enum GamePhase : byte
    {
        /// <summary>Lobby: the game has not started.</summary>
        Waiting = 0,
        /// <summary>Seats, roles, characters and opening hands are being set up.</summary>
        Preparing = 1,
        /// <summary>Game start triggers resolve.</summary>
        GameStart = 2,
        TurnStart = 3,
        JudgePhase = 4,
        DrawPhase = 5,
        PlayPhase = 6,
        DiscardPhase = 7,
        TurnEnd = 8,
        /// <summary>Overlay state: some player must answer a response request (dodge, rescue...).</summary>
        WaitingResponse = 9,
        GameOver = 10
    }
}
