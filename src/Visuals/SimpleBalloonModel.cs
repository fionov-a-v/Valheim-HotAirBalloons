using System.Collections.Generic;
using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>Коробка-коллайдер в локальных координатах шара (поворот — Rotation, если задан, иначе Euler(Pitch, Yaw, 0)).</summary>
    internal struct BoxSpec
    {
        public Vector3 Center;
        public Vector3 Size;
        public float Yaw;
        public float Pitch;
        public Quaternion Rotation;
        public bool HasRotation;

        public Quaternion GetRotation() => HasRotation ? Rotation : Quaternion.Euler(Pitch, Yaw, 0f);
    }

    /// <summary>
    /// Геометрия простого шара по эскизу «Грейд I · 1 персонаж» (тролья ткань, плетёная корзина):
    /// круглая корзина с деревянными ободами и стяжками, купол из синих лоскутов под сеткой канатов с узлами,
    /// деревянный обруч в горловине и чаша огня на четырёх цепях. Только меши и точки — без Unity-сцены,
    /// поэтому модель можно выгрузить и посмотреть без игры (tests/ModelPreview).
    /// </summary>
    internal sealed class SimpleBalloonModel
    {
        public const float FloorTop = 0.12f;
        public const float BasketRadius = 0.9f;
        public const float WallThickness = 0.07f;
        public const float WallHeight = 0.95f;
        public const float HoopY = 2.65f;
        public const int RopeCount = 8;

        public readonly MeshBuilder Wicker = new MeshBuilder();
        public readonly MeshBuilder Wood = new MeshBuilder();
        public readonly MeshBuilder Iron = new MeshBuilder();
        public readonly MeshBuilder Rope = new MeshBuilder();
        public readonly MeshBuilder Envelope = new MeshBuilder();

        public EnvelopeProfile Profile;
        public float EnvelopeBaseY = HoopY;

        /// <summary>Куда ставить ванильный огонь (угли и пламя жаровни) и в каком масштабе.</summary>
        public Vector3 FirePosition;
        public float FireScale = 0.62f;

        /// <summary>Тот, кто держится: стоит под чашей и держится левой рукой за передний левый канат.</summary>
        public Vector3 OperatorPosition = new Vector3(0.04f, FloorTop, 0.22f);
        public float OperatorYaw = 0f;

        public Vector3 BurnerColliderCenter;
        public Vector3 BurnerColliderSize;

        public Vector3 AnchorRopeStart;
        public Vector3 AnchorOutward;

        public readonly List<BoxSpec> WallColliders = new List<BoxSpec>();
        public float BasketTop => FloorTop + WallHeight;

        private List<Vector2> m_dense;
        private float[] m_arc;

        public static SimpleBalloonModel Build()
        {
            var m = new SimpleBalloonModel();
            m.BuildBasket();
            m.BuildEnvelope();
            m.BuildNet();
            m.BuildSuspension();
            m.BuildBurner();
            m.BuildColliders();
            return m;
        }

        // ------------------------------------------------------------------ корзина

        private void BuildBasket()
        {
            const int seg = 40;
            float top = BasketTop;

            // Днище из досок и крестовина под ним.
            Wood.AddCylinder(new Vector3(0f, 0.02f, 0f), BasketRadius - 0.04f, 0.1f, seg, 1f);
            Wood.AddBox(new Vector3(0f, -0.015f, 0f), new Vector3(0.11f, 0.07f, BasketRadius * 1.9f), Quaternion.Euler(0f, 45f, 0f), 1f);
            Wood.AddBox(new Vector3(0f, -0.015f, 0f), new Vector3(0.11f, 0.07f, BasketRadius * 1.9f), Quaternion.Euler(0f, -45f, 0f), 1f);

            // Плетёная стенка и два деревянных обода — сверху и снизу.
            // Плетение крупное, как на эскизе: около десяти рядов лозы по высоте борта.
            Wicker.AddCylinderShell(new Vector3(0f, FloorTop, 0f), BasketRadius, BasketRadius - WallThickness, WallHeight, seg, 0.75f);
            Wood.AddTorus(new Vector3(0f, top, 0f), Quaternion.identity, BasketRadius - 0.01f, BasketRadius - 0.01f, 0.085f, 0.07f, seg, 10, 6f);
            Wood.AddTorus(new Vector3(0f, FloorTop + 0.05f, 0f), Quaternion.identity, BasketRadius + 0.01f, BasketRadius + 0.01f, 0.075f, 0.08f, seg, 10, 6f);

            // Стяжки канатов по стенке с деревянными колодками на ободах.
            for (int k = 0; k < RopeCount; k++)
            {
                float a = k * 360f / RopeCount;
                Vector3 dir = Dir(a);
                float r = BasketRadius + 0.05f;
                Rope.AddTube(new[] { dir * r + Vector3.up * (FloorTop + 0.02f), dir * r + Vector3.up * (top + 0.06f) }, 0.032f, 6, 3f);
                Quaternion blockRot = Quaternion.LookRotation(dir, Vector3.up);
                Wood.AddBox(dir * (BasketRadius + 0.08f) + Vector3.up * top, new Vector3(0.14f, 0.17f, 0.07f), blockRot, 1f);
                Wood.AddBox(dir * (BasketRadius + 0.08f) + Vector3.up * (FloorTop + 0.05f), new Vector3(0.14f, 0.17f, 0.07f), blockRot, 1f);
                Rope.AddSphere(dir * (BasketRadius + 0.06f) + Vector3.up * (top + 0.13f), new Vector3(0.065f, 0.08f, 0.065f), 8, 6);
            }

            // Якорь — у заднего борта, между канатами.
            float anchorAngle = 157.5f;
            AnchorOutward = Dir(anchorAngle);
            AnchorRopeStart = AnchorOutward * (BasketRadius - 0.02f) + Vector3.up * (top + 0.04f);
        }

        // ------------------------------------------------------------------ купол

        private void BuildEnvelope()
        {
            const float r = 1.9f;
            Profile = new EnvelopeProfile
            {
                Radius = r,
                MouthRadius = r * 0.25f,
                NeckHeight = 0.12f,
                LowerHeight = r * 1.2f,
                UpperHeight = r * 0.8f,
                LowerExponent = 0.6f,
            };
            Envelope.AddEnvelope(Profile, 40, 18, 14, 4f, EnvelopeBaseY);
            Profile.Dense(out m_dense, out m_arc);
        }

        private Vector3 OnEnvelope(float s, float angle, float offset)
        {
            return Profile.Surface(m_dense, m_arc, s, angle, offset, EnvelopeBaseY);
        }

        /// <summary>Канат по поверхности купола между двумя точками (s — доля дуги от горловины, угол — градусы).</summary>
        private void NetRope(float s0, float a0, float s1, float a1, int steps = 10)
        {
            float da = Mathf.DeltaAngle(a0, a1);
            var path = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                path.Add(OnEnvelope(Mathf.Lerp(s0, s1, t), a0 + da * t, RopeOffset));
            }
            Rope.AddTube(path, RopeRadius, 6, 3f);
        }

        private void NetRing(float s, int steps = 64)
        {
            var path = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                path.Add(OnEnvelope(s, i * 360f / steps, RopeOffset));
            }
            Rope.AddTube(path, RopeRadius * 1.15f, 6, 3f);
        }

        private void Knot(float s, float angle, float size = 1f)
        {
            // Центр узла приподнят над тканью на ~его толщину — иначе узел протыкает купол изнутри.
            Vector3 radii = new Vector3(0.1f, 0.09f, 0.1f) * size;
            Vector3 p = OnEnvelope(s, angle, RopeOffset + radii.x * 0.55f);
            Rope.AddSphere(p, radii, 8, 6);
        }

        private const float RopeRadius = 0.038f;
        private const float RopeOffset = 0.04f;

        // Сетка как на эскизе: пояс внизу купола, три ряда узлов (соседние сдвинуты на полшага — получаются ромбы)
        // и узел на макушке, к которому сходятся верхние канаты. s — доля дуги профиля от горловины.
        private static readonly float[] s_rows = { 0.17f, 0.38f, 0.60f, 0.81f };
        private const float BottomRingS = 0.17f;

        private void BuildNet()
        {
            float step = 360f / RopeCount;
            float Angle(int row, int k) => k * step + ((row & 1) == 1 ? step * 0.5f : 0f);

            // Ромбы: от пояса и от каждого ряда — два каната к соседним узлам ряда выше.
            for (int row = 0; row < s_rows.Length - 1; row++)
            {
                for (int k = 0; k < RopeCount; k++)
                {
                    float a = Angle(row, k);
                    NetRope(s_rows[row], a, s_rows[row + 1], a + step * 0.5f);
                    NetRope(s_rows[row], a, s_rows[row + 1], a - step * 0.5f);
                }
            }
            NetRing(BottomRingS);

            // Верхний ряд — меридианами к макушке.
            int last = s_rows.Length - 1;
            for (int k = 0; k < RopeCount; k++)
            {
                NetRope(s_rows[last], Angle(last, k), 1f, Angle(last, k), 8);
            }

            for (int row = 1; row < s_rows.Length; row++)
            {
                for (int k = 0; k < RopeCount; k++)
                {
                    Knot(s_rows[row], Angle(row, k));
                }
            }
            for (int k = 0; k < RopeCount; k++)
            {
                Knot(BottomRingS, Angle(0, k), 1.2f);
            }
            Rope.AddSphere(OnEnvelope(1f, 0f, 0.07f), new Vector3(0.13f, 0.08f, 0.13f), 10, 6);
        }

        // ------------------------------------------------------------------ подвес корзины

        private void BuildSuspension()
        {
            float top = BasketTop;
            for (int k = 0; k < RopeCount; k++)
            {
                float a = k * 360f / RopeCount;
                Vector3 from = OnEnvelope(BottomRingS, a, RopeOffset);
                Vector3 to = Dir(a) * (BasketRadius + 0.035f) + Vector3.up * (top + 0.05f);
                Rope.AddTube(new[] { from, to }, 0.042f, 6, 3f);
            }
        }

        // ------------------------------------------------------------------ обруч, цепи, чаша

        private void BuildBurner()
        {
            float hoopR = Profile.MouthRadius + 0.03f;
            Wood.AddTorus(new Vector3(0f, HoopY - 0.03f, 0f), Quaternion.identity, hoopR + 0.01f, hoopR + 0.01f, 0.085f, 0.1f, 32, 10, 4f);

            // Чаша: железная, с бортиком и шишкой снизу.
            float bowlBase = HoopY - 0.53f;
            var bowl = new List<Vector2>
            {
                new Vector2(0.001f, 0f), new Vector2(0.10f, 0.006f), new Vector2(0.20f, 0.045f), new Vector2(0.265f, 0.12f),
                new Vector2(0.295f, 0.185f), new Vector2(0.30f, 0.205f), new Vector2(0.275f, 0.215f), new Vector2(0.255f, 0.19f),
                new Vector2(0.20f, 0.10f), new Vector2(0.10f, 0.055f), new Vector2(0.001f, 0.05f),
            };
            Iron.AddLathe(bowl, 28, 2f, 3f, bowlBase);
            Iron.AddSphere(new Vector3(0f, bowlBase - 0.015f, 0f), new Vector3(0.05f, 0.04f, 0.05f), 8, 5);
            Iron.AddTorus(new Vector3(0f, bowlBase + 0.2f, 0f), Quaternion.identity, 0.3f, 0.3f, 0.018f, 0.018f, 28, 5, 4f);

            // Четыре цепи от обруча к бортику чаши.
            for (int k = 0; k < 4; k++)
            {
                float a = 45f + k * 90f;
                Vector3 top = Dir(a) * (hoopR - 0.02f) + Vector3.up * (HoopY - 0.12f);
                Vector3 bottom = Dir(a) * 0.29f + Vector3.up * (bowlBase + 0.21f);
                AddChain(top, bottom);
            }

            FirePosition = new Vector3(0f, bowlBase + 0.1f, 0f);
            BurnerColliderCenter = new Vector3(0f, (bowlBase - 0.05f + HoopY + 0.05f) * 0.5f, 0f);
            BurnerColliderSize = new Vector3(0.8f, HoopY + 0.05f - (bowlBase - 0.05f), 0.8f);
        }

        private void AddChain(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            Vector3 dir = d / len;
            const float pitch = 0.052f;
            int links = Mathf.Max(2, Mathf.RoundToInt(len / pitch));
            Quaternion baseRot = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.forward : Vector3.up);
            for (int i = 0; i < links; i++)
            {
                Vector3 c = from + dir * (len * (i + 0.5f) / links);
                Quaternion rot = (i & 1) == 0 ? baseRot : baseRot * Quaternion.Euler(0f, 0f, 90f);
                // Кольцо в локальной плоскости XZ, вытянуто вдоль Z (вдоль цепи); поворот «на бок» — вокруг Z.
                Iron.AddTorus(c, rot * Quaternion.Euler(0f, 0f, 90f), 0.017f, 0.032f, 0.0065f, 0.0065f, 10, 4);
            }
        }

        // ------------------------------------------------------------------ коллайдеры

        private void BuildColliders()
        {
            // Стенка — восьмиугольник из восьми досок по окружности корзины.
            const int sides = 8;
            float apothem = BasketRadius - WallThickness * 0.5f;
            float width = 2f * BasketRadius * Mathf.Tan(Mathf.PI / sides) + 0.02f;
            float h = WallHeight + 0.1f;
            for (int k = 0; k < sides; k++)
            {
                float a = k * 360f / sides + 180f / sides;
                WallColliders.Add(new BoxSpec
                {
                    Center = Dir(a) * apothem + Vector3.up * (FloorTop + h * 0.5f),
                    Size = new Vector3(width, h, WallThickness),
                    Yaw = a,
                });
            }
        }

        private static Vector3 Dir(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        }
    }
}
