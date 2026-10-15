using HarmonyLib;
using UnityEngine;

namespace HotAirBalloons
{
    [HarmonyPatch]
    internal static class Patches
    {
        /// <summary>Нельзя разобрать шар в воздухе или с людьми на борту (и износ его тогда не ломает).</summary>
        [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved))]
        [HarmonyPostfix]
        private static void Piece_CanBeRemoved(Piece __instance, ref bool __result)
        {
            if (!__result)
            {
                return;
            }
            BalloonController balloon = __instance.GetComponent<BalloonController>();
            if (balloon != null && !balloon.CanBeRemoved())
            {
                __result = false;
            }
        }

        /// <summary>
        /// Прогоревший купол чинится молотом за материалы (RepairRecipe) — вместо ванильного ремонта, которому нужен
        /// станок рядом (шар мог упасть далеко от дома). Остальные постройки и целый шар — как обычно.
        /// </summary>
        [HarmonyPatch(typeof(Player), "Repair")]
        [HarmonyPrefix]
        private static bool Player_Repair(Player __instance, ItemDrop.ItemData toolItem)
        {
            Piece piece = __instance.GetHoveringPiece();
            BalloonFire fire = piece != null ? piece.GetComponent<BalloonFire>() : null;
            if (fire == null || toolItem == null || !fire.NeedsRepair())
            {
                return true;
            }
            fire.RepairBy(__instance, toolItem, piece);
            return false;
        }

        /// <summary>Огненный урон по шару (горящие стрелы, огненные шары) поджигает купол — настройки CanBurn и BurnFromFireDamage.</summary>
        [HarmonyPatch(typeof(WearNTear), "RPC_Damage")]
        [HarmonyPrefix]
        private static void WearNTear_RPC_Damage(WearNTear __instance, HitData hit)
        {
            if (hit == null || hit.m_damage.m_fire < 5f || !BalloonConfig.BurnFromFireDamage.Value)
            {
                return;
            }
            BalloonFire fire = __instance.GetComponent<BalloonFire>();
            if (fire != null)
            {
                fire.IgniteHere();
            }
        }

        /// <summary>
        /// За управлением игра не даёт наводиться на предметы (Player.UpdateHover), поэтому Shift+E по огню недоступен.
        /// Зато топливо можно подбросить с панели быстрого доступа (1–8) или из инвентаря, не отпуская огня (или руля-палки).
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
        [HarmonyPrefix]
        private static bool Humanoid_UseItem(Humanoid __instance, ItemDrop.ItemData item)
        {
            if (item == null || !ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return true;
            }
            if (!(Player.m_localPlayer.GetDoodadController() is BalloonStation station) || !station.IsValid())
            {
                return true;
            }
            return !station.UseItem(__instance, item);
        }

        /// <summary>
        /// Корабельный HUD для шара: всем, кто на шаре, вместо корабельной логики заполняем тот же виджет данными шара
        /// (без мигания: ванильный код не успевает его выключить); штурвал — только у того, кто держится за огонь или руль.
        /// </summary>
        [HarmonyPatch(typeof(Hud), "UpdateShipHud")]
        [HarmonyPrefix]
        private static bool Hud_UpdateShipHud(Hud __instance, Player player, float dt)
        {
            if (player == null || player.GetControlledShip() != null)
            {
                BalloonShipHud.Restore(__instance);
                return true;
            }
            BalloonStation station = player.GetDoodadController() as BalloonStation;
            if (station != null && !station.IsValid())
            {
                station = null;
            }
            BalloonController balloon = station != null ? station.Controller : null;
            if (balloon == null && BalloonConfig.ShowHud.Value)
            {
                // Пассажирам тоже: высота, груз, якорь и ветер — в том же блоке, только без штурвала.
                balloon = BalloonController.FindOnboard(player);
            }
            if (balloon == null)
            {
                BalloonShipHud.Restore(__instance);
                return true;
            }
            BalloonShipHud.Update(__instance, balloon, station, dt);
            return false;
        }

        /// <summary>На шаре камеру можно отдалить так же далеко, как на корабле, — чтобы видеть шар целиком.</summary>
        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        [HarmonyPrefix]
        private static void GameCamera_UpdateCamera_Prefix(GameCamera __instance, out float __state)
        {
            __state = __instance.m_maxDistance;
            Player player = Player.m_localPlayer;
            if (player != null && BalloonController.FindOnboard(player) != null)
            {
                __instance.m_maxDistance = Mathf.Max(__instance.m_maxDistance, __instance.m_maxDistanceBoat);
            }
        }

        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        [HarmonyPostfix]
        private static void GameCamera_UpdateCamera_Postfix(GameCamera __instance, float __state)
        {
            __instance.m_maxDistance = __state;
        }

        /// <summary>
        /// Вышли из игры в полёте — при входе окажетесь на земле под шаром, а не в воздухе
        /// (иначе после загрузки персонаж упал бы с высоты).
        /// </summary>
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SaveLogoutPoint))]
        [HarmonyPostfix]
        private static void PlayerProfile_SaveLogoutPoint(PlayerProfile __instance)
        {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead())
            {
                return;
            }
            BalloonController balloon = BalloonController.FindOnboard(player);
            if (balloon == null || balloon.GetLocalAgl() < 2f)
            {
                return;
            }
            __instance.SetLogoutPoint(balloon.GetSafeGroundPoint(player.transform.position));
        }
    }
}
