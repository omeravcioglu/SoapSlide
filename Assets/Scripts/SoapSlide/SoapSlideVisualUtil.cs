using UnityEngine;

namespace SoapSlide
{
    /// <summary>Runtime mesh materials compatible with URP/BiRP so CreatePrimitive / prefabs are not pink.</summary>
    public static class SoapSlideVisualUtil
    {
        static Shader _cachedLit;

        public static void ApplyCompatibleLitMaterial(Renderer renderer, Color baseColor)
        {
            if (renderer == null) return;

            var sh = ResolveLitShader();
            if (sh == null)
                return;

            var mat = new Material(sh);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", baseColor);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", baseColor);
            else
                mat.color = baseColor;

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0f);

            renderer.sharedMaterial = mat;
        }

        static Shader ResolveLitShader()
        {
            if (_cachedLit != null)
                return _cachedLit;

            string[] candidates =
            {
                "Universal Render Pipeline/Lit",
                "HDRP/Lit",
                "Standard",
                "Legacy Shaders/Diffuse",
                "Unlit/Color",
                "Sprites/Default"
            };

            foreach (var path in candidates)
            {
                var s = Shader.Find(path);
                if (s != null)
                {
                    _cachedLit = s;
                    return s;
                }
            }

            return null;
        }
    }
}
