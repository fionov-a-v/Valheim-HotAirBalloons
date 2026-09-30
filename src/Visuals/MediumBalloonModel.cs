using System.Collections.Generic;
using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>Сиденье: точка на полу под доской скамьи и куда смотрит сидящий.</summary>
    internal struct SeatSpec
    {
        public Vector3 Position;
        public float Yaw;
    }

    /// <summary>
    /// Боковой парус-веер, как на эскизе: три реи-«пальца» из оси крепления на борту (верхняя, средняя, нижняя),
    /// между ними льняные перепонки, поперёк — планка; реи и планка лежат на полотне с лицевой стороны.
    /// Все меши — в плоскости веера: начало — ось крепления, X — наружу от борта, Y — вверх, Z — вперёд по ходу
    /// (для левого паруса веер развёрнут на 180°, поэтому его «вперёд» — это −Z и лицевая сторона строится с обратным знаком).
    /// Складывается, как пальцы с перепонками: верхняя и нижняя реи поворачиваются к средней целиком,
    /// полотно и планка сжимаются между ними (<see cref="SailFold"/>).
    /// </summary>
    internal sealed class SailParts
    {
        public Vector3 Pivot;
        public float BaseYaw;

        public float UpperAngle = 30f;
        public float MiddleAngle = -4f;
        public float LowerAngle = -27f;

        public readonly MeshBuilder Cloth = new MeshBuilder();
        public readonly MeshBuilder Batten = new MeshBuilder();
        public readonly MeshBuilder Hub = new MeshBuilder();
        public readonly MeshBuilder MiddleWood = new MeshBuilder();
        public readonly MeshBuilder MiddleRope = new MeshBuilder();
        public readonly MeshBuilder UpperWood = new MeshBuilder();
        public readonly MeshBuilder UpperRope = new MeshBuilder();
        public readonly MeshBuilder LowerWood = new MeshBuilder();
        public readonly MeshBuilder LowerRope = new MeshBuilder();
    }

    /// <summary>
    /// Геометрия среднего шара по эскизу «Грейд II · 3 персонажа»: льняной купол почти шаром под восемью канатами-меридианами
    /// с колодками (у экватора каждый расходится на две стропы вниз), железное кольцо горловины, под ним фонарь-чаша огня
    /// на центральном столбе, круглая дощатая кадка с железными полосами и перилами на столбиках, скамья на троих,
    /// руль-палка (вместо штурвала эскиза) и два паруса-веера по бортам.
    /// Корень — центр дна кадки, +Z — вперёд (по ходу), +X — вправо. Только меши и точки — без Unity-сцены.
    /// </summary>
    internal sealed class MediumBalloonModel
    {
        public const float FloorTop = 0.22f;
        public const float RimTop = 1.26f;
        public const float BandBottom = 1.12f;
        public const float BandOuter = 1.83f;
        public const float BandInner = 1.71f;
        public const float WallThickness = 0.085f;
        public const float PostRadius = 1.77f;
        public const float RailY = 1.58f;
        public const float PostTop = 1.78f;
        public const int Meridians = 8;
        public const float EnvelopeRadius = 2.3f;
        public const float MouthY = 3.40f;
        public const float FloorColliderRadius = 1.28f;
        public const float SailPivotY = 1.22f;

        // Кадка снаружи: плоское дно, скруглённая кромка и чуть выпуклый борт (кривая Безье) до обода.
        private const float BottomFlat = 1.18f;
        private const float EdgeRadius = 0.16f;
        private static readonly Vector2 s_wall0 = new Vector2(BottomFlat + EdgeRadius, EdgeRadius);
        private static readonly Vector2 s_wall1 = new Vector2(1.60f, 0.52f);
        private static readonly Vector2 s_wall2 = new Vector2(1.79f, BandBottom);

        public readonly MeshBuilder Planks = new MeshBuilder();
        public readonly MeshBuilder Wood = new MeshBuilder();
        public readonly MeshBuilder Iron = new MeshBuilder();
        public readonly MeshBuilder Rope = new MeshBuilder();
        public readonly MeshBuilder Envelope = new MeshBuilder();

        public readonly SailParts[] Sails = { new SailParts(), new SailParts() };

        /// <summary>Руль-палка: меши в системе оси качания (наклоняется вбок вокруг Z).</summary>
        public readonly MeshBuilder LeverWood = new MeshBuilder();
        public readonly MeshBuilder LeverIron = new MeshBuilder();
        public readonly MeshBuilder LeverRope = new MeshBuilder();
        public Vector3 LeverPivot;

        /// <summary>Наклон руль-палки назад, к рулевому, градусы.</summary>
        public const float LeverLean = 10f;

        public EnvelopeProfile Profile;
        public float EnvelopeBaseY = MouthY;

        /// <summary>Доля длины профиля купола (от горловины), на которой экватор, — для шва на текстуре.</summary>
        public float EquatorV;

        public Vector3 FirePosition;
        public float FireScale = 0.85f;

        /// <summary>У огня: стоит справа-сзади от столба и держится за него левой рукой (поза «за мачту», как у ванильного Karve).</summary>
        public Vector3 FireOperatorPosition;
        public float FireOperatorYaw;

        /// <summary>Рулевой: стоит у кормы лицом вперёд, руль-палка у него слева-спереди.</summary>
        public Vector3 HelmPosition = new Vector3(-0.2f, FloorTop, -0.95f);
        public float HelmYaw;

        public Vector3 BurnerColliderCenter;
        public Vector3 BurnerColliderSize;
        public Vector3 TillerColliderCenter;
        public Vector3 TillerColliderSize;

        public Vector3 AnchorRopeStart;
        public Vector3 AnchorOutward;

        public readonly List<SeatSpec> Seats = new List<SeatSpec>();
        public readonly List<BoxSpec> WallColliders = new List<BoxSpec>();
        public readonly List<BoxSpec> SolidColliders = new List<BoxSpec>();

        /// <summary>Смещение мачты от того, кто держится (поза attach_mast): 0.73 м влево и 0.47 м вперёд.</summary>
        public static readonly Vector3 MastGripOffset = new Vector3(-0.733f, 0f, 0.469f);

        private List<Vector2> m_dense;
        private float[] m_arc;

        public static MediumBalloonModel Build()
        {
            var m = new MediumBalloonModel();
            m.BuildTub();
            m.BuildRailing();
            m.BuildEnvelope();
            m.BuildRigging();
            m.BuildLantern();
            m.BuildBench();
            m.BuildTiller();
            m.BuildSail(0, 1f);
            m.BuildSail(1, -1f);
            m.BuildColliders();
            return m;
        }

        // ------------------------------------------------------------------ кадка

        private static Vector2 WallPoint(float t)
        {
            float u = 1f - t;
            return s_wall0 * (u * u) + s_wall1 * (2f * u * t) + s_wall2 * (t * t);
        }

        /// <summary>Наружный радиус борта на высоте y (между скруглением дна и ободом).</summary>
        public static float OuterRadiusAt(float y)
        {
            if (y <= s_wall0.y)
            {
                return s_wall0.x;
            }
            if (y >= s_wall2.y)
            {
                return s_wall2.x;
            }
            float lo = 0f;
            float hi = 1f;
            for (int k = 0; k < 30; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (WallPoint(mid).y < y)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }
            return WallPoint((lo + hi) * 0.5f).x;
        }

        public static float InnerRadiusAt(float y) => OuterRadiusAt(y) - WallThickness;

        private void BuildTub()
        {
            const int seg = 64;
            const float uvPerMeter = 1f / 1.2f;
            float uScale = Mathf.Round(2f * Mathf.PI * 1.6f / 1.2f);

            // Снаружи: дно, скруглённая кромка, борт.
            var outer = new List<Vector2> { new Vector2(0.001f, 0f), new Vector2(BottomFlat, 0f) };
            for (int k = 1; k <= 6; k++)
            {
                float a = (-90f + k * 15f) * Mathf.Deg2Rad;
                outer.Add(new Vector2(BottomFlat + Mathf.Cos(a) * EdgeRadius, EdgeRadius + Mathf.Sin(a) * EdgeRadius));
            }
            for (int k = 1; k <= 12; k++)
            {
                outer.Add(WallPoint(k / 12f));
            }
            Planks.AddLathe(outer, seg, uScale, uvPerMeter, 0f);

            // Изнутри: борт сверху вниз до пола (нормали внутрь).
            var inner = new List<Vector2>();
            for (int k = 0; k <= 12; k++)
            {
                float y = Mathf.Lerp(BandBottom + 0.01f, FloorTop - 0.02f, k / 12f);
                inner.Add(new Vector2(InnerRadiusAt(y), y));
            }
            Planks.AddLathe(inner, seg, uScale, uvPerMeter, 0f);

            // Пол из досок.
            Planks.AddCylinder(new Vector3(0f, 0.04f, 0f), InnerRadiusAt(FloorTop) + 0.01f, FloorTop - 0.04f, seg, uvPerMeter);

            // Толстый обод по верху борта.
            Wood.AddCylinderShell(new Vector3(0f, BandBottom, 0f), BandOuter, BandInner, RimTop - BandBottom, seg, 1f);

            // Железные полосы с заклёпками — на каждом меридиане (под парусами — крепления парусов).
            for (int k = 0; k < Meridians; k++)
            {
                float a = k * 360f / Meridians;
                Vector3 dir = Dir(a);
                var pts = new List<Vector3>();
                for (int i = 0; i <= 5; i++)
                {
                    float y = Mathf.Lerp(0.26f, BandBottom, i / 5f);
                    pts.Add(dir * (OuterRadiusAt(y) + 0.012f) + Vector3.up * y);
                }
                pts.Add(dir * (BandOuter + 0.012f) + Vector3.up * BandBottom);
                pts.Add(dir * (BandOuter + 0.012f) + Vector3.up * (RimTop - 0.01f));
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector3 d = pts[i + 1] - pts[i];
                    if (d.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }
                    Vector3 normal = Vector3.Cross(d, Vector3.Cross(dir, d)).normalized;
                    if (Vector3.Dot(normal, dir) < 0f)
                    {
                        normal = -normal;
                    }
                    Quaternion rot = Quaternion.LookRotation(d.normalized, normal);
                    Iron.AddBox((pts[i] + pts[i + 1]) * 0.5f, new Vector3(0.15f, 0.028f, d.magnitude + 0.01f), rot, 3f);
                }
                foreach (float y in new[] { 0.36f, 0.72f, 1.19f })
                {
                    float r = y > BandBottom ? BandOuter + 0.026f : OuterRadiusAt(y) + 0.026f;
                    Iron.AddSphere(dir * r + Vector3.up * y, new Vector3(0.028f, 0.028f, 0.028f), 6, 4);
                }
            }
        }

        // ------------------------------------------------------------------ перила

        private void BuildRailing()
        {
            for (int k = 0; k < Meridians; k++)
            {
                Vector3 p = Dir(k * 360f / Meridians) * PostRadius;
                Wood.AddCylinder(p + Vector3.up * (RimTop - 0.01f), 0.066f, PostTop - RimTop, 10, 2f);
                // Обмотки там, где перила касаются столбика, и узел наверху — к нему идут стропы.
                Rope.AddTorus(p + Vector3.up * RailY, Quaternion.identity, 0.074f, 0.074f, 0.026f, 0.06f, 12, 6, 2f);
                Rope.AddTorus(p + Vector3.up * (PostTop - 0.06f), Quaternion.identity, 0.074f, 0.074f, 0.024f, 0.05f, 12, 6, 2f);
                Rope.AddSphere(p + Vector3.up * (PostTop + 0.03f), new Vector3(0.09f, 0.08f, 0.09f), 8, 6);
            }
            Wood.AddTorus(new Vector3(0f, RailY, 0f), Quaternion.identity, PostRadius, PostRadius, 0.042f, 0.042f, 72, 8,
                Mathf.Round(2f * Mathf.PI * PostRadius));
        }

        // ------------------------------------------------------------------ купол

        private void BuildEnvelope()
        {
            const float r = EnvelopeRadius;
            Profile = new EnvelopeProfile
            {
                Radius = r,
                MouthRadius = r * 0.34f,
                NeckHeight = 0.08f,
                LowerHeight = r * 0.81f,
                UpperHeight = r * 1.03f,
                LowerExponent = 0.58f,
            };
            Envelope.AddEnvelope(Profile, 48, 22, 18, 4f, EnvelopeBaseY);
            Profile.Dense(out m_dense, out m_arc);
            EquatorV = SAtHeight(Profile.NeckHeight + Profile.LowerHeight);
        }

        /// <summary>Доля длины дуги профиля (от горловины) на высоте y над горловиной.</summary>
        private float SAtHeight(float y)
        {
            for (int k = 1; k < m_dense.Count; k++)
            {
                if (m_dense[k].y >= y)
                {
                    float span = m_dense[k].y - m_dense[k - 1].y;
                    float f = span > 1e-6f ? (y - m_dense[k - 1].y) / span : 0f;
                    return Mathf.Lerp(m_arc[k - 1], m_arc[k], f) / m_arc[m_arc.Length - 1];
                }
            }
            return 1f;
        }

        private Vector3 OnEnvelope(float s, float angle, float offset)
        {
            return Profile.Surface(m_dense, m_arc, s, angle, offset, EnvelopeBaseY);
        }

        private const float RopeOffset = 0.05f;

        private void BuildRigging()
        {
            float sEquator = EquatorV;
            float sUpper = SAtHeight(Profile.NeckHeight + Profile.LowerHeight + Profile.UpperHeight * Mathf.Sin(35f * Mathf.Deg2Rad));
            // Стропы сходят с купола там, где он уже 0.82 радиуса, — дальше прямо вниз к столбикам перил.
            float t = Mathf.Asin(Mathf.Pow(0.7276f, 1f / Profile.LowerExponent)) / (Mathf.PI * 0.5f);
            float sDepart = SAtHeight(Profile.NeckHeight + Profile.LowerHeight * t);
            const float split = 11f;

            for (int k = 0; k < Meridians; k++)
            {
                float a = k * 360f / Meridians;

                // Меридиан от экватора до макушки.
                var path = new List<Vector3>();
                for (int i = 0; i <= 24; i++)
                {
                    path.Add(OnEnvelope(Mathf.Lerp(sEquator, 1f, i / 24f), a, RopeOffset));
                }
                Rope.AddTube(path, 0.052f, 8, 2.5f);

                ClampBlock(sEquator, a);
                ClampBlock(sUpper, a);

                // У экватора канат расходится на две стропы по нижней части купола, дальше — прямо к столбику перил.
                Vector3 post = Dir(a) * PostRadius + Vector3.up * (PostTop - 0.01f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var branch = new List<Vector3>();
                    for (int i = 0; i <= 12; i++)
                    {
                        float f = i / 12f;
                        branch.Add(OnEnvelope(Mathf.Lerp(sEquator, sDepart, f), a + side * split * Mathf.Sin(f * Mathf.PI * 0.5f), RopeOffset));
                    }
                    branch.Add(post);
                    Rope.AddTube(branch, 0.042f, 7, 2.5f);
                }
            }

            // Шапка на макушке, где сходятся меридианы.
            Vector3 top = OnEnvelope(1f, 0f, 0f);
            Wood.AddCylinder(top + Vector3.down * 0.03f, 0.2f, 0.09f, 16, 2f);
            Rope.AddTorus(top + Vector3.up * 0.03f, Quaternion.identity, 0.2f, 0.2f, 0.035f, 0.03f, 16, 6, 2f);
        }

        /// <summary>Колодка на канате-меридиане: деревянный брусок с железной заклёпкой.</summary>
        private void ClampBlock(float s, float angle)
        {
            Vector3 p0 = OnEnvelope(s, angle, 0f);
            Vector3 normal = (OnEnvelope(s, angle, 0.1f) - p0) / 0.1f;
            Vector3 along = (OnEnvelope(s + 0.01f, angle, 0f) - OnEnvelope(s - 0.01f, angle, 0f)).normalized;
            Quaternion rot = Quaternion.LookRotation(normal, along);
            Wood.AddBox(p0 + normal * 0.075f, new Vector3(0.18f, 0.2f, 0.1f), rot, 3f);
            Iron.AddSphere(p0 + normal * 0.13f, new Vector3(0.03f, 0.03f, 0.018f), 6, 4);
        }

        // ------------------------------------------------------------------ горловина, фонарь-чаша, столб

        private void BuildLantern()
        {
            // Железное кольцо горловины с накладками на болтах.
            Iron.AddCylinderShell(new Vector3(0f, MouthY - 0.14f, 0f), 0.82f, 0.74f, 0.17f, 48, 3f);
            for (int k = 0; k < Meridians; k++)
            {
                float a = k * 360f / Meridians + 22.5f;
                Vector3 dir = Dir(a);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                Iron.AddBox(dir * 0.835f + Vector3.up * (MouthY - 0.055f), new Vector3(0.15f, 0.13f, 0.03f), rot, 3f);
                Iron.AddSphere(dir * 0.852f + Vector3.up * (MouthY - 0.055f) + Quaternion.Euler(0f, a, 0f) * Vector3.right * 0.045f,
                    new Vector3(0.018f, 0.018f, 0.018f), 6, 4);
                Iron.AddSphere(dir * 0.852f + Vector3.up * (MouthY - 0.055f) - Quaternion.Euler(0f, a, 0f) * Vector3.right * 0.045f,
                    new Vector3(0.018f, 0.018f, 0.018f), 6, 4);
            }

            // Клетка фонаря: восемь обмотанных прутьев от кольца к чаше.
            const float bowlBase = 2.26f;
            const float bowlTop = 2.60f;
            for (int k = 0; k < Meridians; k++)
            {
                Vector3 dir = Dir(k * 360f / Meridians + 22.5f);
                Rope.AddTube(new[] { dir * 0.73f + Vector3.up * (MouthY - 0.13f), dir * 0.45f + Vector3.up * (bowlTop - 0.02f) }, 0.045f, 8, 3f);
            }
            Iron.AddTorus(new Vector3(0f, bowlTop, 0f), Quaternion.identity, 0.47f, 0.47f, 0.03f, 0.04f, 36, 6, 3f);
            Iron.AddTorus(new Vector3(0f, bowlBase + 0.14f, 0f), Quaternion.identity, 0.445f, 0.445f, 0.028f, 0.035f, 36, 6, 3f);

            // Чаша: снаружи и внутри, с бортиком.
            var bowl = new List<Vector2>
            {
                new Vector2(0.001f, 0f), new Vector2(0.18f, 0.008f), new Vector2(0.33f, 0.05f), new Vector2(0.42f, 0.14f),
                new Vector2(0.455f, 0.3f), new Vector2(0.46f, 0.34f), new Vector2(0.43f, 0.345f), new Vector2(0.415f, 0.2f),
                new Vector2(0.33f, 0.1f), new Vector2(0.18f, 0.07f), new Vector2(0.001f, 0.065f),
            };
            Iron.AddLathe(bowl, 32, 2f, 3f, bowlBase);

            // Снизу чаши — стропы к узлу на макушке столба.
            const float knotY = 2.1f;
            for (int k = 0; k < 4; k++)
            {
                Vector3 dir = Dir(45f + k * 90f);
                Rope.AddTube(new[] { dir * 0.24f + Vector3.up * (bowlBase + 0.03f), Vector3.up * (knotY + 0.02f) }, 0.026f, 6, 3f);
            }
            Rope.AddSphere(Vector3.up * knotY, new Vector3(0.09f, 0.08f, 0.09f), 10, 6);

            // Столб от пола до чаши (за него держится тот, кто у огня) с обмотками и опорной плахой на полу.
            Wood.AddCylinder(new Vector3(0f, FloorTop, 0f), 0.065f, knotY - FloorTop, 14, 2f);
            Wood.AddBox(new Vector3(0f, FloorTop + 0.035f, 0f), new Vector3(0.32f, 0.07f, 0.32f), 2f);
            foreach (float y in new[] { 0.95f, 1.3f, 1.55f, 2.0f })
            {
                Rope.AddTorus(Vector3.up * y, Quaternion.identity, 0.072f, 0.072f, 0.02f, 0.045f, 14, 6, 2f);
            }

            FirePosition = new Vector3(0f, bowlBase + 0.09f, 0f);
            BurnerColliderCenter = new Vector3(0f, 2.15f, 0f);
            BurnerColliderSize = new Vector3(0.95f, 2.5f, 0.95f);

            FireOperatorYaw = 0f;
            FireOperatorPosition = new Vector3(0f, FloorTop, 0f) - Quaternion.Euler(0f, FireOperatorYaw, 0f) * MastGripOffset;
        }

        // ------------------------------------------------------------------ скамья

        private void BuildBench()
        {
            const float seatY = FloorTop + 0.45f;
            const float rCenter = 1.25f;
            const float depth = 0.42f;
            const int segments = 11;
            const float from = -66f;
            const float to = 66f;
            float step = (to - from) / segments;
            for (int i = 0; i < segments; i++)
            {
                float a = from + (i + 0.5f) * step;
                float len = 2f * rCenter * Mathf.Tan(step * 0.5f * Mathf.Deg2Rad) + 0.01f;
                Wood.AddBox(Dir(a) * rCenter + Vector3.up * seatY, new Vector3(len, 0.05f, depth), Quaternion.Euler(0f, a, 0f), 1f);
            }
            foreach (float a in new[] { -58f, -20f, 20f, 58f })
            {
                foreach (float r in new[] { 1.08f, 1.24f })
                {
                    Wood.AddBox(Dir(a) * r + Vector3.up * (FloorTop + 0.21f), new Vector3(0.06f, 0.43f, 0.06f), Quaternion.Euler(0f, a, 0f), 1f);
                }
            }
            foreach (float a in new[] { -45f, 0f, 45f })
            {
                Seats.Add(new SeatSpec { Position = Dir(a) * rCenter + Vector3.up * FloorTop, Yaw = a + 180f });
            }
        }

        // ------------------------------------------------------------------ руль-палка

        private void BuildTiller()
        {
            HelmYaw = 0f;
            Vector3 grip = HelmPosition + Quaternion.Euler(0f, HelmYaw, 0f) * MastGripOffset;
            const float pivotY = 1.02f;
            LeverPivot = new Vector3(grip.x, pivotY, grip.z);

            // Стойка с опорой на полу и железной вилкой наверху.
            Wood.AddBox(new Vector3(grip.x, FloorTop + 0.035f, grip.z), new Vector3(0.22f, 0.07f, 0.22f), 2f);
            Wood.AddBox(new Vector3(grip.x, (FloorTop + pivotY - 0.05f) * 0.5f, grip.z), new Vector3(0.12f, pivotY - 0.05f - FloorTop, 0.12f), 2f);
            foreach (float dz in new[] { -0.058f, 0.058f })
            {
                Iron.AddBox(new Vector3(grip.x, pivotY - 0.02f, grip.z + dz), new Vector3(0.11f, 0.2f, 0.018f), 3f);
            }
            Iron.AddBox(new Vector3(grip.x, pivotY - 0.1f, grip.z), new Vector3(0.13f, 0.03f, 0.14f), 3f);

            // Сама палка — в системе оси качания: от 0.3 м ниже оси до 0.85 м выше, чуть наклонена к рулевому,
            // набалдашник и обмотка под рукой.
            Quaternion lean = Quaternion.Euler(-LeverLean, 0f, 0f);
            Vector3 axis = lean * Vector3.up;
            LeverWood.AddTube(new[] { axis * -0.3f, axis * 0.85f }, 0.05f, 12, 2f);
            LeverWood.AddSphere(axis * 0.88f, new Vector3(0.085f, 0.075f, 0.085f), 10, 6);
            for (int k = 0; k < 5; k++)
            {
                LeverRope.AddTorus(axis * (0.36f + k * 0.06f), lean, 0.056f, 0.056f, 0.013f, 0.028f, 12, 5, 2f);
            }
            LeverIron.AddBox(Vector3.zero, new Vector3(0.035f, 0.035f, 0.16f), 3f);
            LeverIron.AddTorus(new Vector3(0f, 0.07f, 0f), Quaternion.identity, 0.058f, 0.058f, 0.014f, 0.028f, 12, 5, 3f);

            TillerColliderCenter = new Vector3(grip.x, 1.2f, grip.z);
            TillerColliderSize = new Vector3(0.45f, 1.3f, 0.45f);

            // Якорь — на ободе у кормы, справа от рулевого.
            AnchorOutward = Dir(157.5f);
            AnchorRopeStart = AnchorOutward * (BandOuter - 0.04f) + Vector3.up * (RimTop + 0.04f);
        }

        // ------------------------------------------------------------------ паруса

        private void BuildSail(int index, float side)
        {
            SailParts sp = Sails[index];
            sp.Pivot = new Vector3(side * 1.92f, SailPivotY, 0f);
            sp.BaseYaw = side > 0f ? 0f : 180f;
            // Реи и планка — с лицевой (передней по ходу) стороны полотна и лежат на нём; у левого паруса
            // веер развёрнут на 180°, поэтому его «вперёд» — это −Z шара.
            float front = side > 0f ? 1f : -1f;

            const float lu = 2.25f;
            const float lm = 1.85f;
            const float ll = 2.25f;
            const float ru = 0.055f;
            const float rm = 0.05f;
            const float rl = 0.055f;

            // Крепление на борту (неподвижно, на кадке): накладка и вертикальный палец.
            Vector3 outward = new Vector3(side, 0f, 0f);
            Iron.AddBox(new Vector3(side * (BandOuter + 0.02f), SailPivotY - 0.03f, 0f), new Vector3(0.04f, 0.42f, 0.3f), 3f);
            Iron.AddBox(sp.Pivot - outward * 0.05f + Vector3.up * 0.14f, new Vector3(0.1f, 0.03f, 0.08f), 3f);
            Iron.AddBox(sp.Pivot - outward * 0.05f - Vector3.up * 0.14f, new Vector3(0.1f, 0.03f, 0.08f), 3f);
            Iron.AddCylinder(sp.Pivot - Vector3.up * 0.17f, 0.024f, 0.34f, 8, 3f);

            // Ступица на оси (крутится вместе с парусом).
            sp.Hub.AddBox(new Vector3(0.05f, 0f, front * 0.03f), new Vector3(0.12f, 0.22f, 0.12f), 3f);

            // Реи — «пальцы»: лежат на полотне с лицевой стороны.
            Spar(sp.UpperWood, sp.UpperRope, sp.UpperAngle, lu, ru, front * ru);
            Spar(sp.MiddleWood, sp.MiddleRope, sp.MiddleAngle, lm, rm, front * rm);
            Spar(sp.LowerWood, sp.LowerRope, sp.LowerAngle, ll, rl, front * rl);

            // Полотно — две перепонки между «пальцами»: верхняя рея ↔ средняя и средняя ↔ нижняя.
            // Прибито вдоль всех трёх рей и поперечной планки, между ними чуть провисает назад;
            // кромка между концами рей вогнута, как перепонка между пальцами.
            const float battenX = 0.92f;
            const float innerR = 0.2f;
            const float sag = 0.07f;
            Vector3 cornerU = Polar(sp.UpperAngle, lu - 0.26f);
            Vector3 cornerM = Polar(sp.MiddleAngle, lm - 0.05f);
            Vector3 cornerL = Polar(sp.LowerAngle, ll - 0.26f);
            const int nu = 16;
            const int perPanel = 9;
            const int nv = perPanel * 2;
            var pts = new Vector3[nu + 1, nv + 1];
            var uvs = new Vector2[nu + 1, nv + 1];
            for (int j = 0; j <= nv; j++)
            {
                bool upperPanel = j >= perPanel;
                float t = upperPanel ? (j - perPanel) / (float)perPanel : j / (float)perPanel;
                float angle = upperPanel ? Mathf.Lerp(sp.MiddleAngle, sp.UpperAngle, t) : Mathf.Lerp(sp.LowerAngle, sp.MiddleAngle, t);
                Vector3 a = upperPanel ? cornerM : cornerL;
                Vector3 b = upperPanel ? cornerU : cornerM;
                Vector3 chord = b - a;
                Vector3 bowDir = new Vector3(chord.y, -chord.x, 0f).normalized;
                if (Vector3.Dot(bowDir, (a + b) * 0.5f) > 0f)
                {
                    bowDir = -bowDir;
                }
                Vector3 outer = Vector3.Lerp(a, b, t) + bowDir * (0.05f * chord.magnitude * Mathf.Sin(t * Mathf.PI));
                Vector3 inner = Polar(angle, innerR);
                float span = outer.x - inner.x;
                float uBatten = span > 1e-4f ? Mathf.Clamp((battenX - inner.x) / span, 0.05f, 0.95f) : 0.5f;
                for (int i = 0; i <= nu; i++)
                {
                    float u = i / (float)nu;
                    Vector3 p = Vector3.Lerp(inner, outer, u);
                    // Провис: ноль на реях (края перепонки) и на планке, наибольший посередине ячеек, у оси — меньше.
                    float across = Mathf.Sin(t * Mathf.PI);
                    float along = u < uBatten ? Mathf.Sin(Mathf.PI * u / uBatten) * Mathf.Min(1f, u * 2.5f)
                        : Mathf.Sin(Mathf.PI * 0.75f * (u - uBatten) / (1f - uBatten));
                    p.z = -front * sag * across * along;
                    pts[i, j] = p;
                    uvs[i, j] = new Vector2(p.x, p.y) / 2.2f;
                }
            }
            sp.Cloth.AddGrid(pts, uvs, new Vector3(0f, 0f, front), doubleSided: true);

            // Поперечная планка поверх полотна (складывается вместе с ним) и обмотки там, где она пересекает реи.
            const float rb = 0.035f;
            Vector3 bTop = Polar(sp.UpperAngle, battenX / Mathf.Cos(sp.UpperAngle * Mathf.Deg2Rad));
            Vector3 bBottom = Polar(sp.LowerAngle, battenX / Mathf.Cos(sp.LowerAngle * Mathf.Deg2Rad));
            var batten = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                Vector3 p = Vector3.Lerp(bTop, bBottom, i / 12f);
                p.z = front * rb;
                batten.Add(p);
            }
            sp.Batten.AddTube(batten, rb, 8, 2f);
            Lashing(sp.UpperRope, bTop + new Vector3(0f, 0f, front * ru), sp.UpperAngle, 0.08f);
            Lashing(sp.LowerRope, bBottom + new Vector3(0f, 0f, front * rl), sp.LowerAngle, 0.08f);
            Lashing(sp.MiddleRope, Polar(sp.MiddleAngle, battenX / Mathf.Cos(sp.MiddleAngle * Mathf.Deg2Rad)) + new Vector3(0f, 0f, front * rm),
                sp.MiddleAngle, 0.08f);
            // Углы перепонок привязаны к реям.
            Lashing(sp.UpperRope, cornerU + new Vector3(0f, 0f, front * ru), sp.UpperAngle, 0.07f);
            Lashing(sp.LowerRope, cornerL + new Vector3(0f, 0f, front * rl), sp.LowerAngle, 0.07f);
        }

        /// <summary>Рея: круглая жердь из оси наружу (сдвинута на z, чтобы лежать на полотне), набалдашник и обмотка у конца.</summary>
        private static void Spar(MeshBuilder wood, MeshBuilder rope, float angle, float length, float radius, float z)
        {
            Vector3 dir = Polar(angle, 1f);
            var lift = new Vector3(0f, 0f, z);
            wood.AddTube(new[] { dir * 0.04f + lift, dir * length + lift }, radius, 10, 1f);
            wood.AddSphere(dir * (length + 0.03f) + lift, new Vector3(radius, radius, radius) * 1.5f, 8, 6);
            Lashing(rope, dir * (length - 0.11f) + lift, angle, 0.07f);
        }

        /// <summary>Обмотка каната вокруг реи (кольцо поперёк реи).</summary>
        private static void Lashing(MeshBuilder rope, Vector3 center, float sparAngle, float width)
        {
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, Polar(sparAngle, 1f));
            rope.AddTorus(center, rot, 0.07f, 0.07f, 0.02f, width * 0.5f, 12, 5, 2f);
        }

        private static Vector3 Polar(float angleDeg, float r)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }

        // ------------------------------------------------------------------ коллайдеры

        private void BuildColliders()
        {
            // Борт — 16 досок по кругу, наклонённых наружу, как стенка кадки.
            const int sides = 16;
            float rBottom = InnerRadiusAt(FloorTop);
            float rTop = BandInner;
            float dr = rTop - rBottom;
            float dy = RimTop - FloorTop;
            float tilt = Mathf.Atan2(dr, dy) * Mathf.Rad2Deg;
            float height = Mathf.Sqrt(dr * dr + dy * dy);
            const float thickness = 0.1f;
            float rMid = (rBottom + rTop) * 0.5f + thickness * 0.5f * Mathf.Cos(tilt * Mathf.Deg2Rad);
            float width = 2f * rMid * Mathf.Tan(Mathf.PI / sides) * 1.08f;
            for (int k = 0; k < sides; k++)
            {
                float a = (k + 0.5f) * 360f / sides;
                WallColliders.Add(new BoxSpec
                {
                    Center = Dir(a) * rMid + Vector3.up * ((FloorTop + RimTop) * 0.5f),
                    Size = new Vector3(width, height, thickness),
                    Yaw = a,
                    Pitch = tilt,
                });
            }

            // Столб огня и стойка руля-палки — твёрдые.
            SolidColliders.Add(new BoxSpec { Center = new Vector3(0f, (FloorTop + 2.1f) * 0.5f, 0f), Size = new Vector3(0.14f, 2.1f - FloorTop, 0.14f) });
            SolidColliders.Add(new BoxSpec
            {
                Center = new Vector3(LeverPivot.x, (FloorTop + LeverPivot.y) * 0.5f, LeverPivot.z),
                Size = new Vector3(0.14f, LeverPivot.y - FloorTop, 0.14f),
            });
        }

        private static Vector3 Dir(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
    }
}
