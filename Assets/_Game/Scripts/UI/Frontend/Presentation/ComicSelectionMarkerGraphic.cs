using UnityEngine;
using UnityEngine.UI;

namespace ZZ
{
    /// <summary>
    /// Draws the cream selection arrow and lightning accent used by comic menu entries.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ComicSelectionMarkerGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            AddArrow(vertexHelper, rect);
            AddLightning(vertexHelper, rect);
        }

        private void AddArrow(VertexHelper vertexHelper, Rect rect)
        {
            float left = rect.xMin;
            float centerY = rect.center.y;
            float height = Mathf.Min(rect.height * 0.56f, 54f);
            float bodyLength = Mathf.Min(rect.width * 0.065f, 62f);
            Vector2[] points =
            {
                new(left, centerY - height * 0.28f),
                new(left + bodyLength * 0.58f, centerY - height * 0.28f),
                new(left + bodyLength * 0.58f, centerY - height * 0.5f),
                new(left + bodyLength, centerY),
                new(left + bodyLength * 0.58f, centerY + height * 0.5f),
                new(left + bodyLength * 0.58f, centerY + height * 0.28f),
                new(left, centerY + height * 0.28f),
            };
            AddPolygon(vertexHelper, points);
        }

        private void AddLightning(VertexHelper vertexHelper, Rect rect)
        {
            float right = rect.xMax;
            float centerY = rect.center.y;
            float width = Mathf.Min(rect.width * 0.05f, 44f);
            float height = Mathf.Min(rect.height * 0.72f, 68f);
            Vector2[] points =
            {
                new(right - width * 0.34f, centerY + height * 0.5f),
                new(right, centerY + height * 0.5f),
                new(right - width * 0.28f, centerY + height * 0.06f),
                new(right - width * 0.02f, centerY + height * 0.06f),
                new(right - width * 0.72f, centerY - height * 0.5f),
                new(right - width * 0.5f, centerY - height * 0.08f),
                new(right - width, centerY - height * 0.08f),
            };
            AddPolygon(vertexHelper, points);
        }

        private void AddPolygon(VertexHelper vertexHelper, Vector2[] points)
        {
            int startIndex = vertexHelper.currentVertCount;
            foreach (Vector2 point in points)
            {
                UIVertex vertex = UIVertex.simpleVert;
                vertex.color = color;
                vertex.position = point;
                vertexHelper.AddVert(vertex);
            }

            for (int index = 1; index < points.Length - 1; index++)
            {
                vertexHelper.AddTriangle(startIndex, startIndex + index, startIndex + index + 1);
            }
        }
    }
}
