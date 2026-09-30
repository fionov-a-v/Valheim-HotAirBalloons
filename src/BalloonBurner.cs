using HotAirBalloons.Sim;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Источник огня (чаша / горелка / печь). W/S — огонь сильнее/слабее (вниз → держать высоту → вверх),
    /// ПКМ — якорь; у шаров без руля-палки ещё и A/D — руль. Здесь же подкидывают топливо.
    /// </summary>
    public class BalloonBurner : BalloonStation
    {
        public GameObject[] m_fireObjects = new GameObject[0];
        public EffectList m_fuelAddedEffects = new EffectList();

        private ParticleSystem[] m_particles = new ParticleSystem[0];
        private float[] m_baseRate = new float[0];
        private float[] m_baseSize = new float[0];
        private int m_visualState = -1;

        public override int StationIndex => Fire;

        protected override void Awake()
        {
            base.Awake();
            var list = new System.Collections.Generic.List<ParticleSystem>();
            foreach (GameObject go in m_fireObjects)
            {
                if (go != null)
                {
                    list.AddRange(go.GetComponentsInChildren<ParticleSystem>(true));
                }
            }
            m_particles = list.ToArray();
            m_baseRate = new float[m_particles.Length];
            m_baseSize = new float[m_particles.Length];
            for (int i = 0; i < m_particles.Length; i++)
            {
                m_baseRate[i] = m_particles[i].emission.rateOverTimeMultiplier;
                m_baseSize[i] = m_particles[i].main.startSizeMultiplier;
            }
        }

        protected override void Update()
        {
            if (Valid)
            {
                UpdateFireVisual();
            }
            base.Update();
        }

        protected override void ChangeLevel(int delta)
        {
            m_nview.InvokeRPC("HAB_ChangeMode", delta);
        }

        private void UpdateFireVisual()
        {
            BurnMode mode = m_ctrl.GetMode();
            int state = mode == BurnMode.Down || m_ctrl.GetFuel() <= 0f ? 0 : (int)mode;
            if (state == m_visualState)
            {
                return;
            }
            m_visualState = state;
            foreach (GameObject go in m_fireObjects)
            {
                if (go != null)
                {
                    go.SetActive(state > 0);
                }
            }
            // На полном огне пламя выше и гуще.
            float rateMul = state == (int)BurnMode.Up ? 1.8f : 1f;
            float sizeMul = state == (int)BurnMode.Up ? 1.35f : 1f;
            for (int i = 0; i < m_particles.Length; i++)
            {
                if (m_particles[i] == null)
                {
                    continue;
                }
                ParticleSystem.EmissionModule emission = m_particles[i].emission;
                emission.rateOverTimeMultiplier = m_baseRate[i] * rateMul;
                ParticleSystem.MainModule main = m_particles[i].main;
                main.startSizeMultiplier = m_baseSize[i] * sizeMul;
            }
        }

        public void PlayFuelAddedEffect()
        {
            m_fuelAddedEffects?.Create(transform.position, transform.rotation);
        }

        /// <summary>Подбросить одну единицу топлива из инвентаря.</summary>
        public bool TryAddFuel(Humanoid user, bool silentIfMissing, ItemDrop.ItemData specificItem = null)
        {
            if (!Valid)
            {
                return false;
            }
            ItemDrop fuelItem = m_ctrl.GetFuelItem();
            if (fuelItem == null)
            {
                return false;
            }
            string fuelName = fuelItem.m_itemData.m_shared.m_name;
            if (Mathf.CeilToInt(m_ctrl.GetFuel()) >= m_ctrl.Config.MaxFuel.Value)
            {
                if (!silentIfMissing)
                {
                    user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantaddmore", fuelName));
                }
                return false;
            }
            Inventory inventory = user.GetInventory();
            if (inventory == null)
            {
                return false;
            }
            if (specificItem != null)
            {
                // Предмет должен быть именно в инвентаре игрока (не в открытом сундуке), иначе топливо «из воздуха».
                if (!inventory.ContainsItem(specificItem) || !inventory.RemoveItem(specificItem, 1))
                {
                    return false;
                }
            }
            else if (inventory.HaveItem(fuelName))
            {
                inventory.RemoveItem(fuelName, 1);
            }
            else
            {
                if (!silentIfMissing)
                {
                    user.Message(MessageHud.MessageType.Center, "$msg_outof " + fuelName);
                }
                return false;
            }
            user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", fuelName));
            m_nview.InvokeRPC(ZNetView.Everybody, "HAB_AddFuel", 1f);
            return true;
        }

        public override bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (alt && !hold && Valid && user is Player player && InUseDistance(player))
            {
                return TryAddFuel(player, silentIfMissing: false);
            }
            return base.Interact(user, hold, alt);
        }

        public override bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            if (!Valid || item == null)
            {
                return false;
            }
            ItemDrop fuelItem = m_ctrl.GetFuelItem();
            if (fuelItem == null || item.m_shared.m_name != fuelItem.m_itemData.m_shared.m_name)
            {
                return false;
            }
            TryAddFuel(user, silentIfMissing: false, specificItem: item);
            return true;
        }

        public override string GetHoverText()
        {
            if (!Valid)
            {
                return "";
            }
            Player player = Player.m_localPlayer;
            if (!InUseDistance(player))
            {
                return Localization.instance.Localize("<color=#888888>$piece_toofar</color>");
            }
            string fuelName = m_ctrl.GetFuelDisplayName();
            string altKey = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive() ? "$KEY_AltKeys" : "$KEY_AltPlace";
            string text = $"{m_name} ( {fuelName} {Mathf.CeilToInt(m_ctrl.GetFuel())}/{m_ctrl.Config.MaxFuel.Value} )" +
                          $"\n$hab_hud_fire: {BalloonHud.ModeText(m_ctrl)}" +
                          $"\n[<color=yellow><b>$KEY_Use</b></color>] {m_useText}" +
                          $"\n[<color=yellow><b>{altKey} + $KEY_Use</b></color>] $hab_add_fuel {fuelName}" +
                          "\n[<color=yellow><b>$KEY_HotbarUse</b></color>] $piece_useitem";
            return Localization.instance.Localize(text);
        }
    }
}
