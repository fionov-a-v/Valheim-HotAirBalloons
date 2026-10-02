using System.Collections.Generic;
using HotAirBalloons.Sim;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Главный компонент шара. Владелец ZDO считает физику (ветер, подъём, якорь, топливо, груз),
    /// остальные клиенты только читают состояние из ZDO и показывают его.
    /// </summary>
    public class BalloonController : MonoBehaviour, Hoverable
    {
        public BalloonKind m_kind;

        /// <summary>Объём корзины в локальных координатах: всё, что внутри, считается «на шаре».</summary>
        public Vector3 m_onboardCenter;
        public Vector3 m_onboardHalfSize;

        /// <summary>Круглая корзина: радиус по горизонтали вместо половин X/Z (0 — прямоугольная).</summary>
        public float m_onboardRadius;

        /// <summary>Длинный корпус (драккар): допустимая полуширина по длине, от -halfSize.z до +halfSize.z (пусто — не проверять).</summary>
        public float[] m_onboardHalfBeams = new float[0];

        public BalloonBurner m_burner;

        /// <summary>Руль-палка (есть только у среднего шара): паруса и руль — отсюда, а не от огня.</summary>
        public BalloonTiller m_helm;

        public BalloonAnchor m_anchor;
        public Container m_chest;

        /// <summary>Что поворачивается рулём вокруг вертикали (плавники, боковые паруса).</summary>
        public Transform[] m_rudderPivots = new Transform[0];
        public float m_rudderVisualAngle = 35f;

        /// <summary>Что наклоняется рулём вбок (руль-палка).</summary>
        public Transform[] m_tillerPivots = new Transform[0];
        public float m_tillerVisualAngle = 14f;

        /// <summary>Сиденья с рукоятями, крутящие винт (драккар), и сам винт (крутится вокруг своей оси Z).</summary>
        public BalloonCrankSeat[] m_crankSeats = new BalloonCrankSeat[0];
        public Transform m_propeller;
        public float m_propellerMaxTurns = 2.5f;

        /// <summary>Оболочка и её профиль — чтобы прятать её, когда камера оказалась внутри купола.</summary>
        public Renderer m_envelopeRenderer;
        public float m_envelopeMouthY;
        public float m_envelopeMouthR;
        public float m_envelopeRadius;
        public float m_envelopeLowerH;
        public float m_envelopeUpperH;
        public float m_envelopeNeckH;
        public float m_envelopeLowerExp = 1f;

        /// <summary>Лежачий купол драккара (мяч для американского футбола вдоль Z) с центром m_envelopeCenter.</summary>
        public bool m_envelopeHorizontal;
        public Vector3 m_envelopeCenter;
        public float m_envelopeHalfLength;

        public static readonly List<BalloonController> Instances = new List<BalloonController>();

        public static readonly int s_mode = "hab_mode".GetStableHashCode();
        public static readonly int s_fuel = "hab_fuel".GetStableHashCode();
        public static readonly int s_rudder = "hab_rudder".GetStableHashCode();
        public static readonly int s_anchorOut = "hab_anchorOut".GetStableHashCode();
        public static readonly int s_anchorState = "hab_anchorState".GetStableHashCode();
        public static readonly int s_anchorPos = "hab_anchorPos".GetStableHashCode();
        public static readonly int s_ropeOut = "hab_ropeOut".GetStableHashCode();
        public static readonly int s_load = "hab_load".GetStableHashCode();
        public static readonly int s_user = "hab_user".GetStableHashCode();
        public static readonly int s_helmUser = "hab_helmUser".GetStableHashCode();
        public static readonly int s_sail = "hab_sail".GetStableHashCode();
        public static readonly int s_crankMask = "hab_crankMask".GetStableHashCode();

        /// <summary>Версия данных шара в ZDO: шары из 1.2 (0) — руль в ноль (у драккара руль теперь — скорость поворота).</summary>
        public static readonly int s_dataVersion = "hab_dataVersion".GetStableHashCode();

        /// <summary>На каком сиденье с рукоятью сидит игрок (номер + 1, 0 — ни на каком) — каждый клиент пишет в ZDO своего персонажа.</summary>
        public static readonly int s_playerCrankSeat = "hab_crankSeat".GetStableHashCode();

        /// <summary>Вес инвентаря игрока — каждый клиент пишет его в ZDO своего персонажа.</summary>
        public static readonly int s_playerInventoryWeight = "hab_invWeight".GetStableHashCode();

        /// <summary>Шар поворачивается только вокруг вертикали: вращение по X и Z заморожено.</summary>
        public const RigidbodyConstraints UprightConstraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        /// <summary>Выпрямление: наклон меньше этого (градусы) — уже ровно; скорость — не меньше минимальной, иначе наклон × коэффициент в секунду.</summary>
        private const float UprightTolerance = 0.05f;
        private const float UprightMinRate = 15f;
        private const float UprightGain = 2f;

        private static int s_solidMask;
        private static readonly RaycastHit[] s_hits = new RaycastHit[32];

        private ZNetView m_nview;
        private Rigidbody m_body;
        private Piece m_piece;
        private readonly SimSettings m_settings = new SimSettings();
        private AnchorMemory m_anchorMem;
        private bool m_wasOwner;
        private readonly List<Player> m_onboard = new List<Player>();
        private float m_load;
        private float m_loadTimer;
        private float m_ownerTimer;
        private float m_userTimer;
        private readonly float[] m_userInvalidTime = new float[2];
        private readonly long[] m_grantUser = new long[2];
        private readonly float[] m_grantTime = { -100f, -100f };
        private float m_feedTimer;
        private float m_unattendedTime;
        private float m_lastGroundContact = -10f;
        private float m_rudderVisual;
        private float m_propellerTurns;
        private float m_localAgl;
        private float m_localAglTime = -10f;

        private string m_cachedFuelName;
        private ItemDrop m_cachedFuelItem;

        public bool HasRudder => m_kind != BalloonKind.Simple;
        public bool HasSails => m_helm != null && m_settings.HasSails;

        /// <summary>Корабль с винтом (драккар): курс задаёт руль, скорость — ветер плюс винт.</summary>
        public bool HasPropeller => m_crankSeats.Length > 0 && m_settings.Propeller;
        public KindConfig Config => BalloonConfig.For(m_kind);
        public ZNetView NView => m_nview;
        public Rigidbody Body => m_body;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_body = GetComponent<Rigidbody>();
            m_piece = GetComponent<Piece>();
            if (m_nview == null || m_nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            if (s_solidMask == 0)
            {
                s_solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            }
            if (m_body != null)
            {
                m_body.useGravity = false;
                m_body.maxDepenetrationVelocity = 2f;
                SetupMassDistribution();
            }

            m_nview.Register<long, int>("HAB_RequestControl", RPC_RequestControl);
            m_nview.Register<long, int>("HAB_ReleaseControl", RPC_ReleaseControl);
            m_nview.Register<bool, int>("HAB_ControlResponse", RPC_ControlResponse);
            m_nview.Register<int>("HAB_ChangeMode", RPC_ChangeMode);
            m_nview.Register<int>("HAB_ChangeSail", RPC_ChangeSail);
            m_nview.Register<float>("HAB_Rudder", RPC_Rudder);
            m_nview.Register("HAB_ToggleAnchor", RPC_ToggleAnchor);
            m_nview.Register<float>("HAB_AddFuel", RPC_AddFuel);

            BalloonConfig.Fill(m_settings, m_kind);
            m_loadTimer = Random.Range(0f, 0.5f);
            Instances.Add(this);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
        }

        /// <summary>
        /// Центр масс — на оси шара, главные оси инерции — строго по осям шара. Автоматический расчёт по несимметричным
        /// коллайдерам (стойка руля, нос и корма, сундук) поворачивает оси инерции, и тогда удары (деревья, склон, люди
        /// в корзине) через перекрёстные члены тензора наклоняют шар, хотя вращение по X и Z заморожено.
        /// </summary>
        private void SetupMassDistribution()
        {
            float hx = m_onboardRadius > 0f ? m_onboardRadius : m_onboardHalfSize.x;
            float hz = m_onboardRadius > 0f ? m_onboardRadius : m_onboardHalfSize.z;
            float hy = m_onboardHalfSize.y;
            float m = m_body.mass / 3f;
            m_body.centerOfMass = new Vector3(0f, m_onboardCenter.y, 0f);
            m_body.inertiaTensorRotation = Quaternion.identity;
            m_body.inertiaTensor = new Vector3(m * (hy * hy + hz * hz), m * (hx * hx + hz * hz), m * (hx * hx + hy * hy));
        }

        // ------------------------------------------------------------------ состояние (для всех клиентов)

        public BurnMode GetMode() => m_nview != null && m_nview.IsValid() ? (BurnMode)m_nview.GetZDO().GetInt(s_mode) : BurnMode.Down;

        public float GetFuel() => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetFloat(s_fuel) : 0f;

        public float GetRudder() => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetFloat(s_rudder) : 0f;

        public bool IsAnchorOut() => m_nview != null && m_nview.IsValid() && m_nview.GetZDO().GetBool(s_anchorOut);

        public AnchorState GetAnchorState() => m_nview != null && m_nview.IsValid() ? (AnchorState)m_nview.GetZDO().GetInt(s_anchorState) : AnchorState.Raised;

        public Vector3 GetAnchorPoint() => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetVec3(s_anchorPos, transform.position) : transform.position;

        public float GetRopeOut() => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetFloat(s_ropeOut) : 0f;

        public float GetLoad() => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetFloat(s_load) : 0f;

        /// <summary>Кто держится за огонь (управляющий шаром).</summary>
        public long GetUser() => GetUser(BalloonStation.Fire);

        public long GetUser(int station) => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetLong(UserKey(station)) : 0L;

        private static int UserKey(int station) => station == BalloonStation.Helm ? s_helmUser : s_user;

        public BalloonStation GetStation(int station) => station == BalloonStation.Helm ? m_helm : (BalloonStation)m_burner;

        /// <summary>Откуда поворачивают руль: руль-палка, если она есть, иначе источник огня.</summary>
        public BalloonStation SteeringStation => m_helm != null ? m_helm : (BalloonStation)m_burner;

        public SailLevel GetSails() => m_nview != null && m_nview.IsValid() && m_helm != null
            ? (SailLevel)Mathf.Clamp(m_nview.GetZDO().GetInt(s_sail), (int)SailLevel.Furled, (int)SailLevel.Full)
            : SailLevel.Furled;

        /// <summary>Во сколько раз паруса ускоряют шар относительно ветра (x1 без парусов).</summary>
        public float GetSailSpeedFactor()
        {
            BalloonConfig.Fill(m_settings, m_kind);
            return BalloonSim.SailSpeedFactor(m_settings, GetSails());
        }

        public bool IsBurning() => GetMode() != BurnMode.Down && GetFuel() > 0f;

        public string GetDisplayName() => m_piece != null ? m_piece.m_name : "$hab_simple";

        public ItemDrop GetFuelItem()
        {
            string name = Config.FuelItem.Value;
            if (m_cachedFuelItem != null && m_cachedFuelName == name)
            {
                return m_cachedFuelItem;
            }
            m_cachedFuelName = name;
            GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
            m_cachedFuelItem = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return m_cachedFuelItem;
        }

        public string GetFuelDisplayName()
        {
            ItemDrop item = GetFuelItem();
            return item != null ? item.m_itemData.m_shared.m_name : Config.FuelItem.Value;
        }

        /// <summary>Руль для отображения: у рулевого — его локальное значение, у остальных — из ZDO.</summary>
        public float GetDisplayRudder()
        {
            BalloonStation steering = SteeringStation;
            if (steering != null && steering.TryGetLocalRudder(out float local))
            {
                return local;
            }
            return GetRudder();
        }

        /// <summary>
        /// Максимальное отклонение курса от ветра, градусы (66.67% = 120°). У драккара — условные 90°:
        /// дуга руля в HUD до упора — четверть круга, как у корабля.
        /// </summary>
        public float RudderMaxAngleDeg => HasPropeller ? 90f
            : HasRudder && Config.RudderPercent != null ? Mathf.Clamp(Config.RudderPercent.Value, 0f, 100f) / 100f * 180f : 0f;

        /// <summary>Насколько ветер помогает (0..1) — яркость стрелки ветра в HUD.</summary>
        public float GetWindFactor(Vector3 windDir, float rudder)
        {
            if (HasPropeller)
            {
                // Попутный — ярко, встречный — тускло.
                Vector3 f = transform.forward;
                f.y = 0f;
                return Mathf.Lerp(0.15f, 1f, (Vector3.Dot(f.normalized, windDir) + 1f) * 0.5f);
            }
            return GetRudderSpeedFactor(rudder);
        }

        /// <summary>Кто сидит на сиденьях с рукоятями (бит на сиденье) — считает владелец шара.</summary>
        public int GetCrankMask() => m_nview != null && m_nview.IsValid() ? m_nview.GetZDO().GetInt(s_crankMask) : 0;

        public int GetCrankCount() => CountBits(GetCrankMask() & ((1 << m_crankSeats.Length) - 1));

        /// <summary>Скорость от винта сейчас, м/с.</summary>
        public float GetPropellerSpeed()
        {
            BalloonConfig.Fill(m_settings, m_kind);
            m_settings.CrankSeats = m_crankSeats.Length;
            return BalloonSim.PropellerSpeed(m_settings, GetCrankCount());
        }

        private static int CountBits(int v)
        {
            int n = 0;
            while (v != 0)
            {
                n += v & 1;
                v >>= 1;
            }
            return n;
        }

        /// <summary>Доля скорости ветра при таком положении руля (1 — прямо по ветру).</summary>
        public float GetRudderSpeedFactor(float rudder)
        {
            if (RudderMaxAngleDeg < 0.01f)
            {
                return 1f;
            }
            return Mathf.Lerp(1f, BalloonConfig.RudderMinSpeed.Value / 100f, Mathf.Abs(Mathf.Clamp(rudder, -1f, 1f)));
        }

        /// <summary>Ветер для отображения (как его видит этот клиент), с учётом силы Модера у смотрящего.</summary>
        public Vector3 GetWindDirForDisplay(Player viewer)
        {
            if (HasRudder && viewer != null && viewer.GetSEMan() != null && viewer.GetSEMan().HaveStatusAttribute(StatusEffect.StatusAttribute.SailingPower))
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.normalized;
            }
            if (EnvMan.instance == null)
            {
                return transform.forward;
            }
            Vector3 w = EnvMan.instance.GetWindDir();
            w.y = 0f;
            return w.sqrMagnitude > 1e-4f ? w.normalized : transform.forward;
        }

        /// <summary>Шар на плаву — как считает симуляция (там всё вдвое медленнее).</summary>
        public bool IsOnWater()
        {
            Vector3 p = transform.position;
            return BalloonSim.IsOnWater(GetWaterLevel(), GetSolidHeight(p), p.y);
        }

        /// <summary>Штиль: якорь выпущен и шар ниже границы штиля.</summary>
        public bool IsCalmHere()
        {
            return IsAnchorOut() && GetLocalAgl() < BalloonConfig.CalmBoundary.Value;
        }

        /// <summary>Куда на экране ставить виджет штурвала: правее и выше того, кто держится за это место (как ControlGui у корабля).</summary>
        public Vector3 GetControlGuiPosition(BalloonStation station)
        {
            Transform attach = station != null ? station.m_attachPoint : null;
            if (attach == null)
            {
                return transform.position + Vector3.up * 1.5f;
            }
            return attach.position + attach.right * 0.7f + attach.forward * 0.4f + Vector3.up * 1.3f;
        }

        public bool IsInsideBasket(Vector3 worldPos)
        {
            Vector3 p = transform.InverseTransformPoint(worldPos) - m_onboardCenter;
            if (Mathf.Abs(p.y) > m_onboardHalfSize.y)
            {
                return false;
            }
            if (m_onboardRadius > 0f)
            {
                return p.x * p.x + p.z * p.z <= m_onboardRadius * m_onboardRadius;
            }
            if (Mathf.Abs(p.x) > m_onboardHalfSize.x || Mathf.Abs(p.z) > m_onboardHalfSize.z)
            {
                return false;
            }
            if (m_onboardHalfBeams.Length > 1)
            {
                float f = (p.z / m_onboardHalfSize.z * 0.5f + 0.5f) * (m_onboardHalfBeams.Length - 1);
                int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, m_onboardHalfBeams.Length - 2);
                float halfBeam = Mathf.Lerp(m_onboardHalfBeams[i], m_onboardHalfBeams[i + 1], f - i);
                return Mathf.Abs(p.x) <= halfBeam;
            }
            return true;
        }

        public bool IsOnboard(Player player)
        {
            if (player == null)
            {
                return false;
            }
            Transform attach = player.GetAttachPoint();
            if (attach != null && attach.IsChildOf(transform))
            {
                return true;
            }
            return IsInsideBasket(player.transform.position);
        }

        public static BalloonController FindOnboard(Player player)
        {
            foreach (BalloonController b in Instances)
            {
                if (b != null && b.m_nview != null && b.m_nview.IsValid() && b.IsOnboard(player))
                {
                    return b;
                }
            }
            return null;
        }

        public bool HasPlayersOnboard()
        {
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p != null && !p.IsDead() && IsOnboard(p))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Разобрать молотом можно только пустой шар на земле.</summary>
        public bool CanBeRemoved()
        {
            return !HasPlayersOnboard() && GetLocalAgl() < 1.5f;
        }

        // ------------------------------------------------------------------ земля и высота

        /// <summary>Высота твёрдой поверхности под точкой (без своей корзины и других движущихся объектов).</summary>
        public static float GetSolidHeight(Vector3 pos)
        {
            if (s_solidMask == 0)
            {
                s_solidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            }
            int n = Physics.RaycastNonAlloc(pos + Vector3.up * 0.3f, Vector3.down, s_hits, 3000f, s_solidMask, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            float bestDist = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                RaycastHit hit = s_hits[k];
                if (hit.collider == null || hit.collider.attachedRigidbody != null)
                {
                    continue;
                }
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    best = hit.point.y;
                }
            }
            if (!float.IsNaN(best))
            {
                return best;
            }
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out float ground))
            {
                return ground;
            }
            return WorldGenerator.instance != null ? WorldGenerator.instance.GetHeight(pos.x, pos.z) : pos.y - 1000f;
        }

        public static float GetWaterLevel()
        {
            return ZoneSystem.instance != null ? ZoneSystem.instance.m_waterLevel : 30f;
        }

        /// <summary>Высота дна корзины над землёй или водой (кэшируется на 0.25 с).</summary>
        public float GetLocalAgl()
        {
            if (Time.time - m_localAglTime > 0.25f)
            {
                m_localAglTime = Time.time;
                Vector3 p = transform.position;
                m_localAgl = p.y - Mathf.Max(GetSolidHeight(p), GetWaterLevel());
            }
            return m_localAgl;
        }

        public Vector3 GetSafeGroundPoint(Vector3 around)
        {
            float ground = Mathf.Max(GetSolidHeight(around), GetWaterLevel());
            return new Vector3(around.x, ground + 0.5f, around.z);
        }

        // ------------------------------------------------------------------ физика (только владелец)

        private void FixedUpdate()
        {
            if (m_nview == null || !m_nview.IsValid() || m_body == null)
            {
                return;
            }
            bool owner = m_nview.IsOwner();
            if (!owner)
            {
                m_wasOwner = false;
                return;
            }
            ZDO zdo = m_nview.GetZDO();
            if (!m_wasOwner)
            {
                // Стали владельцем — продолжаем с того состояния, что записал прежний владелец.
                m_wasOwner = true;
                m_anchorMem.State = (AnchorState)zdo.GetInt(s_anchorState);
                Vector3 ap = zdo.GetVec3(s_anchorPos, transform.position);
                m_anchorMem.Point = new V2(ap.x, ap.z);
                m_anchorMem.PointY = ap.y;
                m_anchorMem.RopeOut = zdo.GetFloat(s_ropeOut);
                // Список «на борту» и таймеры остались от прошлого владения — пересчитываем, прежде чем что-то решать.
                RecomputeLoad();
                zdo.Set(s_load, m_load);
                m_loadTimer = 0.5f;
                m_ownerTimer = 2f;
                m_unattendedTime = 0f;
                m_userInvalidTime[0] = 0f;
                m_userInvalidTime[1] = 0f;
                if (zdo.GetInt(s_dataVersion) < 1)
                {
                    zdo.Set(s_rudder, 0f);
                    zdo.Set(s_dataVersion, 1);
                }
            }

            float dt = Time.fixedDeltaTime;
            BalloonConfig.Fill(m_settings, m_kind);
            m_settings.CrankSeats = m_crankSeats.Length;
            UpdateUnattended(dt, zdo);

            m_loadTimer -= dt;
            if (m_loadTimer <= 0f)
            {
                m_loadTimer = 0.5f;
                RecomputeLoad();
                zdo.Set(s_load, m_load);
            }
            else
            {
                // Гребец нажал W или S — винт отзывается сразу, не дожидаясь пересчёта груза.
                UpdateCrankMask();
            }

            Vector3 pos = m_body.position;
            Vector3 vel = m_body.linearVelocity;
            float solid = GetSolidHeight(pos);
            float water = GetWaterLevel();

            GetWind(out V2 windDir, out float windIntensity);

            BurnMode mode = (BurnMode)zdo.GetInt(s_mode);
            float fuel = zdo.GetFloat(s_fuel);
            var input = new SimInput
            {
                Dt = dt,
                Pos = new V2(pos.x, pos.z),
                BottomY = pos.y,
                HVel = new V2(vel.x, vel.z),
                VVel = vel.y,
                YawDeg = m_body.rotation.eulerAngles.y,
                SolidY = solid,
                WaterY = water,
                WindDir = windDir,
                WindIntensity = windIntensity,
                Load = m_load,
                Mode = mode,
                Fuel = fuel,
                Rudder = HasRudder ? zdo.GetFloat(s_rudder) : 0f,
                Sails = GetSails(),
                Cranks = HasPropeller ? GetCrankCount() : 0,
                AnchorOut = zdo.GetBool(s_anchorOut),
                // Стоит на земле: дно касается поверхности (или есть контакт снизу — например, на склоне).
                Grounded = pos.y - solid < 0.06f || Time.time - m_lastGroundContact < 0.2f,
            };

            AnchorState prevState = m_anchorMem.State;
            SimResult r = BalloonSim.Step(m_settings, input, ref m_anchorMem);

            m_body.linearVelocity = new Vector3(r.HVel.X, r.VVel, r.HVel.Z);
            m_body.angularVelocity = new Vector3(0f, r.YawRateDeg * Mathf.Deg2Rad, 0f);
            KeepUpright(dt);

            if (r.Burning)
            {
                float rate = mode == BurnMode.Up ? 1f : BalloonConfig.HoldFuelFactor.Value;
                fuel = Mathf.Max(0f, fuel - dt / Mathf.Max(1f, Config.BurnTime.Value) * rate);
                zdo.Set(s_fuel, fuel);
            }

            if (m_anchorMem.State != prevState)
            {
                zdo.Set(s_anchorState, (int)m_anchorMem.State);
                zdo.Set(s_anchorPos, new Vector3(m_anchorMem.Point.X, m_anchorMem.PointY, m_anchorMem.Point.Z));
            }
            if (Mathf.Abs(zdo.GetFloat(s_ropeOut) - m_anchorMem.RopeOut) > 0.05f)
            {
                zdo.Set(s_ropeOut, m_anchorMem.RopeOut);
            }

            m_ownerTimer -= dt;
            if (m_ownerTimer <= 0f)
            {
                m_ownerTimer = 2f;
                UpdateOwner();
            }
            m_userTimer -= dt;
            if (m_userTimer <= 0f)
            {
                m_userTimer = 1f;
                ValidateUser();
            }
            m_feedTimer -= dt;
            if (m_feedTimer <= 0f)
            {
                m_feedTimer = 1f;
                FeedFromChest();
            }
        }

        /// <summary>
        /// Шар всегда висит ровно — в полёте, у якоря, в штиле и на земле. Каждый шаг физики проверяем наклон;
        /// если удар (дерево, склон) всё-таки наклонил шар, плавно возвращаем его в вертикаль вокруг горизонтальной оси
        /// (чем больше наклон, тем быстрее), курс не трогаем. Вращение по X и Z при этом остаётся замороженным: иначе шар,
        /// который симуляция прижимает к дереву, контакт заваливал бы каждый шаг. MoveRotation — с интерполяцией, без рывков.
        /// </summary>
        private void KeepUpright(float dt)
        {
            Quaternion rot = m_body.rotation;
            Vector3 up = rot * Vector3.up;
            float tilt = Vector3.Angle(up, Vector3.up);
            if (tilt < UprightTolerance)
            {
                return;
            }
            Quaternion upright = Quaternion.FromToRotation(up, Vector3.up) * rot;
            // Остаток меньше шага — встаёт ровно, без перелёта.
            float step = Mathf.Max(UprightMinRate, tilt * UprightGain) * dt;
            m_body.MoveRotation(Quaternion.RotateTowards(rot, upright, step));
        }

        /// <summary>
        /// Без присмотра огонь гаснет: если на шаре никого нет дольше 20 с и якорь не держит,
        /// горелка переходит в режим «вниз», и брошенный шар садится, а не улетает на весь запас топлива.
        /// </summary>
        private void UpdateUnattended(float dt, ZDO zdo)
        {
            bool burning = (BurnMode)zdo.GetInt(s_mode) != BurnMode.Down;
            if (!burning || m_onboard.Count > 0 || m_anchorMem.State == AnchorState.Holding)
            {
                m_unattendedTime = 0f;
                return;
            }
            m_unattendedTime += dt;
            if (m_unattendedTime > 20f)
            {
                zdo.Set(s_mode, (int)BurnMode.Down);
                m_unattendedTime = 0f;
            }
        }

        private void GetWind(out V2 dir, out float intensity)
        {
            dir = V2.Zero;
            intensity = 0f;
            if (EnvMan.instance == null)
            {
                return;
            }
            Vector3 w = EnvMan.instance.GetWindDir();
            intensity = EnvMan.instance.GetWindIntensity();
            dir = new V2(w.x, w.z).Normalized;

            // Сила Модера: ветер дует туда, куда смотрит шар, — с рулём шаром можно править как кораблём.
            if (HasRudder && AnyOnboardHasWindControl())
            {
                Vector3 f = transform.forward;
                dir = new V2(f.x, f.z).Normalized;
            }
        }

        private bool AnyOnboardHasWindControl()
        {
            foreach (Player p in m_onboard)
            {
                if (p != null && p.GetSEMan() != null && p.GetSEMan().HaveStatusAttribute(StatusEffect.StatusAttribute.SailingPower))
                {
                    return true;
                }
            }
            return false;
        }

        private void RecomputeLoad()
        {
            m_onboard.Clear();
            float load = 0f;
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p == null || p.IsDead() || !IsOnboard(p))
                {
                    continue;
                }
                m_onboard.Add(p);
                load += GetPlayerWeight(p);
            }

            Vector3 center = transform.position;
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c is Player || c.IsDead())
                {
                    continue;
                }
                if ((c.transform.position - center).sqrMagnitude > 400f)
                {
                    continue;
                }
                if (IsInsideBasket(c.transform.position))
                {
                    load += BalloonConfig.CreatureWeight.Value;
                }
            }

            if (m_chest != null && m_chest.GetInventory() != null)
            {
                load += m_chest.GetInventory().GetTotalWeight();
            }
            m_load = load;
            UpdateCrankMask();
        }

        /// <summary>Владелец: кто из тех, кто на борту, крутит рукоять на своём сиденье (по меткам в ZDO персонажей).</summary>
        private void UpdateCrankMask()
        {
            if (m_crankSeats.Length == 0)
            {
                return;
            }
            int mask = 0;
            foreach (Player p in m_onboard)
            {
                ZNetView nview = p != null ? p.GetComponent<ZNetView>() : null;
                if (nview == null || !nview.IsValid())
                {
                    continue;
                }
                int seat = nview.GetZDO().GetInt(s_playerCrankSeat) - 1;
                if (seat >= 0 && seat < m_crankSeats.Length)
                {
                    mask |= 1 << seat;
                }
            }
            ZDO zdo = m_nview.GetZDO();
            if (zdo.GetInt(s_crankMask) != mask)
            {
                zdo.Set(s_crankMask, mask);
            }
        }

        public static float GetPlayerWeight(Player p)
        {
            float inventory = -1f;
            ZNetView nview = p.GetComponent<ZNetView>();
            if (nview != null && nview.IsValid())
            {
                inventory = nview.GetZDO().GetFloat(s_playerInventoryWeight, -1f);
            }
            if (inventory < 0f && p == Player.m_localPlayer && p.GetInventory() != null)
            {
                inventory = p.GetInventory().GetTotalWeight();
            }
            return BalloonConfig.PlayerWeight.Value + Mathf.Max(0f, inventory);
        }

        /// <summary>
        /// Как у корабля: если владелец сошёл с шара, симуляцию получает тот, кто на борту.
        /// Предпочтение — управляющему (у него управление без сетевой задержки), если сундук сейчас никто не держит открытым.
        /// </summary>
        private void UpdateOwner()
        {
            // Пока у владельца открыт сундук шара, владение отдавать нельзя: иначе у него закроется окно сундука,
            // а флаг «занят» останется навсегда (снять его может только владелец) — с риском потери вещей.
            if (Player.m_localPlayer == null || m_onboard.Count == 0 || IsChestOpenHere())
            {
                return;
            }
            // Симуляцию лучше вести клиенту того, кто у огня, а если у огня никого — рулевого.
            int station = BalloonStation.Fire;
            Player user = FindPlayer(GetUser(station));
            if (user == null && m_helm != null)
            {
                station = BalloonStation.Helm;
                user = FindPlayer(GetUser(station));
            }
            if (user != null && user != Player.m_localPlayer && m_onboard.Contains(user) && IsValidUser(station, user.GetPlayerID()))
            {
                long userOwner = user.GetOwner();
                if (userOwner != 0L && userOwner != ZDOMan.GetSessionID())
                {
                    m_nview.GetZDO().SetOwner(userOwner);
                    return;
                }
            }
            if (m_onboard.Contains(Player.m_localPlayer))
            {
                return;
            }
            long newOwner = 0L;
            if (user != null && m_onboard.Contains(user))
            {
                newOwner = user.GetOwner();
            }
            if (newOwner == 0L)
            {
                foreach (Player p in m_onboard)
                {
                    if (p != null && p.GetOwner() != 0L)
                    {
                        newOwner = p.GetOwner();
                        break;
                    }
                }
            }
            if (newOwner != 0L && newOwner != ZDOMan.GetSessionID())
            {
                m_nview.GetZDO().SetOwner(newOwner);
            }
        }

        /// <summary>Сундук шара открыт на этом клиенте (флаг выставляет только владелец).</summary>
        private bool IsChestOpenHere()
        {
            return m_chest != null && m_chest.IsInUse();
        }

        /// <summary>
        /// RPC ушёл к бывшему владельцу (у отправителя ещё не обновилось, кто владелец) — пересылаем текущему.
        /// Возвращает true, если этот клиент не владелец и обрабатывать вызов не должен.
        /// </summary>
        private bool ForwardIfNotOwner(long sender, string method, params object[] args)
        {
            if (m_nview.IsOwner())
            {
                return false;
            }
            long owner = m_nview.GetZDO().GetOwner();
            if (owner != 0L && owner != sender && owner != ZDOMan.GetSessionID())
            {
                m_nview.InvokeRPC(owner, method, args);
            }
            return true;
        }

        private void ValidateUser()
        {
            for (int station = BalloonStation.Fire; station <= BalloonStation.Helm; station++)
            {
                long user = GetUser(station);
                if (user == 0L || IsValidUser(station, user))
                {
                    m_userInvalidTime[station] = 0f;
                    continue;
                }
                m_userInvalidTime[station] += 1f;
                if (m_userInvalidTime[station] >= 3f)
                {
                    m_nview.GetZDO().Set(UserKey(station), 0L);
                    m_userInvalidTime[station] = 0f;
                }
            }
        }

        /// <summary>
        /// Управляющий на месте: жив и стоит в точке крепления (держащийся стоит ровно в ней; другие клиенты не видят,
        /// прикреплён ли игрок, поэтому проверяем по положению). Только что допущенный — ещё несколько секунд,
        /// пока его положение не дошло по сети.
        /// </summary>
        private bool IsValidUser(int station, long playerID)
        {
            if (playerID == 0L)
            {
                return false;
            }
            if (m_grantUser[station] == playerID && Time.time - m_grantTime[station] < 4f)
            {
                return true;
            }
            Player p = FindPlayer(playerID);
            BalloonStation st = GetStation(station);
            return p != null && !p.IsDead() && st != null && st.m_attachPoint != null &&
                   Vector3.Distance(p.transform.position, st.m_attachPoint.position) < 0.9f;
        }

        public static Player FindPlayer(long playerID)
        {
            if (playerID == 0L)
            {
                return null;
            }
            foreach (Player p in Player.GetAllPlayers())
            {
                if (p != null && p.GetPlayerID() == playerID)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>Большой шар подкидывает топливо в печь из своего сундука.</summary>
        private void FeedFromChest()
        {
            if (m_chest == null || !BalloonConfig.AutoFeed.Value || m_chest.IsInUse())
            {
                return;
            }
            ZDO zdo = m_nview.GetZDO();
            if ((BurnMode)zdo.GetInt(s_mode) == BurnMode.Down)
            {
                return;
            }
            float fuel = zdo.GetFloat(s_fuel);
            if (fuel >= 1f || fuel + 1f > Config.MaxFuel.Value)
            {
                return;
            }
            Inventory inv = m_chest.GetInventory();
            string fuelName = GetFuelDisplayName();
            if (inv == null || !inv.HaveItem(fuelName))
            {
                return;
            }
            inv.RemoveItem(fuelName, 1);
            m_nview.InvokeRPC(ZNetView.Everybody, "HAB_AddFuel", 1f);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (m_nview == null || !m_nview.IsValid() || !m_nview.IsOwner())
            {
                return;
            }
            if (collision.rigidbody != null && collision.rigidbody.GetComponent<Character>() != null)
            {
                return;
            }
            int n = collision.contactCount;
            for (int k = 0; k < n; k++)
            {
                if (collision.GetContact(k).normal.y > 0.5f)
                {
                    m_lastGroundContact = Time.time;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ RPC

        private void RPC_RequestControl(long sender, long playerID, int station)
        {
            if (ForwardIfNotOwner(sender, "HAB_RequestControl", playerID, station))
            {
                return;
            }
            BalloonStation st = GetStation(station);
            if (st == null)
            {
                return;
            }
            ZDO zdo = m_nview.GetZDO();
            long user = zdo.GetLong(UserKey(station));
            Player requester = FindPlayer(playerID);
            if (requester == null || requester.GetOwner() == 0L)
            {
                return;
            }
            // Отвечаем клиенту самого игрока: запрос мог прийти пересылкой через бывшего владельца.
            long target = requester.GetOwner();
            bool free = user == 0L || user == playerID || !IsValidUser(station, user);
            bool near = requester != null && Vector3.Distance(requester.transform.position, st.transform.position) < st.m_useDistance + 2f;
            if (free && near)
            {
                // Один игрок держится только за одно место.
                int other = station == BalloonStation.Fire ? BalloonStation.Helm : BalloonStation.Fire;
                if (zdo.GetLong(UserKey(other)) == playerID)
                {
                    zdo.Set(UserKey(other), 0L);
                }
                zdo.Set(UserKey(station), playerID);
                m_grantUser[station] = playerID;
                m_grantTime[station] = Time.time;
                m_userInvalidTime[station] = 0f;
                m_nview.InvokeRPC(target, "HAB_ControlResponse", true, station);
                // Владение — тому, кто у огня; рулевому — только если у огня никого.
                bool takeOwnership = station == BalloonStation.Fire || !IsValidUser(BalloonStation.Fire, zdo.GetLong(s_user));
                if (takeOwnership && target != ZDOMan.GetSessionID() && !IsChestOpenHere())
                {
                    // Симуляцию ведёт клиент управляющего — так управление без задержек.
                    // Как у сундука: сначала досылаем свежий ZDO, потом меняем владельца.
                    ZDOMan.instance.ForceSendZDO(target, zdo.m_uid);
                    zdo.SetOwner(target);
                }
            }
            else
            {
                m_nview.InvokeRPC(target, "HAB_ControlResponse", false, station);
            }
        }

        private void RPC_ReleaseControl(long sender, long playerID, int station)
        {
            if (ForwardIfNotOwner(sender, "HAB_ReleaseControl", playerID, station))
            {
                return;
            }
            if (GetUser(station) == playerID)
            {
                m_nview.GetZDO().Set(UserKey(station), 0L);
            }
        }

        private void RPC_ControlResponse(long sender, bool granted, int station)
        {
            Player player = Player.m_localPlayer;
            BalloonStation st = GetStation(station);
            if (player == null || st == null)
            {
                return;
            }
            if (!granted)
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return;
            }
            // Повторный запрос (место «потерялось» при смене владельца) — уже держимся, ничего не меняем.
            if (ReferenceEquals(player.GetDoodadController(), st) && player.IsAttached())
            {
                return;
            }
            if (player.GetDoodadController() != null && !ReferenceEquals(player.GetDoodadController(), st))
            {
                player.StopDoodadControl();
            }
            if (player.IsAttached())
            {
                player.AttachStop();
            }
            player.StartDoodadControl(st);
            if (st.m_attachPoint != null)
            {
                player.AttachStart(st.m_attachPoint, null, hideWeapons: false, isBed: false, onShip: true,
                    st.m_attachAnimation, st.m_detachOffset);
            }
        }

        private void RPC_ChangeMode(long sender, int delta)
        {
            if (ForwardIfNotOwner(sender, "HAB_ChangeMode", delta))
            {
                return;
            }
            int mode = Mathf.Clamp(m_nview.GetZDO().GetInt(s_mode) + delta, (int)BurnMode.Down, (int)BurnMode.Up);
            m_nview.GetZDO().Set(s_mode, mode);
        }

        private void RPC_ChangeSail(long sender, int delta)
        {
            if (ForwardIfNotOwner(sender, "HAB_ChangeSail", delta))
            {
                return;
            }
            int sails = Mathf.Clamp(m_nview.GetZDO().GetInt(s_sail) + delta, (int)SailLevel.Furled, (int)SailLevel.Full);
            m_nview.GetZDO().Set(s_sail, sails);
        }

        private void RPC_Rudder(long sender, float value)
        {
            if (!ForwardIfNotOwner(sender, "HAB_Rudder", value))
            {
                m_nview.GetZDO().Set(s_rudder, Mathf.Clamp(value, -1f, 1f));
            }
        }

        private void RPC_ToggleAnchor(long sender)
        {
            if (!ForwardIfNotOwner(sender, "HAB_ToggleAnchor"))
            {
                ZDO zdo = m_nview.GetZDO();
                zdo.Set(s_anchorOut, !zdo.GetBool(s_anchorOut));
            }
        }

        /// <summary>Рассылается всем: эффект видят все, топливо меняет только владелец.</summary>
        private void RPC_AddFuel(long sender, float amount)
        {
            if (m_burner != null)
            {
                m_burner.PlayFuelAddedEffect();
            }
            if (m_nview.IsOwner())
            {
                ZDO zdo = m_nview.GetZDO();
                float fuel = Mathf.Clamp(zdo.GetFloat(s_fuel) + amount, 0f, Config.MaxFuel.Value);
                zdo.Set(s_fuel, fuel);
            }
        }

        // ------------------------------------------------------------------ визуал (все клиенты)

        private void LateUpdate()
        {
            if (m_nview == null || !m_nview.IsValid())
            {
                return;
            }
            UpdateEnvelopeVisibility();
            UpdatePropeller();
            if (m_rudderPivots.Length == 0 && m_tillerPivots.Length == 0)
            {
                return;
            }
            m_rudderVisual = Mathf.MoveTowards(m_rudderVisual, GetDisplayRudder(), Time.deltaTime * 2f);
            Quaternion rot = Quaternion.Euler(0f, m_rudderVisual * m_rudderVisualAngle, 0f);
            foreach (Transform pivot in m_rudderPivots)
            {
                if (pivot != null)
                {
                    pivot.localRotation = rot;
                }
            }
            // Руль-палку толкают в сторону поворота: вправо — верх палки уходит вправо.
            Quaternion tilt = Quaternion.Euler(0f, 0f, -m_rudderVisual * m_tillerVisualAngle);
            foreach (Transform pivot in m_tillerPivots)
            {
                if (pivot != null)
                {
                    pivot.localRotation = tilt;
                }
            }
        }

        /// <summary>Винт крутится тем быстрее, чем больше гребцов (плавно разгоняется и останавливается).</summary>
        private void UpdatePropeller()
        {
            if (m_propeller == null || m_crankSeats.Length == 0)
            {
                return;
            }
            float target = GetCrankCount() / (float)m_crankSeats.Length * m_propellerMaxTurns;
            m_propellerTurns = Mathf.MoveTowards(m_propellerTurns, target, Time.deltaTime * 1.5f);
            if (m_propellerTurns > 0.001f)
            {
                m_propeller.Rotate(0f, 0f, m_propellerTurns * 360f * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>Камера (при отдалении или взгляде вверх) может оказаться внутри купола — тогда купол прячем.</summary>
        private void UpdateEnvelopeVisibility()
        {
            if (m_envelopeRenderer == null)
            {
                return;
            }
            Camera cam = Utils.GetMainCamera();
            bool hide = cam != null && IsInsideEnvelope(cam.transform.position, 0.4f);
            if (m_envelopeRenderer.enabled == hide)
            {
                m_envelopeRenderer.enabled = !hide;
            }
        }

        public bool IsInsideEnvelope(Vector3 worldPos, float margin)
        {
            if (m_envelopeHorizontal)
            {
                if (m_envelopeRadius <= 0f || m_envelopeHalfLength <= 0f)
                {
                    return false;
                }
                Vector3 q = transform.InverseTransformPoint(worldPos) - m_envelopeCenter;
                if (Mathf.Abs(q.z) > m_envelopeHalfLength + margin)
                {
                    return false;
                }
                float radius = Visuals.DrakkarModel.EnvelopeRadiusAt(q.z * Visuals.DrakkarModel.EnvelopeHalfLength / m_envelopeHalfLength)
                               * m_envelopeRadius / Visuals.DrakkarModel.EnvelopeRadius;
                return q.x * q.x + q.y * q.y < (radius + margin) * (radius + margin);
            }
            var profile = new Visuals.EnvelopeProfile
            {
                Radius = m_envelopeRadius,
                MouthRadius = m_envelopeMouthR,
                NeckHeight = m_envelopeNeckH,
                LowerHeight = m_envelopeLowerH,
                UpperHeight = m_envelopeUpperH,
                LowerExponent = m_envelopeLowerExp,
            };
            if (profile.LowerHeight <= 0f || profile.UpperHeight <= 0f)
            {
                return false;
            }
            Vector3 p = transform.InverseTransformPoint(worldPos);
            float y = p.y - m_envelopeMouthY;
            if (y < -margin || y > profile.Height + margin)
            {
                return false;
            }
            float r = y <= 0f ? profile.MouthRadius : profile.RadiusAt(Mathf.Min(y, profile.Height));
            return new Vector2(p.x, p.z).magnitude < r + margin;
        }

        // ------------------------------------------------------------------ Hoverable

        public string GetHoverText()
        {
            if (m_nview == null || !m_nview.IsValid())
            {
                return "";
            }
            float capacity = Config.Capacity.Value;
            float load = GetLoad();
            string loadColor = load > capacity ? "red" : "white";
            string text = $"{GetDisplayName()}\n$hab_hud_load: <color={loadColor}>{load:0}/{capacity:0} $hab_unit_kg</color>" +
                          $"\n$hab_hud_fuel: {GetFuelDisplayName()} {GetFuel():0.0}/{Config.MaxFuel.Value}";
            return Localization.instance.Localize(text);
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize(GetDisplayName());
        }

        public float GetHoverOffset()
        {
            return 0f;
        }
    }
}
