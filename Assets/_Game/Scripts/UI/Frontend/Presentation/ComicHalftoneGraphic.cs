using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Lightweight procedural halftone pattern for comic-style UI panels.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ComicHalftoneGraphic : MaskableGraphic
    {
        [SerializeField, Range(4, 32)] private int m_columns = 18;
        [SerializeField, Range(2, 24)] private int m_rows = 9;
        [SerializeField, Range(0.05f, 0.45f)] private float m_dotRadius = 0.22f;
        [SerializeField] private bool m_fadeLeftToRight = true;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = GetPixelAdjustedRect();
            float cellWidth = rect.width / m_columns;
            float cellHeight = rect.height / m_rows;
            float baseRadius = Mathf.Min(cellWidth, cellHeight) * m_dotRadius;

            for (int row = 0; row < m_rows; row++)
            {
                for (int column = 0; column < m_columns; column++)
                {
                    float normalizedColumn = m_columns <= 1
                        ? 1f
                        : (float)column / (m_columns - 1);
                    float fade = m_fadeLeftToRight
                        ? normalizedColumn
                        : 1f - normalizedColumn;
                    float radius = baseRadius * Mathf.Lerp(0.35f, 1f, fade);
                    float centerX = rect.xMin + (column + 0.5f) * cellWidth;
                    float centerY = rect.yMin + (row + 0.5f) * cellHeight;
                    AddDot(vertexHelper, new Vector2(centerX, centerY), radius);
                }
            }
        }

        private void AddDot(VertexHelper vertexHelper, Vector2 center, float radius)
        {
            int startIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center + new Vector2(-radius, -radius));
            AddVertex(vertexHelper, center + new Vector2(-radius, radius));
            AddVertex(vertexHelper, center + new Vector2(radius, radius));
            AddVertex(vertexHelper, center + new Vector2(radius, -radius));
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        private void AddVertex(VertexHelper vertexHelper, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vertexHelper.AddVert(vertex);
        }
    }
}
