using UnityEngine;

namespace SoapSlide
{
    [RequireComponent(typeof(Collider))]
    public sealed class SoapSlideFallZone : MonoBehaviour
    {
        ISoapSlideMatchDirector _director;

        public void Bind(ISoapSlideMatchDirector director)
        {
            _director = director;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!SoapSlideNetUtils.ShouldRunGameSimulation) return;
            if (_director == null) return;
            var p = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<SlideParticipant>()
                : other.GetComponent<SlideParticipant>();
            if (p != null)
                _director.NotifyFell(p);
        }
    }
}
