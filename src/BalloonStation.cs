using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Место управления шаром, за которое держится игрок: источник огня (у всех шаров) или руль-палка (у среднего).
    /// Общее: занять место (запрос владельцу шара), держать позу, W/S — «сильнее/слабее» (огонь или паруса),
    /// ПКМ — якорь, A/D — руль (если это место управляет рулём), автоподкидывание топлива.
    /// </summary>
    public abstract class BalloonStation : MonoBehaviour, Hoverable, Interactable, IDoodadController
    {
        public const int Fire = 0;
        public const int Helm = 1;

        public string m_name = "$hab_burner";
        public string m_useText = "$hab_take_control";
        public Transform m_attachPoint;
        public string m_attachAnimation = "";
        public Vector3 m_detachOffset = new Vector3(0f, 0.05f, 0f);
        public float m_useDistance = 3f;

        /// <summary>A/D с этого места поворачивают руль.</summary>
        public bool m_steering;

        protected BalloonController m_ctrl;
        protected ZNetView m_nview;

        private bool m_forwardPressed;
        private bool m_backwardPressed;
        private bool m_blockPressed;
        private bool m_hasLocalRudder;
        private float m_localRudder;
        private float m_sendRudderTime;
        private float m_feedTimer;
        private float m_notUserTime;
        private float m_requestTime = -10f;
        private float m_staleTime;
        private float m_releaseTime = -10f;

        /// <summary>Какое это место: <see cref="Fire"/> или <see cref="Helm"/> (у каждого свой управляющий в ZDO).</summary>
        public abstract int StationIndex { get; }

        public BalloonController Controller => m_ctrl;

        /// <summary>Нажатие A/D прямо сейчас (-1..1) — для анимации штурвала в корабельном HUD.</summary>
        public float RudderInput { get; private set; }

        protected bool Valid => m_ctrl != null && m_nview != null && m_nview.IsValid();

        protected virtual void Awake()
        {
            m_ctrl = GetComponentInParent<BalloonController>();
            m_nview = GetComponentInParent<ZNetView>();
        }

        protected virtual void Update()
        {
            if (Valid)
            {
                UpdateLocalControl();
            }
        }

        /// <summary>W (+1) / S (-1): огонь сильнее/слабее или паруса шире/уже.</summary>
        protected abstract void ChangeLevel(int delta);

        public bool IsLocalUser()
        {
            Player player = Player.m_localPlayer;
            return player != null && ReferenceEquals(player.GetDoodadController(), this);
        }

        /// <summary>Действия на клиенте того, кто держится за это место.</summary>
        private void UpdateLocalControl()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !ReferenceEquals(player.GetDoodadController(), this))
            {
                m_hasLocalRudder = false;
                m_notUserTime = 0f;
                RudderInput = 0f;
                ReleaseIfStale(player);
                return;
            }
            m_staleTime = 0f;

            if (!player.IsAttached())
            {
                player.StopDoodadControl();
                return;
            }

            long user = m_ctrl.GetUser(StationIndex);
            if (user == player.GetPlayerID())
            {
                m_notUserTime = 0f;
            }
            else if (user == 0L)
            {
                // Место свободно — значит, наш запрос или отпускание потерялись при смене владельца шара: просим снова.
                m_notUserTime += Time.deltaTime;
                if (Time.time - m_requestTime > 1f)
                {
                    SendRequest(player);
                }
                if (m_notUserTime > 6f)
                {
                    player.StopDoodadControl();
                    return;
                }
            }
            else
            {
                // Место перехватил кто-то другой.
                m_notUserTime += Time.deltaTime;
                if (m_notUserTime > 2f)
                {
                    player.StopDoodadControl();
                    return;
                }
            }

            // Автоподкидывание топлива из инвентаря управляющего (с любого места управления).
            m_feedTimer -= Time.deltaTime;
            if (m_feedTimer <= 0f)
            {
                m_feedTimer = 1f;
                if (BalloonConfig.AutoFeed.Value && m_ctrl.m_burner != null && m_ctrl.GetMode() != Sim.BurnMode.Down && m_ctrl.GetFuel() < 1f)
                {
                    m_ctrl.m_burner.TryAddFuel(player, silentIfMissing: true);
                }
            }
        }

        /// <summary>
        /// В ZDO мы всё ещё числимся за этим местом, хотя уже не держимся (отпускание потерялось при смене владельца,
        /// выход из игры за управлением): через несколько секунд отпускаем место сами.
        /// </summary>
        private void ReleaseIfStale(Player player)
        {
            if (player == null || m_ctrl.GetUser(StationIndex) != player.GetPlayerID() || Time.time - m_requestTime < 3f)
            {
                m_staleTime = 0f;
                return;
            }
            m_staleTime += Time.deltaTime;
            if (m_staleTime > 2f && Time.time - m_releaseTime > 2f)
            {
                m_releaseTime = Time.time;
                m_nview.InvokeRPC("HAB_ReleaseControl", player.GetPlayerID(), StationIndex);
            }
        }

        private void SendRequest(Player player)
        {
            m_requestTime = Time.time;
            m_nview.InvokeRPC("HAB_RequestControl", player.GetPlayerID(), StationIndex);
        }

        public bool TryGetLocalRudder(out float value)
        {
            value = m_localRudder;
            return m_hasLocalRudder && IsLocalUser();
        }

        protected bool InUseDistance(Humanoid human)
        {
            return human != null && Vector3.Distance(human.transform.position, transform.position) < m_useDistance;
        }

        // ------------------------------------------------------------------ Interactable / Hoverable

        public virtual bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid)
            {
                return false;
            }
            Player player = user as Player;
            if (player == null || !InUseDistance(player))
            {
                return false;
            }
            if (ReferenceEquals(player.GetDoodadController(), this))
            {
                player.StopDoodadControl();
                return false;
            }
            SendRequest(player);
            return false;
        }

        public virtual bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        public abstract string GetHoverText();

        public string GetHoverName()
        {
            return Localization.instance.Localize(m_name);
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        // ------------------------------------------------------------------ IDoodadController

        public void OnUseStop(Player player)
        {
            m_hasLocalRudder = false;
            if (Valid)
            {
                m_nview.InvokeRPC("HAB_ReleaseControl", player.GetPlayerID(), StationIndex);
            }
            player.AttachStop();
        }

        public void ApplyControlls(Vector3 moveDir, Vector3 lookDir, bool run, bool autoRun, bool block)
        {
            if (!Valid)
            {
                return;
            }
            bool forward = moveDir.z > 0.5f;
            bool backward = moveDir.z < -0.5f;
            if (forward && !m_forwardPressed)
            {
                ChangeLevel(1);
            }
            if (backward && !m_backwardPressed)
            {
                ChangeLevel(-1);
            }
            m_forwardPressed = forward;
            m_backwardPressed = backward;

            if (block && !m_blockPressed)
            {
                m_nview.InvokeRPC("HAB_ToggleAnchor");
            }
            m_blockPressed = block;

            if (!m_steering || !m_ctrl.HasRudder)
            {
                RudderInput = 0f;
                return;
            }
            RudderInput = Mathf.Clamp(moveDir.x, -1f, 1f);
            if (!m_hasLocalRudder)
            {
                m_hasLocalRudder = true;
                m_localRudder = m_ctrl.GetRudder();
            }
            m_localRudder = Mathf.Clamp(m_localRudder + moveDir.x * 0.6f * Time.deltaTime, -1f, 1f);
            // Шлём, пока руль в ZDO не совпадёт с нашим: отдельный вызов может потеряться при смене владельца шара.
            if (Time.time - m_sendRudderTime > 0.25f && Mathf.Abs(m_localRudder - m_ctrl.GetRudder()) > 0.001f)
            {
                m_sendRudderTime = Time.time;
                m_nview.InvokeRPC("HAB_Rudder", m_localRudder);
            }
        }

        public Component GetControlledComponent()
        {
            return this;
        }

        public Vector3 GetPosition()
        {
            return transform.position;
        }

        public bool IsValid()
        {
            return this != null && Valid;
        }
    }
}
