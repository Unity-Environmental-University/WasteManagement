using UnityEngine;

namespace _project.Scripts.Object_Scripts
{
    /// <summary>
    ///     Shared MaterialPropertyBlock color application, avoiding per-renderer material instances.
    /// </summary>
    internal static class RendererColorUtility
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static void SetColor(Renderer renderer, Color color, ref MaterialPropertyBlock propertyBlock)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
