using System;
using System.Collections.Generic;
using HotAirBalloons.Sim;

namespace HotAirBalloons.Tests
{
    /// <summary>Мини-мир для прогона логики полёта без Unity.</summary>
    internal sealed class World
    {
        public SimSettings S;
        public SimInput I;
        public AnchorMemory A;
        public SimResult Last;
        public Func<V2, float> Terrain = _ => 0f;
        public float Water = -1000f;
        public float Time;
        public float MaxAnchorDist;

        public World(SimSettings s)
        {
            S = s;
            I.Dt = 0.02f;
            I.Fuel = 1000f;
            I.WindDir = new V2(1f, 0f);
            I.WindIntensity = 1f;
        }

        public void Run(float seconds, Action<World> each = null)
        {
            int steps = (int)(seconds / I.Dt);
            for (int k = 0; k < steps; k++)
            {
                I.SolidY = Terrain(I.Pos);
                I.WaterY = Water;
                I.Grounded = I.BottomY - I.SolidY < 0.05f;
                Last = BalloonSim.Step(S, I, ref A);
                I.HVel = Last.HVel;
                I.VVel = Last.VVel;
                I.Pos = I.Pos + I.HVel * I.Dt;
                I.BottomY += I.VVel * I.Dt;
                I.YawDeg += Last.YawRateDeg * I.Dt;
                float solid = Terrain(I.Pos);
                if (I.BottomY < solid)
                {
                    I.BottomY = solid;
                    if (I.VVel < 0f)
                    {
                        I.VVel = 0f;
                    }
                }
                if (A.State == AnchorState.Holding)
                {
                    MaxAnchorDist = Math.Max(MaxAnchorDist, (I.Pos - A.Point).Length);
                }
                Time += I.Dt;
                each?.Invoke(this);
            }
        }

        public float Agl => I.BottomY - Math.Max(Terrain(I.Pos), Water);
    }

    internal static class Program
    {
        private static int s_failed;
        private static int s_passed;

        private static void Check(string name, bool ok, string details)
        {
            if (ok)
            {
                s_passed++;
                Console.WriteLine($"  OK   {name}: {details}");
            }
            else
            {
                s_failed++;
                Console.WriteLine($"  FAIL {name}: {details}");
            }
        }

        private static SimSettings Medium() => new SimSettings { Capacity = 800f, RudderMaxPercent = 25f, AlignToTravel = true, HasSails = true };

        private static SimSettings Simple() => new SimSettings { Capacity = 200f, RudderMaxPercent = 0f, AlignToTravel = false };

        private static SimSettings Drakkar() => new SimSettings { Capacity = 3000f, Propeller = true, PropellerSpeed = 24f, CrankSeats = 4, TurnRate = 20f };

        private static int Main()
        {
            Console.WriteLine("Вертикаль:");
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Up;
                w.I.Load = 100f;
                w.I.WindIntensity = 0f;
                w.Run(150f);
                Check("подъём до потолка", Math.Abs(w.Agl - 100f) < 1.5f && Math.Abs(w.I.VVel) < 0.2f, $"agl={w.Agl:0.0} vv={w.I.VVel:0.00}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Up;
                w.I.Load = 100f;
                w.Terrain = _ => 200f;
                w.I.BottomY = 200f;
                w.Run(150f);
                Check("потолок считается от земли", Math.Abs(w.I.BottomY - 300f) < 1.5f, $"y={w.I.BottomY:0.0}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.Run(60f);
                Check("держать высоту", Math.Abs(w.I.BottomY - 50f) < 0.01f, $"y={w.I.BottomY:0.000}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 130f;
                w.Run(120f);
                Check("выше потолка — мягко опускается к нему", Math.Abs(w.Agl - 100f) < 1.5f, $"agl={w.Agl:0.0}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Down;
                w.I.BottomY = 50f;
                float maxSink = 0f;
                w.Run(40f, x => maxSink = Math.Max(maxSink, -x.I.VVel));
                Check("вниз: огонь погашен, посадка 3 м/с", w.I.BottomY < 0.01f && Math.Abs(maxSink - 3f) < 0.01f, $"y={w.I.BottomY:0.00} maxSink={maxSink:0.00}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Up;
                w.I.Fuel = 0f;
                w.I.BottomY = 20f;
                w.Run(30f);
                Check("без топлива шар опускается", w.I.BottomY < 0.01f, $"y={w.I.BottomY:0.00}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Up;
                w.I.Load = 900f;
                w.I.BottomY = 30f;
                w.Run(10f);
                Check("перегруз: снижается даже на полном огне", w.I.VVel < -1f && w.Last.Overloaded, $"vv={w.I.VVel:0.00} over={w.Last.Overloaded}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Up;
                w.I.Load = 800f;
                w.Run(10f);
                Check("полная загрузка: подъём медленнее", Math.Abs(w.I.VVel - 1f) < 0.05f, $"vv={w.I.VVel:0.00}");
            }
            {
                var w = new World(Simple());
                w.I.Mode = BurnMode.Up;
                w.I.Load = 80f + 200f;
                w.I.BottomY = 0f;
                w.Run(10f);
                Check("простой шар: 280 кг > 200 — не взлетает", w.I.BottomY < 0.01f, $"y={w.I.BottomY:0.00}");
            }

            Console.WriteLine("Ветер и руль:");
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.Run(30f);
                Check("дрейф по ветру 6 м/с (паруса сложены)", Math.Abs(w.I.HVel.X - 6f) < 0.05f && Math.Abs(w.I.HVel.Z) < 0.01f, $"v={w.I.HVel}");
                Check("шар развернулся по ветру", Math.Abs(BalloonSim.DeltaAngle(w.I.YawDeg, 90f)) < 1f, $"yaw={w.I.YawDeg:0.0}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.Rudder = 1f;
                w.Run(40f);
                float angle = BalloonSim.DeltaAngle(90f, w.I.HVel.YawDeg);
                Check("средний руль: 25% = 45°", Math.Abs(angle - 45f) < 0.5f, $"angle={angle:0.0}");
                Check("скорость на максимуме руля = 30%", Math.Abs(w.I.HVel.Length - 1.8f) < 0.05f, $"speed={w.I.HVel.Length:0.00}");
                Check("руль остаётся впереди (курс = ход)", Math.Abs(BalloonSim.DeltaAngle(w.I.YawDeg, w.I.HVel.YawDeg)) < 1f, $"yaw={w.I.YawDeg:0.0}");
            }
            {
                var s = new SimSettings { Capacity = 3000f, RudderMaxPercent = 45f, AlignToTravel = true };
                var w = new World(s);
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.Rudder = -0.5f;
                w.Run(40f);
                float angle = BalloonSim.DeltaAngle(90f, w.I.HVel.YawDeg);
                Check("большой руль на половине: -40.5°", Math.Abs(angle + 40.5f) < 0.5f, $"angle={angle:0.0}");
                Check("скорость на половине: 65%", Math.Abs(w.I.HVel.Length - 6f * 0.65f) < 0.05f, $"speed={w.I.HVel.Length:0.00}");
            }
            {
                var w = new World(Simple());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.Rudder = 1f;
                w.Run(20f);
                Check("простой шар: руля нет", Math.Abs(w.I.HVel.Z) < 0.01f && Math.Abs(w.I.YawDeg) < 0.01f, $"v={w.I.HVel} yaw={w.I.YawDeg:0.0}");
            }
            foreach (var (sails, expected) in new[] { (SailLevel.Furled, 6f), (SailLevel.Half, 9f), (SailLevel.Full, 12f) })
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.Sails = sails;
                w.Run(40f);
                Check($"паруса {sails}: x{expected / 6f:0.0} от ветра", Math.Abs(w.I.HVel.X - expected) < 0.05f, $"v={w.I.HVel}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.Sails = SailLevel.Full;
                w.I.Rudder = 1f;
                w.Run(60f);
                float angle = BalloonSim.DeltaAngle(90f, w.I.HVel.YawDeg);
                Check("полные паруса + руль до упора: 45° и 2 x 30%", Math.Abs(angle - 45f) < 0.5f && Math.Abs(w.I.HVel.Length - 3.6f) < 0.05f,
                    $"angle={angle:0.0} speed={w.I.HVel.Length:0.00}");
            }
            {
                var s = new SimSettings { Capacity = 3000f, RudderMaxPercent = 45f, AlignToTravel = true };
                var w = new World(s);
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.Sails = SailLevel.Full;
                w.Run(30f);
                Check("у шара без парусов положение парусов ни на что не влияет", Math.Abs(w.I.HVel.X - 6f) < 0.05f, $"v={w.I.HVel}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 2f;
                w.I.AnchorOut = true;
                w.I.Sails = SailLevel.Full;
                w.Run(20f);
                Check("штиль у якоря: паруса не тянут", w.I.Pos.Length < 0.01f, $"pos={w.I.Pos}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Down;
                w.I.WindIntensity = 0.5f;
                w.Run(20f);
                Check("на земле ветер не тащит", w.I.Pos.Length < 0.01f, $"pos={w.I.Pos}");
            }

            Console.WriteLine("Винт (большой шар):");
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.WindDir = V2.Zero;
                w.I.Cranks = 1;
                w.Run(20f);
                Check("штиль, один гребец: 6 м/с по курсу", Math.Abs(w.I.HVel.Z - 6f) < 0.05f && Math.Abs(w.I.HVel.X) < 0.01f, $"v={w.I.HVel}");
                w.I.Rudder = 1f;
                w.Run(4.5f);
                w.I.Rudder = 0f;
                w.Run(20f);
                Check("руль до упора: 20°/с, в штиль летит куда повернули", Math.Abs(w.I.YawDeg - 90f) < 0.5f && Math.Abs(w.I.HVel.X - 6f) < 0.05f,
                    $"yaw={w.I.YawDeg:0.0} v={w.I.HVel}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.YawDeg = 270f;
                w.I.Cranks = 4;
                w.Run(40f);
                Check("четыре гребца против самого сильного ветра: 18 м/с = x3 базовой", Math.Abs(w.I.HVel.X + 18f) < 0.05f, $"v={w.I.HVel}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.YawDeg = 90f;
                w.I.Cranks = 4;
                w.Run(40f);
                Check("четыре гребца по ветру: 24 + 6 = 30 м/с", Math.Abs(w.I.HVel.X - 30f) < 0.05f, $"v={w.I.HVel}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.YawDeg = 270f;
                w.I.Cranks = 1;
                w.Run(30f);
                Check("один гребец против самого сильного ветра: стоит на месте", Math.Abs(w.I.HVel.X) < 0.05f, $"v={w.I.HVel}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.YawDeg = 270f;
                w.I.Cranks = 2;
                w.Run(30f);
                Check("два гребца против самого сильного ветра: 6 м/с вперёд", Math.Abs(w.I.HVel.X + 6f) < 0.05f, $"v={w.I.HVel}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 50f;
                w.I.YawDeg = 0f;
                w.I.Cranks = 2;
                w.Run(30f);
                Check("поперёк ветра: ветер и винт складываются вектором", Math.Abs(w.I.HVel.X - 6f) < 0.05f && Math.Abs(w.I.HVel.Z - 12f) < 0.05f, $"v={w.I.HVel}");
                Check("корабль с винтом не разворачивается сам по ходу", Math.Abs(w.I.YawDeg) < 0.01f, $"yaw={w.I.YawDeg:0.0}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Down;
                w.I.Cranks = 4;
                w.I.Rudder = 1f;
                w.Run(10f);
                Check("на земле винт и руль не двигают корабль", w.I.Pos.Length < 0.01f && Math.Abs(w.I.YawDeg) < 0.01f, $"pos={w.I.Pos} yaw={w.I.YawDeg:0.0}");
            }
            {
                var w = new World(Drakkar());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 2f;
                w.I.AnchorOut = true;
                w.I.Cranks = 4;
                w.Run(20f);
                Check("штиль у якоря: винт не тянет", w.I.Pos.Length < 0.01f, $"pos={w.I.Pos}");
            }

            Console.WriteLine("Якорь и штиль:");
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 8f;
                w.I.AnchorOut = true;
                w.I.WindIntensity = 1f;
                w.Run(60f);
                Check("якорь зацепился на 8 м", w.A.State == AnchorState.Holding, $"state={w.A.State} rope={w.A.RopeOut:0.0}");
                Check("радиус не больше половины каната", w.MaxAnchorDist <= 5.05f, $"max={w.MaxAnchorDist:0.00}");
                Check("шар стоит у края круга по ветру", Math.Abs((w.I.Pos - w.A.Point).Length - 5f) < 0.2f && Math.Abs(w.I.HVel.Length) < 0.2f, $"d={(w.I.Pos - w.A.Point).Length:0.00} v={w.I.HVel}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 8f;
                w.I.AnchorOut = true;
                w.Run(30f);
                w.I.WindDir = new V2(0f, 1f);
                w.Run(60f);
                V2 off = w.I.Pos - w.A.Point;
                Check("ветер сменился — шар переходит по окружности", w.MaxAnchorDist <= 5.05f && off.Z > 4.5f, $"off={off} max={w.MaxAnchorDist:0.00}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 30f;
                w.I.AnchorOut = true;
                w.Run(20f);
                Check("выше длины каната якорь не держит", w.A.State == AnchorState.Hanging && w.I.Pos.X > 60f, $"state={w.A.State} x={w.I.Pos.X:0}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Down;
                w.I.BottomY = 30f;
                w.I.AnchorOut = true;
                float xAtCatch = float.NaN;
                w.Run(40f, x =>
                {
                    if (float.IsNaN(xAtCatch) && x.A.State == AnchorState.Holding)
                    {
                        xAtCatch = x.I.Pos.X;
                    }
                });
                Check("спуск с выпущенным якорем: цепляется на 10 м", !float.IsNaN(xAtCatch) && w.A.State == AnchorState.Holding, $"catchX={xAtCatch:0.0}");
                Check("ниже штиля — не двигается", Math.Abs(w.I.HVel.Length) < 0.01f && (w.I.Pos - w.A.Point).Length <= 5.05f, $"v={w.I.HVel} d={(w.I.Pos - w.A.Point).Length:0.00}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 8f;
                w.I.AnchorOut = true;
                w.Run(20f);
                w.I.Mode = BurnMode.Up;
                w.Run(10f);
                Check("поднялся выше каната — якорь оторвался", w.A.State == AnchorState.Hanging, $"state={w.A.State} agl={w.Agl:0.0}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 2f;
                w.I.AnchorOut = true;
                w.Run(30f);
                Check("якорь брошен ниже штиля — ветер не действует", w.I.Pos.Length < 0.01f, $"pos={w.I.Pos}");
            }
            {
                var w = new World(Medium());
                w.I.Mode = BurnMode.Hold;
                w.I.BottomY = 8f;
                w.I.AnchorOut = true;
                w.Run(20f);
                w.I.AnchorOut = false;
                w.Run(10f);
                Check("якорь поднят — шар свободен", w.A.State == AnchorState.Raised && w.I.Pos.X > 20f && w.I.HVel.X > 5f, $"state={w.A.State} x={w.I.Pos.X:0} v={w.I.HVel}");
            }

            Console.WriteLine("Вода:");
            {
                var w = new World(Medium());
                w.Terrain = _ => -20f;
                w.Water = 0f;
                w.I.Mode = BurnMode.Down;
                w.I.BottomY = 15f;
                float minY = float.MaxValue;
                w.Run(40f, x => minY = Math.Min(minY, x.I.BottomY));
                Check("садится на воду и держится", Math.Abs(w.I.BottomY) < 0.1f && minY > -0.3f, $"y={w.I.BottomY:0.00} min={minY:0.00}");
                Check("на воде дрейфует", w.I.Pos.X > 50f, $"x={w.I.Pos.X:0}");
            }
            {
                var w = new World(Medium());
                w.Terrain = _ => -20f;
                w.Water = 0f;
                w.I.Mode = BurnMode.Hold;
                w.Run(20f);
                Check("на воде вдвое медленнее: 6 / 2 = 3 м/с", Math.Abs(w.I.HVel.X - 3f) < 0.05f && Math.Abs(w.I.BottomY) < 0.01f, $"v={w.I.HVel} y={w.I.BottomY:0.00}");
                w.I.Sails = SailLevel.Full;
                w.Run(20f);
                Check("на воде полные паруса: 6 x 2 / 2 = 6 м/с", Math.Abs(w.I.HVel.X - 6f) < 0.05f, $"v={w.I.HVel}");
                w.I.Mode = BurnMode.Up;
                w.Run(25f);
                Check("взлетел с воды — снова x2 от ветра: 12 м/с", w.Agl > 5f && Math.Abs(w.I.HVel.X - 12f) < 0.05f, $"agl={w.Agl:0.0} v={w.I.HVel}");
            }
            {
                var w = new World(Drakkar());
                w.Terrain = _ => -20f;
                w.Water = 0f;
                w.I.WindDir = V2.Zero;
                w.I.Mode = BurnMode.Hold;
                w.I.Cranks = 4;
                w.Run(30f);
                Check("на воде и винт вдвое слабее: четверо в штиль — 24 / 2 = 12 м/с", Math.Abs(w.I.HVel.Z - 12f) < 0.05f && Math.Abs(w.I.HVel.X) < 0.01f,
                    $"v={w.I.HVel}");
            }
            {
                var w = new World(Medium());
                w.Terrain = _ => -30f;
                w.Water = 0f;
                w.I.Mode = BurnMode.Down;
                w.I.BottomY = 0f;
                w.I.AnchorOut = true;
                w.Run(20f);
                Check("глубоко — якорь не достаёт дна", w.A.State == AnchorState.Hanging, $"state={w.A.State}");
            }

            Console.WriteLine($"\nИтого: {s_passed} OK, {s_failed} FAIL");
            return s_failed == 0 ? 0 : 1;
        }
    }
}
