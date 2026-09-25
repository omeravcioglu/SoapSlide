using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Keeps follow target correct (local player vs spectate) and pushes match phase into the camera
    /// so planning / action / between rounds can use different rigs.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class SoapSlideSpectatorCameraDriver : MonoBehaviour
    {
        ISoapSlideMatchDirector _director;

        public void Init(ISoapSlideMatchDirector director) => _director = director;

        void LateUpdate()
        {
            if (_director == null) return;
            var follow = GetComponent<SoapSlideCameraFollow>();
            if (follow == null) return;

            follow.SetMatchPhase(_director.Phase);

            if (_director.IsLocalPlayerSpectating)
            {
                var t = _director.GetSpectateCameraTarget();
                if (t != null)
                    follow.SetTarget(t.transform);
            }
            else if (_director.LocalHuman != null)
            {
                follow.SetTarget(_director.LocalHuman.transform);
            }
        }
    }
}
