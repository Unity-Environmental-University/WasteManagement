using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using _project.Scripts.Object_Scripts;

namespace _project.Scripts.UI
{
    /// <summary>Barycentric ratio picker: each corner is 100% of its numbered outlet.</summary>
    public class SplitRatioTriangle : MaskableGraphic, IPointerDownHandler, IDragHandler, IMoveHandler
    {
        public event Action<Vector3> Changed;
        private Vector3 _shares = new(0.5f, 0.5f, 0f);
        public Vector3 Shares => _shares;

        public void SetShares(int main, int branch, int third)
        {
            var shares = new Vector3(main, branch, third) / 100f;
            if (shares == _shares) return;
            _shares = shares;
            SetVerticesDirty();
        }

        private Vector2 Corner(int index)
        {
            var r = rectTransform.rect;
            return index switch
            {
                0 => new Vector2(r.xMin + 18, r.yMin + 18),
                1 => new Vector2(r.xMax - 18, r.yMin + 18),
                _ => new Vector2(r.center.x, r.yMax - 18)
            };
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            EventSystem.current?.SetSelectedGameObject(gameObject, data);
            OnDrag(data);
        }

        public void OnDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position,
                    data.pressEventCamera, out var point)) Pick(point);
        }

        public void OnMove(AxisEventData data)
        {
            Pick(DotPosition() + data.moveVector * 8f);
        }

        private void Pick(Vector2 point)
        {
            ToShares(point, out var third, out var branch);
            var main = 1f - third - branch;
            if (main < 0 || branch < 0 || third < 0)
            {
                var nearest = Corner(0);
                var distance = float.PositiveInfinity;
                for (var i = 0; i < 3; i++)
                {
                    var start = Corner(i); var edge = Corner((i + 1) % 3) - start;
                    var candidate = start + edge * Mathf.Clamp01(Vector2.Dot(point - start, edge) / edge.sqrMagnitude);
                    var d = (candidate - point).sqrMagnitude;
                    if (d < distance) { distance = d; nearest = candidate; }
                }
                ToShares(nearest, out third, out branch);
                third = Mathf.Clamp01(third);
                branch = Mathf.Clamp01(branch);
            }
            var t = Mathf.RoundToInt(third * 100);
            var bShare = Mathf.Clamp(Mathf.RoundToInt(branch * 100), 0, 100 - t);
            _shares = new Vector3(100 - t - bShare, bShare, t) / 100f;
            SetVerticesDirty();
            Changed?.Invoke(_shares);
        }

        // Barycentric weights of a point toward the third and branch corners.
        private void ToShares(Vector2 point, out float third, out float branch)
        {
            var a = Corner(0); var b = Corner(1); var c = Corner(2);
            third = (point.y - a.y) / (c.y - a.y);
            branch = (point.x - a.x - (c.x - a.x) * third) / (b.x - a.x);
        }

        private Vector2 DotPosition() => Corner(0) * _shares.x + Corner(1) * _shares.y + Corner(2) * _shares.z;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (var i = 0; i < 3; i++) vh.AddVert(Corner(i), PathSplitter.OutputColor(i) * new Color(0.65f, 0.65f, 0.65f, 1f), Vector2.zero);
            vh.AddTriangle(0, 2, 1);
            for (var i = 0; i < 3; i++)
            {
                Line(vh, Corner(i), Corner((i + 1) % 3), 2, new Color(0.8f, 0.85f, 0.9f));
                Disc(vh, Corner(i), 6, PathSplitter.OutputColor(i));
            }
            var dot = DotPosition();
            Disc(vh, dot, 10, new Color(0.04f, 0.06f, 0.09f));
            Disc(vh, dot, 6, Color.white);
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var n = new Vector2(-(b - a).y, (b - a).x).normalized * width / 2;
            var start = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color color)
        {
            var start = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (var i = 0; i <= 24; i++)
            {
                var angle = i * Mathf.PI * 2 / 24;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
