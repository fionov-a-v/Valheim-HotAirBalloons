using System;
using BepInEx.Configuration;
using HotAirBalloons.Sim;

namespace HotAirBalloons
{
    public enum BalloonKind
    {
        Simple = 0,
        Medium = 1,
        Large = 2,
    }

    /// <summary>Настройки одного вида шара.</summary>
    public sealed class KindConfig
    {
        public ConfigEntry<bool> Enabled;
        public ConfigEntry<float> Capacity;
        public ConfigEntry<string> FuelItem;
        public ConfigEntry<int> MaxFuel;
        public ConfigEntry<float> BurnTime;
        public ConfigEntry<float> RudderPercent;
        public ConfigEntry<float> SailHalfSpeed;
        public ConfigEntry<float> SailFullSpeed;
        public ConfigEntry<float> PropellerSpeed;
        public ConfigEntry<float> TurnRate;
        public ConfigEntry<string> Station;
        public ConfigEntry<string> Recipe;
    }

    /// <summary>
    /// Все настройки мода. Игровые параметры синхронизируются с сервера (Jotunn, IsAdminOnly),
    /// поэтому у всех игроков на сервере шары ведут себя одинаково.
    /// </summary>
    public static class BalloonConfig
    {
        public static ConfigEntry<float> MaxAltitude;
        public static ConfigEntry<float> AnchorLength;
        public static ConfigEntry<float> CalmBoundary;

        public static ConfigEntry<float> WindSpeed;
        public static ConfigEntry<float> ClimbSpeed;
        public static ConfigEntry<float> DescentSpeed;
        public static ConfigEntry<float> PlayerWeight;
        public static ConfigEntry<float> CreatureWeight;
        public static ConfigEntry<float> HoldFuelFactor;
        public static ConfigEntry<float> RudderMinSpeed;
        public static ConfigEntry<bool> AutoFeed;

        public static ConfigEntry<bool> ShowHud;

        private static readonly KindConfig[] s_kinds = new KindConfig[3];

        public static event Action RecipesChanged;

        public static KindConfig For(BalloonKind kind) => s_kinds[(int)kind];

        public static void Bind(ConfigFile cfg)
        {
            const string general = "1 - General";
            MaxAltitude = Synced(cfg, general, "MaxAltitude", 100f,
                "Максимальная высота полёта над землёй или водой, м. Выше неё горелка не поднимает шар.",
                new AcceptableValueRange<float>(10f, 1000f));
            AnchorLength = Synced(cfg, general, "AnchorLength", 10f,
                "Длина якоря (каната), м. Якорь держит, пока шар не выше этой высоты над точкой крепления; " +
                "шар может отходить от точки крепления не дальше половины длины.",
                new AcceptableValueRange<float>(2f, 100f));
            CalmBoundary = Synced(cfg, general, "CalmBoundary", 3f,
                "Граница штиля, м. С выпущенным якорем ниже этой высоты шар не чувствует ветра и не двигается по горизонтали.",
                new AcceptableValueRange<float>(0f, 50f));

            const string balance = "2 - Balance";
            WindSpeed = Synced(cfg, balance, "WindSpeed", 6f,
                "Скорость шара при максимальном ветре, м/с (при слабом ветре — от 25% этого значения; паруса среднего шара умножают её).",
                new AcceptableValueRange<float>(1f, 40f));
            ClimbSpeed = Synced(cfg, balance, "ClimbSpeed", 2.5f,
                "Скорость подъёма в режиме «Вверх», м/с (для пустого шара; с грузом меньше).",
                new AcceptableValueRange<float>(0.2f, 20f));
            DescentSpeed = Synced(cfg, balance, "DescentSpeed", 4f,
                "Скорость снижения с погашенным огнём, м/с.",
                new AcceptableValueRange<float>(0.2f, 20f));
            PlayerWeight = Synced(cfg, balance, "PlayerWeight", 80f,
                "Вес самого игрока, кг. К нему прибавляется вес инвентаря (1 единица веса = 1 кг).",
                new AcceptableValueRange<float>(0f, 300f));
            CreatureWeight = Synced(cfg, balance, "CreatureWeight", 100f,
                "Вес любого другого существа в корзине (прирученные животные и т.п.), кг.",
                new AcceptableValueRange<float>(0f, 2000f));
            HoldFuelFactor = Synced(cfg, balance, "HoldFuelFactor", 0.5f,
                "Расход топлива в режиме «Держать высоту» относительно режима «Вверх».",
                new AcceptableValueRange<float>(0f, 1f));
            RudderMinSpeed = Synced(cfg, balance, "RudderMinSpeedPercent", 30f,
                "Скорость (в % от скорости ветра) при максимальном отклонении руля.",
                new AcceptableValueRange<float>(0f, 100f));
            AutoFeed = Synced(cfg, balance, "AutoFeedFuel", true,
                "Когда топливо кончается, управляющий автоматически подкидывает его из своего инвентаря " +
                "(у большого шара — ещё и из сундука шара).", null);

            ShowHud = cfg.Bind("3 - Client", "ShowHud", true,
                "Показывать панель шара (высота, топливо, груз, якорь) — только для этого клиента.");

            s_kinds[(int)BalloonKind.Simple] = BindKind(cfg, "4 - Simple balloon", enabled: true,
                capacity: 200f, fuel: "Resin", maxFuel: 20, burnTime: 30f, rudder: -1f,
                station: "piece_workbench", recipe: "Wood:20,TrollHide:8,LeatherScraps:20,Resin:20");
            s_kinds[(int)BalloonKind.Medium] = BindKind(cfg, "5 - Medium balloon", enabled: true,
                capacity: 800f, fuel: "Wood", maxFuel: 30, burnTime: 40f, rudder: 30f,
                station: "forge", recipe: "FineWood:30,TrollHide:16,Guck:10,Iron:6,Chain:2");
            KindConfig medium = s_kinds[(int)BalloonKind.Medium];
            medium.SailHalfSpeed = Synced(cfg, "5 - Medium balloon", "SailHalfSpeed", 1.5f,
                "Скорость с парусами, раскрытыми наполовину, — во сколько раз быстрее ветра (сложенные паруса — x1).",
                new AcceptableValueRange<float>(0.1f, 10f));
            medium.SailFullSpeed = Synced(cfg, "5 - Medium balloon", "SailFullSpeed", 2f,
                "Скорость с полностью раскрытыми парусами — во сколько раз быстрее ветра.",
                new AcceptableValueRange<float>(0.1f, 10f));
            s_kinds[(int)BalloonKind.Large] = BindKind(cfg, "6 - Large balloon", enabled: true,
                capacity: 3000f, fuel: "Coal", maxFuel: 40, burnTime: 40f, rudder: -1f,
                station: "piece_artisanstation", recipe: "FineWood:50,LinenThread:40,Tar:15,BlackMetal:8,Chain:4");
            KindConfig large = s_kinds[(int)BalloonKind.Large];
            large.PropellerSpeed = Synced(cfg, "6 - Large balloon", "PropellerSpeed", 24f,
                "Скорость от винта, м/с, когда крутят все четыре сиденья с рукоятями (каждый крутящий — четверть). Складывается с ветром: " +
                "против ветра ветер вычитается, по ветру прибавляется. 24 — против самого сильного ветра (WindSpeed = 6) корабль идёт 18 м/с, " +
                "втрое быстрее базовой скорости; в штиль хватает одного гребца (6 м/с).",
                new AcceptableValueRange<float>(0f, 100f));
            large.TurnRate = Synced(cfg, "6 - Large balloon", "TurnRate", 20f,
                "Скорость поворота корабля рулём-палкой до упора, градусов в секунду.",
                new AcceptableValueRange<float>(1f, 90f));

            MigrateOldDefaults(cfg);
        }

        /// <summary>
        /// BepInEx не меняет значения, уже записанные в .cfg, поэтому при обновлении мода старые значения по умолчанию
        /// переносим на новые сами — только те, что совпадают со старыми умолчаниями (своё игрок не трогаем). Один раз.
        /// </summary>
        private static void MigrateOldDefaults(ConfigFile cfg)
        {
            const int current = 1;
            ConfigEntry<int> version = cfg.Bind("0 - Internal", "ConfigVersion", 0,
                new ConfigDescription("Версия файла настроек: по ней мод переносит старые значения по умолчанию на новые. Не меняйте.",
                    null, new ConfigurationManagerAttributes { Browsable = false }));
            if (version.Value >= current)
            {
                return;
            }
            bool save = cfg.SaveOnConfigSet;
            cfg.SaveOnConfigSet = false;
            // 1.2 → 1.3: ветер 12 → 6, снижение 2 → 4, средний: уголь → дерево, руль 25% → 30%; большой: дерево → уголь.
            Migrate(WindSpeed, 12f, 6f);
            Migrate(DescentSpeed, 2f, 4f);
            KindConfig medium = For(BalloonKind.Medium);
            Migrate(medium.FuelItem, "Coal", "Wood");
            Migrate(medium.RudderPercent, 25f, 30f);
            Migrate(For(BalloonKind.Large).FuelItem, "Wood", "Coal");
            version.Value = current;
            cfg.SaveOnConfigSet = save;
            cfg.Save();
        }

        private static void Migrate(ConfigEntry<float> entry, float oldDefault, float newDefault)
        {
            if (entry != null && Math.Abs(entry.Value - oldDefault) < 0.001f)
            {
                entry.Value = newDefault;
            }
        }

        private static void Migrate(ConfigEntry<string> entry, string oldDefault, string newDefault)
        {
            if (entry != null && string.Equals(entry.Value?.Trim(), oldDefault, StringComparison.OrdinalIgnoreCase))
            {
                entry.Value = newDefault;
            }
        }

        private static KindConfig BindKind(ConfigFile cfg, string section, bool enabled, float capacity, string fuel,
            int maxFuel, float burnTime, float rudder, string station, string recipe)
        {
            var k = new KindConfig
            {
                Enabled = Synced(cfg, section, "Enabled", enabled, "Можно ли строить этот шар.", null),
                Capacity = Synced(cfg, section, "Capacity", capacity,
                    "Грузоподъёмность, кг: сумма всех, кто на шаре (с инвентарём), и содержимого сундука.",
                    new AcceptableValueRange<float>(10f, 100000f)),
                FuelItem = Synced(cfg, section, "FuelItem", fuel,
                    "Топливо (имя префаба предмета: Resin, Coal, Wood, FineWood, RoundLog...).", null),
                MaxFuel = Synced(cfg, section, "MaxFuel", maxFuel, "Сколько единиц топлива вмещает источник огня.",
                    new AcceptableValueRange<int>(1, 500)),
                BurnTime = Synced(cfg, section, "BurnTimePerFuel", burnTime,
                    "Сколько секунд горит одна единица топлива в режиме «Вверх».",
                    new AcceptableValueRange<float>(1f, 3600f)),
                Station = Synced(cfg, section, "CraftingStation", station,
                    "Станок, рядом с которым строится шар (piece_workbench, forge, piece_artisanstation, blackforge...; пусто — без станка).",
                    null),
                Recipe = Synced(cfg, section, "Recipe", recipe, "Ресурсы для постройки: Предмет:Кол-во через запятую.", null),
            };
            if (rudder >= 0f)
            {
                k.RudderPercent = Synced(cfg, section, "RudderMaxPercent", rudder,
                    "Насколько руль может отклонить курс от направления ветра, % от 180° (25% = 45°).",
                    new AcceptableValueRange<float>(0f, 100f));
            }
            k.Station.SettingChanged += (_, _) => RecipesChanged?.Invoke();
            k.Recipe.SettingChanged += (_, _) => RecipesChanged?.Invoke();
            k.Enabled.SettingChanged += (_, _) => RecipesChanged?.Invoke();
            return k;
        }

        private static ConfigEntry<T> Synced<T>(ConfigFile cfg, string section, string key, T value, string description,
            AcceptableValueBase range)
        {
            var attributes = new ConfigurationManagerAttributes { IsAdminOnly = true };
            return cfg.Bind(section, key, value, new ConfigDescription(description, range, attributes));
        }

        /// <summary>Параметры симуляции для конкретного шара из текущего (синхронизированного) конфига.</summary>
        public static void Fill(SimSettings s, BalloonKind kind)
        {
            KindConfig k = For(kind);
            s.MaxAltitude = MaxAltitude.Value;
            s.AnchorLength = AnchorLength.Value;
            s.CalmBoundary = CalmBoundary.Value;
            s.Capacity = k.Capacity.Value;
            s.WindSpeed = WindSpeed.Value;
            s.ClimbSpeed = ClimbSpeed.Value;
            s.DescentSpeed = DescentSpeed.Value;
            s.RudderMaxPercent = k.RudderPercent?.Value ?? 0f;
            s.RudderMinSpeedFactor = RudderMinSpeed.Value / 100f;
            s.Propeller = k.PropellerSpeed != null;
            s.PropellerSpeed = k.PropellerSpeed?.Value ?? 0f;
            s.TurnRate = k.TurnRate?.Value ?? 20f;
            // Корабль с винтом разворачивают рулём; остальные шары сами встают носом по ходу.
            s.AlignToTravel = kind != BalloonKind.Simple && !s.Propeller;
            s.HasSails = k.SailHalfSpeed != null && k.SailFullSpeed != null;
            s.SailHalfFactor = k.SailHalfSpeed?.Value ?? 1f;
            s.SailFullFactor = k.SailFullSpeed?.Value ?? 1f;
        }
    }
}
