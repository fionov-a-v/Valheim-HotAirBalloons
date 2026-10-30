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

    /// <summary>Винт: точка оси и поворот — винт крутится вокруг своей оси Z.</summary>
    internal struct PropellerSpec
    {
        public Vector3 Pivot;
        public Quaternion Rotation;
    }

    /// <summary>
    /// Общее у кораблей под лежачим куполом — «Небесного драккара» и «Небесного ялика»: из этих мешей и точек
    /// BalloonPieces собирает префаб. Купол — мяч для американского футбола из полосатого льна (полосы поперёк длины,
    /// железные наконечники на остриях); кривошипы, сиденье рулевого, щиты и фонарь огня у обоих одинаковые.
    /// Корень — низ киля посередине, +Z — нос, +X — правый борт. Только меши и точки — без Unity-сцены.
    /// </summary>
    internal abstract class ShipModel
    {
        public readonly MeshBuilder Hull = new MeshBuilder();
        public readonly MeshBuilder Deck = new MeshBuilder();
        public readonly MeshBuilder Wood = new MeshBuilder();
        public readonly MeshBuilder Iron = new MeshBuilder();
        public readonly MeshBuilder Rope = new MeshBuilder();
        public readonly MeshBuilder Envelope = new MeshBuilder();
        public readonly MeshBuilder Shields = new MeshBuilder();

        /// <summary>
        /// Маска воды, как у ванильных кораблей: невидимая «крышка» над палубой чуть ниже планширя (шейдер игры
        /// Custom/WaterMask). Где она видна, игра не рисует воду — волна, захлестнувшая палубу, внутри корпуса не видна.
        /// </summary>
        public readonly MeshBuilder WaterMask = new MeshBuilder();

        /// <summary>Винт: меши в системе оси винта (крутится вокруг своей Z); одинаковых винтов может быть несколько.</summary>
        public readonly MeshBuilder PropellerWood = new MeshBuilder();
        public readonly MeshBuilder PropellerIron = new MeshBuilder();
        public readonly List<PropellerSpec> Propellers = new List<PropellerSpec>();

        /// <summary>Докуда от оси винта достают лопасти (с запасом), м — боковому зонду.</summary>
        public float PropellerReach = 0.75f;

        /// <summary>Руль: баллер, перо и руль-палка в системе оси баллера (поворачивается вокруг Y).</summary>
        public readonly MeshBuilder RudderWood = new MeshBuilder();
        public readonly MeshBuilder RudderIron = new MeshBuilder();
        public Vector3 RudderPivot;

        /// <summary>Поворот руля до упора, градусов (руль вправо — руль-палка уходит влево).</summary>
        public float RudderVisualAngle = -16f;

        /// <summary>Один кривошип в своей системе: ось вала — Z (к гребцу), рукоять — по Y.</summary>
        public readonly MeshBuilder CrankIron = new MeshBuilder();
        public readonly MeshBuilder CrankWood = new MeshBuilder();
        public readonly List<CrankSpec> Cranks = new List<CrankSpec>();

        /// <summary>Кривошип перед самим рулевым (ялик): своего сиденья у него нет, крутит пилот.</summary>
        public bool PilotCrank;

        /// <summary>Границы полос купола в долях V текстуры (от кормового острия к носовому).</summary>
        public float[] StripeV;

        public Vector3 FirePosition;
        public float FireScale = 0.7f;
        public Vector3 FireOperatorPosition;
        public float FireOperatorYaw;
        public Vector3 BurnerColliderCenter;
        public Vector3 BurnerColliderSize;

        public Vector3 HelmPosition;
        public float HelmYaw;
        public Vector3 TillerColliderCenter;
        public Vector3 TillerColliderSize;

        public Vector3 ChestPosition;
        public float ChestScale = 0.75f;
        public Vector3 AnchorRopeStart;
        public Vector3 AnchorOutward;

        public readonly List<LadderSpec> Ladders = new List<LadderSpec>();
        public readonly List<Vector3> HullColliderPoints = new List<Vector3>();
        public readonly List<BoxSpec> WallColliders = new List<BoxSpec>();
        public readonly List<BoxSpec> SolidColliders = new List<BoxSpec>();

        /// <summary>
        /// «На борту» — от палубы вверх на рост человека и в ширину корпуса на этой длине (у носа и кормы он узкий):
        /// стоящие на земле у штевней не считаются. Полуширины — от -OnboardHalfSize.z до +OnboardHalfSize.z.
        /// </summary>
        public Vector3 OnboardCenter;
        public Vector3 OnboardHalfSize;
        public float[] OnboardHalfBeams = new float[0];

        /// <summary>Купол: высота оси, наибольший радиус и полудлина.</summary>
        public readonly float EnvelopeCenterY;
        public readonly float EnvelopeRadius;
        public readonly float EnvelopeHalfLength;

        private readonly int m_stripes;

        /// <summary>Железные наконечники на остриях купола: докуда от острия они закрывают ткань и насколько выступают, м.</summary>
        protected float EnvelopeCapLength = 0.2f;
        protected float EnvelopeTipLength = 0.1f;

        protected ShipModel(float envelopeCenterY, float envelopeRadius, float envelopeHalfLength, int stripes)
        {
            EnvelopeCenterY = envelopeCenterY;
            EnvelopeRadius = envelopeRadius;
            EnvelopeHalfLength = envelopeHalfLength;
            m_stripes = stripes;
        }

        /// <summary>«На борту» по обводам корпуса: halfBeamAt — полуширина по планширю на длине z.</summary>
        protected void SetOnboard(float deckY, float halfLength, float halfBeam, System.Func<float, float> halfBeamAt)
        {
            OnboardCenter = new Vector3(0f, deckY + 1.3f, 0f);
            OnboardHalfSize = new Vector3(halfBeam + 0.3f, 1.5f, halfLength + 0.3f);
            OnboardHalfBeams = new float[27];
            for (int i = 0; i < OnboardHalfBeams.Length; i++)
            {
                float z = Mathf.Lerp(-OnboardHalfSize.z, OnboardHalfSize.z, i / (float)(OnboardHalfBeams.Length - 1));
                OnboardHalfBeams[i] = halfBeamAt(z) + 0.3f;
            }
        }

        // ------------------------------------------------------------------ купол — мяч для американского футбола

        /// <summary>
        /// Радиус лежачего купола на расстоянии z от середины — форма мяча для американского футбола, снятая с силуэта эскиза:
        /// самый толстый посередине, плавно сужается к почти острым концам (r = R·(1 − a^2.2)^0.7, a — доля полудлины).
        /// </summary>
        public static float EnvelopeRadiusAt(float z, float radius, float halfLength)
        {
            float a = Mathf.Clamp01(Mathf.Abs(z) / halfLength);
            return radius * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(a, 2.2f)), 0.7f);
        }

        public float EnvelopeRadiusAt(float z) => EnvelopeRadiusAt(z, EnvelopeRadius, EnvelopeHalfLength);

        /// <summary>Низ купола посередине.</summary>
        public float EnvelopeBottomY => EnvelopeCenterY - EnvelopeRadius;

        protected void BuildEnvelope()
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
            StripeV = new float[m_stripes - 1];
            for (int s = 1; s < m_stripes; s++)
            {
                float zb = -EnvelopeHalfLength + 2f * EnvelopeHalfLength * s / m_stripes;
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
                    float z = EnvelopeHalfLength - EnvelopeCapLength + EnvelopeCapLength * k / 6f;
                    cap.Add(new Vector2(k == 6 ? 0.001f : EnvelopeRadiusAt(z) + 0.03f, z));
                }
                cap.Add(new Vector2(0.001f, EnvelopeHalfLength + EnvelopeTipLength));
                int f = Iron.VertexCount;
                Iron.AddLathe(cap, 16, 2f, 3f, 0f);
                Iron.Transform(f, Quaternion.Euler(end > 0f ? 90f : -90f, 0f, 0f), new Vector3(0f, EnvelopeCenterY, 0f));
            }
        }

        // ------------------------------------------------------------------ корпус: станции и маска воды

        /// <summary>
        /// Станции по длине корпуса для внутренней обшивки и палубы — одни и те же у обеих (иначе на сужении к штевням
        /// хорды палубы и обшивки расходятся щелью): равномерно от штевня до штевня плюс заданные точки (концы палубы).
        /// </summary>
        protected static List<float> Stations(float halfLength, int count, params float[] extra)
        {
            var z = new List<float>();
            for (int i = 0; i <= count; i++)
            {
                z.Add(halfLength * Mathf.Sin((i / (float)count * 2f - 1f) * Mathf.PI * 0.5f));
            }
            foreach (float e in extra)
            {
                if (!z.Exists(v => Mathf.Abs(v - e) < 0.005f))
                {
                    z.Add(e);
                }
            }
            z.Sort();
            return z;
        }

        /// <summary>
        /// Маска воды — «крышка» по проёму корпуса: на станциях z, на высоте topAt(z), во всю внутреннюю ширину
        /// halfWidthAt(z, y) без 1 см (наружу из корпуса не выходит). Лицом вверх: видна сверху, снизу — нет.
        /// </summary>
        protected void BuildWaterMask(List<float> stations, System.Func<float, float> topAt, System.Func<float, float, float> halfWidthAt)
        {
            const int nx = 6;
            var pts = new Vector3[stations.Count, nx + 1];
            var uvs = new Vector2[stations.Count, nx + 1];
            for (int i = 0; i < stations.Count; i++)
            {
                float z = stations[i];
                float y = topAt(z);
                float w = Mathf.Max(0f, halfWidthAt(z, y) - 0.01f);
                for (int j = 0; j <= nx; j++)
                {
                    pts[i, j] = new Vector3(Mathf.Lerp(-w, w, j / (float)nx), y, z);
                    uvs[i, j] = Vector2.zero;
                }
            }
            WaterMask.AddGrid(pts, uvs, Vector3.up, doubleSided: false);
        }

        /// <summary>Точка на поясном канате купола (по борту на уровне оси).</summary>
        protected Vector3 WaistPoint(float z, float side)
        {
            return new Vector3(side * (EnvelopeRadiusAt(z) + 0.05f), EnvelopeCenterY, z);
        }

        // ------------------------------------------------------------------ фонарь огня

        /// <summary>
        /// Фонарь огня: железная чаша, клетка из восьми прутьев с кольцами и шапка; k — масштаб (1 — фонарь драккара).
        /// Возвращает высоту верхнего кольца клетки.
        /// </summary>
        protected float AddLanternBody(float bowlBase, float k)
        {
            var bowl = new List<Vector2>
            {
                new Vector2(0.001f * k, 0f), new Vector2(0.145f * k, 0.009f * k), new Vector2(0.265f * k, 0.05f * k),
                new Vector2(0.33f * k, 0.145f * k), new Vector2(0.35f * k, 0.265f * k), new Vector2(0.325f * k, 0.272f * k),
                new Vector2(0.305f * k, 0.17f * k), new Vector2(0.24f * k, 0.095f * k), new Vector2(0.12f * k, 0.06f * k),
                new Vector2(0.001f * k, 0.055f * k),
            };
            Iron.AddLathe(bowl, 28, 2f, 3f, bowlBase);
            // Клетка: восемь прутьев от бортика к верхнему кольцу и шапка.
            float top = bowlBase + 0.62f * k;
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Iron.AddTube(new[] { d * (0.34f * k) + Vector3.up * (bowlBase + 0.26f * k), d * (0.29f * k) + Vector3.up * top }, 0.021f * k, 6, 3f);
            }
            Iron.AddTorus(Vector3.up * top, Quaternion.identity, 0.29f * k, 0.29f * k, 0.025f * k, 0.03f * k, 28, 6, 3f);
            Iron.AddTorus(Vector3.up * (bowlBase + 0.265f * k), Quaternion.identity, 0.35f * k, 0.35f * k, 0.022f * k, 0.03f * k, 28, 6, 3f);
            var capProfile = new List<Vector2>
            {
                new Vector2(0.315f * k, 0f), new Vector2(0.19f * k, 0.1f * k), new Vector2(0.05f * k, 0.16f * k), new Vector2(0.001f * k, 0.17f * k),
            };
            Iron.AddLathe(capProfile, 20, 2f, 3f, top);
            return top;
        }

        // ------------------------------------------------------------------ щиты

        /// <summary>Круглый щит: выпуклые доски (текстура щита), железные обод и умбон; rot — Z смотрит наружу.</summary>
        protected void Shield(Vector3 center, Quaternion rot, float radius)
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

        // ------------------------------------------------------------------ кривошипы и сиденье рулевого

        /// <summary>Один кривошип (в своей системе): вал к гребцу по Z, колено по Y, ручка параллельно валу, деревянная втулка.</summary>
        protected void BuildCrankMesh()
        {
            CrankIron.AddTube(new[] { new Vector3(0f, 0f, -0.14f), new Vector3(0f, 0f, 0.02f) }, 0.025f, 8, 3f);
            CrankIron.AddBox(new Vector3(0f, 0.1f, 0.02f), new Vector3(0.045f, 0.24f, 0.03f), 3f);
            CrankIron.AddTube(new[] { new Vector3(0f, 0.2f, 0.02f), new Vector3(0f, 0.2f, 0.2f) }, 0.016f, 6, 3f);
            int f = CrankWood.VertexCount;
            CrankWood.AddCylinder(Vector3.zero, 0.028f, 0.13f, 8, 3f);
            CrankWood.Transform(f, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 0.2f, 0.06f));
        }

        /// <summary>Стойка кривошипа перед сидящим (лицом к +Z) с железной втулкой вала.</summary>
        protected void AddCrankPost(Vector3 seat)
        {
            var post = new Vector3(seat.x, seat.y, seat.z + 0.64f);
            Wood.AddBox(post + new Vector3(0f, 0.47f, 0f), new Vector3(0.12f, 0.94f, 0.12f), 2f);
            Wood.AddBox(post + new Vector3(0f, 0.035f, 0f), new Vector3(0.26f, 0.07f, 0.26f), 2f);
            Iron.AddTorus(post + new Vector3(0f, 0.9f, -0.07f), Quaternion.Euler(90f, 0f, 0f), 0.04f, 0.04f, 0.014f, 0.014f, 12, 5, 3f);
            Iron.AddBox(post + new Vector3(0f, 0.9f, -0.062f), new Vector3(0.1f, 0.1f, 0.012f), 3f);
            Cranks.Add(new CrankSpec
            {
                SeatPosition = seat,
                SeatYaw = 0f,
                CrankPivot = new Vector3(seat.x, seat.y + 0.9f, seat.z + 0.56f),
                CrankRotation = Quaternion.Euler(0f, 180f, 0f),
            });
        }

        /// <summary>Сиденье рулевого: доска на двух ножках.</summary>
        protected void BuildHelmSeat()
        {
            Wood.AddBox(HelmPosition + new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.05f, 0.42f), 1f);
            foreach (float dx in new[] { -0.2f, 0.2f })
            {
                Wood.AddBox(HelmPosition + new Vector3(dx, 0.21f, 0f), new Vector3(0.06f, 0.42f, 0.34f), 1f);
            }
        }
    }
}
