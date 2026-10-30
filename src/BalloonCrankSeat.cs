using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Сиденье с рукоятью драккара. Устроено как место управления (IDoodadController, как руль корабля): [E] — сесть;
    /// сидя, W — крутить, S — не крутить (каждый гребец решает за себя, севший сначала не крутит); встать — [E] или прыжок.
    /// Кручение тратит выносливость и качает навык бега, как бег (CrankStamina): выдохся — рукоять встаёт, снова крутить —
    /// новым нажатием W (у пилота ялика — Shift+W). Решение каждый клиент пишет в ZDO своего персонажа (номер сиденья, пока крутит),
    /// владелец шара собирает это в маску сидений (BalloonController); здесь же — анимация: рукоять крутится, пока бит
    /// сиденья в маске.
    /// </summary>
    public class BalloonCrankSeat : MonoBehaviour, Hoverable, Interactable, IDoodadController
    {
        public int m_index;

        /// <summary>Кривошип: крутится вокруг своей оси Z (ось вала).</summary>
        public Transform m_crank;
        public float m_turnsPerSecond = 0.9f;

        public string m_name = "$hab_crank_seat";
        public Transform m_attachPoint;
        public string m_attachAnimation = "attach_sitship";
        public Vector3 m_detachOffset = new Vector3(0f, 0.6f, 0.3f);
        public float m_useDistance = 2.5f;

        private BalloonController m_ctrl;
        private float m_speed;

        /// <summary>Где сидит локальный игрок и крутит ли он (пересел или встал — сначала не крутит).</summary>
        private static BalloonCrankSeat s_localSeat;
        private static bool s_localCranking;

        /// <summary>W был нажат в прошлый раз: крутить начинают нажатием, а не удержанием.</summary>
        private static bool s_forwardHeld;

        /// <summary>Сколько крутит без перерыва на прокачку: навык бега растёт раз в секунду, как у бега.</summary>
        private static float s_runSkillTimer;

        public BalloonController Controller => m_ctrl;

        /// <summary>Локальный игрок сейчас крутит рукоять.</summary>
        public static bool LocalCranking => s_localSeat != null && s_localCranking;

        /// <summary>Локальный игрок у руля «Небесного ялика» и крутит его рукоять (сменил место или встал — сначала не крутит).</summary>
        private static BalloonStation s_pilotStation;
        private static bool s_pilotCranking;

        public static bool LocalPilotCranking => s_pilotStation != null && s_pilotCranking;

        /// <summary>Пилот ялика: Shift+W — крутить рукоять, Shift+S — перестать.</summary>
        public static void SetPilotCranking(BalloonStation station, bool on)
        {
            TrackPilot(station);
            s_pilotCranking = on && CanStartCranking(Player.m_localPlayer);
        }

        /// <summary>Начать крутить: если рукоять тратит выносливость — только когда она есть (иначе мигает её полоска).</summary>
        private static bool CanStartCranking(Player player)
        {
            if (!BalloonConfig.CrankStamina.Value || player == null || player.HaveStamina())
            {
                return true;
            }
            StaminaEmptyFlash();
            return false;
        }

        /// <summary>
        /// Выносливость на рукоять за кадр — столько же, сколько на бег у этого персонажа: навык бега, снаряжение, эффекты
        /// и настройки мира; навык бега при этом растёт, как у бегущего (всё — как Player.CheckRun). false — выносливость кончилась.
        /// </summary>
        private static bool UseCrankStamina(Player player, float dt)
        {
            if (player.HaveStamina())
            {
                float drain = player.m_runStaminaDrain * Mathf.Lerp(1f, 0.5f, player.GetSkillFactor(Skills.SkillType.Run));
                drain -= drain * player.GetEquipmentMovementModifier();
                drain += drain * player.GetEquipmentRunStaminaModifier();
                player.GetSEMan().ModifyRunStaminaDrain(drain, ref drain, Vector3.zero);
                player.UseStamina(dt * drain * Game.m_moveStaminaRate);
                if (player.HaveStamina())
                {
                    s_runSkillTimer += dt;
                    if (s_runSkillTimer > 1f)
                    {
                        s_runSkillTimer = 0f;
                        player.RaiseSkill(Skills.SkillType.Run);
                    }
                    return true;
                }
            }
            StaminaEmptyFlash();
            return false;
        }

        private static void StaminaEmptyFlash()
        {
            if (Hud.instance != null)
            {
                Hud.instance.StaminaBarEmptyFlash();
            }
        }

        private void Awake()
        {
            m_ctrl = GetComponentInParent<BalloonController>();
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;
            if (player != null && ReferenceEquals(player.GetDoodadController(), this) && !player.IsAttached())
            {
                // Встал не через нас (например, телепорт) — отпустить и управление.
                player.StopDoodadControl();
            }
            if (m_ctrl == null || m_crank == null)
            {
                return;
            }
            bool busy = (m_ctrl.GetCrankMask() & (1 << m_index)) != 0;
            m_speed = Mathf.MoveTowards(m_speed, busy ? m_turnsPerSecond : 0f, Time.deltaTime * 2f);
            if (m_speed > 0.001f)
            {
                m_crank.Rotate(0f, 0f, m_speed * 360f * Time.deltaTime, Space.Self);
            }
        }

        /// <summary>Сиденье с рукоятью, на котором сидит игрок, или null.</summary>
        public static BalloonCrankSeat GetSeat(Player player)
        {
            Transform attach = player != null && player.IsAttached() ? player.GetAttachPoint() : null;
            return attach != null ? attach.GetComponentInParent<BalloonCrankSeat>() : null;
        }

        /// <summary>
        /// Каждый кадр: метка в ZDO персонажа локального игрока — номер сиденья + 1, пока он на нём крутит, иначе 0.
        /// По этим меткам владелец шара считает гребцов.
        /// </summary>
        public static void ReportLocal()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            ZNetView nview = player.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            BalloonCrankSeat seat = GetSeat(player);
            TrackLocalSeat(seat);
            // Пилот ялика за рулём-палкой — сам себе гребец номер 0.
            BalloonStation pilot = player.GetDoodadController() as BalloonStation;
            if (pilot != null && (!pilot.IsValid() || pilot.StationIndex != BalloonStation.Helm || !pilot.Controller.m_pilotCrank))
            {
                pilot = null;
            }
            TrackPilot(pilot);
            bool cranking = seat != null ? s_localCranking : pilot != null && s_pilotCranking;
            // Выдохся — рукоять встала; снова крутить — новым нажатием W (у пилота — Shift+W): удерживать мало.
            if (cranking && BalloonConfig.CrankStamina.Value && !UseCrankStamina(player, Time.deltaTime))
            {
                s_localCranking = false;
                s_pilotCranking = false;
                cranking = false;
                player.Message(MessageHud.MessageType.Center, seat != null ? "$hab_msg_crank_tired" : "$hab_msg_crank_tired_pilot");
            }
            int value = !cranking ? 0 : seat != null ? seat.m_index + 1 : 1;
            if (nview.GetZDO().GetInt(BalloonController.s_playerCrankSeat) != value)
            {
                nview.GetZDO().Set(BalloonController.s_playerCrankSeat, value);
            }
        }

        private static void TrackLocalSeat(BalloonCrankSeat seat)
        {
            if (seat != s_localSeat)
            {
                s_localSeat = seat;
                s_localCranking = false;
            }
        }

        private static void TrackPilot(BalloonStation station)
        {
            if (station != s_pilotStation)
            {
                s_pilotStation = station;
                s_pilotCranking = false;
            }
        }

        /// <summary>Занято: кто-то уже сидит в точке сиденья.</summary>
        private bool IsInUse()
        {
            return Player.GetClosestPlayer(m_attachPoint.position, 0.25f) != null;
        }

        private bool InUseDistance(Humanoid human)
        {
            return human != null && Vector3.Distance(human.transform.position, m_attachPoint.position) < m_useDistance;
        }

        // ------------------------------------------------------------------ Interactable / Hoverable

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            Player player = user as Player;
            if (hold || !IsValid() || player == null || !InUseDistance(player))
            {
                return false;
            }
            if (ReferenceEquals(player.GetDoodadController(), this))
            {
                player.StopDoodadControl();
                return false;
            }
            if (IsInUse())
            {
                player.Message(MessageHud.MessageType.Center, "$msg_inuse");
                return false;
            }
            if (player.IsEncumbered())
            {
                return false;
            }
            if (player.GetDoodadController() != null)
            {
                player.StopDoodadControl();
            }
            if (player.IsAttached())
            {
                player.AttachStop();
            }
            player.AttachStart(m_attachPoint, null, hideWeapons: false, isBed: false, onShip: true, m_attachAnimation, m_detachOffset);
            player.StartDoodadControl(this);
            return false;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }

        public string GetHoverText()
        {
            if (!InUseDistance(Player.m_localPlayer))
            {
                return Localization.instance.Localize("<color=#888888>$piece_toofar</color>");
            }
            return Localization.instance.Localize($"{m_name}\n[<color=yellow><b>$KEY_Use</b></color>] $hab_sit_crank");
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize(m_name);
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        // ------------------------------------------------------------------ IDoodadController

        /// <summary>[E] (ни на что не наведено, пока сидишь), прыжок или атака — встать.</summary>
        public void OnUseStop(Player player)
        {
            player.AttachStop();
        }

        /// <summary>Сидящий гребец: нажал W — крутить, S — не крутить.</summary>
        public void ApplyControlls(Vector3 moveDir, Vector3 lookDir, bool run, bool autoRun, bool block)
        {
            TrackLocalSeat(this);
            bool forward = moveDir.z > 0.5f;
            // Начинают крутить нажатием W, а не удержанием: выдохшийся гребец, не отпуская W, снова не закрутит.
            if (forward && !s_forwardHeld)
            {
                s_localCranking = CanStartCranking(Player.m_localPlayer);
            }
            else if (moveDir.z < -0.5f)
            {
                s_localCranking = false;
            }
            s_forwardHeld = forward;
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
            return this != null && m_ctrl != null && m_ctrl.NView != null && m_ctrl.NView.IsValid();
        }
    }
}
