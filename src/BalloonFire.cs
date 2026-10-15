using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Пожар на куполе (настройка CanBurn). Поджечь шар может другой мод (BalloonsApi.Ignite — например, огонь драконов
    /// «Парящих островов») или огненный урон по шару (BurnFromFireDamage). Купол горит BurnDuration секунд — пламя и дым
    /// видят все, на воде огонь сразу гаснет — и теряет подъёмную силу. Прогоревший купол темнеет от копоти; подняться шар
    /// не может, пока его не починят молотом за материалы (RepairRecipe). Состояние — в ZDO шара, меняет его только владелец.
    /// </summary>
    public class BalloonFire : MonoBehaviour
    {
        /// <summary>Пламя на куполе (копии огня жаровни) и дым — включаются, пока купол горит.</summary>
        public GameObject[] m_flames = new GameObject[0];
        public ParticleSystem m_smoke;

        /// <summary>До какого момента сетевого времени (мс) горит купол.</summary>
        public static readonly int s_burnEnd = "hab_burnEnd".GetStableHashCode();

        /// <summary>Купол прогорел — с поджога и до ремонта.</summary>
        public static readonly int s_burnt = "hab_burnt".GetStableHashCode();

        private static readonly Color s_soot = new Color(0.2f, 0.17f, 0.15f);
        private static Material s_smokeMaterial;

        private ZNetView m_nview;
        private BalloonController m_ctrl;
        private bool m_flamesOn;
        private bool m_sooty;
        private bool m_haveBaseColor;
        private Color m_baseColor;

        private bool Valid => m_nview != null && m_nview.IsValid() && m_ctrl != null;

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_ctrl = GetComponent<BalloonController>();
            if (m_nview == null || m_nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            m_nview.Register("HAB_Ignite", RPC_Ignite);
            m_nview.Register("HAB_Repair", RPC_Repair);
        }

        private static long NowMs() => ZNet.instance != null ? (long)(ZNet.instance.GetTimeSeconds() * 1000.0) : 0L;

        public bool IsBurning() => Valid && m_nview.GetZDO().GetLong(s_burnEnd) > NowMs();

        public bool NeedsRepair() => Valid && m_nview.GetZDO().GetBool(s_burnt);

        /// <summary>Поджечь — с любого клиента, просьба уходит владельцу шара. false — выключено настройкой или купол уже горит.</summary>
        public bool Ignite()
        {
            if (!BalloonConfig.CanBurn.Value || !Valid || IsBurning())
            {
                return false;
            }
            m_nview.InvokeRPC("HAB_Ignite");
            return true;
        }

        /// <summary>Владелец шара: купол загорается и с этой минуты нуждается в ремонте.</summary>
        public void IgniteHere()
        {
            if (!BalloonConfig.CanBurn.Value || !Valid || !m_nview.IsOwner() || IsBurning())
            {
                return;
            }
            ZDO zdo = m_nview.GetZDO();
            zdo.Set(s_burnEnd, NowMs() + (long)(Mathf.Max(1f, BalloonConfig.BurnDuration.Value) * 1000f));
            zdo.Set(s_burnt, true);
        }

        /// <summary>Владелец шара: потушить (на воде); прогоревшим купол остаётся.</summary>
        public void Extinguish()
        {
            if (Valid && m_nview.IsOwner() && IsBurning())
            {
                m_nview.GetZDO().Set(s_burnEnd, 0L);
            }
        }

        private void RPC_Ignite(long sender)
        {
            if (!m_ctrl.ForwardIfNotOwner(sender, "HAB_Ignite"))
            {
                IgniteHere();
            }
        }

        private void RPC_Repair(long sender)
        {
            if (!m_ctrl.ForwardIfNotOwner(sender, "HAB_Repair") && !IsBurning())
            {
                m_nview.GetZDO().Set(s_burnt, false);
            }
        }

        // ------------------------------------------------------------------ ремонт молотом

        /// <summary>Материалы ремонта по настройке RepairRecipe: предмет игры и количество.</summary>
        public List<KeyValuePair<ItemDrop, int>> GetRepairCost()
        {
            var list = new List<KeyValuePair<ItemDrop, int>>();
            if (ObjectDB.instance == null || m_ctrl == null)
            {
                return list;
            }
            foreach (KeyValuePair<string, int> r in BalloonPieces.ParseRecipe(m_ctrl.Config.RepairRecipe.Value))
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(r.Key);
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null)
                {
                    list.Add(new KeyValuePair<ItemDrop, int>(drop, r.Value));
                }
            }
            return list;
        }

        private string CostText(List<KeyValuePair<ItemDrop, int>> cost)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<ItemDrop, int> c in cost)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(c.Key.m_itemData.m_shared.m_name).Append(" ×").Append(c.Value);
            }
            return sb.ToString();
        }

        /// <summary>Строка для подсказки при наведении на шар: горит или прогорел и чем чинить (с токенами, без перевода).</summary>
        public string GetHoverLine()
        {
            if (IsBurning())
            {
                return "\n<color=#ff5030>$hab_envelope_burning</color>";
            }
            if (!NeedsRepair())
            {
                return "";
            }
            string cost = CostText(GetRepairCost());
            return "\n<color=#ffa040>$hab_envelope_burnt</color>" + (cost.Length > 0 ? "\n$hab_repair_need: " + cost : "");
        }

        /// <summary>
        /// Ремонт прогоревшего купола молотом (вместо ванильного Player.Repair): охрана, материалы из инвентаря, затем —
        /// как у ванильного ремонта: удар, эффект, сообщение, выносливость, износ молота. Станок рядом не нужен:
        /// шар мог упасть далеко от дома.
        /// </summary>
        public void RepairBy(Player player, ItemDrop.ItemData tool, Piece piece)
        {
            if (IsBurning())
            {
                player.Message(MessageHud.MessageType.Center, "$hab_repair_wait");
                return;
            }
            if (!PrivateArea.CheckAccess(transform.position))
            {
                return;
            }
            Inventory inventory = player.GetInventory();
            List<KeyValuePair<ItemDrop, int>> cost = GetRepairCost();
            var missing = new List<KeyValuePair<ItemDrop, int>>();
            foreach (KeyValuePair<ItemDrop, int> c in cost)
            {
                int have = inventory.CountItems(c.Key.m_itemData.m_shared.m_name);
                if (have < c.Value)
                {
                    missing.Add(new KeyValuePair<ItemDrop, int>(c.Key, c.Value - have));
                }
            }
            if (missing.Count > 0)
            {
                player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$hab_repair_need: " + CostText(missing)));
                return;
            }
            foreach (KeyValuePair<ItemDrop, int> c in cost)
            {
                inventory.RemoveItem(c.Key.m_itemData.m_shared.m_name, c.Value);
            }
            m_nview.InvokeRPC("HAB_Repair");

            player.FaceLookDirection();
            ZSyncAnimation zanim = Traverse.Create(player).Field("m_zanim").GetValue<ZSyncAnimation>();
            zanim?.SetTrigger(tool.m_shared.m_attack.m_attackAnimation);
            piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);
            player.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize("$hab_repaired"));
            object stamina = AccessTools.Method(typeof(Player), "GetBuildStamina")?.Invoke(player, null);
            player.UseStamina(stamina is float s ? s : 10f);
            if (tool.m_shared.m_useDurability)
            {
                tool.m_durability -= tool.m_shared.m_useDurabilityDrain * Game.m_durabilityRate;
            }
        }

        // ------------------------------------------------------------------ вид (все клиенты)

        private void LateUpdate()
        {
            if (!Valid)
            {
                return;
            }
            bool burning = IsBurning();
            if (burning != m_flamesOn)
            {
                m_flamesOn = burning;
                foreach (GameObject f in m_flames)
                {
                    if (f != null)
                    {
                        f.SetActive(burning);
                    }
                }
                if (m_smoke != null)
                {
                    // Дым не обрывается: новые клубы перестают появляться, старые рассеиваются сами.
                    ParticleSystem.EmissionModule emission = m_smoke.emission;
                    emission.enabled = burning;
                    if (burning && !m_smoke.isPlaying)
                    {
                        m_smoke.Play();
                    }
                }
            }
            // Копоть: горящий и прогоревший купол тёмный, после ремонта — прежний.
            bool sooty = NeedsRepair();
            Renderer envelope = m_ctrl.m_envelopeRenderer;
            if (sooty != m_sooty && envelope != null)
            {
                m_sooty = sooty;
                Material material = envelope.material;
                if (!m_haveBaseColor)
                {
                    m_baseColor = material.color;
                    m_haveBaseColor = true;
                }
                material.color = sooty ? Color.Lerp(m_baseColor, s_soot, 0.75f) : m_baseColor;
            }
        }

        /// <summary>Дым пожара: серые клубы с поверхности купола, тянутся за шаром. Шейдер Sprites/Default есть в игре всегда.</summary>
        internal static ParticleSystem CreateSmoke(Transform parent, Vector3 localCenter, float radius)
        {
            Material material = SmokeMaterial();
            if (material == null)
            {
                return null;
            }
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCenter;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.7f, radius * 1.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.15f, 0.14f, 0.6f), new Color(0.3f, 0.29f, 0.28f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 9f;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>Мягкое круглое пятно — клуб дыма (цвет и прозрачность задают частицы).</summary>
        private static Material SmokeMaterial()
        {
            if (s_smokeMaterial != null)
            {
                return s_smokeMaterial;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "hab_smoke" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            s_smokeMaterial = new Material(shader) { name = "hab_smoke", mainTexture = texture };
            return s_smokeMaterial;
        }
    }
}
