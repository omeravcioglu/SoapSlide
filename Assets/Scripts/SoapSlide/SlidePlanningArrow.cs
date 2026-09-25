using UnityEngine;

namespace SoapSlide
{
    /// <summary>World-space line from the player: direction = aim, length scales with force 1–10.</summary>
    public sealed class SlidePlanningArrow : MonoBehaviour
    {
        [SerializeField] float _minLength = 0.75f;
        [SerializeField] float _maxLength = 5.5f;
        [SerializeField] float _originHeight = 0.2f;

        LineRenderer _lr;
        SlideParticipant _participant;

        public void Init(SlideParticipant participant, Color color)
        {
            _participant = participant;

            _lr = gameObject.AddComponent<LineRenderer>();
            _lr.positionCount = 2;
            _lr.useWorldSpace = true;
            _lr.numCapVertices = 4;
            _lr.numCornerVertices = 2;
            _lr.startWidth = 0.14f;
            _lr.endWidth = 0.05f;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lr.receiveShadows = false;

            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Unlit/Color")
                        ?? Shader.Find("Sprites/Default");
            if (sh != null)
            {
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", color);
                else if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
                else
                    mat.color = color;
                _lr.material = mat;
            }

            _lr.startColor = color;
            _lr.endColor = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, 0.9f);
            _lr.enabled = false;
        }

        public void SetVisible(bool visible)
        {
            if (_lr != null)
                _lr.enabled = visible;
        }

        public void Refresh()
        {
            if (_lr == null || !_lr.enabled || _participant == null || _participant.IsEliminated)
                return;

            Vector3 dir = _participant.PlanDirectionWorld;
            int level = Mathf.Clamp(_participant.PlanForceLevel, 1, 10);
            float t = (level - 1) / 9f;
            float len = Mathf.Lerp(_minLength, _maxLength, t);

            Vector3 origin = _participant.transform.position + Vector3.up * _originHeight;
            _lr.SetPosition(0, origin);
            _lr.SetPosition(1, origin + dir * len);
        }
    }
}
