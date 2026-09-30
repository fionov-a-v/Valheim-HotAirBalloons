using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>
    /// Складывание паруса-веера: полотно и реи сходятся к средней рее, как складной веер.
    /// Точка в плоскости веера (X — наружу, Y — вверх, начало — ось крепления) поворачивается вокруг оси Z
    /// так, что её угол от средней реи умножается на k (1 — раскрыт, 0.5 — наполовину, ~0 — сложен).
    /// </summary>
    internal static class SailFold
    {
        public const float Full = 1f;
        public const float Half = 0.5f;
        public const float Furled = 0.07f;

        public static float Factor(int level)
        {
            return level >= 2 ? Full : level == 1 ? Half : Furled;
        }

        /// <summary>На сколько градусов повернуть вокруг Z точку (или рею), лежащую под углом angleDeg.</summary>
        public static float DeltaDeg(float angleDeg, float centerDeg, float k)
        {
            return (angleDeg - centerDeg) * (k - 1f);
        }

        public static Vector3 Point(Vector3 p, float centerDeg, float k)
        {
            float r = Mathf.Sqrt(p.x * p.x + p.y * p.y);
            if (r < 1e-5f)
            {
                return p;
            }
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            float a = (angle + DeltaDeg(angle, centerDeg, k)) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, p.z);
        }

        /// <summary>Нормаль поворачивается вместе с точкой (для тонкого полотна этого достаточно).</summary>
        public static Vector3 Normal(Vector3 p, Vector3 n, float centerDeg, float k)
        {
            float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            float d = DeltaDeg(angle, centerDeg, k) * Mathf.Deg2Rad;
            float c = Mathf.Cos(d);
            float s = Mathf.Sin(d);
            return new Vector3(n.x * c - n.y * s, n.x * s + n.y * c, n.z);
        }
    }
}
