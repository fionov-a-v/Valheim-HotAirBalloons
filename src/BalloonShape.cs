using System.Collections.Generic;
using HotAirBalloons.Visuals;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Купол шара (у него нет коллайдера), паруса карви и винт драккара — набором «шаров» в координатах шара.
    /// По ним боковой зонд не пускает купол в скалы, обрывы и острова, BalloonsApi.FindAt находит шар по точке удара,
    /// а пламя пожара встаёт на поверхность купола.
    /// </summary>
    internal sealed class BalloonShape
    {
        private struct Sphere
        {
            public Vector3 Center;
            public float Radius;
        }

        /// <summary>Запас до стены, м: в него купол не заходит.</summary>
        private const float Margin = 0.3f;

        /// <summary>Реи парусов карви от их креплений на бортах, м.</summary>
        private const float SailReach = 2.2f;

        private static readonly Vector3[] s_dirs = BuildDirs();
        private static readonly RaycastHit[] s_hits = new RaycastHit[16];
        private static int s_hardMask;

        private readonly List<Sphere> m_spheres = new List<Sphere>();
        private readonly BalloonController m_ctrl;

        public BalloonShape(BalloonController ctrl)
        {
            m_ctrl = ctrl;
            AddEnvelope(ctrl, (center, radius) => m_spheres.Add(new Sphere { Center = center, Radius = radius }));
            // Паруса карви — по бортам и поворачиваются рулём: кольцо на их высоте до концов рей.
            if (ctrl.m_kind == BalloonKind.Medium && ctrl.m_rudderPivots.Length > 0)
            {
                float reach = 0f;
                float y = 0f;
                foreach (Transform pivot in ctrl.m_rudderPivots)
                {
                    Vector3 p = ctrl.transform.InverseTransformPoint(pivot.position);
                    reach = Mathf.Max(reach, new Vector2(p.x, p.z).magnitude + SailReach);
                    y += p.y / ctrl.m_rudderPivots.Length;
                }
                m_spheres.Add(new Sphere { Center = new Vector3(0f, y, 0f), Radius = reach });
            }
            // Винт драккара — за кормой.
            if (ctrl.m_propeller != null)
            {
                m_spheres.Add(new Sphere { Center = ctrl.transform.InverseTransformPoint(ctrl.m_propeller.position), Radius = 0.75f });
            }
        }

        /// <summary>Купол «шарами»: стоячий — три пояса по высоте, лежачий (драккар) — пять по длине.</summary>
        private static void AddEnvelope(BalloonController c, System.Action<Vector3, float> add)
        {
            if (c.m_envelopeHorizontal)
            {
                if (c.m_envelopeHalfLength <= 0f || c.m_envelopeRadius <= 0f)
                {
                    return;
                }
                for (int k = -2; k <= 2; k++)
                {
                    float z = k * 0.4f * c.m_envelopeHalfLength;
                    add(c.m_envelopeCenter + new Vector3(0f, 0f, z), HorizontalRadiusAt(c, z));
                }
                return;
            }
            EnvelopeProfile profile = Profile(c);
            if (profile.Radius <= 0f || profile.LowerHeight <= 0f || profile.UpperHeight <= 0f)
            {
                return;
            }
            foreach (float t in new[] { 0.3f, 0.55f, 0.8f })
            {
                float y = t * profile.Height;
                add(new Vector3(0f, c.m_envelopeMouthY + y, 0f), profile.RadiusAt(y));
            }
        }

        private static EnvelopeProfile Profile(BalloonController c) => new EnvelopeProfile
        {
            Radius = c.m_envelopeRadius,
            MouthRadius = c.m_envelopeMouthR,
            NeckHeight = c.m_envelopeNeckH,
            LowerHeight = c.m_envelopeLowerH,
            UpperHeight = c.m_envelopeUpperH,
            LowerExponent = c.m_envelopeLowerExp,
        };

        private static float HorizontalRadiusAt(BalloonController c, float z) =>
            DrakkarModel.EnvelopeRadiusAt(z * DrakkarModel.EnvelopeHalfLength / c.m_envelopeHalfLength) * c.m_envelopeRadius / DrakkarModel.EnvelopeRadius;

        /// <summary>
        /// Где на куполе горит пламя (координаты шара): по поясу, выше и на макушке; у драккара — по верху и бокам.
        /// Плюс центр и радиус облака дыма. Для сборки префаба: нужны только поля купола у контроллера.
        /// </summary>
        public static Vector3[] FlamePoints(BalloonController c, out Vector3 smokeCenter, out float smokeRadius)
        {
            var points = new List<Vector3>();
            if (c.m_envelopeHorizontal && c.m_envelopeHalfLength > 0f && c.m_envelopeRadius > 0f)
            {
                smokeCenter = c.m_envelopeCenter;
                smokeRadius = c.m_envelopeRadius;
                float side = 1f;
                foreach (float k in new[] { -0.5f, 0f, 0.5f })
                {
                    float z = k * c.m_envelopeHalfLength;
                    float r = HorizontalRadiusAt(c, z) * 0.95f;
                    points.Add(c.m_envelopeCenter + new Vector3(0f, r, z));
                    float z2 = z + 0.25f * c.m_envelopeHalfLength * side;
                    points.Add(c.m_envelopeCenter + new Vector3(side * HorizontalRadiusAt(c, z2) * 0.95f, 0.2f, z2));
                    side = -side;
                }
                return points.ToArray();
            }
            EnvelopeProfile profile = Profile(c);
            smokeCenter = new Vector3(0f, c.m_envelopeMouthY + 0.6f * profile.Height, 0f);
            smokeRadius = Mathf.Max(0.5f, c.m_envelopeRadius);
            if (profile.Radius <= 0f || profile.LowerHeight <= 0f || profile.UpperHeight <= 0f)
            {
                return points.ToArray();
            }
            float yBelt = 0.55f * profile.Height;
            float yHigh = 0.8f * profile.Height;
            foreach (float a in new[] { 30f, 150f, 270f })
            {
                points.Add(OnRing(c.m_envelopeMouthY + yBelt, profile.RadiusAt(yBelt) * 0.96f, a));
            }
            foreach (float a in new[] { 90f, 210f })
            {
                points.Add(OnRing(c.m_envelopeMouthY + yHigh, profile.RadiusAt(yHigh) * 0.95f, a));
            }
            points.Add(new Vector3(0f, c.m_envelopeMouthY + 0.97f * profile.Height, 0f));
            return points.ToArray();
        }

        private static Vector3 OnRing(float y, float radius, float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius);
        }

        /// <summary>
        /// Боковой зонд (владелец шара, каждый шаг физики): от центров «шаров» — лучи наружу на радиус плюс путь за шаг
        /// против твёрдого (static_solid и terrain: скалы, обрывы, острова); деревья и кусты пропускаем, чтобы шар не
        /// застревал в лесу. Скорость в стену убираем — шар скользит вдоль; влезший в стену выталкиваем (кроме стоящего
        /// на земле). Только по горизонтали: вверх не пускает зонд вверх, вниз — корзина.
        /// </summary>
        public Vector3 ProbeSides(Vector3 vel, float dt, bool grounded)
        {
            if (m_spheres.Count == 0)
            {
                return vel;
            }
            if (s_hardMask == 0)
            {
                s_hardMask = LayerMask.GetMask("static_solid", "terrain");
            }
            Transform t = m_ctrl.transform;
            float reach = Margin + new Vector2(vel.x, vel.z).magnitude * dt * 2f;
            float pushDepth = 0f;
            Vector3 pushDir = Vector3.zero;
            foreach (Sphere s in m_spheres)
            {
                Vector3 center = t.TransformPoint(s.Center);
                foreach (Vector3 d in s_dirs)
                {
                    if (!HardHit(center, d, s.Radius + reach, out RaycastHit hit))
                    {
                        continue;
                    }
                    Vector3 n = hit.normal;
                    n.y = 0f;
                    if (n.sqrMagnitude < 0.04f)
                    {
                        continue; // пол или потолок — не боковая стена
                    }
                    n.Normalize();
                    float into = vel.x * n.x + vel.z * n.z;
                    if (into < 0f)
                    {
                        vel.x -= n.x * into;
                        vel.z -= n.z * into;
                    }
                    float depth = s.Radius + Margin - hit.distance;
                    if (depth > pushDepth)
                    {
                        pushDepth = depth;
                        pushDir = n;
                    }
                }
            }
            if (pushDepth > 0f && !grounded)
            {
                float push = Mathf.Min(pushDepth * 3f, 3f);
                vel.x += pushDir.x * push;
                vel.z += pushDir.z * push;
            }
            return vel;
        }

        /// <summary>Расстояние от точки до купола (парусов, винта) или корзины, м; 0 — внутри.</summary>
        public float DistanceTo(Vector3 point)
        {
            Transform t = m_ctrl.transform;
            float best = BasketDistance(t.InverseTransformPoint(point));
            foreach (Sphere s in m_spheres)
            {
                best = Mathf.Min(best, Mathf.Max(0f, Vector3.Distance(point, t.TransformPoint(s.Center)) - s.Radius));
            }
            return best;
        }

        private float BasketDistance(Vector3 local)
        {
            BalloonController c = m_ctrl;
            Vector3 p = local - c.m_onboardCenter;
            Vector3 h = c.m_onboardHalfSize;
            float dy = Mathf.Max(0f, Mathf.Abs(p.y) - h.y);
            float dxz;
            if (c.m_onboardRadius > 0f)
            {
                dxz = Mathf.Max(0f, new Vector2(p.x, p.z).magnitude - c.m_onboardRadius);
            }
            else
            {
                float dx = Mathf.Max(0f, Mathf.Abs(p.x) - h.x);
                float dz = Mathf.Max(0f, Mathf.Abs(p.z) - h.z);
                dxz = Mathf.Sqrt(dx * dx + dz * dz);
            }
            return Mathf.Sqrt(dxz * dxz + dy * dy);
        }

        private static bool HardHit(Vector3 origin, Vector3 dir, float length, out RaycastHit best)
        {
            best = default(RaycastHit);
            int n = Physics.RaycastNonAlloc(origin, dir, s_hits, length, s_hardMask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                RaycastHit hit = s_hits[k];
                if (hit.collider == null || hit.collider.attachedRigidbody != null || hit.distance >= bestDist || IsSoft(hit.collider))
                {
                    continue;
                }
                best = hit;
                bestDist = hit.distance;
            }
            return bestDist < float.MaxValue;
        }

        /// <summary>Деревья, брёвна, кусты, пни и мелкие камни купол не держат — сквозь них шар проходит, чтобы не застревать в лесу.</summary>
        public static bool IsSoft(Collider c) =>
            c.GetComponentInParent<TreeBase>() != null || c.GetComponentInParent<TreeLog>() != null || c.GetComponentInParent<Destructible>() != null;

        /// <summary>12 направлений по горизонтали и по 4 на 35° вверх и вниз.</summary>
        private static Vector3[] BuildDirs()
        {
            var list = new List<Vector3>();
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                list.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }
            foreach (float elevation in new[] { 35f, -35f })
            {
                float e = elevation * Mathf.Deg2Rad;
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + i * 90f) * Mathf.Deg2Rad;
                    list.Add(new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)));
                }
            }
            return list.ToArray();
        }
    }
}
