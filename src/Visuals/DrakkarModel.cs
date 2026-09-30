using System.Collections.Generic;
using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>Сиденье с рукоятью: где сидит гребец (точка на палубе, лицом вперёд) и где ось его кривошипа.</summary>
    internal struct CrankSpec
    {
        public Vector3 SeatPosition;
        public float SeatYaw;
        public Vector3 CrankPivot;
        public Quaternion CrankRotation;
    }

    /// <summary>Трап на борту: где висит (снаружи) и куда переносит (на палубу).</summary>
    internal struct LadderSpec
    {
        public Vector3 Center;
        public Vector3 Size;
        public float Yaw;
        public Vector3 Target;
        public float TargetYaw;
    }

    /// <summary>
    /// Геометрия «Небесного драккара» по эскизу «Грейд III · 5–7 персонажей»: купол — мяч для американского футбола из полосатого льна
    /// (красные и кремовые полосы, поясной канат, стропы зигзагом к стойкам фальшборта, железные наконечники),
    /// под ним — корпус драккара: обшивка досками, подъём к носу и корме, драконья голова на форштевне,
    /// щиты по бортам, железные бандажи, палуба, фальшборт со стойками и поручнем. Посередине — фонарь огня на стойке,
    /// на корме — перо руля с рулём-палкой (вместо штурвала эскиза) и винт; внутри — сиденья с рукоятями
    /// (кривошипы крутят винт; их четыре), место рулевого, сундук и трапы по бортам.
    /// Корень — низ киля посередине, +Z — нос, +X — правый борт. Только меши и точки — без Unity-сцены.
    /// </summary>
    internal sealed class DrakkarModel
    {
        public const float HalfLength = 4.6f;
        public const float HalfBeam = 1.35f;
        public const float DeckY = 0.8f;
        public const float GunwaleY = 1.65f;
        public const float WallT = 0.07f;
        public const float RailHeight = 0.35f;
        public const float KeelBow = 1.1f;
        public const float KeelStern = 0.95f;
        public const float EnvelopeCenterY = 5.12f;
        public const float EnvelopeRadius = 1.7f;
        public const float EnvelopeHalfLength = 6.15f;
        public const int Stripes = 9;

        /// <summary>Низ чаши фонаря: 1.72 м над палубой — выше головы стоящего рядом.</summary>
        public const float LanternY = DeckY + 1.72f;

        public readonly MeshBuilder Hull = new MeshBuilder();
        public readonly MeshBuilder Deck = new MeshBuilder();
        public readonly MeshBuilder Wood = new MeshBuilder();
        public readonly MeshBuilder Iron = new MeshBuilder();
        public readonly MeshBuilder Rope = new MeshBuilder();
        public readonly MeshBuilder Envelope = new MeshBuilder();
        public readonly MeshBuilder Shields = new MeshBuilder();

        /// <summary>Винт: меши в системе оси винта (крутится вокруг своей Z).</summary>
        public readonly MeshBuilder PropellerWood = new MeshBuilder();
        public readonly MeshBuilder PropellerIron = new MeshBuilder();
        public Vector3 PropellerPivot;

        /// <summary>Руль: баллер, перо и руль-палка в системе оси баллера (поворачивается вокруг Y).</summary>
        public readonly MeshBuilder RudderWood = new MeshBuilder();
        public readonly MeshBuilder RudderIron = new MeshBuilder();
        public Vector3 RudderPivot;

        /// <summary>Один кривошип в своей системе: ось вала — Z (к гребцу), рукоять — по Y.</summary>
        public readonly MeshBuilder CrankIron = new MeshBuilder();
        public readonly MeshBuilder CrankWood = new MeshBuilder();
        public readonly List<CrankSpec> Cranks = new List<CrankSpec>();

        /// <summary>Границы полос купола в долях V текстуры (от кормового острия к носовому).</summary>
        public float[] StripeV;

        public Vector3 FirePosition;
        public float FireScale = 0.7f;
        public Vector3 FireOperatorPosition;
        public float FireOperatorYaw;
        public Vector3 BurnerColliderCenter;
        public Vector3 BurnerColliderSize;

        /// <summary>Рулевой сидит на корме слева от руль-палки (как на ванильных кораблях — сидя).</summary>
        public Vector3 HelmPosition;
        public float HelmYaw;
        public Vector3 TillerColliderCenter;
        public Vector3 TillerColliderSize;

        public Vector3 ChestPosition;
        public Vector3 AnchorRopeStart;
        public Vector3 AnchorOutward;

        public readonly List<LadderSpec> Ladders = new List<LadderSpec>();
        public readonly List<Vector3> HullColliderPoints = new List<Vector3>();
        public readonly List<BoxSpec> WallColliders = new List<BoxSpec>();
        public readonly List<BoxSpec> SolidColliders = new List<BoxSpec>();

        public static DrakkarModel Build()
        {
            var m = new DrakkarModel();
            m.BuildHull();
            m.BuildDeckAndRailing();
            m.BuildStemAndDragon();
            m.BuildShieldsAndStraps();
            m.BuildEnvelope();
            m.BuildRigging();
            m.BuildLantern();
            m.BuildCrankSeats();
            m.BuildStern();
            m.BuildLadders();
            m.BuildColliders();
            m.ChestPosition = new Vector3(0f, DeckY, 2.95f);
            float bz = 3.4f;
            m.AnchorOutward = Vector3.left;
            m.AnchorRopeStart = new Vector3(-(HalfBeamAt(bz) - 0.05f), GunwaleAt(bz) + 0.05f, bz);
            return m;
        }

        // ------------------------------------------------------------------ обводы корпуса

        /// <summary>Полуширина корпуса по планширю; к корме полнее, к носу острее.</summary>
        public static float HalfBeamAt(float z)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / HalfLength);
            return z >= 0f
                ? HalfBeam * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(a, 2.4f)), 0.8f)
                : HalfBeam * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(a, 3.2f)), 0.7f);
        }

        /// <summary>Линия киля: выгнута по всей длине («банан», как у драккара), к носу круче.</summary>
        public static float KeelAt(float z)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / HalfLength);
            return z >= 0f ? KeelBow * Mathf.Pow(a, 2.0f) : KeelStern * Mathf.Pow(a, 2.4f);
        }

        /// <summary>Линия планширя: к штевням выше, у носа ещё круче.</summary>
        public static float GunwaleAt(float z)
        {
            float a = Mathf.Clamp(z / HalfLength, -1f, 1f);
            return GunwaleY + 0.4f * a * a + (a > 0f ? 0.12f * a * a * a : 0f);
        }

        /// <summary>Точка обшивки: t = 0 — киль, t = 1 — планширь; side = ±1 — борт.</summary>
        public static Vector3 Section(float z, float t, float side)
        {
            float b = HalfBeamAt(z);
            float k = KeelAt(z);
            float g = GunwaleAt(z);
            float x = b * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f), 0.7f);
            float y = k + (g - k) * Mathf.Pow(Mathf.Clamp01(t), 1.35f);
            return new Vector3(side * x, y, z);
        }

        /// <summary>t, на котором обшивка проходит через высоту y (0, если киль выше).</summary>
        public static float TAtHeight(float z, float y)
        {
            float k = KeelAt(z);
            float g = GunwaleAt(z);
            if (y <= k)
            {
                return 0f;
            }
            return Mathf.Pow(Mathf.Clamp01((y - k) / (g - k)), 1f / 1.35f);
        }

        /// <summary>Внутренняя полуширина корпуса на высоте y (за вычетом толщины обшивки).</summary>
        public static float InnerHalfWidth(float z, float y)
        {
            return Mathf.Max(0f, Mathf.Abs(Section(z, TAtHeight(z, y), 1f).x) - WallT);
        }

        private static Vector3 HullOutward(Vector3 p)
        {
            return new Vector3(p.x, p.y - (DeckY + 0.35f), p.z * 0.15f);
        }

        private void BuildHull()
        {
            const int nz = 64;
            const int nt = 16;
            const float uvScale = 1f / 1.2f;
            foreach (float side in new[] { -1f, 1f })
            {
                // Снаружи: от киля до планширя по всей длине.
                var pts = new Vector3[nz + 1, nt + 1];
                var uvs = new Vector2[nz + 1, nt + 1];
                // Пояса обшивки идут вдоль корпуса и сходятся к штевням, как у клинкерной обшивки:
                // V — доля обвода от киля к планширю (одна и та же доска на всей длине), а не длина дуги.
                const float girth = 2.2f;
                for (int i = 0; i <= nz; i++)
                {
                    float z = Mathf.Lerp(-HalfLength, HalfLength, i / (float)nz);
                    for (int j = 0; j <= nt; j++)
                    {
                        float t = j / (float)nt;
                        pts[i, j] = Section(z, t, side);
                        uvs[i, j] = new Vector2(z * uvScale / 3f, t * girth * uvScale);
                    }
                }
                Hull.AddGrid(pts, uvs, HullOutward, doubleSided: false);

                // Изнутри: фальшборт от палубы до планширя (у штевней — от киля).
                const float end = 0.25f;
                var ipts = new Vector3[nz + 1, 7];
                var iuvs = new Vector2[nz + 1, 7];
                for (int i = 0; i <= nz; i++)
                {
                    float z = Mathf.Lerp(-HalfLength + end, HalfLength - end, i / (float)nz);
                    float t0 = TAtHeight(z, DeckY);
                    for (int j = 0; j <= 6; j++)
                    {
                        float t = Mathf.Lerp(t0, 1f, j / 6f);
                        Vector3 p = Section(z, t, side);
                        p.x = side * Mathf.Max(0f, Mathf.Abs(p.x) - WallT);
                        ipts[i, j] = p;
                        iuvs[i, j] = new Vector2(z * uvScale / 3f, t * 2.2f * uvScale);
                    }
                }
                Hull.AddGrid(ipts, iuvs, p => new Vector3(-p.x, 0.2f, 0f), doubleSided: false);

                // Планширь — брус по верху борта.
                var gun = new List<Vector3>();
                for (int i = 0; i <= 40; i++)
                {
                    float z = Mathf.Lerp(-HalfLength + 0.15f, HalfLength - 0.15f, i / 40f);
                    Vector3 p = Section(z, 1f, side);
                    p.x -= side * WallT * 0.5f;
                    p.y += 0.03f;
                    gun.Add(p);
                }
                Wood.AddTube(gun, 0.07f, 8, 1f);
            }

            // Киль — брус по низу.
            var keel = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float z = Mathf.Lerp(-HalfLength + 0.2f, HalfLength - 0.2f, i / 40f);
                keel.Add(new Vector3(0f, KeelAt(z) + 0.02f, z));
            }
            Wood.AddTube(keel, 0.07f, 8, 1f);
        }

        // ------------------------------------------------------------------ палуба, стойки, поручень

        private void BuildDeckAndRailing()
        {
            const int nz = 48;
            const int nx = 8;
            const float zFrom = -4.1f;
            const float zTo = 3.72f;
            var pts = new Vector3[nz + 1, nx + 1];
            var uvs = new Vector2[nz + 1, nx + 1];
            for (int i = 0; i <= nz; i++)
            {
                float z = Mathf.Lerp(zFrom, zTo, i / (float)nz);
                float w = InnerHalfWidth(z, DeckY) + 0.01f;
                for (int j = 0; j <= nx; j++)
                {
                    float x = Mathf.Lerp(-w, w, j / (float)nx);
                    pts[i, j] = new Vector3(x, DeckY, z);
                    uvs[i, j] = new Vector2(z / 3.6f, x / 1.2f + 0.3f);
                }
            }
            Deck.AddGrid(pts, uvs, Vector3.up, doubleSided: false);

            // Стойки фальшборта с поручнем по верху; на каждой второй — узел, к нему идут стропы купола.
            foreach (float side in new[] { -1f, 1f })
            {
                var rail = new List<Vector3>();
                for (int k = -5; k <= 5; k++)
                {
                    float z = k * 0.8f;
                    Vector3 p = Section(z, 1f, side);
                    p.x -= side * WallT * 0.5f;
                    Wood.AddCylinder(p + Vector3.up * 0.05f, 0.045f, RailHeight, 8, 2f);
                    rail.Add(p + Vector3.up * (RailHeight + 0.05f));
                    if ((k & 1) == 0)
                    {
                        Rope.AddSphere(p + Vector3.up * (RailHeight + 0.08f), new Vector3(0.07f, 0.06f, 0.07f), 8, 5);
                    }
                }
                Wood.AddTube(rail, 0.04f, 8, 1f);
            }
        }

        /// <summary>Верх стойки фальшборта, к которой идут стропы (каждая вторая стойка).</summary>
        private static Vector3 RailPostTop(float z, float side)
        {
            Vector3 p = Section(z, 1f, side);
            p.x -= side * WallT * 0.5f;
            return p + Vector3.up * (RailHeight + 0.06f);
        }

        // ------------------------------------------------------------------ форштевень и драконья голова

        private void BuildStemAndDragon()
        {
            float kb = KeelAt(HalfLength);
            float gb = GunwaleAt(HalfLength);
            var stem = new List<Vector3>
            {
                new Vector3(0f, KeelAt(HalfLength - 0.55f) + 0.05f, HalfLength - 0.55f),
                new Vector3(0f, kb, HalfLength),
                new Vector3(0f, gb, HalfLength + 0.1f),
                new Vector3(0f, gb + 0.38f, HalfLength + 0.26f),
                new Vector3(0f, gb + 0.72f, HalfLength + 0.52f),
                new Vector3(0f, gb + 0.93f, HalfLength + 0.82f),
            };
            Wood.AddTube(stem, 0.11f, 8, 1f);

            // Голова: череп, морда, приоткрытая пасть, глаза, рога назад и гребень по шее.
            Vector3 head = new Vector3(0f, gb + 1.12f, HalfLength + 1.06f);
            Quaternion tilt = Quaternion.Euler(12f, 0f, 0f);
            const float hs = 1.1f;
            Wood.AddBox(head, new Vector3(0.38f, 0.36f, 0.62f) * hs, tilt, 2f);
            Wood.AddBox(head + tilt * new Vector3(0f, -0.04f, 0.5f) * hs, new Vector3(0.28f, 0.22f, 0.46f) * hs, tilt, 2f);
            Quaternion jaw = Quaternion.Euler(24f, 0f, 0f);
            Wood.AddBox(head + tilt * new Vector3(0f, -0.2f, 0.42f) * hs, new Vector3(0.26f, 0.08f, 0.46f) * hs, jaw, 2f);
            foreach (float s in new[] { -1f, 1f })
            {
                Iron.AddSphere(head + tilt * new Vector3(s * 0.19f, 0.08f, 0.14f) * hs, new Vector3(0.065f, 0.065f, 0.065f), 8, 5);
                Wood.AddTube(new[] { head + tilt * new Vector3(s * 0.12f, 0.16f, -0.12f) * hs, head + tilt * new Vector3(s * 0.22f, 0.42f, -0.5f) * hs }, 0.065f, 6, 2f);
                Wood.AddSphere(head + tilt * new Vector3(s * 0.1f, -0.02f, 0.74f) * hs, new Vector3(0.045f, 0.045f, 0.045f), 6, 4);
            }
            // Гребень — зубцы по задней стороне шеи (сидят на ней, а не висят в воздухе).
            for (int k = 1; k < stem.Count - 1; k++)
            {
                for (int h = 0; h < 2; h++)
                {
                    Vector3 a = stem[k];
                    Vector3 b = stem[k + 1];
                    Vector3 p = Vector3.Lerp(a, b, h * 0.5f + 0.25f);
                    Vector3 along = (b - a).normalized;
                    Vector3 back = Vector3.Cross(along, Vector3.right).normalized;
                    if (back.z > 0f)
                    {
                        back = -back;
                    }
                    Wood.AddBox(p + back * 0.14f, new Vector3(0.035f, 0.14f, 0.09f), Quaternion.LookRotation(along, back), 2f);
                }
            }

            // Ахтерштевень с завитком.
            float ks = KeelAt(-HalfLength);
            float gs = GunwaleAt(-HalfLength);
            Wood.AddTube(new[]
            {
                new Vector3(0f, KeelAt(-HalfLength + 0.55f) + 0.05f, -HalfLength + 0.55f),
                new Vector3(0f, ks, -HalfLength),
                new Vector3(0f, gs, -HalfLength - 0.07f),
                new Vector3(0f, gs + 0.3f, -HalfLength - 0.04f),
            }, 0.1f, 8, 1f);
        }

        // ------------------------------------------------------------------ щиты и бандажи

        private void BuildShieldsAndStraps()
        {
            // Железные бандажи поперёк корпуса — «усиленный корпус».
            foreach (float z in new[] { -3.6f, -1.5f, 0f, 1.5f, 3.4f })
            {
                var pts = new List<Vector3>();
                for (int j = 12; j >= 0; j--)
                {
                    pts.Add(Section(z, j / 12f, -1f));
                }
                for (int j = 1; j <= 12; j++)
                {
                    pts.Add(Section(z, j / 12f, 1f));
                }
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector3 a = pts[i];
                    Vector3 b = pts[i + 1];
                    Vector3 d = b - a;
                    if (d.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }
                    Vector3 mid = (a + b) * 0.5f;
                    Vector3 outward = HullOutward(mid);
                    outward = (outward - d.normalized * Vector3.Dot(outward, d.normalized)).normalized;
                    Iron.AddBox(mid + outward * 0.014f, new Vector3(0.13f, 0.024f, d.magnitude + 0.01f), Quaternion.LookRotation(d.normalized, outward), 3f);
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    foreach (float t in new[] { 0.35f, 0.7f, 0.95f })
                    {
                        Vector3 p = Section(z, t, side);
                        Iron.AddSphere(p + HullOutward(p).normalized * 0.03f, new Vector3(0.022f, 0.022f, 0.022f), 6, 4);
                    }
                }
            }

            // Круглые щиты по бортам: доски, красно-кремовые четверти, железные умбон и обод.
            foreach (float z in new[] { -2.55f, 2.45f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float t = TAtHeight(z, GunwaleAt(z) - 0.62f);
                    Vector3 p = Section(z, t, side);
                    Vector3 n = HullOutward(p);
                    n.y *= 0.4f;
                    n = n.normalized;
                    Quaternion rot = Quaternion.LookRotation(n, Vector3.up);
                    Vector3 c = p + n * 0.06f;
                    Shield(c, rot, 0.45f);
                }
            }
        }

        private void Shield(Vector3 center, Quaternion rot, float radius)
        {
            const int rings = 6;
            const int segs = 28;
            var pts = new Vector3[rings + 1, segs + 1];
            var uvs = new Vector2[rings + 1, segs + 1];
            for (int i = 0; i <= rings; i++)
            {
                float r = radius * i / rings;
                float dome = 0.07f * (1f - (r / radius) * (r / radius));
                for (int j = 0; j <= segs; j++)
                {
                    float a = j / (float)segs * Mathf.PI * 2f;
                    var local = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, dome);
                    pts[i, j] = center + rot * local;
                    uvs[i, j] = new Vector2(0.5f + local.x / (2f * radius), 0.5f + local.y / (2f * radius));
                }
            }
            Vector3 front = rot * Vector3.forward;
            Shields.AddGrid(pts, uvs, front, doubleSided: false);
            Iron.AddTorus(center, rot * Quaternion.Euler(90f, 0f, 0f), radius, radius, 0.03f, 0.03f, 32, 6, 4f);
            Iron.AddSphere(center + front * 0.08f, new Vector3(0.12f, 0.12f, 0.12f), 10, 6);
        }

        // ------------------------------------------------------------------ купол — мяч для американского футбола

        /// <summary>
        /// Радиус купола на расстоянии z от середины — форма мяча для американского футбола, снятая с силуэта эскиза:
        /// самый толстый посередине, плавно сужается к почти острым концам (r = R·(1 − a^2.2)^0.7, a — доля полудлины).
        /// </summary>
        public static float EnvelopeRadiusAt(float z)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / EnvelopeHalfLength);
            return EnvelopeRadius * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(a, 2.2f)), 0.7f);
        }

        private void BuildEnvelope()
        {
            // Профиль (r, «высота» = z) от кормового острия к носовому; гуще у концов.
            var profile = new List<Vector2>();
            const int n = 72;
            for (int k = 0; k <= n; k++)
            {
                float u = k / (float)n * 2f - 1f;
                float z = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 0.8f) * EnvelopeHalfLength;
                profile.Add(new Vector2(k == 0 || k == n ? 0.001f : EnvelopeRadiusAt(z), z));
            }
            int from = Envelope.VertexCount;
            Envelope.AddLathe(profile, 48, 4f, -1f, 0f, doubleSided: true, closedTop: true);
            // Лёг на бок: ось вращения Y → Z.
            Envelope.Transform(from, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, EnvelopeCenterY, 0f));

            // Границы полос — в долях длины дуги профиля (ею задан V на куполе).
            var arc = new float[profile.Count];
            for (int k = 1; k < profile.Count; k++)
            {
                arc[k] = arc[k - 1] + Vector2.Distance(profile[k], profile[k - 1]);
            }
            float total = arc[arc.Length - 1];
            StripeV = new float[Stripes - 1];
            for (int s = 1; s < Stripes; s++)
            {
                float zb = -EnvelopeHalfLength + 2f * EnvelopeHalfLength * s / Stripes;
                int k = 1;
                while (k < profile.Count - 1 && profile[k].y < zb)
                {
                    k++;
                }
                float span = profile[k].y - profile[k - 1].y;
                float f = span > 1e-6f ? (zb - profile[k - 1].y) / span : 0f;
                StripeV[s - 1] = Mathf.Lerp(arc[k - 1], arc[k], f) / total;
            }

            // Железные наконечники на остриях.
            foreach (float end in new[] { -1f, 1f })
            {
                var cap = new List<Vector2>();
                for (int k = 0; k <= 6; k++)
                {
                    float z = EnvelopeHalfLength - 0.2f + 0.2f * k / 6f;
                    cap.Add(new Vector2(k == 6 ? 0.001f : EnvelopeRadiusAt(z) + 0.03f, z));
                }
                cap.Add(new Vector2(0.001f, EnvelopeHalfLength + 0.1f));
                int f = Iron.VertexCount;
                Iron.AddLathe(cap, 16, 2f, 3f, 0f);
                Iron.Transform(f, Quaternion.Euler(end > 0f ? 90f : -90f, 0f, 0f), new Vector3(0f, EnvelopeCenterY, 0f));
            }
        }

        /// <summary>Точка на поясном канате купола (по борту на уровне оси).</summary>
        private static Vector3 WaistPoint(float z, float side)
        {
            return new Vector3(side * (EnvelopeRadiusAt(z) + 0.05f), EnvelopeCenterY, z);
        }

        private void BuildRigging()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                // Поясной канат вдоль купола.
                var waist = new List<Vector3>();
                for (int i = 0; i <= 60; i++)
                {
                    waist.Add(WaistPoint(Mathf.Lerp(-5.95f, 5.95f, i / 60f), side));
                }
                Rope.AddTube(waist, 0.05f, 8, 2.5f);

                // Стропы зигзагом: от каждой второй стойки фальшборта — две к поясу, вперёд и назад.
                for (int k = -2; k <= 2; k++)
                {
                    float z = k * 1.6f;
                    Vector3 post = RailPostTop(z, side);
                    foreach (float dz in new[] { -0.8f, 0.8f })
                    {
                        Rope.AddTube(new[] { post, WaistPoint(z + dz, side) - new Vector3(side * 0.02f, 0.03f, 0f) }, 0.035f, 6, 2.5f);
                    }
                }
                // Узлы на поясе, где сходятся стропы.
                for (int k = -3; k <= 2; k++)
                {
                    float z = k * 1.6f + 0.8f;
                    Rope.AddSphere(WaistPoint(z, side), new Vector3(0.09f, 0.09f, 0.09f), 8, 5);
                }
            }
        }

        // ------------------------------------------------------------------ фонарь огня на стойке

        private void BuildLantern()
        {
            float bowlBase = LanternY;
            // Чаша.
            var bowl = new List<Vector2>
            {
                new Vector2(0.001f, 0f), new Vector2(0.145f, 0.009f), new Vector2(0.265f, 0.05f), new Vector2(0.33f, 0.145f),
                new Vector2(0.35f, 0.265f), new Vector2(0.325f, 0.272f), new Vector2(0.305f, 0.17f), new Vector2(0.24f, 0.095f),
                new Vector2(0.12f, 0.06f), new Vector2(0.001f, 0.055f),
            };
            Iron.AddLathe(bowl, 28, 2f, 3f, bowlBase);
            // Клетка: восемь прутьев от бортика к верхнему кольцу и шапка.
            float top = bowlBase + 0.62f;
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Iron.AddTube(new[] { d * 0.34f + Vector3.up * (bowlBase + 0.26f), d * 0.29f + Vector3.up * top }, 0.021f, 6, 3f);
            }
            Iron.AddTorus(Vector3.up * top, Quaternion.identity, 0.29f, 0.29f, 0.025f, 0.03f, 28, 6, 3f);
            Iron.AddTorus(Vector3.up * (bowlBase + 0.265f), Quaternion.identity, 0.35f, 0.35f, 0.022f, 0.03f, 28, 6, 3f);
            var capProfile = new List<Vector2> { new Vector2(0.315f, 0f), new Vector2(0.19f, 0.1f), new Vector2(0.05f, 0.16f), new Vector2(0.001f, 0.17f) };
            Iron.AddLathe(capProfile, 20, 2f, 3f, top);
            // Подвес к куполу: четыре стропы от шапки.
            float envBottom = EnvelopeCenterY - EnvelopeRadiusAt(0f);
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Rope.AddTube(new[] { d * 0.1f + Vector3.up * (top + 0.14f), d * 0.5f + Vector3.up * (envBottom + 0.04f) }, 0.028f, 6, 3f);
            }
            // Стойка от палубы до чаши — за неё держится тот, кто у огня.
            Wood.AddCylinder(new Vector3(0f, DeckY, 0f), 0.055f, bowlBase - DeckY, 12, 2f);
            Wood.AddBox(new Vector3(0f, DeckY + 0.035f, 0f), new Vector3(0.28f, 0.07f, 0.28f), 2f);
            foreach (float y in new[] { DeckY + 0.85f, DeckY + 1.15f, bowlBase - 0.1f })
            {
                Rope.AddTorus(Vector3.up * y, Quaternion.identity, 0.068f, 0.068f, 0.018f, 0.04f, 12, 5, 2f);
            }

            FirePosition = new Vector3(0f, bowlBase + 0.08f, 0f);
            BurnerColliderCenter = new Vector3(0f, (DeckY + 0.7f + top + 0.2f) * 0.5f, 0f);
            BurnerColliderSize = new Vector3(0.75f, top + 0.2f - (DeckY + 0.7f), 0.75f);
            FireOperatorYaw = 0f;
            FireOperatorPosition = new Vector3(0f, DeckY, 0f) - Quaternion.Euler(0f, FireOperatorYaw, 0f) * MediumBalloonModel.MastGripOffset;
            SolidColliders.Add(new BoxSpec { Center = new Vector3(0f, (DeckY + bowlBase) * 0.5f, 0f), Size = new Vector3(0.14f, bowlBase - DeckY, 0.14f) });
        }

        // ------------------------------------------------------------------ сиденья с рукоятями

        private void BuildCrankSeats()
        {
            // Один кривошип (в своей системе): вал к гребцу по Z, колено по Y, ручка параллельно валу, деревянная втулка.
            CrankIron.AddTube(new[] { new Vector3(0f, 0f, -0.14f), new Vector3(0f, 0f, 0.02f) }, 0.025f, 8, 3f);
            CrankIron.AddBox(new Vector3(0f, 0.1f, 0.02f), new Vector3(0.045f, 0.24f, 0.03f), 3f);
            CrankIron.AddTube(new[] { new Vector3(0f, 0.2f, 0.02f), new Vector3(0f, 0.2f, 0.2f) }, 0.016f, 6, 3f);
            int f = CrankWood.VertexCount;
            CrankWood.AddCylinder(Vector3.zero, 0.028f, 0.13f, 8, 3f);
            CrankWood.Transform(f, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 0.2f, 0.06f));

            // Четыре сиденья: пара перед фонарём огня и пара за ним.
            foreach (float z in new[] { 1.7f, -1.9f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    var seat = new Vector3(side * 0.7f, DeckY, z);
                    // Сиденье: доска на двух ножках.
                    Wood.AddBox(seat + new Vector3(0f, 0.45f, 0f), new Vector3(0.48f, 0.05f, 0.42f), 1f);
                    foreach (float dx in new[] { -0.17f, 0.17f })
                    {
                        Wood.AddBox(seat + new Vector3(dx, 0.21f, 0f), new Vector3(0.06f, 0.42f, 0.34f), 1f);
                    }
                    // Стойка кривошипа перед гребцом с железной втулкой вала.
                    var post = new Vector3(seat.x, DeckY, z + 0.64f);
                    Wood.AddBox(post + new Vector3(0f, 0.47f, 0f), new Vector3(0.12f, 0.94f, 0.12f), 2f);
                    Wood.AddBox(post + new Vector3(0f, 0.035f, 0f), new Vector3(0.26f, 0.07f, 0.26f), 2f);
                    Iron.AddTorus(post + new Vector3(0f, 0.9f, -0.07f), Quaternion.Euler(90f, 0f, 0f), 0.04f, 0.04f, 0.014f, 0.014f, 12, 5, 3f);
                    Iron.AddBox(post + new Vector3(0f, 0.9f, -0.062f), new Vector3(0.1f, 0.1f, 0.012f), 3f);
                    Cranks.Add(new CrankSpec
                    {
                        SeatPosition = seat,
                        SeatYaw = 0f,
                        CrankPivot = new Vector3(seat.x, DeckY + 0.9f, z + 0.56f),
                        CrankRotation = Quaternion.Euler(0f, 180f, 0f),
                    });
                }
            }
        }

        // ------------------------------------------------------------------ корма: руль, руль-палка, винт, рулевой

        private void BuildStern()
        {
            // Баллер руля — прямо за ахтерштевнем; перо — позади, руль-палка — вперёд, в корпус.
            RudderPivot = new Vector3(0f, 2.0f, -HalfLength - 0.2f);
            RudderWood.AddCylinder(new Vector3(0f, -0.55f, 0f), 0.06f, 1.1f, 10, 2f);
            RudderWood.AddBox(new Vector3(0f, 0.02f, -0.36f), new Vector3(0.06f, 1.0f, 0.6f), 1f);
            RudderWood.AddBox(new Vector3(0f, 0.02f, -0.68f), new Vector3(0.08f, 1.05f, 0.05f), 1f);
            Vector3 handle = new Vector3(0.3f, DeckY + 0.62f, -3.15f) - RudderPivot;
            RudderWood.AddTube(new[] { new Vector3(0f, 0.45f, 0.03f), handle }, 0.04f, 8, 1f);
            RudderWood.AddCylinder(handle + new Vector3(0f, -0.02f, 0f), 0.05f, 0.06f, 10, 2f);
            RudderWood.AddSphere(handle + (handle - new Vector3(0f, 0.45f, 0f)).normalized * 0.04f, new Vector3(0.055f, 0.055f, 0.055f), 8, 5);
            foreach (float y in new[] { -0.35f, 0.33f })
            {
                RudderIron.AddBox(new Vector3(0f, y, -0.36f), new Vector3(0.08f, 0.05f, 0.62f), 3f);
            }
            // Петли баллера на ахтерштевне (неподвижно).
            foreach (float y in new[] { 1.6f, 2.2f })
            {
                Iron.AddBox(new Vector3(0f, y, -HalfLength - 0.11f), new Vector3(0.18f, 0.05f, 0.18f), 3f);
            }

            // Винт: вал из днища кормы назад, четыре лопасти, ступица и кронштейн.
            PropellerPivot = new Vector3(0f, 0.75f, -HalfLength - 0.55f);
            Iron.AddTube(new[] { new Vector3(0f, 0.75f, -HalfLength + 0.6f), PropellerPivot + new Vector3(0f, 0f, 0.1f) }, 0.045f, 8, 3f);
            Iron.AddTube(new[] { new Vector3(0f, 1.5f, -HalfLength - 0.11f), PropellerPivot + new Vector3(0f, 0.05f, 0.16f) }, 0.03f, 6, 3f);
            PropellerIron.AddSphere(Vector3.zero, new Vector3(0.1f, 0.1f, 0.13f), 10, 6);
            PropellerIron.AddCylinder(new Vector3(0f, 0f, -0.09f), 0.085f, 0.18f, 12, 3f);
            for (int k = 0; k < 4; k++)
            {
                Quaternion r = Quaternion.Euler(0f, 0f, k * 90f);
                // Лопасть с шагом: повёрнута вокруг своей оси на 25°.
                PropellerWood.AddBox(r * new Vector3(0f, 0.33f, 0f), new Vector3(0.19f, 0.5f, 0.035f), r * Quaternion.Euler(0f, 25f, 0f), 2f);
                PropellerWood.AddBox(r * new Vector3(0f, 0.56f, 0f), new Vector3(0.14f, 0.07f, 0.03f), r * Quaternion.Euler(0f, 25f, 0f), 2f);
            }

            // Рулевой: сидит на корме лицом вперёд, руль-палка у него за спиной справа
            // (поворачиваясь, она ходит позади и сбоку, не задевая его).
            HelmYaw = 0f;
            HelmPosition = new Vector3(0f, DeckY, -2.9f);
            Wood.AddBox(HelmPosition + new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.05f, 0.42f), 1f);
            foreach (float dx in new[] { -0.2f, 0.2f })
            {
                Wood.AddBox(HelmPosition + new Vector3(dx, 0.21f, 0f), new Vector3(0.06f, 0.42f, 0.34f), 1f);
            }
            TillerColliderCenter = new Vector3(0.15f, DeckY + 1.0f, -4.0f);
            TillerColliderSize = new Vector3(0.7f, 1.1f, 1.9f);
        }

        // ------------------------------------------------------------------ трапы

        private void BuildLadders()
        {
            const float z = 0.75f;
            foreach (float side in new[] { -1f, 1f })
            {
                // Две тетивы по обшивке от низа борта до планширя и перекладины между ними.
                var railA = new List<Vector3>();
                var railB = new List<Vector3>();
                for (int j = 3; j <= 12; j++)
                {
                    Vector3 p = Section(z, j / 12f, side);
                    Vector3 n = HullOutward(p).normalized;
                    railA.Add(p + n * 0.08f + new Vector3(0f, 0f, -0.22f));
                    railB.Add(p + n * 0.08f + new Vector3(0f, 0f, 0.22f));
                }
                Wood.AddTube(railA, 0.035f, 6, 2f);
                Wood.AddTube(railB, 0.035f, 6, 2f);
                for (int j = 4; j <= 12; j += 2)
                {
                    Vector3 p = Section(z, j / 12f, side);
                    Vector3 n = HullOutward(p).normalized;
                    Vector3 c = p + n * 0.09f;
                    Wood.AddTube(new[] { c + new Vector3(0f, 0f, -0.22f), c + new Vector3(0f, 0f, 0.22f) }, 0.028f, 6, 2f);
                }
                Vector3 mid = Section(z, 0.6f, side) + HullOutward(Section(z, 0.6f, side)).normalized * 0.12f;
                Ladders.Add(new LadderSpec
                {
                    Center = mid,
                    Size = new Vector3(0.35f, 1.5f, 0.6f),
                    Yaw = 0f,
                    Target = new Vector3(side * 0.7f, DeckY + 0.05f, z),
                    TargetYaw = side > 0f ? -90f : 90f,
                });
            }
        }

        // ------------------------------------------------------------------ коллайдеры

        private void BuildColliders()
        {
            // Корпус ниже палубы — выпуклый: точки обшивки от киля до уровня палубы. Точек немного и без повторов
            // (точка киля — одна на оба борта), чтобы выпуклая оболочка уложилась в предел PhysX в 255 граней.
            // Только там, где киль ниже палубы: оболочка, натянутая на поднятые выше палубы штевни, легла бы
            // «крышей» над всей палубой (выпуклость!), и по палубе ходили бы над досками.
            float zBow = HalfLength * Mathf.Sqrt((DeckY - 0.05f) / KeelBow);
            float zStern = -HalfLength * Mathf.Pow((DeckY - 0.05f) / KeelStern, 1f / 2.4f);
            for (int i = 0; i <= 12; i++)
            {
                float z = Mathf.Lerp(zStern, zBow, i / 12f);
                float t0 = TAtHeight(z, DeckY);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int j = side < 0f ? 0 : 1; j <= 3; j++)
                    {
                        HullColliderPoints.Add(Section(z, t0 * j / 3f, side));
                    }
                    if (KeelAt(z) < DeckY)
                    {
                        HullColliderPoints.Add(new Vector3(side * InnerHalfWidth(z, DeckY), DeckY, z));
                    }
                }
            }

            // Фальшборт — наклонные доски вдоль обоих бортов, от палубы до планширя.
            const int segments = 10;
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < segments; i++)
                {
                    float z0 = Mathf.Lerp(-HalfLength + 0.3f, HalfLength - 0.3f, i / (float)segments);
                    float z1 = Mathf.Lerp(-HalfLength + 0.3f, HalfLength - 0.3f, (i + 1) / (float)segments);
                    float zm = (z0 + z1) * 0.5f;
                    float yb = Mathf.Max(DeckY, KeelAt(zm));
                    float yt = GunwaleAt(zm) + 0.1f;
                    float xb = side * InnerHalfWidth(zm, yb);
                    float xt = side * InnerHalfWidth(zm, GunwaleAt(zm));
                    Vector3 a = new Vector3(side * InnerHalfWidth(z0, (yb + yt) * 0.5f), 0f, z0);
                    Vector3 b = new Vector3(side * InnerHalfWidth(z1, (yb + yt) * 0.5f), 0f, z1);
                    Vector3 dir = (b - a).normalized;
                    float flare = Mathf.Atan2(Mathf.Abs(xt - xb), yt - yb) * Mathf.Rad2Deg;
                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, -side * flare);
                    Vector3 outward = new Vector3(side, 0f, 0f);
                    WallColliders.Add(new BoxSpec
                    {
                        Center = (a + b) * 0.5f + Vector3.up * ((yb + yt) * 0.5f) + outward * 0.05f,
                        Size = new Vector3(0.1f, (yt - yb) / Mathf.Cos(flare * Mathf.Deg2Rad), (b - a).magnitude + 0.1f),
                        Rotation = rot,
                        HasRotation = true,
                    });
                }
            }
        }
    }
}
