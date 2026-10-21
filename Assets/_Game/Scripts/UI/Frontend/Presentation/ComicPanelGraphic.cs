using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Resolution-independent comic banner used by frontend panels and selection states.
    /// The shape is generated from the current RectTransform, so it remains crisp at every
    /// Canvas scale without requiring a dedicated nine-slice sprite.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ComicPanelGraphic : MaskableGraphic
    {
        [SerializeField, Range(0f, 0.2f)] private float m_leftCut = 0.045f;
        [SerializeField, Range(0f, 0.2f)] private float m_rightCut = 0.08f;
        [SerializeField] private Texture2D m_originalTexture;

        public override Texture mainTexture => m_originalTexture != null ? m_originalTexture : base.mainTexture;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            if (m_originalTexture != null)
            {
                UIVertex[] quad = new UIVertex[4];
                Vector2[] corners = { rect.min, new(rect.xMin, rect.yMax), rect.max, new(rect.xMax, rect.yMin) };
                Vector2[] uv = { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                for (int index = 0; index < quad.Length; index++)
                {
                    quad[index] = UIVertex.simpleVert;
                    quad[index].position = corners[index];
                    quad[index].uv0 = uv[index];
                    quad[index].color = color;
                }
                vertexHelper.AddUIVertexQuad(quad);
                return;
            }
            float leftCut = rect.width * m_leftCut;
            float rightCut = rect.width * m_rightCut;
            Vector2[] points =
            {
                new(rect.xMin, rect.yMin + rect.height * 0.16f),
                new(rect.xMin + leftCut, rect.yMax),
                new(rect.xMax - rightCut, rect.yMax - rect.height * 0.04f),
                new(rect.xMax, rect.yMax - rect.height * 0.30f),
                new(rect.xMax - rightCut * 0.5f, rect.yMin + rect.height * 0.08f),
                new(rect.xMin + leftCut * 1.8f, rect.yMin),
            };

            for (int index = 0; index < points.Length; index++)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = points[index];
                vertexHelper.AddVert(vertex);
            }

            for (int index = 1; index < points.Length - 1; index++)
            {
                vertexHelper.AddTriangle(0, index, index + 1);
            }
        }
    }
}
