using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Spawns only on the local machine under the character this peer controls — other players never get this object.
    /// Use optional prefab (world-space child) or a built-in floor ring.
    /// </summary>
    public sealed class SoapSlideLocalPlayerVisuals : MonoBehaviour
    {
        public static GameObject SharedIndicatorPrefab { get; set; }

        [SerializeField] Color _ringColor = new Color(0.25f, 0.95f, 1f, 0.95f);
        [SerializeField] float _ringRadius = 0.62f;
        [SerializeField] float _lineWidth = 0.07f;
        [SerializeField] int _segments = 40;

        public void Init(GameObject prefabOverride)
        {
            var prefab = prefabOverride != null ? prefabOverride : SharedIndicatorPrefab;
            if (prefab != null)
            {
                var inst = Instantiate(prefab, transform, false);
                inst.name = "LocalControlVisualPrefab";
                return;
            }

            BuildProceduralRing();
        }

        void BuildProceduralRing()
        {
            var ringGo = new GameObject("LocalControlRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.04f, 0f);

            var lr = ringGo.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.positionCount = _segments;
            lr.widthMultiplier = _lineWidth;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", _ringColor);
            else
                mat.color = _ringColor;
            lr.material = mat;
            lr.startColor = _ringColor;
            lr.endColor = _ringColor;

            float step = Mathf.PI * 2f / _segments;
            for (int i = 0; i < _segments; i++)
            {
                float a = i * step;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * _ringRadius, 0f, Mathf.Sin(a) * _ringRadius));
            }
        }

        public static void AttachTo(Transform playerRoot, GameObject prefabOverride = null)
        {
            if (playerRoot == null) return;
            if (playerRoot.GetComponentInChildren<SoapSlideLocalPlayerVisuals>(true) != null) return;

            var holder = new GameObject("LocalPlayerVisuals");
            holder.transform.SetParent(playerRoot, false);
            holder.transform.localPosition = Vector3.zero;
            var v = holder.AddComponent<SoapSlideLocalPlayerVisuals>();
            v.Init(prefabOverride);
        }
    }
}
