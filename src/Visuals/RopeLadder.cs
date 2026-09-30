using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>
    /// Верёвочный трап, висящий снаружи на ободе корзины: две верёвки петлями через обод, деревянные перекладины.
    /// Доходит до низа корзины (не ниже — иначе на земле уходил бы в грунт), так что до него дотягиваются и вплавь.
    /// </summary>
    internal static class RopeLadder
    {
        /// <param name="angleDeg">Где висит: угол вокруг оси шара (0 — +Z, 90 — +X).</param>
        /// <param name="rimRadius">Радиус наружной кромки обода.</param>
        /// <param name="rimTop">Высота верха обода.</param>
        /// <param name="bottomY">До какой высоты свисает.</param>
        /// <param name="target">Куда трап переносит (на дне корзины, лицом внутрь).</param>
        public static LadderSpec Build(MeshBuilder rope, MeshBuilder wood, float angleDeg, float rimRadius, float rimTop, float bottomY,
            float width, Vector3 target)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var across = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            float hang = rimRadius + 0.05f;
            float mid = (rimTop + bottomY) * 0.5f;
            foreach (float s in new[] { -0.5f, 0.5f })
            {
                Vector3 side = across * (width * s);
                // Петля через обод и верёвка вниз, чуть провисающая наружу.
                rope.AddTube(new[]
                {
                    outward * (rimRadius - 0.09f) + side + Vector3.up * (rimTop + 0.01f),
                    outward * (rimRadius - 0.02f) + side + Vector3.up * (rimTop + 0.06f),
                    outward * (hang + 0.01f) + side + Vector3.up * (rimTop - 0.04f),
                    outward * (hang + 0.035f) + side + Vector3.up * mid,
                    outward * (hang + 0.02f) + side + Vector3.up * bottomY,
                }, 0.02f, 6, 3f);
                rope.AddSphere(outward * (hang + 0.02f) + side + Vector3.up * (bottomY - 0.02f), new Vector3(0.035f, 0.045f, 0.035f), 6, 4);
            }
            // Перекладины через ~0.3 м, с узлами на концах.
            int rungs = Mathf.Max(2, Mathf.RoundToInt((rimTop - 0.2f - bottomY) / 0.3f));
            for (int k = 0; k <= rungs; k++)
            {
                float y = Mathf.Lerp(bottomY + 0.06f, rimTop - 0.2f, k / (float)rungs);
                float bulge = 0.035f * Mathf.Sin(Mathf.Clamp01((y - bottomY) / (rimTop - bottomY)) * Mathf.PI);
                Vector3 c = outward * (hang + 0.02f + bulge) + Vector3.up * y;
                wood.AddTube(new[] { c - across * (width * 0.5f + 0.035f), c + across * (width * 0.5f + 0.035f) }, 0.022f, 6, 2f);
                rope.AddSphere(c - across * (width * 0.5f), new Vector3(0.03f, 0.035f, 0.03f), 6, 4);
                rope.AddSphere(c + across * (width * 0.5f), new Vector3(0.03f, 0.035f, 0.03f), 6, 4);
            }
            return new LadderSpec
            {
                Center = outward * (hang + 0.1f) + Vector3.up * mid,
                Size = new Vector3(width + 0.3f, rimTop - bottomY + 0.25f, 0.45f),
                Yaw = angleDeg,
                Target = target,
                TargetYaw = angleDeg + 180f,
            };
        }
    }
}
