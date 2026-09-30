using HotAirBalloons.Sim;
using HotAirBalloons.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotAirBalloons
{
    /// <summary>
    /// Ванильный корабельный HUD (иконка паруса справа вверху, циферблат ветра, штурвал с дугой руля и стрелками хода)
    /// для всех, кто на шаре. Логика — как в Hud.UpdateShipHud, углы — шара:
    /// <list type="bullet">
    /// <item>иконка справа вверху: у «Летучего карви» — его паруса, как у корабля (сложены — значок руля, наполовину, полностью);
    /// у остальных — огонь («вверх» — полный парус, «держать высоту» — половина, «вниз» — ничего);</item>
    /// <item>рядом (добавлено модом): значок выброшенного якоря (белый — висит, зелёный — держит, голубой — штиль),
    /// под циферблатом — высота и груз (красный при перегрузе);</item>
    /// <item>штурвал со стрелками — только у того, кто держится за огонь или руль-палку: стрелки — то, чем управляют W/S
    /// с этого места (паруса у руля-палки карви, иначе огонь);</item>
    /// <item>циферблат: курс шара относительно камеры и ветер относительно шара; стрелка ветра тускнеет,
    /// когда руль забирает скорость или (у драккара) ветер встречный, и гаснет в штиль у якоря;</item>
    /// <item>дуга руля — настоящий угол отклонения от ветра (до 54° у карви), у драккара — положение руля, как у корабля.</item>
    /// </list>
    /// </summary>
    internal static class BalloonShipHud
    {
        private static readonly Color s_windIconDark = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color s_anchorHanging = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color s_anchorHolding = new Color(0.55f, 1f, 0.5f, 1f);
        private static readonly Color s_anchorCalm = new Color(0.55f, 0.85f, 1f, 1f);

        private static TextMeshProUGUI s_altitude;
        private static TextMeshProUGUI s_load;
        private static TextMeshProUGUI s_warning;
        private static Image s_anchor;
        private static Sprite s_anchorSprite;
        private static float s_refresh;

        /// <summary>
        /// Шар не наш — ванильная логика корабля: вернуть штурвал (ванильный код его никогда не прячет,
        /// поэтому просто включаем, если выключили мы) и спрятать добавленное.
        /// </summary>
        public static void Restore(Hud hud)
        {
            if (hud.m_shipControlsRoot != null && !hud.m_shipControlsRoot.activeSelf)
            {
                hud.m_shipControlsRoot.SetActive(true);
            }
            SetExtrasActive(false);
        }

        /// <param name="station">Место, за которое держится игрок, или null — пассажир (тогда без штурвала).</param>
        public static void Update(Hud hud, BalloonController balloon, BalloonStation station, float dt)
        {
            // Как у корабля: пока HUD скрыт (например, Ctrl+F3), ничего не трогаем.
            if (!hud.IsVisible())
            {
                return;
            }
            hud.m_shipHudRoot.SetActive(true);

            BurnMode mode = balloon.GetMode();
            bool burning = balloon.IsBurning();
            int fire = !burning ? 0 : (mode == BurnMode.Up ? 2 : 1);
            float rudder = balloon.HasRudder ? balloon.GetDisplayRudder() : 0f;

            if (balloon.HasSails)
            {
                SailLevel sails = balloon.GetSails();
                hud.m_halfSail.SetActive(sails == SailLevel.Half);
                hud.m_fullSail.SetActive(sails == SailLevel.Full);
                // Сложенные паруса — как «медленно» у корабля: вместо паруса значок руля.
                hud.m_rudder.SetActive(sails == SailLevel.Furled);
            }
            else
            {
                hud.m_halfSail.SetActive(fire == 1);
                hud.m_fullSail.SetActive(fire == 2);
                hud.m_rudder.SetActive(balloon.HasRudder && Mathf.Abs(rudder) > 0.02f);
            }

            Camera cam = Utils.GetMainCamera();
            Transform t = balloon.transform;
            if (cam != null)
            {
                float yaw = -Utils.YawFromDirection(cam.transform.InverseTransformDirection(t.forward));
                hud.m_shipWindIndicatorRoot.localRotation = Quaternion.Euler(0f, 0f, yaw);
            }
            Vector3 wind = balloon.GetWindDirForDisplay(Player.m_localPlayer);
            float windAngle = -Utils.YawFromDirection(t.InverseTransformDirection(wind));
            hud.m_shipWindIconRoot.localRotation = Quaternion.Euler(0f, 0f, windAngle);
            hud.m_shipWindIcon.color = Color.Lerp(s_windIconDark, Color.white, balloon.IsCalmHere() ? 0f : balloon.GetWindFactor(wind, rudder));

            UpdateControls(hud, balloon, station, fire, rudder, dt, cam);
            UpdateExtras(hud, balloon, dt);
        }

        /// <summary>Штурвал со стрелками и дугой руля — у того, кто держится за место управления.</summary>
        private static void UpdateControls(Hud hud, BalloonController balloon, BalloonStation station, int fire, float rudder, float dt, Camera cam)
        {
            if (station == null)
            {
                if (hud.m_shipControlsRoot.activeSelf)
                {
                    hud.m_shipControlsRoot.SetActive(false);
                }
                return;
            }
            if (!hud.m_shipControlsRoot.activeSelf)
            {
                hud.m_shipControlsRoot.SetActive(true);
            }

            // Стрелки хода: у руля-палки карви — паруса (одна, две, три), иначе — огонь (две, три).
            bool sailArrows = station.StationIndex == BalloonStation.Helm && balloon.HasSails;
            int level = sailArrows ? 1 + (int)balloon.GetSails() : fire + (fire > 0 ? 1 : 0);
            hud.m_rudderSlow.SetActive(level == 1);
            hud.m_rudderForward.SetActive(level == 2);
            hud.m_rudderFastForward.SetActive(level == 3);
            hud.m_rudderBackward.SetActive(false);
            hud.m_rudderLeft.SetActive(false);
            hud.m_rudderRight.SetActive(false);

            // Штурвал крутится, пока руль двигается (как у корабля).
            float input = station.m_steering && balloon.HasRudder ? station.RudderInput : 0f;
            if ((input > 0f && rudder < 1f) || (input < 0f && rudder > -1f))
            {
                hud.m_shipRudderIcon.transform.Rotate(new Vector3(0f, 0f, 200f * -input * dt));
            }

            // Дуга руля: угол отклонения курса от ветра, у драккара — положение руля (до упора — четверть круга).
            float angle = rudder * balloon.RudderMaxAngleDeg;
            if (Mathf.Abs(angle) < 0.5f)
            {
                hud.m_shipRudderIndicator.gameObject.SetActive(false);
            }
            else
            {
                hud.m_shipRudderIndicator.gameObject.SetActive(true);
                hud.m_shipRudderIndicator.fillClockwise = angle > 0f;
                hud.m_shipRudderIndicator.fillAmount = Mathf.Abs(angle) / 360f;
            }

            if (cam != null)
            {
                hud.m_shipControlsRoot.transform.position = cam.WorldToScreenPointScaled(balloon.GetControlGuiPosition(station));
            }
        }

        // ------------------------------------------------------------------ высота, груз, якорь

        private static void UpdateExtras(Hud hud, BalloonController balloon, float dt)
        {
            if (!BalloonConfig.ShowHud.Value)
            {
                SetExtrasActive(false);
                return;
            }
            EnsureExtras(hud);
            SetExtrasActive(true);

            // Якорь: виден, пока выпущен.
            bool anchorOut = balloon.IsAnchorOut();
            if (s_anchor != null)
            {
                s_anchor.enabled = anchorOut;
                if (anchorOut)
                {
                    s_anchor.color = balloon.IsCalmHere() ? s_anchorCalm
                        : balloon.GetAnchorState() == AnchorState.Holding ? s_anchorHolding : s_anchorHanging;
                }
            }

            s_refresh -= dt;
            if (s_refresh > 0f)
            {
                return;
            }
            s_refresh = 0.2f;
            float agl = Mathf.Max(0f, balloon.GetLocalAgl());
            if (s_altitude != null)
            {
                s_altitude.text = Localization.instance.Localize(
                    $"$hab_hud_altitude: {agl:0} / {BalloonConfig.MaxAltitude.Value:0} $hab_unit_m");
            }
            if (s_load != null)
            {
                float load = balloon.GetLoad();
                float capacity = balloon.Config.Capacity.Value;
                string color = load > capacity ? "#ff5050" : "#ffffff";
                s_load.text = Localization.instance.Localize($"$hab_hud_load: <color={color}>{load:0} / {capacity:0} $hab_unit_kg</color>");
            }
            // Огонь и топливо в панели больше не показываются — предупреждаем, только когда огонь просят, а гореть нечему.
            if (s_warning != null)
            {
                bool noFuel = balloon.GetMode() != BurnMode.Down && balloon.GetFuel() <= 0f;
                s_warning.text = noFuel ? Localization.instance.Localize("<color=#ff5050>$hab_no_fuel</color>") : "";
            }
        }

        private static void SetExtrasActive(bool active)
        {
            SetActive(s_altitude, active);
            SetActive(s_load, active);
            SetActive(s_warning, active);
            SetActive(s_anchor, active);
        }

        private static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active)
            {
                c.gameObject.SetActive(active);
            }
        }

        /// <summary>Создать надписи и значок один раз — в том же корне, что и корабельный HUD (прячутся вместе с ним).</summary>
        private static void EnsureExtras(Hud hud)
        {
            if (s_altitude != null && s_load != null && s_warning != null && s_anchor != null)
            {
                return;
            }
            Transform root = hud.m_shipHudRoot.transform;
            TMP_Text template = hud.m_hoverName;
            // Под циферблатом ветра (он на -146, -437, размер 130) — высота и груз.
            if (s_altitude == null)
            {
                s_altitude = CreateText(root, "HAB_Altitude", template, new Vector2(-146f, -526f));
            }
            if (s_load == null)
            {
                s_load = CreateText(root, "HAB_Load", template, new Vector2(-146f, -552f));
            }
            if (s_warning == null)
            {
                s_warning = CreateText(root, "HAB_Warning", template, new Vector2(-146f, -578f));
            }
            // Слева от иконки паруса (она на -143, -288, размер 100) — якорь.
            if (s_anchor == null)
            {
                var go = new GameObject("HAB_Anchor", typeof(RectTransform));
                go.transform.SetParent(root, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-232f, -300f);
                rt.sizeDelta = new Vector2(46f, 46f);
                s_anchor = go.AddComponent<Image>();
                if (s_anchorSprite == null)
                {
                    Texture2D tex = TextureFactory.AnchorIcon();
                    s_anchorSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                s_anchor.sprite = s_anchorSprite;
                s_anchor.raycastTarget = false;
                s_anchor.preserveAspect = true;
            }
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_Text template, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(260f, 26f);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            if (template != null)
            {
                text.font = template.font;
                text.fontSharedMaterial = template.fontSharedMaterial;
            }
            text.fontSize = 19f;
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }
    }
}
