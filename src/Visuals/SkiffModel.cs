using System.Collections.Generic;
using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>
    /// Геометрия «Небесного ялика» по черновому макету «A»: короткая пузатая лодка-люлька 3.4 × 1.5 м (обшивка досками,
    /// подъём киля и планширя к штевням), на форштевне — резной деревянный ворон, по щиту на борт. Купол — бело-синий
    /// «мяч» ⌀2.6 × 6.2 м, стропы веером от поясного каната к планширю. Фонарь огня висит на цепях под куполом, без стойки:
    /// под ним проходят к сундуку на носу; у огня держатся за стропу. На корме — перо руля с изогнутой руль-палкой,
    /// сиденье пилота с кривошипом перед ним и два маленьких винта на выносных балках по углам кормы.
    /// Корень — низ киля посередине, +Z — нос, +X — правый борт. Только меши и точки — без Unity-сцены.
    /// </summary>
    internal sealed class SkiffModel : ShipModel
    {
        public const float HalfLength = 1.7f;
        public const float HalfBeam = 0.75f;
        public const float GunwaleY = 1.15f;
        public const float Sheer = 0.38f;
        public const float Rocker = 0.25f;
        public const float DeckY = 0.45f;
        public const float WallT = 0.05f;

        /// <summary>Осадка на воде, м: ватерлиния на 0.33 над низом киля, палуба — на 0.12 выше воды.</summary>
        public const float Draft = 0.33f;

        /// <summary>Низ чаши фонаря: 2.1 м над палубой — под ним проходят, не пригибаясь.</summary>
        public const float LanternY = DeckY + 2.1f;

        /// <summary>Фонарь ялика — фонарь драккара в этом масштабе.</summary>
        private const float LanternScale = 0.75f;

        /// <summary>Стропы купола веером, по пять на борт: где они на планшире и где на поясном канате (z).</summary>
        private static readonly float[] s_rigGunwaleZ = { -1.05f, -0.52f, 0f, 0.52f, 1.05f };
        private static readonly float[] s_rigWaistZ = { -2f, -1f, 0f, 1f, 2f };

        /// <summary>Площадка ворона на верху форштевня — где стоят его лапы.</summary>
        private static readonly Vector3 RavenPerch = new Vector3(0f, 1.965f, 1.875f);

        /// <summary>У огня держатся левой рукой за эту стропу левого борта.</summary>
        private const int GripRope = 3;

        private SkiffModel() : base(envelopeCenterY: 4.65f, envelopeRadius: 1.3f, envelopeHalfLength: 3.08f, stripes: 9)
        {
            EnvelopeCapLength = 0.09f;
            EnvelopeTipLength = 0.05f;
        }

        public static SkiffModel Build()
        {
            var m = new SkiffModel();
            m.BuildHull();
            m.BuildDeck();
            m.BuildWaterMask(InnerStations(), z => GunwaleAt(z) - 0.035f, InnerHalfWidth);
            m.BuildStems();
            m.BuildRaven(RavenPerch);
            m.BuildShields();
            m.BuildEnvelope();
            m.BuildRigging();
            m.BuildLantern();
            m.BuildStern();
            m.BuildPropellers();
            m.BuildLadders();
            m.BuildColliders();
            // Сундук на 8 ячеек у носа, лицом к корме: поменьше — в узком носу ему как раз.
            m.ChestPosition = new Vector3(0f, DeckY, 0.85f);
            m.ChestScale = 0.5f;
            const float bz = 1.3f;
            m.AnchorOutward = Vector3.left;
            m.AnchorRopeStart = new Vector3(-(HalfBeamAt(bz) - 0.04f), GunwaleAt(bz) + 0.04f, bz);
            m.BuildCrankMesh();
            m.BuildHelmSeat();
            m.AddCrankPost(m.HelmPosition);
            m.PilotCrank = true;
            m.SetOnboard(DeckY, HalfLength, HalfBeam, HalfBeamAt);
            return m;
        }

        // ------------------------------------------------------------------ обводы корпуса

        /// <summary>Полуширина корпуса по планширю: пузатый посередине, к штевням сходится остриём.</summary>
        public static float HalfBeamAt(float z)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / HalfLength);
            return HalfBeam * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(a, 2.2f)));
        }

        /// <summary>Линия киля: плавно поднимается к штевням.</summary>
        public static float KeelAt(float z)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / HalfLength);
            return Rocker * a * a;
        }

        /// <summary>Линия планширя: к штевням выше.</summary>
        public static float GunwaleAt(float z)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / HalfLength);
            return GunwaleY + Sheer * a * a;
        }

        /// <summary>Точка обшивки: t = 0 — киль, t = 1 — планширь; side = ±1 — борт. Сечение — «U»: плоское днище, отвесный верх борта.</summary>
        public static Vector3 Section(float z, float t, float side)
        {
            float k = KeelAt(z);
            float x = HalfBeamAt(z) * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f), 0.6f);
            float y = k + (GunwaleAt(z) - k) * Mathf.Pow(Mathf.Clamp01(t), 1.6f);
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
            return Mathf.Pow(Mathf.Clamp01((y - k) / (g - k)), 1f / 1.6f);
        }

        /// <summary>Внутренняя полуширина корпуса на высоте y (за вычетом толщины обшивки).</summary>
        public static float InnerHalfWidth(float z, float y)
        {
            return Mathf.Max(0f, Mathf.Abs(Section(z, TAtHeight(z, y), 1f).x) - WallT);
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

        private static Vector3 HullOutward(Vector3 p)
        {
            return new Vector3(p.x, p.y - (DeckY + 0.3f), p.z * 0.3f);
        }

        /// <summary>Точки по длине корпуса гуще к штевням: там обшивка круто сходится.</summary>
        private static float LengthAt(float u, float halfLength)
        {
            return halfLength * Mathf.Sin(Mathf.Clamp(u, -1f, 1f) * Mathf.PI * 0.5f);
        }

        /// <summary>Станции внутренней обшивки и палубы (общие): от штевня до штевня — киль везде ниже палубы.</summary>
        private static List<float> InnerStations() => Stations(HalfLength, 40);

        private void BuildHull()
        {
            const int nz = 48;
            const int nt = 14;
            const float uvScale = 1f / 1.2f;
            // V — доля обвода от киля к планширю (одна и та же доска на всей длине, сходится к штевням).
            const float girth = 1.6f;
            foreach (float side in new[] { -1f, 1f })
            {
                var pts = new Vector3[nz + 1, nt + 1];
                var uvs = new Vector2[nz + 1, nt + 1];
                for (int i = 0; i <= nz; i++)
                {
                    float z = LengthAt(i / (float)nz * 2f - 1f, HalfLength);
                    for (int j = 0; j <= nt; j++)
                    {
                        float t = j / (float)nt;
                        pts[i, j] = Section(z, t, side);
                        uvs[i, j] = new Vector2(z * uvScale / 2f, t * girth * uvScale);
                    }
                }
                Hull.AddGrid(pts, uvs, HullOutward, doubleSided: false);

                // Изнутри: от палубы до планширя и до самых штевней (иначе изнутри видно сквозь обшивку: она односторонняя).
                List<float> stations = InnerStations();
                var ipts = new Vector3[stations.Count, 6];
                var iuvs = new Vector2[stations.Count, 6];
                for (int i = 0; i < stations.Count; i++)
                {
                    float z = stations[i];
                    float t0 = TAtHeight(z, DeckY);
                    for (int j = 0; j <= 5; j++)
                    {
                        float t = Mathf.Lerp(t0, 1f, j / 5f);
                        Vector3 p = Section(z, t, side);
                        p.x = side * Mathf.Max(0f, Mathf.Abs(p.x) - WallT);
                        ipts[i, j] = p;
                        iuvs[i, j] = new Vector2(z * uvScale / 2f, t * girth * uvScale);
                    }
                }
                Hull.AddGrid(ipts, iuvs, p => new Vector3(-p.x, 0.2f, 0f), doubleSided: false);

                // Планширь — брус по верху борта, до самых штевней.
                var gun = new List<Vector3>();
                for (int i = 0; i <= 36; i++)
                {
                    Vector3 p = Section(LengthAt(i / 36f * 2f - 1f, HalfLength), 1f, side);
                    p.x = side * Mathf.Max(0f, Mathf.Abs(p.x) - WallT * 0.5f);
                    p.y += 0.025f;
                    gun.Add(p);
                }
                Wood.AddTube(gun, 0.045f, 8, 1f);

                // Привальный брус вдоль борта.
                var wale = new List<Vector3>();
                for (int i = 0; i <= 36; i++)
                {
                    float z = LengthAt(i / 36f * 2f - 1f, HalfLength - 0.04f);
                    wale.Add(Section(z, 0.72f, side) + SurfaceNormal(z, 0.72f, side) * 0.012f);
                }
                Wood.AddTube(wale, 0.028f, 6, 1f);

                // Шпангоуты изнутри — от палубы до планширя.
                foreach (float z in new[] { -1.1f, -0.55f, 0f, 0.55f, 1.1f })
                {
                    var rib = new List<Vector3>();
                    float t0 = TAtHeight(z, DeckY);
                    for (int j = 0; j <= 5; j++)
                    {
                        Vector3 p = Section(z, Mathf.Lerp(t0, 0.97f, j / 5f), side);
                        p.x = side * (Mathf.Abs(p.x) - WallT - 0.02f);
                        rib.Add(p);
                    }
                    Wood.AddTube(rib, 0.024f, 6, 2f);
                }
            }

            // Киль — брус по низу.
            var keel = new List<Vector3>();
            for (int i = 0; i <= 24; i++)
            {
                float z = Mathf.Lerp(-HalfLength + 0.12f, HalfLength - 0.12f, i / 24f);
                keel.Add(new Vector3(0f, KeelAt(z) + 0.02f, z));
            }
            Wood.AddTube(keel, 0.05f, 8, 1f);
        }

        // ------------------------------------------------------------------ палуба

        private void BuildDeck()
        {
            // Палуба — от штевня до штевня по тем же станциям, что и внутренняя обшивка: на носу и корме сужается в точку.
            const int nx = 6;
            List<float> stations = InnerStations();
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
                    uvs[i, j] = new Vector2(z / 2.4f, x / 1.2f + 0.3f);
                }
            }
            Deck.AddGrid(pts, uvs, Vector3.up, doubleSided: false);
        }

        // ------------------------------------------------------------------ штевни

        private void BuildStems()
        {
            // Форштевень: по килю к носу, вверх по ребру, где сходятся борта, и выше планширя — с наклоном вперёд,
            // к площадке ворона. Сечение узкое поперёк и глубокое вдоль, как у настоящего штевня.
            Wood.AddLoft(new[]
                {
                    new Vector3(0f, KeelAt(1.3f) + 0.03f, 1.3f), new Vector3(0f, KeelAt(1.58f) + 0.03f, 1.58f),
                    new Vector3(0f, KeelAt(HalfLength) + 0.07f, 1.705f), new Vector3(0f, 0.8f, 1.72f), new Vector3(0f, 1.3f, 1.735f),
                    new Vector3(0f, GunwaleAt(HalfLength) + 0.02f, 1.755f), new Vector3(0f, 1.74f, 1.795f), new Vector3(0f, 1.86f, 1.845f),
                    new Vector3(0f, 1.925f, 1.87f), new Vector3(0f, 1.935f, 1.873f),
                },
                new[]
                {
                    new Vector2(0.05f, 0.05f), new Vector2(0.05f, 0.06f), new Vector2(0.05f, 0.07f), new Vector2(0.05f, 0.07f), new Vector2(0.05f, 0.07f),
                    new Vector2(0.055f, 0.07f), new Vector2(0.06f, 0.07f), new Vector2(0.065f, 0.072f), new Vector2(0.06f, 0.065f), new Vector2(0.002f, 0.002f),
                },
                10, 1.5f);
            // Площадка ворона на верху форштевня.
            Wood.AddCylinder(new Vector3(0f, RavenPerch.y - 0.05f, RavenPerch.z), 0.1f, 0.05f, 12, 2f);

            // Ахтерштевень — пониже: за ним висит руль, над ним проходит руль-палка.
            Wood.AddLoft(new[]
                {
                    new Vector3(0f, KeelAt(-1.3f) + 0.03f, -1.3f), new Vector3(0f, KeelAt(-1.58f) + 0.03f, -1.58f),
                    new Vector3(0f, KeelAt(-HalfLength) + 0.07f, -1.705f), new Vector3(0f, 0.8f, -1.72f), new Vector3(0f, 1.3f, -1.735f),
                    new Vector3(0f, GunwaleAt(-HalfLength) + 0.02f, -1.75f), new Vector3(0f, 1.585f, -1.755f), new Vector3(0f, 1.6f, -1.757f),
                },
                new[]
                {
                    new Vector2(0.05f, 0.05f), new Vector2(0.05f, 0.06f), new Vector2(0.05f, 0.065f), new Vector2(0.05f, 0.065f),
                    new Vector2(0.05f, 0.065f), new Vector2(0.055f, 0.065f), new Vector2(0.045f, 0.05f), new Vector2(0.002f, 0.002f),
                },
                10, 1.5f);
        }

        // ------------------------------------------------------------------ ворон на форштевне

        /// <summary>
        /// Резной ворон, сидящий на форштевне (perch — верх штевня), клювом вперёд: тело, шея, голова и тяжёлый клюв —
        /// одна поверхность (труба переменного сечения), сложенные крылья по бокам, хвост клином, лапы с когтями.
        /// </summary>
        private void BuildRaven(Vector3 perch)
        {
            const float k = 1.25f;
            Vector3 P(float x, float y, float z) => perch + new Vector3(x, y, z) * k;
            Vector2 R(float x, float y) => new Vector2(x, y) * k;

            // Корень хвоста → гузка → спина → грудь (шире всего) → шея → затылок → голова → тяжёлый клюв, загнутый вниз.
            // Сидит низко, грудью почти на площадке.
            Wood.AddLoft(new[]
                {
                    P(0f, 0.1f, -0.25f), P(0f, 0.125f, -0.18f), P(0f, 0.17f, -0.07f), P(0f, 0.225f, 0.04f), P(0f, 0.29f, 0.12f),
                    P(0f, 0.36f, 0.17f), P(0f, 0.41f, 0.205f), P(0f, 0.45f, 0.26f), P(0f, 0.455f, 0.32f), P(0f, 0.445f, 0.37f),
                    P(0f, 0.425f, 0.43f), P(0f, 0.395f, 0.49f), P(0f, 0.37f, 0.52f),
                },
                new[]
                {
                    R(0.055f, 0.045f), R(0.12f, 0.1f), R(0.16f, 0.14f), R(0.17f, 0.155f), R(0.135f, 0.13f), R(0.095f, 0.098f),
                    R(0.097f, 0.102f), R(0.102f, 0.098f), R(0.078f, 0.075f), R(0.055f, 0.06f), R(0.038f, 0.045f), R(0.018f, 0.024f),
                    R(0.003f, 0.004f),
                },
                12, 2f);
            foreach (float s in new[] { -1f, 1f })
            {
                // Глаза.
                Iron.AddSphere(P(s * 0.075f, 0.468f, 0.3f), Vector3.one * (0.02f * k), 8, 5);
                // Сложенные крылья: от плеча вдоль боков назад, концы перьев заходят на хвост.
                Wood.AddLoft(new[] { P(s * 0.12f, 0.29f, 0.1f), P(s * 0.15f, 0.22f, -0.02f), P(s * 0.14f, 0.15f, -0.17f), P(s * 0.1f, 0.09f, -0.33f), P(s * 0.055f, 0.05f, -0.45f) },
                    new[] { R(0.035f, 0.08f), R(0.04f, 0.1f), R(0.032f, 0.085f), R(0.018f, 0.055f), R(0.003f, 0.014f) },
                    8, 2f);
                // Короткие лапы и когти на площадке штевня.
                Wood.AddTube(new[] { P(s * 0.05f, 0.12f, 0.06f), P(s * 0.05f, 0.06f, 0.055f), P(s * 0.05f, 0.01f, 0.05f) }, 0.017f * k, 6, 2f);
                foreach (float a in new[] { -25f, 0f, 25f })
                {
                    Quaternion r = Quaternion.Euler(0f, a, 0f);
                    Wood.AddBox(P(s * 0.05f, 0.008f, 0.05f) + r * new Vector3(0f, 0f, 0.035f * k), new Vector3(0.014f, 0.014f, 0.07f) * k, r, 2f);
                }
                Wood.AddBox(P(s * 0.05f, 0.008f, 0.02f), new Vector3(0.014f, 0.014f, 0.05f) * k, 2f);
            }
            // Хвост клином: пять перьев веером, средние длиннее и лежат сверху.
            Vector3 root = P(0f, 0.105f, -0.24f);
            Vector3 dir = new Vector3(0f, -0.25f, -0.968f).normalized;
            Vector3 up = Vector3.Cross(dir, Vector3.right).normalized;
            if (up.y < 0f)
            {
                up = -up;
            }
            for (int i = -2; i <= 2; i++)
            {
                Quaternion fan = Quaternion.AngleAxis(i * 11f, up);
                Vector3 d = fan * dir;
                float len = (0.36f - 0.04f * Mathf.Abs(i)) * k;
                Wood.AddBox(root + d * (len * 0.5f) + up * (0.009f * k * (2 - Mathf.Abs(i))), new Vector3(0.07f, 0.016f, len) * k, Quaternion.LookRotation(d, up), 2f);
            }
        }

        // ------------------------------------------------------------------ щиты

        private void BuildShields()
        {
            // По щиту на борт (синие с белым) — в касательной плоскости обшивки: борт выпуклый, и плоский щит,
            // повёрнутый по касательной, нигде не уходит в доски. Зазор — на толщину обода.
            const float z = 0.35f;
            foreach (float side in new[] { -1f, 1f })
            {
                float t = TAtHeight(z, GunwaleAt(z) - 0.38f);
                Vector3 p = Section(z, t, side);
                Vector3 n = SurfaceNormal(z, t, side);
                Shield(p + n * 0.05f, Quaternion.LookRotation(n, Vector3.up), 0.27f);
            }
        }

        // ------------------------------------------------------------------ стропы

        private Vector3 RigLow(int k, float side)
        {
            Vector3 p = Section(s_rigGunwaleZ[k], 1f, side);
            p.x -= side * WallT * 0.5f;
            return p + Vector3.up * 0.075f;
        }

        private Vector3 RigHigh(int k, float side)
        {
            return WaistPoint(s_rigWaistZ[k], side) - new Vector3(side * 0.02f, 0.03f, 0f);
        }

        private void BuildRigging()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                // Поясной канат вдоль купола.
                var waist = new List<Vector3>();
                for (int i = 0; i <= 40; i++)
                {
                    waist.Add(WaistPoint(Mathf.Lerp(-2.9f, 2.9f, i / 40f), side));
                }
                Rope.AddTube(waist, 0.04f, 8, 2.5f);

                // Стропы веером: от железных проушин на планшире к узлам на поясе.
                for (int k = 0; k < s_rigGunwaleZ.Length; k++)
                {
                    Vector3 low = RigLow(k, side);
                    Rope.AddTube(new[] { low, RigHigh(k, side) }, 0.026f, 6, 2.5f);
                    Rope.AddSphere(WaistPoint(s_rigWaistZ[k], side), new Vector3(0.07f, 0.07f, 0.07f), 8, 5);
                    Iron.AddTorus(low - Vector3.up * 0.035f, Quaternion.Euler(0f, 0f, 90f), 0.035f, 0.035f, 0.011f, 0.011f, 12, 5, 3f);
                }
            }

            // У огня стоят посередине, лицом к носу, и держатся левой рукой за стропу левого борта
            // (поза «держаться за мачту»: рука — на 0.73 м левее и 0.47 м впереди, на высоте плеча).
            Vector3 a = RigLow(GripRope, -1f);
            Vector3 b = RigHigh(GripRope, -1f);
            float handY = DeckY + 1.35f;
            Vector3 grip = Vector3.Lerp(a, b, (handY - a.y) / (b.y - a.y));
            FireOperatorYaw = 0f;
            FireOperatorPosition = new Vector3(grip.x, DeckY, grip.z) - Quaternion.Euler(0f, FireOperatorYaw, 0f) * MediumBalloonModel.MastGripOffset;
        }

        // ------------------------------------------------------------------ фонарь огня на цепях

        /// <summary>Точка на нижней стороне купола над (x, z).</summary>
        private Vector3 EnvelopeUnderside(float x, float z)
        {
            float r = EnvelopeRadiusAt(z);
            return new Vector3(x, EnvelopeCenterY - Mathf.Sqrt(Mathf.Max(0f, r * r - x * x)), z);
        }

        private void BuildLantern()
        {
            float bowlBase = LanternY;
            float top = AddLanternBody(bowlBase, LanternScale);
            float capTop = top + 0.17f * LanternScale;
            // Подвес: четыре цепи от ушек на краю шапки к проушинам под куполом.
            float rim = 0.315f * LanternScale + 0.012f;
            for (int k = 0; k < 4; k++)
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 ear = d * rim + Vector3.up * (top + 0.012f);
                Iron.AddTorus(ear, Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0f, 0f, 90f), 0.018f, 0.018f, 0.007f, 0.007f, 10, 4, 3f);
                Vector3 eye = EnvelopeUnderside(d.x * 0.32f, d.z * 0.32f);
                Iron.AddSphere(eye, new Vector3(0.04f, 0.025f, 0.04f), 8, 5);
                Chain(ear + Vector3.up * 0.02f, eye - Vector3.up * 0.015f);
            }

            FirePosition = new Vector3(0f, bowlBase + 0.06f, 0f);
            FireScale = 0.7f * LanternScale;
            BurnerColliderCenter = new Vector3(0f, (bowlBase - 0.05f + capTop + 0.05f) * 0.5f, 0f);
            BurnerColliderSize = new Vector3(0.62f, capTop + 0.1f - bowlBase, 0.62f);
        }

        /// <summary>Железная цепь от точки к точке: звенья по очереди повёрнуты на 90° вокруг цепи.</summary>
        private void Chain(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.01f)
            {
                return;
            }
            Vector3 dir = d / len;
            Quaternion look = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up);
            int links = Mathf.Max(2, Mathf.RoundToInt(len / 0.045f));
            for (int i = 0; i < links; i++)
            {
                Vector3 c = Vector3.Lerp(from, to, (i + 0.5f) / links);
                Quaternion r = look * Quaternion.Euler(0f, 0f, (i & 1) == 0 ? 0f : 90f);
                Iron.AddTorus(c, r, 0.016f, 0.028f, 0.0065f, 0.0065f, 10, 4, 3f);
            }
        }

        // ------------------------------------------------------------------ корма: руль, руль-палка, пилот

        private void BuildStern()
        {
            // Баллер руля — сразу за ахтерштевнем; перо — позади, изогнутая руль-палка — над ахтерштевнем
            // вперёд и вниз, к пилоту.
            RudderPivot = new Vector3(0f, 1.15f, -1.88f);
            RudderWood.AddCylinder(new Vector3(0f, -0.88f, 0f), 0.04f, 1.45f, 10, 2f);
            // Перо — плавником: от баллера назад и вниз, шире всего внизу, края закруглены.
            RudderWood.AddLoft(new[]
                {
                    new Vector3(0f, -0.25f, -0.06f), new Vector3(0f, -0.3f, -0.12f), new Vector3(0f, -0.45f, -0.155f), new Vector3(0f, -0.62f, -0.18f),
                    new Vector3(0f, -0.75f, -0.19f), new Vector3(0f, -0.83f, -0.18f), new Vector3(0f, -0.87f, -0.165f), new Vector3(0f, -0.885f, -0.15f),
                },
                new[]
                {
                    new Vector2(0.004f, 0.004f), new Vector2(0.09f, 0.022f), new Vector2(0.13f, 0.026f), new Vector2(0.155f, 0.026f),
                    new Vector2(0.15f, 0.024f), new Vector2(0.11f, 0.02f), new Vector2(0.05f, 0.012f), new Vector2(0.003f, 0.003f),
                },
                10, 1.5f);
            foreach (float y in new[] { -0.45f, -0.74f })
            {
                RudderIron.AddBox(new Vector3(0f, y, -0.06f), new Vector3(0.065f, 0.035f, 0.14f), 3f);
            }
            Vector3 handle = new Vector3(0.24f, DeckY + 0.8f, -1.34f) - RudderPivot;
            RudderWood.AddTube(new[]
            {
                new Vector3(0f, 0.5f, 0f), new Vector3(0f, 0.55f, 0.13f), new Vector3(0.06f, 0.5f, 0.28f), new Vector3(0.16f, 0.33f, 0.43f), handle,
            }, 0.032f, 8, 1f);
            RudderWood.AddSphere(handle + new Vector3(0.025f, -0.08f, 0.035f), new Vector3(0.045f, 0.06f, 0.045f), 8, 5);
            RudderIron.AddTorus(new Vector3(0f, 0.5f, 0f), Quaternion.identity, 0.045f, 0.045f, 0.012f, 0.03f, 12, 5, 3f);
            // Петли баллера на ахтерштевне (неподвижно).
            foreach (float y in new[] { 0.55f, 1.2f })
            {
                Iron.AddBox(new Vector3(0f, y, -1.835f), new Vector3(0.1f, 0.04f, 0.12f), 3f);
            }

            // Пилот сидит лицом к носу, руль-палка — у него за спиной справа.
            HelmYaw = 0f;
            HelmPosition = new Vector3(0f, DeckY, -0.95f);
            TillerColliderCenter = new Vector3(0.1f, DeckY + 0.95f, -1.42f);
            TillerColliderSize = new Vector3(0.6f, 1.0f, 0.7f);
            RudderVisualAngle = -12f;
        }

        // ------------------------------------------------------------------ винты на выносных балках

        private void BuildPropellers()
        {
            foreach (float side in new[] { -1f, 1f })
            {
                // Винт левого борта развёрнут: крутятся навстречу друг другу, как отражение.
                var hub = new Vector3(side * 0.98f, 0.82f, -1.66f);
                Propellers.Add(new PropellerSpec { Pivot = hub, Rotation = side > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f) });

                // Подшипник на конце балок, вал к ступице и обтекатель позади.
                Vector3 bearing = hub + new Vector3(0f, 0f, 0.2f);
                Iron.AddTube(new[] { bearing + new Vector3(0f, 0f, 0.08f), bearing - new Vector3(0f, 0f, 0.08f) }, 0.048f, 10, 3f);
                Iron.AddTube(new[] { bearing, hub }, 0.02f, 8, 3f);
                var cone = new List<Vector2> { new Vector2(0.045f, 0f), new Vector2(0.035f, 0.04f), new Vector2(0.015f, 0.075f), new Vector2(0.001f, 0.085f) };
                int f = Iron.VertexCount;
                Iron.AddLathe(cone, 10, 1f, 3f, 0f);
                Iron.Transform(f, Quaternion.Euler(-90f, 0f, 0f), hub - new Vector3(0f, 0f, 0.05f));

                // Выносные балки — верхняя от планширя, нижняя от борта, сходятся у подшипника; железные хомуты.
                Vector3 top = Section(-1.2f, 1f, side) + new Vector3(-side * WallT * 0.5f, 0.02f, 0f);
                float lowT = TAtHeight(-1.12f, 0.56f);
                Vector3 low = Section(-1.12f, lowT, side) - SurfaceNormal(-1.12f, lowT, side) * 0.01f;
                Wood.AddTube(new[] { top, bearing + new Vector3(-side * 0.02f, 0.05f, 0f) }, 0.034f, 8, 2f);
                Wood.AddTube(new[] { low, bearing + new Vector3(-side * 0.02f, -0.05f, 0f) }, 0.034f, 8, 2f);
                Iron.AddTorus(bearing, Quaternion.Euler(90f, 0f, 0f), 0.062f, 0.062f, 0.012f, 0.03f, 12, 5, 3f);
                Iron.AddSphere(low, new Vector3(0.05f, 0.05f, 0.05f), 8, 5);
            }

            // Винт: ступица и три лопасти с шагом 25° (вокруг своей оси Z), сужаются к закруглённым концам.
            PropellerIron.AddSphere(Vector3.zero, new Vector3(0.055f, 0.055f, 0.065f), 10, 6);
            for (int k = 0; k < 3; k++)
            {
                int f = PropellerWood.VertexCount;
                PropellerWood.AddLoft(new[] { new Vector3(0f, 0.035f, 0f), new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0.27f, 0f), new Vector3(0f, 0.305f, 0f), new Vector3(0f, 0.315f, 0f) },
                    new[] { new Vector2(0.014f, 0.03f), new Vector2(0.013f, 0.055f), new Vector2(0.011f, 0.06f), new Vector2(0.009f, 0.05f), new Vector2(0.006f, 0.03f), new Vector2(0.002f, 0.008f) },
                    8, 2f);
                PropellerWood.Transform(f, Quaternion.Euler(0f, 0f, k * 120f) * Quaternion.Euler(0f, 25f, 0f), Vector3.zero);
            }
            PropellerReach = 0.45f;
        }

        // ------------------------------------------------------------------ трапы

        private void BuildLadders()
        {
            const float z = -0.25f;
            foreach (float side in new[] { -1f, 1f })
            {
                // Две тетивы по обшивке от скулы до планширя и перекладины между ними.
                var railA = new List<Vector3>();
                var railB = new List<Vector3>();
                for (int j = 4; j <= 12; j++)
                {
                    float t = j / 12f;
                    Vector3 p = Section(z, t, side) + SurfaceNormal(z, t, side) * 0.06f;
                    railA.Add(p + new Vector3(0f, 0f, -0.17f));
                    railB.Add(p + new Vector3(0f, 0f, 0.17f));
                }
                Wood.AddTube(railA, 0.03f, 6, 2f);
                Wood.AddTube(railB, 0.03f, 6, 2f);
                for (int j = 5; j <= 12; j += 2)
                {
                    float t = j / 12f;
                    Vector3 c = Section(z, t, side) + SurfaceNormal(z, t, side) * 0.07f;
                    Wood.AddTube(new[] { c + new Vector3(0f, 0f, -0.17f), c + new Vector3(0f, 0f, 0.17f) }, 0.024f, 6, 2f);
                }
                Vector3 mid = Section(z, 0.65f, side) + SurfaceNormal(z, 0.65f, side) * 0.12f;
                Ladders.Add(new LadderSpec
                {
                    Center = mid,
                    Size = new Vector3(0.35f, 1.1f, 0.5f),
                    Yaw = 0f,
                    Target = new Vector3(side * 0.35f, DeckY + 0.05f, z),
                    TargetYaw = side > 0f ? -90f : 90f,
                });
            }
        }

        // ------------------------------------------------------------------ коллайдеры

        private void BuildColliders()
        {
            // Корпус ниже палубы — выпуклый: точки обшивки от киля до уровня палубы (киль везде ниже палубы).
            // Точек немного и без повторов, чтобы выпуклая оболочка уложилась в предел PhysX в 255 граней.
            for (int i = 0; i <= 12; i++)
            {
                float z = Mathf.Lerp(-HalfLength + 0.08f, HalfLength - 0.08f, i / 12f);
                float t0 = TAtHeight(z, DeckY);
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int j = side < 0f ? 0 : 1; j <= 3; j++)
                    {
                        HullColliderPoints.Add(Section(z, t0 * j / 3f, side));
                    }
                    HullColliderPoints.Add(new Vector3(side * InnerHalfWidth(z, DeckY), DeckY, z));
                }
            }

            // Борта — наклонные доски от палубы до планширя.
            const int segments = 8;
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < segments; i++)
                {
                    float z0 = Mathf.Lerp(-HalfLength + 0.15f, HalfLength - 0.15f, i / (float)segments);
                    float z1 = Mathf.Lerp(-HalfLength + 0.15f, HalfLength - 0.15f, (i + 1) / (float)segments);
                    float zm = (z0 + z1) * 0.5f;
                    float yb = DeckY;
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
