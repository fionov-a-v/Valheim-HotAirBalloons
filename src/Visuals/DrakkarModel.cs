using System.Collections.Generic;
using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>
    /// Геометрия «Небесного драккара» по эскизу «Грейд III · 5–7 персонажей»: купол — мяч для американского футбола из полосатого льна
    /// (красные и кремовые полосы, поясной канат, стропы зигзагом к стойкам фальшборта, железные наконечники),
    /// под ним — корпус драккара: обшивка досками, подъём к носу и корме, драконья голова на форштевне,
    /// щиты по бортам, железные бандажи, палуба, фальшборт со стойками и поручнем. Посередине — фонарь огня на стойке,
    /// на корме — перо руля с рулём-палкой (вместо штурвала эскиза) и винт; внутри — сиденья с рукоятями
    /// (кривошипы крутят винт; их четыре), место рулевого, сундук и трапы по бортам.
    /// Корень — низ киля посередине, +Z — нос, +X — правый борт. Только меши и точки — без Unity-сцены.
    /// </summary>
    internal sealed class DrakkarModel : ShipModel
    {
        public const float HalfLength = 4.6f;
        public const float HalfBeam = 1.35f;
        public const float DeckY = 0.8f;
        public const float GunwaleY = 1.65f;
        public const float WallT = 0.07f;
        public const float RailHeight = 0.35f;
        public const float KeelBow = 1.1f;
        public const float KeelStern = 0.95f;

        /// <summary>Осадка на воде, м: ватерлиния на 0.45 над низом киля, палуба — на 0.35 выше воды.</summary>
        public const float Draft = 0.45f;

        /// <summary>Низ чаши фонаря: 1.72 м над палубой — выше головы стоящего рядом.</summary>
        public const float LanternY = DeckY + 1.72f;

        private DrakkarModel() : base(envelopeCenterY: 5.12f, envelopeRadius: 1.7f, envelopeHalfLength: 6.15f, stripes: 9)
        {
        }

        public static DrakkarModel Build()
        {
            var m = new DrakkarModel();
            m.BuildHull();
            m.BuildDeckAndRailing();
            m.BuildWaterMask(m.InnerStations(), z => GunwaleAt(z) - 0.04f, InnerHalfWidth);
            m.BuildStemAndDragon();
            m.BuildShieldsAndStraps();
            m.BuildEnvelope();
            m.BuildRigging();
            m.BuildLantern();
            m.BuildStern();
            m.BuildLadders();
            m.BuildColliders();
            m.ChestPosition = new Vector3(0f, DeckY, 2.95f);
            float bz = 3.4f;
            m.AnchorOutward = Vector3.left;
            m.AnchorRopeStart = new Vector3(-(HalfBeamAt(bz) - 0.05f), GunwaleAt(bz) + 0.05f, bz);
            m.BuildCrankMesh();
            m.BuildHelmSeat();
            m.BuildCrankSeats();
            m.SetOnboard(DeckY, HalfLength, HalfBeam, HalfBeamAt);
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

        /// <summary>Палуба — до тех мест у носа и кормы, где киль поднимается до её уровня (дальше дно — сама обшивка).</summary>
        private static readonly float DeckBow = HalfLength * Mathf.Sqrt(DeckY / KeelBow);
        private static readonly float DeckStern = -HalfLength * Mathf.Pow(DeckY / KeelStern, 1f / 2.4f);

        /// <summary>Станции внутренней обшивки и палубы (общие, с концами палубы).</summary>
        private List<float> InnerStations() => Stations(HalfLength, 72, DeckStern, DeckBow);

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

                // Изнутри: фальшборт от палубы до планширя, у штевней — от киля и до самого штевня
                // (иначе изнутри видно сквозь обшивку: она односторонняя).
                List<float> stations = InnerStations();
                var ipts = new Vector3[stations.Count, 7];
                var iuvs = new Vector2[stations.Count, 7];
                for (int i = 0; i < stations.Count; i++)
                {
                    float z = stations[i];
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

                // Планширь — брус по верху борта, до самых штевней (накрывает торец обшивки).
                var gun = new List<Vector3>();
                for (int i = 0; i <= 48; i++)
                {
                    float z = HalfLength * Mathf.Sin((i / 48f * 2f - 1f) * Mathf.PI * 0.5f);
                    Vector3 p = Section(z, 1f, side);
                    p.x = side * Mathf.Max(0f, Mathf.Abs(p.x) - WallT * 0.5f);
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
            const int nx = 8;
            List<float> stations = InnerStations().FindAll(z => z >= DeckStern - 0.001f && z <= DeckBow + 0.001f);
            var pts = new Vector3[stations.Count, nx + 1];
            var uvs = new Vector2[stations.Count, nx + 1];
            for (int i = 0; i < stations.Count; i++)
            {
                float z = stations[i];
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

            // Форштевень, шея и голова — одна поверхность (одна труба переменного сечения): голова растёт из шеи,
            // текстура идёт без шва. Затылок → череп (шире всего) → надбровья → морда → нос.
            var spine = new List<Vector3>(stem);
            var radii = new List<Vector2>();
            foreach (Vector3 _ in stem)
            {
                radii.Add(new Vector2(0.11f, 0.11f));
            }
            Vector3 Hp(float y, float z) => new Vector3(0f, gb + y, HalfLength + z);
            spine.Add(Hp(1.04f, 0.98f));
            radii.Add(new Vector2(0.15f, 0.16f));
            spine.Add(Hp(1.12f, 1.16f));
            radii.Add(new Vector2(0.19f, 0.2f));
            spine.Add(Hp(1.15f, 1.36f));
            radii.Add(new Vector2(0.17f, 0.17f));
            spine.Add(Hp(1.12f, 1.56f));
            radii.Add(new Vector2(0.13f, 0.12f));
            spine.Add(Hp(1.08f, 1.74f));
            radii.Add(new Vector2(0.095f, 0.085f));
            spine.Add(Hp(1.06f, 1.82f));
            radii.Add(new Vector2(0.05f, 0.045f));
            spine.Add(Hp(1.05f, 1.86f));
            radii.Add(new Vector2(0.001f, 0.001f));
            Wood.AddLoft(spine, radii, 12, 1f);

            // Нижняя челюсть — из-под черепа вперёд и вниз: пасть приоткрыта.
            Wood.AddLoft(new[] { Hp(0.99f, 1.1f), Hp(0.93f, 1.38f), Hp(0.885f, 1.6f), Hp(0.865f, 1.69f), Hp(0.86f, 1.72f) },
                new[] { new Vector2(0.12f, 0.06f), new Vector2(0.105f, 0.05f), new Vector2(0.075f, 0.038f), new Vector2(0.04f, 0.022f), new Vector2(0.001f, 0.001f) },
                10, 1f);
            foreach (float s in new[] { -1f, 1f })
            {
                // Глаза, ноздри, клыки из верхней челюсти и рога назад (сужаются к концам).
                Iron.AddSphere(Hp(1.21f, 1.3f) + Vector3.right * (s * 0.148f), new Vector3(0.055f, 0.055f, 0.055f), 8, 5);
                Iron.AddSphere(Hp(1.14f, 1.77f) + Vector3.right * (s * 0.045f), new Vector3(0.022f, 0.022f, 0.022f), 6, 4);
                Iron.AddLoft(new[] { Hp(1.0f, 1.66f) + Vector3.right * (s * 0.06f), Hp(0.93f, 1.69f) + Vector3.right * (s * 0.062f) },
                    new[] { new Vector2(0.016f, 0.016f), new Vector2(0.002f, 0.002f) }, 6, 2f);
                Wood.AddLoft(new[]
                    {
                        Hp(1.24f, 1.14f) + Vector3.right * (s * 0.08f), Hp(1.4f, 0.94f) + Vector3.right * (s * 0.15f),
                        Hp(1.52f, 0.74f) + Vector3.right * (s * 0.2f), Hp(1.58f, 0.6f) + Vector3.right * (s * 0.22f),
                    },
                    new[] { new Vector2(0.055f, 0.055f), new Vector2(0.042f, 0.042f), new Vector2(0.022f, 0.022f), new Vector2(0.002f, 0.002f) },
                    8, 2f);
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

            // Круглые щиты по бортам: доски, красно-кремовые четверти, железные умбон и обод. Щит лежит в касательной
            // плоскости обшивки: борт здесь отклонён наружу (~20°) и сужается к штевням, а плоский щит, повёрнутый
            // по касательной к выпуклому борту, нигде не уходит в доски. Зазор — на толщину обода.
            foreach (float z in new[] { -2.55f, 2.45f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float t = TAtHeight(z, GunwaleAt(z) - 0.62f);
                    Vector3 p = Section(z, t, side);
                    Vector3 n = SurfaceNormal(z, t, side);
                    Shield(p + n * 0.05f, Quaternion.LookRotation(n, Vector3.up), 0.45f);
                }
            }
        }

        /// <summary>Наружная нормаль обшивки в точке (z, t) — по производным сечения.</summary>
        private static Vector3 SurfaceNormal(float z, float t, float side)
        {
            const float e = 0.01f;
            Vector3 dz = Section(z + e, t, side) - Section(z - e, t, side);
            Vector3 dt = Section(z, Mathf.Min(1f, t + e), side) - Section(z, Mathf.Max(0f, t - e), side);
            Vector3 n = Vector3.Cross(dt, dz).normalized;
            return n.x * side < 0f ? -n : n;
        }

        // ------------------------------------------------------------------ купол и оснастка

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
            float top = AddLanternBody(bowlBase, 1f);
            // Подвес к куполу: четыре стропы от шапки.
            float envBottom = EnvelopeBottomY;
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
                    AddCrankPost(seat);
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
            var pivot = new Vector3(0f, 0.75f, -HalfLength - 0.55f);
            Propellers.Add(new PropellerSpec { Pivot = pivot, Rotation = Quaternion.identity });
            Iron.AddTube(new[] { new Vector3(0f, 0.75f, -HalfLength + 0.6f), pivot + new Vector3(0f, 0f, 0.1f) }, 0.045f, 8, 3f);
            Iron.AddTube(new[] { new Vector3(0f, 1.5f, -HalfLength - 0.11f), pivot + new Vector3(0f, 0.05f, 0.16f) }, 0.03f, 6, 3f);
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
