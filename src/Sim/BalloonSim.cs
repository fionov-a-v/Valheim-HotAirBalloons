using System;

namespace HotAirBalloons.Sim
{
    /// <summary>Горизонтальный вектор (X, Z) в мировых координатах Unity.</summary>
    public struct V2
    {
        public float X;
        public float Z;

        public V2(float x, float z)
        {
            X = x;
            Z = z;
        }

        public static readonly V2 Zero = new V2(0f, 0f);

        public float Length => (float)Math.Sqrt(X * X + Z * Z);

        public V2 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new V2(X / len, Z / len) : Zero;
            }
        }

        /// <summary>Курс в градусах в соглашении Unity: 0 = +Z, 90 = +X.</summary>
        public float YawDeg => (float)(Math.Atan2(X, Z) * 180.0 / Math.PI);

        public static V2 FromYawDeg(float yawDeg)
        {
            double r = yawDeg * Math.PI / 180.0;
            return new V2((float)Math.Sin(r), (float)Math.Cos(r));
        }

        /// <summary>Поворот по часовой стрелке (вид сверху), как Quaternion.Euler(0, deg, 0).</summary>
        public V2 RotatedDeg(float deg)
        {
            double r = deg * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            return new V2(X * c + Z * s, -X * s + Z * c);
        }

        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Z + b.Z);
        public static V2 operator -(V2 a, V2 b) => new V2(a.X - b.X, a.Z - b.Z);
        public static V2 operator *(V2 a, float k) => new V2(a.X * k, a.Z * k);
        public static V2 operator /(V2 a, float k) => new V2(a.X / k, a.Z / k);

        public static float Dot(V2 a, V2 b) => a.X * b.X + a.Z * b.Z;

        public static V2 MoveTowards(V2 current, V2 target, float maxDelta)
        {
            V2 delta = target - current;
            float len = delta.Length;
            if (len <= maxDelta || len < 1e-6f)
            {
                return target;
            }
            return current + delta / len * maxDelta;
        }

        public override string ToString() => $"({X:0.00}, {Z:0.00})";
    }

    /// <summary>Режим горелки: вниз — огонь погашен, держать высоту — средний расход, вверх — полный.</summary>
    public enum BurnMode
    {
        Down = 0,
        Hold = 1,
        Up = 2,
    }

    /// <summary>Паруса среднего шара, как у кораблей: сложены, наполовину, полностью.</summary>
    public enum SailLevel
    {
        Furled = 0,
        Half = 1,
        Full = 2,
    }

    public enum AnchorState
    {
        Raised = 0,
        Hanging = 1,
        Holding = 2,
    }

    /// <summary>Параметры симуляции одного шара (берутся из конфига и типа шара).</summary>
    public sealed class SimSettings
    {
        public float MaxAltitude = 100f;
        public float AnchorLength = 10f;
        public float CalmBoundary = 3f;

        public float Capacity = 800f;
        public float WindSpeed = 6f;
        public float ClimbSpeed = 2.5f;
        public float DescentSpeed = 3f;

        public float VerticalAccel = 0.8f;
        public float HorizontalAccel = 0.6f;
        public float StopAccel = 4f;
        public float WaterStopAccel = 15f;

        /// <summary>Максимальное отклонение руля в процентах от 180° (0 — руля нет).</summary>
        public float RudderMaxPercent;

        /// <summary>Доля скорости ветра при максимальном отклонении руля.</summary>
        public float RudderMinSpeedFactor = 0.3f;

        /// <summary>Паруса: во сколько раз быстрее ветра летит шар (сложенные — x1).</summary>
        public bool HasSails;
        public float SailHalfFactor = 1.5f;
        public float SailFullFactor = 2f;

        /// <summary>
        /// Винт (большой шар): шар летит туда, куда смотрит нос, со скоростью от винта, плюс ветер (вектором).
        /// Скорость винта — PropellerSpeed при всех CrankSeats крутящих, пропорционально их числу.
        /// Курс задаёт руль: TurnRate градусов в секунду при руле до упора.
        /// </summary>
        public bool Propeller;
        public float PropellerSpeed = 24f;
        public int CrankSeats = 6;
        public float TurnRate = 20f;
        public float PropellerAccel = 1.5f;

        /// <summary>На плаву шар движется медленнее, чем в воздухе: доля скорости — для всего (ветер, паруса, винт).</summary>
        public float WaterSpeedFactor = 0.5f;

        public float AnchorDropSpeed = 6f;
        public bool AlignToTravel;
        public float MaxYawRate = 20f;
    }

    public struct SimInput
    {
        public float Dt;

        /// <summary>Положение центра дна корзины.</summary>
        public V2 Pos;
        public float BottomY;

        public V2 HVel;
        public float VVel;
        public float YawDeg;

        /// <summary>Высота твёрдой поверхности под корзиной (земля, постройки, морское дно).</summary>
        public float SolidY;

        /// <summary>Уровень воды (море).</summary>
        public float WaterY;

        public V2 WindDir;
        public float WindIntensity;

        /// <summary>Текущий груз, кг: люди на шаре + их инвентарь + сундук.</summary>
        public float Load;

        public BurnMode Mode;
        public float Fuel;

        /// <summary>Положение руля -1..1.</summary>
        public float Rudder;

        public SailLevel Sails;

        /// <summary>Сколько человек крутят винт.</summary>
        public int Cranks;

        public bool AnchorOut;

        /// <summary>Корзина стоит на твёрдой поверхности.</summary>
        public bool Grounded;
    }

    /// <summary>Состояние якоря, которое хранит владелец шара.</summary>
    public struct AnchorMemory
    {
        public AnchorState State;
        public float RopeOut;
        public V2 Point;
        public float PointY;
    }

    public struct SimResult
    {
        public V2 HVel;
        public float VVel;
        public float YawRateDeg;

        public V2 TargetHVel;
        public float TargetVVel;

        public float Agl;
        public bool Burning;
        public bool Overloaded;
        public bool Calm;
        public bool Friction;
    }

    /// <summary>
    /// Чистая логика полёта (без Unity): подъёмная сила, ветер, руль, якорь, штиль, перегруз.
    /// Вызывается только на клиенте-владельце шара.
    /// </summary>
    public static class BalloonSim
    {
        private const float WaterStopDepth = 0.15f;

        public static float Agl(in SimInput i) => i.BottomY - Math.Max(i.SolidY, i.WaterY);

        public static bool IsBurning(in SimInput i) => i.Mode != BurnMode.Down && i.Fuel > 0f;

        public static float LoadRatio(SimSettings s, float load) => s.Capacity > 0f ? load / s.Capacity : 0f;

        public static float RudderMaxAngleDeg(SimSettings s) => Clamp(s.RudderMaxPercent, 0f, 100f) / 100f * 180f;

        public static float RudderAngleDeg(SimSettings s, float rudder) => Clamp(rudder, -1f, 1f) * RudderMaxAngleDeg(s);

        /// <summary>Чем больше угол руля, тем меньше скорость — до RudderMinSpeedFactor на максимуме.</summary>
        public static float RudderSpeedFactor(SimSettings s, float rudder)
        {
            if (RudderMaxAngleDeg(s) < 0.01f)
            {
                return 1f;
            }
            return Lerp(1f, s.RudderMinSpeedFactor, Math.Abs(Clamp(rudder, -1f, 1f)));
        }

        public static float WindSpeed(SimSettings s, float intensity) => s.WindSpeed * Lerp(0.25f, 1f, Clamp(intensity, 0f, 1f));

        /// <summary>Паруса разгоняют шар: x1 сложенные, x1.5 наполовину, x2 полностью (по умолчанию).</summary>
        public static float SailSpeedFactor(SimSettings s, SailLevel sails)
        {
            if (!s.HasSails)
            {
                return 1f;
            }
            switch (sails)
            {
                case SailLevel.Full:
                    return Math.Max(0f, s.SailFullFactor);
                case SailLevel.Half:
                    return Math.Max(0f, s.SailHalfFactor);
                default:
                    return 1f;
            }
        }

        public static float VerticalTarget(SimSettings s, in SimInput i, out bool overloaded)
        {
            float agl = Agl(i);
            float ratio = LoadRatio(s, i.Load);
            overloaded = ratio > 1f;

            float target;
            if (!IsBurning(i))
            {
                target = -s.DescentSpeed;
            }
            else if (overloaded)
            {
                // Перегруз: горелка не вытягивает, шар снижается тем быстрее, чем сильнее перегруз.
                target = -s.DescentSpeed * Clamp(0.35f + (ratio - 1f) * 2f, 0.35f, 1f);
            }
            else
            {
                target = i.Mode == BurnMode.Up ? s.ClimbSpeed * (1f - 0.6f * ratio * ratio) : 0f;

                // У потолка подъёмная сила иссякает, выше него шар мягко опускается.
                float ceiling = (s.MaxAltitude - agl) * 0.5f;
                if (ceiling < target)
                {
                    target = Math.Max(ceiling, -s.DescentSpeed * 0.5f);
                }
            }

            // Корзина держится на воде.
            if (i.WaterY >= i.SolidY)
            {
                float gap = i.BottomY - i.WaterY;
                if (gap < 0f)
                {
                    target = Math.Max(target, Math.Min(-gap * 2f, 2f));
                }
                else if (gap < 0.05f && target < 0f)
                {
                    target = 0f;
                }
            }

            if (i.Grounded && target < 0f)
            {
                target = 0f;
            }
            return target;
        }

        /// <summary>Скорость от винта: доля крутящих от всех сидений с рукоятками.</summary>
        public static float PropellerSpeed(SimSettings s, int cranks)
        {
            if (!s.Propeller || s.CrankSeats <= 0)
            {
                return 0f;
            }
            return Math.Max(0f, s.PropellerSpeed) * Clamp(cranks / (float)s.CrankSeats, 0f, 1f);
        }

        /// <summary>Скорость, которую задаёт ветер с учётом руля и парусов (у корабля с винтом — ветер плюс винт по курсу).</summary>
        public static V2 HorizontalTarget(SimSettings s, in SimInput i)
        {
            V2 dir = i.WindDir.Normalized;
            if (s.Propeller)
            {
                V2 wind = dir.Length < 0.5f ? V2.Zero : dir * WindSpeed(s, i.WindIntensity);
                return wind + V2.FromYawDeg(i.YawDeg) * PropellerSpeed(s, i.Cranks);
            }
            if (dir.Length < 0.5f)
            {
                return V2.Zero;
            }
            dir = dir.RotatedDeg(RudderAngleDeg(s, i.Rudder));
            return dir * (WindSpeed(s, i.WindIntensity) * RudderSpeedFactor(s, i.Rudder) * SailSpeedFactor(s, i.Sails));
        }

        /// <summary>
        /// Якорь: выпущенный якорь опускается под корзину; если дотянулся до дна (корзина не выше длины каната) —
        /// цепляется. Если шар поднялся выше длины каната над точкой крепления — якорь отрывается и снова висит.
        /// </summary>
        public static void UpdateAnchor(SimSettings s, in SimInput i, ref AnchorMemory a)
        {
            if (!i.AnchorOut)
            {
                a.State = AnchorState.Raised;
                a.RopeOut = Math.Max(0f, a.RopeOut - s.AnchorDropSpeed * i.Dt);
                return;
            }

            if (a.State == AnchorState.Raised)
            {
                a.State = AnchorState.Hanging;
            }

            if (a.State == AnchorState.Hanging)
            {
                a.RopeOut = Math.Min(s.AnchorLength, a.RopeOut + s.AnchorDropSpeed * i.Dt);
                float height = i.BottomY - i.SolidY;
                if (height <= a.RopeOut + 0.05f)
                {
                    a.State = AnchorState.Holding;
                    a.Point = i.Pos;
                    a.PointY = i.SolidY;
                    a.RopeOut = Math.Max(0f, height);
                }
            }
            else if (a.State == AnchorState.Holding)
            {
                if (i.BottomY - a.PointY > s.AnchorLength + 0.25f)
                {
                    a.State = AnchorState.Hanging;
                    a.RopeOut = s.AnchorLength;
                }
            }
        }

        /// <summary>Якорь держит только по горизонтали: шар не может уйти дальше половины длины каната.</summary>
        public static V2 ApplyAnchorLimit(SimSettings s, in SimInput i, in AnchorMemory a, V2 hvel)
        {
            float radius = s.AnchorLength * 0.5f;
            V2 offset = i.Pos - a.Point;
            float dist = offset.Length;
            if (dist < 1e-4f)
            {
                return hvel;
            }

            V2 n = offset / dist;
            float radial = V2.Dot(hvel, n);
            V2 next = offset + hvel * i.Dt;
            if (next.Length > radius && radial > 0f)
            {
                hvel -= n * radial;
            }
            if (dist > radius)
            {
                hvel -= n * Math.Min((dist - radius) * 1.5f, 3f);
            }
            return hvel;
        }

        public static SimResult Step(SimSettings s, in SimInput i, ref AnchorMemory a)
        {
            var r = new SimResult
            {
                Agl = Agl(i),
                Burning = IsBurning(i),
            };

            r.TargetVVel = VerticalTarget(s, i, out r.Overloaded);

            // Вода, в отличие от земли, не коллайдер: касание воды гасит снижение резко, как удар о поверхность, —
            // при любой скорости снижения корзина уходит под воду не глубже ~15 см.
            bool touchingWater = i.WaterY >= i.SolidY && i.BottomY - i.WaterY < 0.1f && i.VVel < 0f;
            float vTarget = r.TargetVVel;
            float vAccel = s.VerticalAccel;
            if (touchingWater)
            {
                float room = Math.Max(0.05f, i.BottomY - i.WaterY + WaterStopDepth);
                vAccel = Math.Max(s.WaterStopAccel, i.VVel * i.VVel / (2f * room));
                vTarget = Math.Max(vTarget, 0f);
            }
            r.VVel = MoveTowards(i.VVel, vTarget, vAccel * i.Dt);

            UpdateAnchor(s, i, ref a);

            bool onWater = IsOnWater(i.WaterY, i.SolidY, i.BottomY);
            r.Calm = i.AnchorOut && r.Agl < s.CalmBoundary;
            r.Friction = i.Grounded && !onWater;

            bool stopped = r.Calm || r.Friction;
            V2 target = stopped ? V2.Zero : HorizontalTarget(s, i);
            if (onWater)
            {
                target = target * Clamp(s.WaterSpeedFactor, 0f, 1f);
            }
            r.TargetHVel = target;

            float hAccel = stopped ? s.StopAccel : (s.Propeller ? s.PropellerAccel : s.HorizontalAccel);
            V2 hvel = V2.MoveTowards(i.HVel, target, hAccel * i.Dt);
            if (a.State == AnchorState.Holding)
            {
                hvel = ApplyAnchorLimit(s, i, a, hvel);
            }
            r.HVel = hvel;

            // Корабль с винтом поворачивает руль (на земле — нет); остальные шары разворачивает по ходу движения.
            if (s.Propeller)
            {
                r.YawRateDeg = r.Friction ? 0f : Clamp(i.Rudder, -1f, 1f) * s.TurnRate;
            }
            else if (s.AlignToTravel && !stopped)
            {
                V2 travel = hvel.Length > 0.3f ? hvel : target;
                if (travel.Length > 0.3f)
                {
                    float delta = DeltaAngle(i.YawDeg, travel.YawDeg);
                    r.YawRateDeg = Clamp(delta, -s.MaxYawRate, s.MaxYawRate);
                }
            }
            return r;
        }

        /// <summary>На плаву: под корзиной вода глубже 5 см, дно не выше 0,3 м над ней.</summary>
        public static bool IsOnWater(float waterY, float solidY, float bottomY) => waterY > solidY + 0.05f && bottomY - waterY < 0.3f;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0f, 1f);

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }
            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>Кратчайшая разница углов в градусах (-180..180].</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f)
            {
                d -= 360f;
            }
            else if (d <= -180f)
            {
                d += 360f;
            }
            return d;
        }
    }
}
