using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;

namespace HotAirBalloons
{
    internal static class BalloonLocalization
    {
        public static void Register()
        {
            CustomLocalization loc = LocalizationManager.Instance.GetLocalization();

            loc.AddTranslation("English", new Dictionary<string, string>
            {
                { "hab_simple", "Puffed Troll" },
                { "hab_simple_desc", "A one-person balloon of troll cloth with a wicker basket. No seats: stand under the fire bowl and hold on to the rigging. Flies only where the wind blows." },
                { "hab_medium", "Flying Karve" },
                { "hab_medium_desc", "Linen envelope and a wooden tub gondola: three seats on a bench, a fire bowl and a tiller that turns the side sails. Sails speed the balloon up to twice the wind; the tiller steers up to 120° off the wind." },
                { "hab_large", "Sky Drakkar" },
                { "hab_large_desc", "An airship: a striped linen envelope over a longship hull with a dragon head and shields. Four crank seats turn the propeller, so it can fly against the wind; the tiller turns the ship. An 8-slot chest, burns coal." },

                { "hab_torch", "Balloon torch" },
                { "hab_firebowl", "Fire bowl" },
                { "hab_tiller", "Tiller" },
                { "hab_take_tiller", "Take the tiller" },
                { "hab_sit_tiller", "Sit at the tiller" },
                { "hab_crank_seat", "Crank seat" },
                { "hab_lantern", "Fire lantern" },
                { "hab_hold_on", "Hold on" },
                { "hab_burner", "Burner" },
                { "hab_stove", "Stove" },
                { "hab_seat", "Seat" },
                { "hab_chest", "Balloon chest" },
                { "hab_anchor", "Anchor" },

                { "hab_hold_torch", "Hold on to the torch" },
                { "hab_take_control", "Take the controls" },
                { "hab_release", "Let go" },
                { "hab_add_fuel", "Add" },
                { "hab_anchor_drop", "Drop anchor" },
                { "hab_anchor_raise", "Raise anchor" },

                { "hab_mode_up", "Up" },
                { "hab_mode_hold", "Hold altitude" },
                { "hab_mode_down", "Down (fire off)" },
                { "hab_no_fuel", "no fuel" },
                { "hab_anchor_raised", "raised" },
                { "hab_anchor_hanging", "hanging" },
                { "hab_anchor_holding", "holding" },
                { "hab_calm", "calm" },
                { "hab_sails_furled", "furled" },
                { "hab_sails_half", "half" },
                { "hab_sails_full", "full" },

                { "hab_hud_fire", "Fire" },
                { "hab_hud_altitude", "Altitude" },
                { "hab_hud_fuel", "Fuel" },
                { "hab_hud_load", "Load" },
                { "hab_hud_overload", "OVERLOADED" },
                { "hab_hud_wind", "Wind" },
                { "hab_hud_rudder", "Rudder" },
                { "hab_hud_anchor", "Anchor" },
                { "hab_hud_sails", "Sails" },
                { "hab_hud_propeller", "Propeller" },
                { "hab_hud_rowers", "cranking" },
                { "hab_hud_keys", "[W/S] fire up/down   [RMB] anchor   [1-8] add fuel   [E] let go" },
                { "hab_hud_keys_tiller", "[W/S] sails out/in   [A/D] steer   [RMB] anchor   [1-8] add fuel   [E] let go" },
                { "hab_hud_keys_helm", "[W/S] fire up/down   [A/D] turn   [RMB] anchor   [1-8] add fuel   [E] let go" },
                { "hab_hud_keys_rudder", "[W/S] fire up/down   [A/D] rudder   [RMB] anchor   [1-8] add fuel   [E] let go" },
                { "hab_hud_keys_crank", "[W] turn the crank   [S] stop   [E] stand up" },
                { "hab_sit_crank", "Sit at the crank" },
                { "hab_hud_crank", "Your crank" },
                { "hab_crank_on", "turning" },
                { "hab_crank_off", "idle" },
                { "hab_on_water", "on water" },
                { "hab_unit_m", "m" },
                { "hab_unit_kg", "kg" },
                { "hab_unit_ms", "m/s" },

                { "hab_msg_airborne", "Land the balloon and get everyone out first" },
            });

            loc.AddTranslation("Russian", new Dictionary<string, string>
            {
                { "hab_simple", "Дутый тролль" },
                { "hab_simple_desc", "Одноместный шар из тролльей ткани с плетёной корзиной. Сидений нет — стоите под чашей огня и держитесь за стропу. Летит только по ветру." },
                { "hab_medium", "Летучий карви" },
                { "hab_medium_desc", "Льняная оболочка и гондола-кадка: три места на скамье, чаша огня и руль-палка, поворачивающая боковые паруса. Паруса разгоняют шар до двойной скорости ветра, руль отклоняет курс от ветра до 120°." },
                { "hab_large", "Небесный драккар" },
                { "hab_large_desc", "Летучий корабль: полосатый льняной купол над корпусом драккара с драконьей головой и щитами. Четыре сиденья с рукоятями крутят винт — можно лететь против ветра; руль-палка поворачивает корабль. Сундук на 8 ячеек, топливо — уголь." },

                { "hab_torch", "Факел шара" },
                { "hab_firebowl", "Чаша огня" },
                { "hab_tiller", "Руль-палка" },
                { "hab_take_tiller", "Взяться за руль" },
                { "hab_sit_tiller", "Сесть за руль" },
                { "hab_crank_seat", "Сиденье с рукоятью" },
                { "hab_lantern", "Фонарь огня" },
                { "hab_hold_on", "Держаться" },
                { "hab_burner", "Горелка" },
                { "hab_stove", "Печь" },
                { "hab_seat", "Сиденье" },
                { "hab_chest", "Сундук шара" },
                { "hab_anchor", "Якорь" },

                { "hab_hold_torch", "Держаться за факел" },
                { "hab_take_control", "Встать к огню" },
                { "hab_release", "Отпустить" },
                { "hab_add_fuel", "Подбросить" },
                { "hab_anchor_drop", "Бросить якорь" },
                { "hab_anchor_raise", "Поднять якорь" },

                { "hab_mode_up", "Вверх" },
                { "hab_mode_hold", "Держать высоту" },
                { "hab_mode_down", "Вниз (огонь погашен)" },
                { "hab_no_fuel", "нет топлива" },
                { "hab_anchor_raised", "поднят" },
                { "hab_anchor_hanging", "висит" },
                { "hab_anchor_holding", "держит" },
                { "hab_calm", "штиль" },
                { "hab_sails_furled", "сложены" },
                { "hab_sails_half", "наполовину" },
                { "hab_sails_full", "полностью" },

                { "hab_hud_fire", "Огонь" },
                { "hab_hud_altitude", "Высота" },
                { "hab_hud_fuel", "Топливо" },
                { "hab_hud_load", "Груз" },
                { "hab_hud_overload", "ПЕРЕГРУЗ" },
                { "hab_hud_wind", "Ветер" },
                { "hab_hud_rudder", "Руль" },
                { "hab_hud_anchor", "Якорь" },
                { "hab_hud_sails", "Паруса" },
                { "hab_hud_propeller", "Винт" },
                { "hab_hud_rowers", "крутят" },
                { "hab_hud_keys", "[W/S] огонь сильнее/слабее   [ПКМ] якорь   [1–8] подбросить топливо   [E] отпустить" },
                { "hab_hud_keys_tiller", "[W/S] паруса шире/уже   [A/D] руль   [ПКМ] якорь   [1–8] подбросить топливо   [E] отпустить" },
                { "hab_hud_keys_helm", "[W/S] огонь сильнее/слабее   [A/D] поворот   [ПКМ] якорь   [1–8] подбросить топливо   [E] отпустить" },
                { "hab_hud_keys_rudder", "[W/S] огонь сильнее/слабее   [A/D] руль   [ПКМ] якорь   [1–8] подбросить топливо   [E] отпустить" },
                { "hab_hud_keys_crank", "[W] крутить   [S] не крутить   [E] встать" },
                { "hab_sit_crank", "Сесть за рукоять" },
                { "hab_hud_crank", "Ваша рукоять" },
                { "hab_crank_on", "крутите" },
                { "hab_crank_off", "не крутите" },
                { "hab_on_water", "на воде" },
                { "hab_unit_m", "м" },
                { "hab_unit_kg", "кг" },
                { "hab_unit_ms", "м/с" },

                { "hab_msg_airborne", "Сначала посадите шар и высадите всех" },
            });
        }
    }
}
