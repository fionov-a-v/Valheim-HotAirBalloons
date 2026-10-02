using System.Text;
using HotAirBalloons.Sim;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Панель шара для того, кто на нём находится (скорость, ветер, руль, паруса или винт, клавиши),
    /// плюс отправка в ZDO персонажа локального игрока веса инвентаря и того, крутит ли он рукоять, —
    /// по ним владелец шара считает груз и гребцов.
    /// </summary>
    public class BalloonHud : MonoBehaviour
    {
        private string m_text;
        private float m_refreshTimer;
        private float m_weightTimer;
        private GUIStyle m_style;
        private Texture2D m_background;
        private readonly StringBuilder m_sb = new StringBuilder();

        private void Update()
        {
            ReportLocalWeight();
            BalloonCrankSeat.ReportLocal();
            m_refreshTimer -= Time.deltaTime;
            if (m_refreshTimer <= 0f)
            {
                m_refreshTimer = 0.2f;
                Rebuild();
            }
        }

        private void ReportLocalWeight()
        {
            m_weightTimer -= Time.deltaTime;
            if (m_weightTimer > 0f)
            {
                return;
            }
            m_weightTimer = 1f;
            Player player = Player.m_localPlayer;
            if (player == null || player.GetInventory() == null)
            {
                return;
            }
            ZNetView nview = player.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
            {
                return;
            }
            // Сравниваем с тем, что реально лежит в ZDO персонажа: после перезахода ZDO новый, а вес мог не измениться.
            float weight = player.GetInventory().GetTotalWeight();
            float stored = nview.GetZDO().GetFloat(BalloonController.s_playerInventoryWeight, -1f);
            if (Mathf.Abs(weight - stored) > 0.05f)
            {
                nview.GetZDO().Set(BalloonController.s_playerInventoryWeight, weight);
            }
        }

        public static string ModeText(BalloonController b)
        {
            BurnMode mode = b.GetMode();
            if (mode != BurnMode.Down && b.GetFuel() <= 0f)
            {
                return "<color=#ff6060>$hab_no_fuel</color>";
            }
            switch (mode)
            {
                case BurnMode.Up:
                    return "<color=#ffb040>$hab_mode_up</color>";
                case BurnMode.Hold:
                    return "<color=#ffe080>$hab_mode_hold</color>";
                default:
                    return "<color=#a0c0ff>$hab_mode_down</color>";
            }
        }

        public static string SailText(BalloonController b)
        {
            switch (b.GetSails())
            {
                case SailLevel.Full:
                    return "<color=#ffe080>$hab_sails_full</color>";
                case SailLevel.Half:
                    return "$hab_sails_half";
                default:
                    return "<color=#c0c0c0>$hab_sails_furled</color>";
            }
        }

        public static string AnchorText(BalloonController b)
        {
            switch (b.GetAnchorState())
            {
                case AnchorState.Holding:
                    return "<color=#80ff80>$hab_anchor_holding</color>";
                case AnchorState.Hanging:
                    return "$hab_anchor_hanging";
                default:
                    return b.IsAnchorOut() ? "$hab_anchor_hanging" : "$hab_anchor_raised";
            }
        }

        private void Rebuild()
        {
            m_text = null;
            Player player = Player.m_localPlayer;
            if (player == null || !BalloonConfig.ShowHud.Value)
            {
                return;
            }

            BalloonController b = null;
            BalloonStation station = null;
            if (player.GetDoodadController() is BalloonStation st && st.IsValid())
            {
                b = st.Controller;
                station = b != null ? st : null;
            }
            if (b == null)
            {
                b = BalloonController.FindOnboard(player);
            }
            if (b == null)
            {
                return;
            }
            BalloonCrankSeat crankSeat = station == null ? BalloonCrankSeat.GetSeat(player) : null;
            if (crankSeat != null && crankSeat.Controller != b)
            {
                crankSeat = null;
            }

            // Высота, груз и якорь — в правом блоке корабельного HUD (BalloonShipHud), огонь и топливо — там же
            // (иконка паруса) и на самом огне. Здесь — скорость, ветер, руль, паруса или винт и подсказка по клавишам.
            KindConfig cfg = b.Config;
            Vector3 vel = b.Body != null ? b.Body.linearVelocity : Vector3.zero;
            float hSpeed = new Vector2(vel.x, vel.z).magnitude;

            m_sb.Length = 0;
            m_sb.Append("<b>").Append(b.GetDisplayName()).Append("</b>\n");

            string arrow = vel.y > 0.15f ? "↑" : (vel.y < -0.15f ? "↓" : "•");
            m_sb.Append(arrow).Append(' ').Append(Mathf.Abs(vel.y).ToString("0.0")).Append(" $hab_unit_ms   → ")
                .Append(hSpeed.ToString("0.0")).Append(" $hab_unit_ms");
            if (EnvMan.instance != null)
            {
                float wind = BalloonConfig.WindSpeed.Value * Mathf.Lerp(0.25f, 1f, EnvMan.instance.GetWindIntensity());
                m_sb.Append("   $hab_hud_wind: ").Append(wind.ToString("0.0")).Append(" $hab_unit_ms");
            }
            // На плаву всё вдвое медленнее (WaterSpeedPercent) — и ветер, и паруса, и винт.
            float waterFactor = b.IsOnWater() ? BalloonConfig.WaterSpeedPercent.Value / 100f : 1f;
            if (b.IsCalmHere())
            {
                m_sb.Append(" ($hab_calm)");
            }
            else if (waterFactor < 1f)
            {
                m_sb.Append(" ($hab_on_water x").Append(waterFactor.ToString("0.##")).Append(')');
            }
            m_sb.Append('\n');

            if (b.HasPropeller)
            {
                float rudder = b.GetDisplayRudder() * 100f;
                m_sb.Append("$hab_hud_propeller: ").Append(b.GetCrankCount()).Append('/').Append(b.m_crankSeats.Length)
                    .Append(" $hab_hud_rowers, ").Append((b.GetPropellerSpeed() * waterFactor).ToString("0.0")).Append(" $hab_unit_ms")
                    .Append("   $hab_hud_rudder: ").Append(rudder >= 0f ? "+" : "").Append(rudder.ToString("0")).Append('%');
                if (crankSeat != null)
                {
                    m_sb.Append("   $hab_hud_crank: ").Append(BalloonCrankSeat.LocalCranking ? "<color=#ffe080>$hab_crank_on</color>" : "$hab_crank_off");
                }
            }
            else
            {
                if (b.HasRudder && cfg.RudderPercent != null)
                {
                    float angle = b.GetDisplayRudder() * cfg.RudderPercent.Value / 100f * 180f;
                    m_sb.Append("$hab_hud_rudder: ").Append(angle >= 0f ? "+" : "").Append(angle.ToString("0")).Append("°   ");
                }
                if (b.HasSails)
                {
                    m_sb.Append("$hab_hud_sails: ").Append(SailText(b)).Append(" (x").Append(b.GetSailSpeedFactor().ToString("0.#")).Append(')');
                }
            }
            if (station != null)
            {
                string keys = station.StationIndex == BalloonStation.Helm ? (b.HasSails ? "$hab_hud_keys_tiller" : "$hab_hud_keys_helm")
                    : station.m_steering && b.HasRudder ? "$hab_hud_keys_rudder" : "$hab_hud_keys";
                m_sb.Append("\n<color=#c0c0c0>").Append(keys).Append("</color>");
            }
            else if (crankSeat != null)
            {
                m_sb.Append("\n<color=#c0c0c0>$hab_hud_keys_crank</color>");
            }
            m_text = Localization.instance.Localize(m_sb.ToString().TrimEnd());
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(m_text) || Player.m_localPlayer == null)
            {
                return;
            }
            if (Hud.IsUserHidden() || InventoryGui.IsVisible() || Menu.IsVisible() || Minimap.IsOpen() || TextInput.IsVisible() ||
                StoreGui.IsVisible() || Console.IsVisible())
            {
                return;
            }
            if (m_style == null)
            {
                m_background = new Texture2D(1, 1);
                m_background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                m_background.Apply();
                m_style = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = false,
                    padding = new RectOffset(12, 12, 8, 8),
                };
                m_style.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
                m_style.normal.background = m_background;
            }
            m_style.fontSize = Mathf.Clamp(Screen.height / 62, 12, 26);
            var content = new GUIContent(m_text);
            Vector2 size = m_style.CalcSize(content);
            var rect = new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.075f, size.x, size.y);
            GUI.Label(rect, content, m_style);
        }
    }
}
