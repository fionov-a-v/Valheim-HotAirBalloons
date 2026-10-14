using System;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Связка для других модов (ищут этот класс рефлексией, жёсткой зависимости нет). Версия 1:
    /// <list type="bullet">
    /// <item><see cref="CeilingProvider"/> — абсолютный потолок: позиция → наибольшая высота дна корзины (мировая y),
    /// <c>+∞</c> — без ограничения. Так «Парящие острова» не пускают шар выше середины острова: у архипелага
    /// потолок — высота низкого края, а не «100 м над поверхностью».</item>
    /// <item>Версия 2: швартовка — <see cref="FindOnboard"/>, <see cref="Moor"/>, <see cref="Unmoor"/>,
    /// <see cref="IsMoored"/>. Пришвартованный шар держится у точки (крюк в стене острова): ветер и горелка не
    /// уводят корзину дальше длины верёвки; верёвку от перил видят все.</item>
    /// </list>
    /// Зонд вверх (не подниматься, если над куполом твёрдое) встроен в сам шар и работает без других модов.
    /// </summary>
    public static class BalloonsApi
    {
        public const int Version = 2;

        public static Func<Vector3, float> CeilingProvider;

        /// <summary>Шар, на борту которого стоит игрок (или null).</summary>
        public static GameObject FindOnboard(Player player)
        {
            BalloonController b = BalloonController.FindOnboard(player);
            return b != null ? b.gameObject : null;
        }

        /// <summary>Пришвартовать шар к точке верёвкой длины length (от перил корзины).</summary>
        public static bool Moor(GameObject balloon, Vector3 point, float length)
        {
            BalloonController b = balloon != null ? balloon.GetComponentInParent<BalloonController>() : null;
            if (b == null)
            {
                return false;
            }
            b.Moor(point, length);
            return true;
        }

        public static void Unmoor(GameObject balloon)
        {
            BalloonController b = balloon != null ? balloon.GetComponentInParent<BalloonController>() : null;
            b?.Unmoor();
        }

        public static bool IsMoored(GameObject balloon)
        {
            BalloonController b = balloon != null ? balloon.GetComponentInParent<BalloonController>() : null;
            return b != null && b.IsMoored();
        }

        /// <summary>Перила корзины — откуда идёт швартовая верёвка.</summary>
        public static Vector3 RailPoint(GameObject balloon)
        {
            BalloonController b = balloon != null ? balloon.GetComponentInParent<BalloonController>() : null;
            return b != null ? b.GetRailPoint() : Vector3.zero;
        }

        /// <summary>Потолок для позиции; ошибки чужого кода не роняют полёт.</summary>
        public static bool TryGetCeiling(Vector3 pos, out float ceiling)
        {
            ceiling = float.PositiveInfinity;
            Func<Vector3, float> f = CeilingProvider;
            if (f == null)
            {
                return false;
            }
            try
            {
                ceiling = f(pos);
            }
            catch (Exception e)
            {
                Jotunn.Logger.LogWarning($"Hot Air Balloons: ceiling provider failed: {e.Message}");
                CeilingProvider = null;
                return false;
            }
            return !float.IsInfinity(ceiling) && !float.IsNaN(ceiling);
        }
    }
}
