using System.Collections.Generic;
using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>
    /// Профиль оболочки (тело вращения), от горловины вверх:
    /// короткая горловина → нижняя часть, расширяющаяся до экватора → купол-эллипс до макушки.
    /// Один и тот же профиль строит меш, сетку канатов и проверку «камера внутри купола».
    /// </summary>
    public struct EnvelopeProfile
    {
        public float Radius;
        public float MouthRadius;
        public float NeckHeight;
        public float LowerHeight;
        public float UpperHeight;

        /// <summary>Показатель степени нижней части: 1 — «грушевидная», меньше 1 — полнее, круглее.</summary>
        public float LowerExponent;

        public float Height => NeckHeight + LowerHeight + UpperHeight;

        /// <summary>Радиус на высоте y над горловиной (0 снаружи профиля).</summary>
        public float RadiusAt(float y)
        {
            if (y < 0f || y > Height)
            {
                return 0f;
            }
            if (y <= NeckHeight)
            {
                return MouthRadius;
            }
            y -= NeckHeight;
            if (y <= LowerHeight)
            {
                float t = LowerHeight > 0f ? y / LowerHeight : 1f;
                return MouthRadius + (Radius - MouthRadius) * Ease(t);
            }
            float s = UpperHeight > 0f ? Mathf.Clamp01((y - LowerHeight) / UpperHeight) : 1f;
            return Radius * Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
        }

        private float Ease(float t)
        {
            float e = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
            return LowerExponent > 0f && !Mathf.Approximately(LowerExponent, 1f) ? Mathf.Pow(e, LowerExponent) : e;
        }

        /// <summary>Точки профиля (r, y) от горловины до макушки.</summary>
        public List<Vector2> Points(int lowerRings, int upperRings)
        {
            var points = new List<Vector2>();
            if (NeckHeight > 0.001f)
            {
                points.Add(new Vector2(MouthRadius, 0f));
            }
            for (int k = 0; k <= lowerRings; k++)
            {
                float t = (float)k / lowerRings;
                points.Add(new Vector2(MouthRadius + (Radius - MouthRadius) * Ease(t), NeckHeight + LowerHeight * t));
            }
            for (int k = 1; k <= upperRings; k++)
            {
                float a = (float)k / upperRings * Mathf.PI * 0.5f;
                points.Add(new Vector2(k == upperRings ? 0f : Radius * Mathf.Cos(a), NeckHeight + LowerHeight + UpperHeight * Mathf.Sin(a)));
            }
            return points;
        }

        /// <summary>
        /// Точка на поверхности по доле длины дуги профиля (0 — горловина, 1 — макушка) и углу вокруг оси (градусы, 0 = +Z),
        /// приподнятая над поверхностью на offset (по нормали).
        /// </summary>
        public Vector3 Surface(List<Vector2> dense, float[] arc, float s, float angleDeg, float offset, float baseY)
        {
            float target = Mathf.Clamp01(s) * arc[arc.Length - 1];
            int i = 1;
            while (i < arc.Length - 1 && arc[i] < target)
            {
                i++;
            }
            float span = arc[i] - arc[i - 1];
            float f = span > 1e-6f ? (target - arc[i - 1]) / span : 0f;
            Vector2 p = Vector2.Lerp(dense[i - 1], dense[i], f);
            Vector2 tangent = (dense[i] - dense[i - 1]).normalized;
            Vector2 n = new Vector2(tangent.y, -tangent.x);
            p += n * offset;
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * p.x, baseY + p.y, Mathf.Cos(a) * p.x);
        }

        /// <summary>Плотный профиль и накопленная длина дуги — для Surface().</summary>
        public void Dense(out List<Vector2> dense, out float[] arc)
        {
            dense = Points(60, 60);
            arc = new float[dense.Count];
            for (int k = 1; k < dense.Count; k++)
            {
                arc[k] = arc[k - 1] + Vector2.Distance(dense[k], dense[k - 1]);
            }
        }
    }
}
