namespace SoapSlide
{
    public enum SoapSlidePhase
    {
        Planning,
        Action,
        BetweenRounds,
        GameOver,
        /// <summary>Online: everyone at load; tap Start before planning timer begins.</summary>
        WaitingToStart
    }

    public enum SoapSlideGameType
    {
        EveryoneAlone,
        Teams4v4
    }
}
