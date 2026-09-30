using System;
using System.Collections.Generic;
using HotAirBalloons.Visuals;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace HotAirBalloons
{
    /// <summary>
    /// Собирает префабы трёх шаров процедурно (без ассет-бандлов): корзина, оболочка, стропы, якорь, руль —
    /// из своих мешей, а огонь, сундук и материалы — из ванильных префабов игры.
    /// </summary>
    internal static class BalloonPieces
    {
        public static readonly string[] PrefabNames = { "HAB_BalloonSimple", "HAB_BalloonMedium", "HAB_BalloonLarge" };
        private static readonly string[] s_pieceNames = { "$hab_simple", "$hab_medium", "$hab_large" };
        private static readonly string[] s_pieceDescriptions = { "$hab_simple_desc", "$hab_medium_desc", "$hab_large_desc" };

        private static readonly HashSet<string> s_dropChildren = new HashSet<string>
        {
            "PlayerBase", "Pathfinding_blocker", "FireBurn", "SmokeSpawner", "collider", "floor_2x2_snow", "destruction",
        };

        private static readonly Dictionary<BalloonKind, GameObject> s_prefabs = new Dictionary<BalloonKind, GameObject>();

        private static GameObject s_holder;
        private static Material s_wood;
        private static Material s_iron;
        private static Material s_rope;
        private static Material s_ropeMesh;
        private static Material s_wickerDark;
        private static Material s_planks;
        private static Material s_hull;
        private static Material s_shield;
        private static Material s_sailCloth;
        private static readonly Material[] s_envelope = new Material[3];

        private static int s_vehicleLayer;
        private static int s_nonSolidLayer;

        private sealed class Layout
        {
            public float W;
            public float D;
            public float EnvR;
            public float EnvMouthY;
            public float EnvMouthR;
            public int GorePairs;
            public float Mass;
            public float Health;
            public float AnchorScale;

            public static Layout For(BalloonKind kind)
            {
                switch (kind)
                {
                    case BalloonKind.Simple:
                        return new Layout { W = 1.8f, D = 1.8f, EnvR = 3.2f, EnvMouthY = 3.4f, EnvMouthR = 0.75f, GorePairs = 6, Mass = 300f, Health = 400f, AnchorScale = 0.7f };
                    case BalloonKind.Medium:
                        return new Layout { W = 2.6f, D = 2.2f, EnvR = 4.4f, EnvMouthY = 3.9f, EnvMouthR = 1.0f, GorePairs = 8, Mass = 800f, Health = 800f, AnchorScale = 1f };
                    default:
                        return new Layout { W = 3.6f, D = 3.4f, EnvR = 5.8f, EnvMouthY = 4.5f, EnvMouthR = 1.3f, GorePairs = 10, Mass = 2000f, Health = 1500f, AnchorScale = 1.25f };
                }
            }
        }

        /// <summary>Меши лебёдки якоря (барабан), которые BuildAnchor добавляет к модели шара.</summary>
        private sealed class Parts
        {
            public readonly MeshBuilder Wood = new MeshBuilder();
        }

        // ================================================================== регистрация

        public static void Register()
        {
            s_vehicleLayer = LayerMask.NameToLayer("vehicle");
            s_nonSolidLayer = LayerMask.NameToLayer("piece_nonsolid");

            s_holder = new GameObject("HotAirBalloons_Prefabs");
            s_holder.SetActive(false);
            Object.DontDestroyOnLoad(s_holder);

            CreateMaterials();

            foreach (BalloonKind kind in new[] { BalloonKind.Simple, BalloonKind.Medium, BalloonKind.Large })
            {
                try
                {
                    GameObject prefab = Build(kind);
                    KindConfig cfg = BalloonConfig.For(kind);
                    var pieceConfig = new PieceConfig
                    {
                        Name = s_pieceNames[(int)kind],
                        Description = s_pieceDescriptions[(int)kind],
                        PieceTable = PieceTables.Hammer,
                        Category = PieceCategories.Misc,
                        Usage = new[] { PieceUsages.Transport },
                        CraftingStation = cfg.Station.Value,
                        Enabled = cfg.Enabled.Value,
                        Icon = RenderIcon(prefab) ?? TextureFactory.Icon(EnvelopeScheme.For(kind)),
                        Requirements = ParseRecipe(cfg.Recipe.Value).ConvertAll(r => new RequirementConfig(r.Key, r.Value, 0, true)).ToArray(),
                    };
                    PieceManager.Instance.AddPiece(new CustomPiece(prefab, false, pieceConfig));
                    s_prefabs[kind] = prefab;
                }
                catch (Exception e)
                {
                    Logger.LogError($"HotAirBalloons: не удалось создать шар {kind}: {e}");
                }
            }
        }

        public static List<KeyValuePair<string, int>> ParseRecipe(string recipe)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (string.IsNullOrWhiteSpace(recipe))
            {
                return result;
            }
            foreach (string part in recipe.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                string item = kv[0].Trim();
                int amount = 1;
                if (kv.Length > 1 && !int.TryParse(kv[1].Trim(), out amount))
                {
                    Logger.LogWarning($"HotAirBalloons: неверное количество в рецепте '{part}'");
                    continue;
                }
                if (item.Length > 0 && amount > 0)
                {
                    result.Add(new KeyValuePair<string, int>(item, amount));
                }
            }
            return result;
        }

        /// <summary>Применить рецепты/станки из (синхронизированного) конфига к уже созданным префабам.</summary>
        public static void ApplyRecipes()
        {
            if (ObjectDB.instance == null || ObjectDB.instance.m_items.Count == 0)
            {
                return;
            }
            foreach (KeyValuePair<BalloonKind, GameObject> pair in s_prefabs)
            {
                Piece piece = pair.Value != null ? pair.Value.GetComponent<Piece>() : null;
                if (piece == null)
                {
                    continue;
                }
                KindConfig cfg = BalloonConfig.For(pair.Key);
                var requirements = new List<Piece.Requirement>();
                foreach (KeyValuePair<string, int> r in ParseRecipe(cfg.Recipe.Value))
                {
                    GameObject item = ObjectDB.instance.GetItemPrefab(r.Key);
                    ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
                    if (drop == null)
                    {
                        Logger.LogWarning($"HotAirBalloons: предмет '{r.Key}' не найден (рецепт {pair.Key})");
                        continue;
                    }
                    requirements.Add(new Piece.Requirement { m_resItem = drop, m_amount = r.Value, m_recover = true });
                }
                piece.m_resources = requirements.ToArray();
                piece.m_craftingStation = FindStation(cfg.Station.Value);
                piece.m_enabled = cfg.Enabled.Value;
            }
        }

        private static CraftingStation FindStation(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }
            name = CraftingStations.GetInternalName(name.Trim());
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
            if (go == null)
            {
                go = PrefabManager.Instance.GetPrefab(name);
            }
            CraftingStation station = go != null ? go.GetComponent<CraftingStation>() : null;
            if (station == null)
            {
                Logger.LogWarning($"HotAirBalloons: станок '{name}' не найден");
            }
            return station;
        }

        private static Sprite RenderIcon(GameObject prefab)
        {
            try
            {
                return RenderManager.Instance.Render(new RenderManager.RenderRequest(prefab)
                {
                    Width = 128,
                    Height = 128,
                    Rotation = RenderManager.IsometricRotation,
                    UseCache = false,
                });
            }
            catch (Exception e)
            {
                Logger.LogWarning($"HotAirBalloons: иконка {prefab.name} не отрисована: {e.Message}");
                return null;
            }
        }

        // ================================================================== материалы

        private static Material FindMaterial(string prefabName, string materialName)
        {
            GameObject prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                return null;
            }
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null && m.name.StartsWith(materialName, StringComparison.Ordinal))
                    {
                        return m;
                    }
                }
            }
            return null;
        }

        private static Material CloneMaterial(Material source, string name)
        {
            Material m = source != null ? new Material(source) : new Material(Shader.Find("Standard"));
            m.name = name;
            // У ванильного woodwall тайлинг (-0.56, 0.12) под свою развёртку — нашим мешам с UV в метрах нужен (1, 1).
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            // Шар движется: отключаем мировой шум цвета, который «ползал» бы по поверхности.
            SetFloat(m, "_MoveableObject", 1f);
            SetFloat(m, "_ValueNoise", 0f);
            SetFloat(m, "_ValueNoiseVertex", 0f);
            return m;
        }

        private static void SetFloat(Material m, string property, float value)
        {
            if (m.HasProperty(property))
            {
                m.SetFloat(property, value);
            }
        }

        private static void CreateMaterials()
        {
            Material woodSource = FindMaterial("piece_banner01", "woodwall") ?? FindMaterial("wood_wall", "woodwall") ?? FindMaterial("Karve", "smallboat");
            if (woodSource == null)
            {
                Logger.LogWarning("HotAirBalloons: ванильный материал дерева не найден, будет простой шейдер");
            }

            s_wood = CloneMaterial(woodSource, "hab_wood");

            Texture2D flatNormal = TextureFactory.FlatNormal();

            // Железо — свой материал на том же шейдере: у ванильной жаровни материал с альфа-вырезом,
            // на наших ящиках он дал бы дыры.
            s_iron = CloneMaterial(woodSource, "hab_iron");
            s_iron.SetTexture("_MainTex", TextureFactory.Iron(11));
            s_iron.SetTexture("_BumpMap", flatNormal);
            s_iron.color = Color.white;
            SetFloat(s_iron, "_Metallic", 0.55f);
            SetFloat(s_iron, "_Glossiness", 0.35f);

            s_wickerDark = CloneMaterial(woodSource, "hab_wicker_dark");
            s_wickerDark.SetTexture("_MainTex", TextureFactory.Wicker(7, dark: true));
            s_wickerDark.SetTexture("_BumpMap", TextureFactory.WickerNormal());
            s_wickerDark.color = Color.white;

            // Канаты-трубы (сетка и стропы простого шара) — свой витой канат на шейдере построек.
            s_ropeMesh = CloneMaterial(woodSource, "hab_rope");
            s_ropeMesh.SetTexture("_MainTex", TextureFactory.Rope(5));
            s_ropeMesh.SetTexture("_BumpMap", flatNormal);
            s_ropeMesh.color = Color.white;
            SetFloat(s_ropeMesh, "_Glossiness", 0.08f);

            // Доски кадки среднего шара и льняное полотно его парусов.
            s_planks = CloneMaterial(woodSource, "hab_planks");
            s_planks.SetTexture("_MainTex", TextureFactory.Planks(9));
            s_planks.SetTexture("_BumpMap", flatNormal);
            s_planks.color = Color.white;
            SetFloat(s_planks, "_Glossiness", 0.1f);

            // Корпус драккара — те же доски, темнее (просмолённые), и щиты.
            s_hull = CloneMaterial(woodSource, "hab_hull");
            s_hull.SetTexture("_MainTex", TextureFactory.Planks(10));
            s_hull.SetTexture("_BumpMap", flatNormal);
            s_hull.color = new Color(0.78f, 0.74f, 0.7f);
            SetFloat(s_hull, "_Glossiness", 0.12f);

            s_shield = CloneMaterial(woodSource, "hab_shield");
            s_shield.SetTexture("_MainTex", TextureFactory.Shield(4));
            s_shield.SetTexture("_BumpMap", flatNormal);
            s_shield.color = Color.white;
            SetFloat(s_shield, "_Glossiness", 0.1f);

            s_sailCloth = CloneMaterial(woodSource, "hab_sailcloth");
            s_sailCloth.SetTexture("_MainTex", TextureFactory.Linen(3, -1f));
            s_sailCloth.SetTexture("_BumpMap", flatNormal);
            s_sailCloth.color = Color.white;
            SetFloat(s_sailCloth, "_Glossiness", 0.1f);
            SetFloat(s_sailCloth, "_Metallic", 0f);

            for (int k = 0; k < 3; k++)
            {
                Material m = CloneMaterial(woodSource, "hab_envelope_" + k);
                // Простой шар — «тролья ткань» (эскиз «Грейд I»); у среднего и большого текстуру (лён со швом по экватору,
                // полосатый лён) кладёт сборка модели — ей нужны размеры купола.
                if (k == (int)BalloonKind.Simple)
                {
                    m.SetTexture("_MainTex", TextureFactory.TrollCloth(k + 1));
                }
                m.SetTexture("_BumpMap", flatNormal);
                m.color = Color.white;
                SetFloat(m, "_Glossiness", 0.12f);
                SetFloat(m, "_Metallic", 0f);
                s_envelope[k] = m;
            }

            GameObject cart = PrefabManager.Instance.GetPrefab("Cart");
            LineRenderer cartRope = cart != null ? cart.GetComponent<LineRenderer>() : null;
            s_rope = cartRope != null && cartRope.sharedMaterial != null ? cartRope.sharedMaterial : s_wood;
        }

        // ================================================================== сборка префаба

        private static GameObject Build(BalloonKind kind)
        {
            Layout L = Layout.For(kind);
            var root = new GameObject(PrefabNames[(int)kind]);
            root.transform.SetParent(s_holder.transform, false);
            root.layer = s_vehicleLayer;

            ZNetView nview = root.AddComponent<ZNetView>();
            nview.m_persistent = true;
            nview.m_type = ZDO.ObjectType.Prioritized;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = L.Mass;
            body.useGravity = false;
            body.isKinematic = false;
            body.linearDamping = 0f;
            body.angularDamping = 0.5f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            ZSyncTransform sync = root.AddComponent<ZSyncTransform>();
            sync.m_syncPosition = true;
            sync.m_syncRotation = true;
            sync.m_syncBodyVelocity = true;

            GameObject karve = PrefabManager.Instance.GetPrefab("Karve");
            Piece karvePiece = karve != null ? karve.GetComponent<Piece>() : null;
            WearNTear karveWear = karve != null ? karve.GetComponent<WearNTear>() : null;

            Piece piece = root.AddComponent<Piece>();
            piece.m_name = s_pieceNames[(int)kind];
            piece.m_description = s_pieceDescriptions[(int)kind];
            piece.m_waterPiece = false;
            piece.m_noInWater = true;
            // Без проверки пересечений: иначе на небольшом склоне призрак красный (корзина широкая),
            // а небольшое погружение в землю физика сама исправит. Совсем крутые склоны запрещены.
            piece.m_noClipping = false;
            piece.m_notOnTiltingSurface = true;
            piece.m_canBeRemoved = true;
            piece.m_primaryTarget = false;
            piece.m_randomTarget = true;
            if (karvePiece != null)
            {
                piece.m_placeEffect = karvePiece.m_placeEffect;
            }

            WearNTear wear = root.AddComponent<WearNTear>();
            wear.m_health = L.Health;
            wear.m_noRoofWear = false;
            wear.m_noSupportWear = false;
            wear.m_supports = false;
            wear.m_staticPosition = false;
            wear.m_burnable = false;
            wear.m_autoCreateFragments = false;
            wear.m_materialType = WearNTear.MaterialType.Wood;
            if (karveWear != null)
            {
                wear.m_hitEffect = karveWear.m_hitEffect;
                wear.m_destroyedEffect = karveWear.m_destroyedEffect;
                wear.m_switchEffect = karveWear.m_switchEffect;
                wear.m_damages = karveWear.m_damages;
            }

            BalloonController ctrl = root.AddComponent<BalloonController>();
            ctrl.m_kind = kind;
            ctrl.m_onboardCenter = new Vector3(0f, 1.3f, 0f);
            ctrl.m_onboardHalfSize = new Vector3(L.W * 0.5f + 0.1f, 1.35f, L.D * 0.5f + 0.1f);

            if (kind == BalloonKind.Simple)
            {
                ctrl.m_onboardRadius = SimpleBalloonModel.BasketRadius + 0.1f;
                BuildSimpleFromModel(root, L, ctrl);
                return root;
            }
            if (kind == BalloonKind.Medium)
            {
                ctrl.m_onboardCenter = new Vector3(0f, 1.35f, 0f);
                ctrl.m_onboardHalfSize = new Vector3(1.9f, 1.4f, 1.9f);
                ctrl.m_onboardRadius = MediumBalloonModel.BandOuter + 0.05f;
                BuildMediumFromModel(root, L, ctrl);
                return root;
            }

            // «На борту» — от палубы вверх и в ширину корпуса на этой длине (у носа и кормы он узкий):
            // стоящие на земле у штевней не считаются.
            ctrl.m_onboardCenter = new Vector3(0f, DrakkarModel.DeckY + 1.3f, 0f);
            ctrl.m_onboardHalfSize = new Vector3(DrakkarModel.HalfBeam + 0.3f, 1.5f, DrakkarModel.HalfLength + 0.3f);
            ctrl.m_onboardHalfBeams = new float[27];
            for (int i = 0; i < ctrl.m_onboardHalfBeams.Length; i++)
            {
                float z = Mathf.Lerp(-DrakkarModel.HalfLength, DrakkarModel.HalfLength, i / (float)(ctrl.m_onboardHalfBeams.Length - 1));
                ctrl.m_onboardHalfBeams[i] = DrakkarModel.HalfBeamAt(z) + 0.3f;
            }
            BuildLargeFromModel(root, L, ctrl);
            return root;
        }

        private static GameObject AddMesh(Transform parent, string name, MeshBuilder mb, Material material, bool castShadows = true)
        {
            if (mb.IsEmpty)
            {
                return null;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = s_vehicleLayer;
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("hab_" + name);
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        private static GameObject AddChild(Transform parent, string name, Vector3 localPos, Quaternion localRot, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.layer = layer;
            return go;
        }

        private static BoxCollider AddBoxCollider(Transform parent, string name, Vector3 center, Vector3 size, int layer, Quaternion? rotation = null)
        {
            GameObject go = AddChild(parent, name, center, rotation ?? Quaternion.identity, layer);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        // ------------------------------------------------------------------ общие части

        /// <summary>Место на скамье: Chair (посадка как на корабле) без своей доски — доска уже есть в модели.</summary>
        private static Chair AddChair(Transform root, string name, Vector3 floorPos, float yaw, float width = 0.5f)
        {
            GameObject seat = AddChild(root, name, floorPos, Quaternion.Euler(0f, yaw, 0f), s_nonSolidLayer);
            BoxCollider box = seat.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.47f, 0f);
            box.size = new Vector3(width, 0.2f, 0.45f);
            GameObject attach = AddChild(seat.transform, "attach", new Vector3(0f, 0.02f, 0f), Quaternion.identity, s_nonSolidLayer);
            Chair chair = seat.AddComponent<Chair>();
            chair.m_name = "$hab_seat";
            chair.m_useDistance = 2.5f;
            chair.m_attachPoint = attach.transform;
            chair.m_attachAnimation = "attach_sitship";
            chair.m_detachOffset = new Vector3(0f, 0.6f, 0.3f);
            chair.m_inShip = true;
            return chair;
        }

        /// <summary>
        /// Якорь: лебёдка на борту (с ней можно взаимодействовать) и модель якоря, которая
        /// висит на канате, опускается и лежит на земле в точке крепления.
        /// </summary>
        private static void BuildAnchor(Transform root, Layout L, BalloonController ctrl, Parts parts, Vector3 ropeStart, Vector3 outward)
        {
            // Барабан лебёдки на ободе.
            parts.Wood.AddBox(ropeStart + outward * 0.02f + Vector3.down * 0.12f, new Vector3(0.36f, 0.16f, 0.16f), Quaternion.LookRotation(outward), 1f);

            GameObject winch = AddChild(root, "AnchorWinch", ropeStart, Quaternion.LookRotation(outward), s_nonSolidLayer);
            BoxCollider box = winch.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.3f, 0.05f);
            box.size = new Vector3(0.7f, 0.7f, 0.35f);
            GameObject start = AddChild(winch.transform, "RopeStart", new Vector3(0f, 0f, 0.12f), Quaternion.identity, s_nonSolidLayer);

            float s = L.AnchorScale;
            var anchorMesh = new MeshBuilder();
            anchorMesh.AddBeam(new Vector3(0f, -0.02f, 0f), new Vector3(0f, -0.8f * s, 0f), 0.07f * s);
            anchorMesh.AddBeam(new Vector3(0f, -0.8f * s, 0f), new Vector3(0.38f * s, -0.5f * s, 0f), 0.06f * s);
            anchorMesh.AddBeam(new Vector3(0f, -0.8f * s, 0f), new Vector3(-0.38f * s, -0.5f * s, 0f), 0.06f * s);
            anchorMesh.AddBox(new Vector3(0.38f * s, -0.47f * s, 0f), new Vector3(0.14f * s, 0.12f * s, 0.05f * s), Quaternion.Euler(0f, 0f, 35f), 1f);
            anchorMesh.AddBox(new Vector3(-0.38f * s, -0.47f * s, 0f), new Vector3(0.14f * s, 0.12f * s, 0.05f * s), Quaternion.Euler(0f, 0f, -35f), 1f);
            anchorMesh.AddBeam(new Vector3(0f, -0.14f * s, -0.3f * s), new Vector3(0f, -0.14f * s, 0.3f * s), 0.05f * s);
            anchorMesh.AddBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.1f, 0.12f) * s, 1f);

            Vector3 stowedPos = ropeStart + outward * 0.14f + Vector3.down * 0.12f;
            Quaternion stowedRot = Quaternion.LookRotation(outward);
            GameObject model = AddChild(root, "AnchorModel", stowedPos, stowedRot, s_vehicleLayer);
            model.AddComponent<MeshFilter>().sharedMesh = anchorMesh.ToMesh("hab_anchor");
            model.AddComponent<MeshRenderer>().sharedMaterial = s_iron;
            GameObject ring = AddChild(model.transform, "Ring", new Vector3(0f, 0.05f, 0f), Quaternion.identity, s_vehicleLayer);

            GameObject ropeGo = AddChild(root, "AnchorRope", Vector3.zero, Quaternion.identity, s_vehicleLayer);
            LineRenderer rope = ropeGo.AddComponent<LineRenderer>();
            rope.useWorldSpace = true;
            rope.positionCount = 2;
            rope.startWidth = 0.04f;
            rope.endWidth = 0.04f;
            rope.sharedMaterial = s_rope;
            rope.textureMode = LineTextureMode.Tile;
            rope.generateLightingData = true;
            rope.receiveShadows = false;
            rope.enabled = false;

            BalloonAnchor anchor = winch.AddComponent<BalloonAnchor>();
            anchor.m_ropeStart = start.transform;
            anchor.m_model = model.transform;
            anchor.m_modelRing = ring.transform;
            anchor.m_stowedLocalPos = stowedPos;
            anchor.m_stowedLocalRot = stowedRot;
            anchor.m_rope = rope;
            ctrl.m_anchor = anchor;
        }

        /// <summary>Клон дочернего объекта ванильного префаба без логики (только визуал/огонь/звук/тепло).</summary>
        private static GameObject CloneVanilla(GameObject prefab, string childName, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }
            Transform source = FindDeep(prefab.transform, childName);
            if (source == null)
            {
                Logger.LogWarning($"HotAirBalloons: у {prefab.name} нет '{childName}'");
                return null;
            }
            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = source.name;
            Strip(copy);
            return copy;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name)
            {
                return t;
            }
            foreach (Transform child in t)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static void Strip(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t != go.transform && s_dropChildren.Contains(t.name))
                {
                    Object.DestroyImmediate(t.gameObject);
                }
            }
            foreach (Component c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null)
                {
                    continue;
                }
                if (c is Collider col)
                {
                    // Триггер тепла у огня оставляем (греет замёрзших), остальные коллайдеры убираем.
                    if (!(col.isTrigger && col.GetComponent<EffectArea>() != null))
                    {
                        Object.DestroyImmediate(col);
                    }
                    continue;
                }
                if (c is LODGroup || c is ZNetView || c is Piece || c is WearNTear || c is Fireplace || c is SmokeSpawner || c is Aoe ||
                    c is Container || c is Chair)
                {
                    Object.DestroyImmediate(c);
                }
            }
        }

        private static EffectList FuelEffects(GameObject firePrefab)
        {
            Fireplace fireplace = firePrefab != null ? firePrefab.GetComponent<Fireplace>() : null;
            return fireplace != null ? fireplace.m_fuelAddedEffects : new EffectList();
        }

        // ------------------------------------------------------------------ простой шар

        /// <summary>
        /// Простой шар по эскизу «Грейд I · 1 персонаж»: геометрия — SimpleBalloonModel (её же можно выгрузить
        /// и посмотреть без игры), здесь — сборка в префаб: материалы, огонь, коллайдеры, управление, якорь.
        /// </summary>
        private static void BuildSimpleFromModel(GameObject root, Layout L, BalloonController ctrl)
        {
            SimpleBalloonModel model = SimpleBalloonModel.Build();
            Transform t = root.transform;

            // Корзина — на самом корне (нужно для иконки Jotunn), остальное — дочерними мешами.
            root.AddComponent<MeshFilter>().sharedMesh = model.Wicker.ToMesh("hab_simple_wicker");
            MeshRenderer rootRenderer = root.AddComponent<MeshRenderer>();
            rootRenderer.sharedMaterial = s_wickerDark;
            rootRenderer.shadowCastingMode = ShadowCastingMode.On;
            AddMesh(t, "WoodParts", model.Wood, s_wood);
            AddMesh(t, "IronParts", model.Iron, s_iron);
            AddMesh(t, "Ropes", model.Rope, s_ropeMesh);

            GameObject env = AddMesh(t, "Envelope", model.Envelope, s_envelope[(int)BalloonKind.Simple]);
            if (env != null)
            {
                MeshRenderer renderer = env.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                ctrl.m_envelopeRenderer = renderer;
            }
            EnvelopeProfile p = model.Profile;
            ctrl.m_envelopeMouthY = model.EnvelopeBaseY;
            ctrl.m_envelopeMouthR = p.MouthRadius;
            ctrl.m_envelopeRadius = p.Radius;
            ctrl.m_envelopeNeckH = p.NeckHeight;
            ctrl.m_envelopeLowerH = p.LowerHeight;
            ctrl.m_envelopeUpperH = p.UpperHeight;
            ctrl.m_envelopeLowerExp = p.LowerExponent;

            // Физика: днище — выпуклый восьмиугольник, стенка — восемь досок по кругу.
            Transform colliders = AddChild(t, "Colliders", Vector3.zero, Quaternion.identity, s_vehicleLayer).transform;
            GameObject floor = AddChild(colliders, "floor", Vector3.zero, Quaternion.identity, s_vehicleLayer);
            MeshCollider floorCollider = floor.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = PolygonPrism(8, SimpleBalloonModel.BasketRadius, 0f, SimpleBalloonModel.FloorTop);
            floorCollider.convex = true;
            int wall = 0;
            foreach (BoxSpec b in model.WallColliders)
            {
                AddBoxCollider(colliders, "wall_" + wall++, b.Center, b.Size, s_vehicleLayer, b.GetRotation());
            }

            // Огонь — ванильные угли и пламя жаровни в чаше.
            GameObject brazierPrefab = PrefabManager.Instance.GetPrefab("piece_brazierfloor01");
            GameObject fireRoot = AddChild(t, "Fire", model.FirePosition, Quaternion.identity, s_vehicleLayer);
            fireRoot.transform.localScale = Vector3.one * model.FireScale;
            var fires = new List<GameObject>();
            foreach (string child in new[] { "_enabled", "_enabled_high" })
            {
                GameObject f = CloneVanilla(brazierPrefab, child, fireRoot.transform);
                if (f != null)
                {
                    f.transform.localPosition = Vector3.zero;
                    f.SetActive(false);
                    fires.Add(f);
                }
            }
            // Квадрат тлеющей золы у жаровни шире нашей чаши — уменьшаем, чтобы углы не торчали сквозь стенки.
            Transform ash = FindDeep(fireRoot.transform, "Quad (1)");
            if (ash != null)
            {
                ash.localScale *= 0.6f;
            }

            // Держится левой рукой за передний левый канат, стоя под чашей (поза «держаться за мачту»).
            Quaternion operatorRot = Quaternion.Euler(0f, model.OperatorYaw, 0f);
            GameObject attach = AddChild(t, "OperatorAttach", model.OperatorPosition, operatorRot, s_nonSolidLayer);

            // Навестись на огонь можно по всей чаше с цепями и обручем.
            GameObject control = AddChild(t, "BurnerControl", model.BurnerColliderCenter, operatorRot, s_nonSolidLayer);
            BoxCollider box = control.AddComponent<BoxCollider>();
            box.size = model.BurnerColliderSize;
            BalloonBurner burner = control.AddComponent<BalloonBurner>();
            burner.m_name = "$hab_firebowl";
            burner.m_useText = "$hab_hold_on";
            burner.m_useDistance = 3.5f;
            burner.m_attachPoint = attach.transform;
            burner.m_attachAnimation = "attach_mast";
            burner.m_detachOffset = new Vector3(0f, 0.05f, 0f);
            burner.m_fireObjects = fires.ToArray();
            burner.m_fuelAddedEffects = FuelEffects(brazierPrefab);
            ctrl.m_burner = burner;

            var anchorParts = new Parts();
            BuildAnchor(t, L, ctrl, anchorParts, model.AnchorRopeStart, model.AnchorOutward);
            AddMesh(t, "AnchorDrum", anchorParts.Wood, s_wood);
        }

        /// <summary>Выпуклая n-гранная призма (для MeshCollider днища круглой корзины).</summary>
        private static Mesh PolygonPrism(int sides, float radius, float y0, float y1)
        {
            var verts = new List<Vector3>();
            for (int k = 0; k < sides; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / sides;
                verts.Add(new Vector3(Mathf.Sin(a) * radius, y0, Mathf.Cos(a) * radius));
                verts.Add(new Vector3(Mathf.Sin(a) * radius, y1, Mathf.Cos(a) * radius));
            }
            var tris = new List<int>();
            for (int k = 0; k < sides; k++)
            {
                int a0 = k * 2;
                int a1 = k * 2 + 1;
                int b0 = (k + 1) % sides * 2;
                int b1 = b0 + 1;
                tris.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
            }
            for (int k = 1; k < sides - 1; k++)
            {
                tris.AddRange(new[] { 0, (k + 1) * 2, k * 2 });
                tris.AddRange(new[] { 1, k * 2 + 1, (k + 1) * 2 + 1 });
            }
            var mesh = new Mesh { name = "hab_floor_" + sides };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        // ------------------------------------------------------------------ средний шар

        /// <summary>
        /// Средний шар по эскизу «Грейд II»: геометрия — MediumBalloonModel (её можно выгрузить и посмотреть без игры),
        /// здесь — сборка префаба: материалы, огонь в чаше, коллайдеры, два места управления (чаша огня и руль-палка),
        /// три места на скамье, паруса (складываются и поворачиваются рулём) и якорь.
        /// </summary>
        private static void BuildMediumFromModel(GameObject root, Layout L, BalloonController ctrl)
        {
            MediumBalloonModel model = MediumBalloonModel.Build();
            Transform t = root.transform;

            // Льняной купол со швом по экватору — шов кладём на ту высоту, где экватор у этой модели.
            Material linen = s_envelope[(int)BalloonKind.Medium];
            linen.SetTexture("_MainTex", TextureFactory.Linen(2, model.EquatorV));

            // Кадка — на самом корне (нужно для иконки Jotunn), остальное — дочерними мешами.
            root.AddComponent<MeshFilter>().sharedMesh = model.Planks.ToMesh("hab_medium_tub");
            MeshRenderer rootRenderer = root.AddComponent<MeshRenderer>();
            rootRenderer.sharedMaterial = s_planks;
            rootRenderer.shadowCastingMode = ShadowCastingMode.On;
            AddMesh(t, "WoodParts", model.Wood, s_wood);
            AddMesh(t, "IronParts", model.Iron, s_iron);
            AddMesh(t, "Ropes", model.Rope, s_ropeMesh);

            GameObject env = AddMesh(t, "Envelope", model.Envelope, linen);
            if (env != null)
            {
                MeshRenderer renderer = env.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                ctrl.m_envelopeRenderer = renderer;
            }
            EnvelopeProfile p = model.Profile;
            ctrl.m_envelopeMouthY = model.EnvelopeBaseY;
            ctrl.m_envelopeMouthR = p.MouthRadius;
            ctrl.m_envelopeRadius = p.Radius;
            ctrl.m_envelopeNeckH = p.NeckHeight;
            ctrl.m_envelopeLowerH = p.LowerHeight;
            ctrl.m_envelopeUpperH = p.UpperHeight;
            ctrl.m_envelopeLowerExp = p.LowerExponent;

            // Физика: днище — выпуклый 16-угольник, борт — 16 наклонных досок, столб огня и стойка руля — твёрдые.
            Transform colliders = AddChild(t, "Colliders", Vector3.zero, Quaternion.identity, s_vehicleLayer).transform;
            GameObject floor = AddChild(colliders, "floor", Vector3.zero, Quaternion.identity, s_vehicleLayer);
            MeshCollider floorCollider = floor.AddComponent<MeshCollider>();
            floorCollider.sharedMesh = PolygonPrism(16, MediumBalloonModel.FloorColliderRadius, 0f, MediumBalloonModel.FloorTop);
            floorCollider.convex = true;
            int n = 0;
            foreach (BoxSpec b in model.WallColliders)
            {
                AddBoxCollider(colliders, "wall_" + n++, b.Center, b.Size, s_vehicleLayer, b.GetRotation());
            }
            foreach (BoxSpec b in model.SolidColliders)
            {
                AddBoxCollider(colliders, "solid_" + n++, b.Center, b.Size, s_vehicleLayer, b.GetRotation());
            }

            // Огонь — ванильные угли и пламя жаровни в чаше фонаря.
            GameObject brazierPrefab = PrefabManager.Instance.GetPrefab("piece_brazierfloor01");
            GameObject fireRoot = AddChild(t, "Fire", model.FirePosition, Quaternion.identity, s_vehicleLayer);
            fireRoot.transform.localScale = Vector3.one * model.FireScale;
            var fires = new List<GameObject>();
            foreach (string child in new[] { "_enabled", "_enabled_high" })
            {
                GameObject f = CloneVanilla(brazierPrefab, child, fireRoot.transform);
                if (f != null)
                {
                    f.transform.localPosition = Vector3.zero;
                    f.SetActive(false);
                    fires.Add(f);
                }
            }
            Transform ash = FindDeep(fireRoot.transform, "Quad (1)");
            if (ash != null)
            {
                ash.localScale *= 0.8f;
            }

            // У огня: держится левой рукой за столб под чашей (поза «держаться за мачту»).
            Quaternion fireRot = Quaternion.Euler(0f, model.FireOperatorYaw, 0f);
            GameObject fireAttach = AddChild(t, "FireAttach", model.FireOperatorPosition, fireRot, s_nonSolidLayer);
            GameObject fireControl = AddChild(t, "BurnerControl", model.BurnerColliderCenter, fireRot, s_nonSolidLayer);
            fireControl.AddComponent<BoxCollider>().size = model.BurnerColliderSize;
            BalloonBurner burner = fireControl.AddComponent<BalloonBurner>();
            burner.m_name = "$hab_firebowl";
            burner.m_useText = "$hab_hold_on";
            burner.m_useDistance = 3.5f;
            burner.m_attachPoint = fireAttach.transform;
            burner.m_attachAnimation = "attach_mast";
            burner.m_detachOffset = new Vector3(0f, 0.05f, 0f);
            burner.m_steering = false;
            burner.m_fireObjects = fires.ToArray();
            burner.m_fuelAddedEffects = FuelEffects(brazierPrefab);
            ctrl.m_burner = burner;

            // Руль-палка: рулевой у кормы держится за неё левой рукой; палка качается вбок вместе с рулём.
            Quaternion helmRot = Quaternion.Euler(0f, model.HelmYaw, 0f);
            GameObject helmAttach = AddChild(t, "HelmAttach", model.HelmPosition, helmRot, s_nonSolidLayer);
            GameObject lever = AddChild(t, "TillerPivot", model.LeverPivot, Quaternion.identity, s_vehicleLayer);
            AddMesh(lever.transform, "Wood", model.LeverWood, s_wood);
            AddMesh(lever.transform, "Iron", model.LeverIron, s_iron);
            AddMesh(lever.transform, "Rope", model.LeverRope, s_ropeMesh);
            GameObject tillerControl = AddChild(t, "TillerControl", model.TillerColliderCenter, helmRot, s_nonSolidLayer);
            tillerControl.AddComponent<BoxCollider>().size = model.TillerColliderSize;
            BalloonTiller tiller = tillerControl.AddComponent<BalloonTiller>();
            tiller.m_name = "$hab_tiller";
            tiller.m_useText = "$hab_take_tiller";
            tiller.m_useDistance = 3f;
            tiller.m_attachPoint = helmAttach.transform;
            tiller.m_attachAnimation = "attach_mast";
            tiller.m_detachOffset = new Vector3(0f, 0.05f, 0f);
            tiller.m_steering = true;
            ctrl.m_helm = tiller;
            ctrl.m_tillerPivots = new[] { lever.transform };
            ctrl.m_tillerVisualAngle = 14f;

            // Три места на скамье у носа, лицом к огню.
            for (int i = 0; i < model.Seats.Count; i++)
            {
                AddChair(t, "Seat" + i, model.Seats[i].Position, model.Seats[i].Yaw);
            }

            // Паруса-веера по бортам: поворачиваются рулём вокруг оси крепления и складываются к средней рее.
            var pivots = new List<Transform>();
            foreach (SailParts sp in model.Sails)
            {
                GameObject mount = AddChild(t, sp.BaseYaw < 90f ? "SailRight" : "SailLeft", sp.Pivot, Quaternion.Euler(0f, sp.BaseYaw, 0f), s_vehicleLayer);
                GameObject yaw = AddChild(mount.transform, "Sail", Vector3.zero, Quaternion.identity, s_vehicleLayer);
                GameObject cloth = AddMesh(yaw.transform, "Cloth", sp.Cloth, s_sailCloth);
                if (cloth != null)
                {
                    cloth.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
                }
                GameObject batten = AddMesh(yaw.transform, "Batten", sp.Batten, s_wood);
                AddMesh(yaw.transform, "Hub", sp.Hub, s_iron);
                AddMesh(yaw.transform, "MiddleSpar", sp.MiddleWood, s_wood);
                AddMesh(yaw.transform, "MiddleRope", sp.MiddleRope, s_ropeMesh);
                GameObject upper = AddChild(yaw.transform, "UpperSpar", Vector3.zero, Quaternion.identity, s_vehicleLayer);
                AddMesh(upper.transform, "Wood", sp.UpperWood, s_wood);
                AddMesh(upper.transform, "Rope", sp.UpperRope, s_ropeMesh);
                GameObject lower = AddChild(yaw.transform, "LowerSpar", Vector3.zero, Quaternion.identity, s_vehicleLayer);
                AddMesh(lower.transform, "Wood", sp.LowerWood, s_wood);
                AddMesh(lower.transform, "Rope", sp.LowerRope, s_ropeMesh);

                BalloonSail sail = yaw.AddComponent<BalloonSail>();
                var deform = new List<MeshFilter>();
                if (cloth != null)
                {
                    deform.Add(cloth.GetComponent<MeshFilter>());
                }
                if (batten != null)
                {
                    deform.Add(batten.GetComponent<MeshFilter>());
                }
                sail.m_deform = deform.ToArray();
                sail.m_upper = upper.transform;
                sail.m_lower = lower.transform;
                sail.m_upperAngle = sp.UpperAngle;
                sail.m_middleAngle = sp.MiddleAngle;
                sail.m_lowerAngle = sp.LowerAngle;
                pivots.Add(yaw.transform);
            }
            ctrl.m_rudderPivots = pivots.ToArray();
            ctrl.m_rudderVisualAngle = 30f;

            var anchorParts = new Parts();
            BuildAnchor(t, L, ctrl, anchorParts, model.AnchorRopeStart, model.AnchorOutward);
            AddMesh(t, "AnchorDrum", anchorParts.Wood, s_wood);
        }

        // ------------------------------------------------------------------ большой шар

        // ------------------------------------------------------------------ большой шар (драккар)

        /// <summary>
        /// «Небесный драккар» по эскизу «Грейд III»: геометрия — DrakkarModel (её можно выгрузить и посмотреть без игры),
        /// здесь — сборка префаба: материалы, огонь в фонаре, коллайдеры корпуса и фальшборта, места управления
        /// (фонарь огня — стоя, руль-палка — сидя, как на ванильных кораблях), четыре сиденья с кривошипами, винт, руль,
        /// трапы, сундук и якорь.
        /// </summary>
        private static void BuildLargeFromModel(GameObject root, Layout L, BalloonController ctrl)
        {
            DrakkarModel model = DrakkarModel.Build();
            Transform t = root.transform;

            Material striped = s_envelope[(int)BalloonKind.Large];
            striped.SetTexture("_MainTex", TextureFactory.StripedLinen(3, model.StripeV));

            // Корпус — на самом корне (нужно для иконки Jotunn), остальное — дочерними мешами.
            root.AddComponent<MeshFilter>().sharedMesh = model.Hull.ToMesh("hab_drakkar_hull");
            MeshRenderer rootRenderer = root.AddComponent<MeshRenderer>();
            rootRenderer.sharedMaterial = s_hull;
            rootRenderer.shadowCastingMode = ShadowCastingMode.TwoSided;
            AddMesh(t, "Deck", model.Deck, s_planks);
            AddMesh(t, "WoodParts", model.Wood, s_wood);
            AddMesh(t, "IronParts", model.Iron, s_iron);
            AddMesh(t, "Ropes", model.Rope, s_ropeMesh);
            AddMesh(t, "Shields", model.Shields, s_shield);

            GameObject env = AddMesh(t, "Envelope", model.Envelope, striped);
            if (env != null)
            {
                MeshRenderer renderer = env.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                ctrl.m_envelopeRenderer = renderer;
            }
            ctrl.m_envelopeHorizontal = true;
            ctrl.m_envelopeCenter = new Vector3(0f, DrakkarModel.EnvelopeCenterY, 0f);
            ctrl.m_envelopeRadius = DrakkarModel.EnvelopeRadius;
            ctrl.m_envelopeHalfLength = DrakkarModel.EnvelopeHalfLength;

            // Физика: корпус ниже палубы — выпуклый, фальшборт — наклонные доски вдоль бортов, стойка фонаря — твёрдая.
            Transform colliders = AddChild(t, "Colliders", Vector3.zero, Quaternion.identity, s_vehicleLayer).transform;
            GameObject hull = AddChild(colliders, "hull", Vector3.zero, Quaternion.identity, s_vehicleLayer);
            MeshCollider hullCollider = hull.AddComponent<MeshCollider>();
            hullCollider.sharedMesh = PointCloud(model.HullColliderPoints, "hab_drakkar_hull_collider");
            hullCollider.convex = true;
            int n = 0;
            foreach (BoxSpec b in model.WallColliders)
            {
                AddBoxCollider(colliders, "wall_" + n++, b.Center, b.Size, s_vehicleLayer, b.GetRotation());
            }
            foreach (BoxSpec b in model.SolidColliders)
            {
                AddBoxCollider(colliders, "solid_" + n++, b.Center, b.Size, s_vehicleLayer, b.GetRotation());
            }

            // Огонь — ванильные угли и пламя жаровни в чаше фонаря.
            GameObject brazierPrefab = PrefabManager.Instance.GetPrefab("piece_brazierfloor01");
            GameObject fireRoot = AddChild(t, "Fire", model.FirePosition, Quaternion.identity, s_vehicleLayer);
            fireRoot.transform.localScale = Vector3.one * model.FireScale;
            var fires = new List<GameObject>();
            foreach (string child in new[] { "_enabled", "_enabled_high" })
            {
                GameObject f = CloneVanilla(brazierPrefab, child, fireRoot.transform);
                if (f != null)
                {
                    f.transform.localPosition = Vector3.zero;
                    f.SetActive(false);
                    fires.Add(f);
                }
            }
            Transform ash = FindDeep(fireRoot.transform, "Quad (1)");
            if (ash != null)
            {
                ash.localScale *= 0.8f;
            }

            // У огня: держится левой рукой за стойку фонаря (поза «держаться за мачту»).
            Quaternion fireRot = Quaternion.Euler(0f, model.FireOperatorYaw, 0f);
            GameObject fireAttach = AddChild(t, "FireAttach", model.FireOperatorPosition, fireRot, s_nonSolidLayer);
            GameObject fireControl = AddChild(t, "BurnerControl", model.BurnerColliderCenter, fireRot, s_nonSolidLayer);
            fireControl.AddComponent<BoxCollider>().size = model.BurnerColliderSize;
            BalloonBurner burner = fireControl.AddComponent<BalloonBurner>();
            burner.m_name = "$hab_lantern";
            burner.m_useText = "$hab_hold_on";
            burner.m_useDistance = 3.5f;
            burner.m_attachPoint = fireAttach.transform;
            burner.m_attachAnimation = "attach_mast";
            burner.m_detachOffset = new Vector3(0f, 0.05f, 0f);
            burner.m_steering = false;
            burner.m_fireObjects = fires.ToArray();
            // Уголь подбрасывают со звуком плавильни (у неё тоже уголь), иначе — как в жаровню.
            Smelter smelter = PrefabManager.Instance.GetPrefab("smelter")?.GetComponent<Smelter>();
            burner.m_fuelAddedEffects = smelter != null && smelter.m_fuelAddedEffects != null && smelter.m_fuelAddedEffects.m_effectPrefabs.Length > 0
                ? smelter.m_fuelAddedEffects
                : FuelEffects(brazierPrefab);
            ctrl.m_burner = burner;

            // Руль: баллер с пером и руль-палкой поворачивается вокруг вертикали. Руль вправо — палка уходит влево,
            // перо вправо (как у лодки). Рулевой сидит, как на ванильных кораблях.
            GameObject rudder = AddChild(t, "Rudder", model.RudderPivot, Quaternion.identity, s_vehicleLayer);
            AddMesh(rudder.transform, "Wood", model.RudderWood, s_wood);
            AddMesh(rudder.transform, "Iron", model.RudderIron, s_iron);
            ctrl.m_rudderPivots = new[] { rudder.transform };
            ctrl.m_rudderVisualAngle = -16f;

            Quaternion helmRot = Quaternion.Euler(0f, model.HelmYaw, 0f);
            GameObject helmAttach = AddChild(t, "HelmAttach", model.HelmPosition, helmRot, s_nonSolidLayer);
            GameObject tillerControl = AddChild(t, "TillerControl", model.TillerColliderCenter, helmRot, s_nonSolidLayer);
            tillerControl.AddComponent<BoxCollider>().size = model.TillerColliderSize;
            BalloonTiller tiller = tillerControl.AddComponent<BalloonTiller>();
            tiller.m_name = "$hab_tiller";
            tiller.m_useText = "$hab_sit_tiller";
            tiller.m_useDistance = 3f;
            tiller.m_attachPoint = helmAttach.transform;
            tiller.m_attachAnimation = "attach_sitship";
            tiller.m_detachOffset = new Vector3(0f, 0.6f, 0.35f);
            tiller.m_steering = true;
            ctrl.m_helm = tiller;

            // Винт: крутится тем быстрее, чем больше гребцов.
            GameObject propeller = AddChild(t, "Propeller", model.PropellerPivot, Quaternion.identity, s_vehicleLayer);
            AddMesh(propeller.transform, "Wood", model.PropellerWood, s_wood);
            AddMesh(propeller.transform, "Iron", model.PropellerIron, s_iron);
            ctrl.m_propeller = propeller.transform;

            // Четыре сиденья с рукоятями: сел — крутишь винт (кривошип перед сиденьем крутится, пока место занято).
            Mesh crankIron = model.CrankIron.ToMesh("hab_crank_iron");
            Mesh crankWood = model.CrankWood.ToMesh("hab_crank_wood");
            var seats = new List<BalloonCrankSeat>();
            for (int i = 0; i < model.Cranks.Count; i++)
            {
                CrankSpec c = model.Cranks[i];
                Chair chair = AddChair(t, "CrankSeat" + i, c.SeatPosition, c.SeatYaw);
                chair.m_name = "$hab_crank_seat";
                GameObject crank = AddChild(t, "Crank" + i, c.CrankPivot, c.CrankRotation, s_vehicleLayer);
                AddSharedMesh(crank.transform, "Iron", crankIron, s_iron);
                AddSharedMesh(crank.transform, "Wood", crankWood, s_wood);
                BalloonCrankSeat seat = chair.gameObject.AddComponent<BalloonCrankSeat>();
                seat.m_index = i;
                seat.m_crank = crank.transform;
                seats.Add(seat);
            }
            ctrl.m_crankSeats = seats.ToArray();

            // Трапы по бортам (ванильная лестница корабля: [E] — и вы на палубе): борт выше, чем допрыгнуть.
            int ladder = 0;
            foreach (LadderSpec spec in model.Ladders)
            {
                GameObject go = AddChild(t, "Ladder" + ladder, spec.Center, Quaternion.Euler(0f, spec.Yaw, 0f), s_nonSolidLayer);
                go.AddComponent<BoxCollider>().size = spec.Size;
                GameObject target = AddChild(t, "LadderTarget" + ladder++, spec.Target, Quaternion.Euler(0f, spec.TargetYaw, 0f), s_nonSolidLayer);
                Ladder lad = go.AddComponent<Ladder>();
                lad.m_targetPos = target.transform;
                lad.m_name = "$piece_ship_ladder";
                lad.m_useDistance = 3.5f;
            }

            // Сундук на 8 ячеек у носа, лицом к корме (поменьше, чтобы уместился в узком носу).
            BuildChest(t, ctrl, model.ChestPosition, 0.75f);

            var anchorParts = new Parts();
            BuildAnchor(t, L, ctrl, anchorParts, model.AnchorRopeStart, model.AnchorOutward);
            AddMesh(t, "AnchorDrum", anchorParts.Wood, s_wood);
        }

        private static GameObject AddSharedMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = s_vehicleLayer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        /// <summary>Облако точек в меш для выпуклого MeshCollider: оболочку по вершинам строит физика.</summary>
        private static Mesh PointCloud(List<Vector3> points, string name)
        {
            var tris = new List<int>();
            for (int i = 1; i + 1 < points.Count; i++)
            {
                tris.Add(0);
                tris.Add(i);
                tris.Add(i + 1);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(points);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildChest(Transform root, BalloonController ctrl, Vector3 floorPos, float scale = 0.85f)
        {
            GameObject chestPrefab = PrefabManager.Instance.GetPrefab("piece_chest_wood");
            Container vanilla = chestPrefab != null ? chestPrefab.GetComponent<Container>() : null;

            GameObject chest = AddChild(root, "Chest", floorPos, Quaternion.Euler(0f, 180f, 0f), s_vehicleLayer);
            BoxCollider box = chest.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.33f, 0f) * (scale / 0.85f);
            box.size = new Vector3(1.4f, 0.66f, 0.66f) * (scale / 0.85f);

            GameObject visual = CloneVanilla(chestPrefab, "New", chest.transform);
            GameObject open = null;
            GameObject closed = null;
            if (visual != null)
            {
                visual.transform.localScale = Vector3.one * scale;
                Transform o = FindDeep(visual.transform, "woodchesttop_open");
                Transform c = FindDeep(visual.transform, "woodchesttop_closed");
                open = o != null ? o.gameObject : null;
                closed = c != null ? c.gameObject : null;
            }

            Container container = chest.AddComponent<Container>();
            container.m_name = "$hab_chest";
            container.m_width = 4;
            container.m_height = 2;
            container.m_privacy = Container.PrivacySetting.Public;
            container.m_checkGuardStone = false;
            container.m_autoDestroyEmpty = false;
            container.m_rootObjectOverride = root.GetComponent<ZNetView>();
            container.m_open = open;
            container.m_closed = closed;
            if (vanilla != null)
            {
                container.m_bkg = vanilla.m_bkg;
                container.m_openEffects = vanilla.m_openEffects;
                container.m_closeEffects = vanilla.m_closeEffects;
            }
            ctrl.m_chest = container;
        }
    }
}
