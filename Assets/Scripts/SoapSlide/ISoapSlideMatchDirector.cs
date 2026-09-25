namespace SoapSlide
{
    /// <summary>Match director API for local SoapSlide gameplay.</summary>
    public interface ISoapSlideMatchDirector
    {
        SoapSlidePhase Phase { get; }
        /// <summary>Whether the match is running (always true for the local round director once started).</summary>
        bool MatchIsActive { get; }
        float ArenaHalfExtents { get; }
        SlideParticipant LocalHuman { get; }
        bool IsLocalPlayerSpectating { get; }
        SlideParticipant GetSpectateCameraTarget();
        void NotifyFell(SlideParticipant p);
    }
}
