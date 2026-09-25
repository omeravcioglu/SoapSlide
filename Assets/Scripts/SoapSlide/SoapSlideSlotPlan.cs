namespace SoapSlide
{
    /// <summary>Committed planning for one arena slot (direction XZ + force 1–10).</summary>
    public struct SoapSlideSlotPlan
    {
        public float DirX;
        public float DirZ;
        public int ForceLevel;
        public bool IsBot;
    }
}
