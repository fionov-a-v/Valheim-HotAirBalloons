using UnityEngine;

namespace HotAirBalloons.Visuals
{
    /// <summary>Цветовая схема оболочки.</summary>
    internal struct EnvelopeScheme
    {
        public Color A;
        public Color B;
        public Color Seam;
        public Color Band;
        public Color Crown;
        public bool Patchwork;

        public static EnvelopeScheme For(BalloonKind kind)
        {
            switch (kind)
            {
                case BalloonKind.Simple:
                    // Сшитые шкуры, пропитанные смолой.
                    return new EnvelopeScheme
                    {
                        A = new Color(0.66f, 0.50f, 0.33f),
                        B = new Color(0.55f, 0.40f, 0.26f),
                        Seam = new Color(0.22f, 0.15f, 0.09f),
                        Band = new Color(0.36f, 0.25f, 0.15f),
                        Crown = new Color(0.45f, 0.32f, 0.20f),
                        Patchwork = true,
                    };
                case BalloonKind.Medium:
                    // Красно-белые полосы, как на парусе драккара.
                    return new EnvelopeScheme
                    {
                        A = new Color(0.62f, 0.11f, 0.09f),
                        B = new Color(0.87f, 0.83f, 0.72f),
                        Seam = new Color(0.25f, 0.08f, 0.06f),
                        Band = new Color(0.38f, 0.07f, 0.06f),
                        Crown = new Color(0.87f, 0.83f, 0.72f),
                    };
                default:
                    // Лён: синий с охрой.
                    return new EnvelopeScheme
                    {
                        A = new Color(0.14f, 0.22f, 0.44f),
                        B = new Color(0.80f, 0.62f, 0.24f),
                        Seam = new Color(0.08f, 0.10f, 0.18f),
                        Band = new Color(0.10f, 0.14f, 0.28f),
                        Crown = new Color(0.90f, 0.87f, 0.78f),
                    };
            }
        }
    }

    internal static class TextureFactory
    {
        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144269504;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix;
            float fy = y - iy;
            float a = Hash(ix, iy, seed);
            float b = Hash(ix + 1, iy, seed);
            float c = Hash(ix, iy + 1, seed);
            float d = Hash(ix + 1, iy + 1, seed);
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        private static Texture2D NewTexture(string name, int w, int h, bool linear)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            return tex;
        }

        /// <summary>
        /// Текстура оболочки: по U — одна пара клиньев (две полосы), по V — от горловины к макушке.
        /// </summary>
        public static Texture2D Envelope(EnvelopeScheme s, int seed)
        {
            const int w = 256;
            const int h = 512;
            Texture2D tex = NewTexture("hab_envelope_" + seed, w, h, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c = u < 0.5f ? s.A : s.B;

                    if (s.Patchwork)
                    {
                        // Лоскуты шкур разного оттенка.
                        int row = Mathf.FloorToInt(v * 9f);
                        int col = u < 0.5f ? 0 : 1;
                        float tint = 0.88f + 0.24f * Hash(col, row, seed);
                        c *= tint;
                        float fv = v * 9f - row;
                        if (fv < 0.025f || fv > 0.975f)
                        {
                            c = Color.Lerp(c, s.Seam, 0.7f);
                        }
                    }

                    if (v < 0.07f)
                    {
                        c = s.Band;
                    }
                    else if (v > 0.30f && v < 0.335f)
                    {
                        c = s.Band;
                    }
                    else if (v > 0.93f)
                    {
                        c = s.Crown;
                    }

                    // Швы по краям клиньев и лёгкая выпуклость клина.
                    float fu = u * 2f - Mathf.Floor(u * 2f);
                    float seam = Mathf.Min(fu, 1f - fu);
                    if (seam < 0.018f)
                    {
                        c = Color.Lerp(c, s.Seam, 0.85f);
                    }
                    float bulge = 0.9f + 0.1f * Mathf.Sin(fu * Mathf.PI);
                    float grain = 0.95f + 0.1f * ValueNoise(u * 64f, v * 128f, seed);
                    c *= bulge * grain;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// «Тролья ткань»: синяя мозаика мелких квадратиков (выделанная шкура тролля), сшитая из лоскутов.
        /// По U — одна пара клиньев, по V — от горловины к макушке. Швы — тёмные линии со светлыми стежками поперёк.
        /// </summary>
        public static Texture2D TrollCloth(int seed)
        {
            const int w = 512;
            const int h = 1024;
            const int cell = 20;
            Texture2D tex = NewTexture("hab_trollcloth_" + seed, w, h, false);
            var px = new Color32[w * h];

            var dark = new Color(0.31f, 0.39f, 0.53f);
            var light = new Color(0.60f, 0.69f, 0.82f);
            var seamColor = new Color(0.10f, 0.12f, 0.18f);
            var stitch = new Color(0.70f, 0.55f, 0.35f);
            var stitchShadow = new Color(0.25f, 0.18f, 0.12f);

            // Горизонтальные швы лоскутов: у каждого клина свои (в долях V).
            float[][] rows =
            {
                new[] { 0.22f, 0.58f },
                new[] { 0.40f, 0.78f },
            };

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    int gore = u < 0.5f ? 0 : 1;
                    float fu = u * 2f - gore;

                    // Номер лоскута — для своего оттенка.
                    int patch = 0;
                    foreach (float r in rows[gore])
                    {
                        if (v > r)
                        {
                            patch++;
                        }
                    }
                    float patchTint = 0.82f + 0.36f * Hash(gore, patch, seed);

                    // Мозаика: квадратики со случайной яркостью, лёгкий «выпуклый» блик к середине клина.
                    int cx = x / cell;
                    int cy = y / cell;
                    float n = Hash(cx, cy, seed) * 0.6f + ValueNoise(u * 6f, v * 10f, seed + 3) * 0.4f;
                    Color c = Color.Lerp(dark, light, n) * patchTint;
                    c *= 0.88f + 0.12f * Mathf.Sin(fu * Mathf.PI);

                    // Вертикальный шов по краю клина и горизонтальные швы лоскутов.
                    float seamU = Mathf.Min(fu, 1f - fu) * (w / 2f);
                    float nearestRow = 1f;
                    foreach (float r in rows[gore])
                    {
                        nearestRow = Mathf.Min(nearestRow, Mathf.Abs(v - r));
                    }
                    float seamV = nearestRow * h;

                    if (seamU < 2f || seamV < 2f)
                    {
                        c = seamColor;
                    }

                    // Стежки: короткие светлые перемычки поперёк шва через равные промежутки.
                    if (seamU < 13f && (y % 34) < 9)
                    {
                        c = seamU < 11f && (y % 34) < 8 ? stitch : stitchShadow;
                    }
                    else if (seamV < 13f && (x % 34) < 9)
                    {
                        c = seamV < 11f && (x % 34) < 8 ? stitch : stitchShadow;
                    }

                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        /// <summary>
        /// Льняная ткань среднего шара (эскиз «Грейд II»): кремовая мозаика квадратиков разного оттенка, местами крупнее,
        /// с тонкой фактурой переплетения. Для купола (seamV &gt;= 0): по U — одна пара клиньев с едва заметными швами
        /// по краям, по V — от горловины к макушке, на высоте seamV — шов по экватору со стежками. Для парусов (seamV &lt; 0) —
        /// без швов, квадратная плитка.
        /// </summary>
        public static Texture2D Linen(int seed, float seamV)
        {
            bool envelope = seamV >= 0f;
            int w = 512;
            int h = envelope ? 1024 : 512;
            const int cell = 16;
            Texture2D tex = NewTexture(envelope ? "hab_linen_" + seed : "hab_sailcloth_" + seed, w, h, false);
            var px = new Color32[w * h];

            var light = new Color(0.86f, 0.80f, 0.69f);
            var mid = new Color(0.79f, 0.73f, 0.62f);
            var dark = new Color(0.69f, 0.63f, 0.54f);
            var seamColor = new Color(0.60f, 0.55f, 0.46f);
            var stitch = new Color(0.52f, 0.45f, 0.35f);
            var soot = new Color(0.30f, 0.27f, 0.24f);

            int cellsX = w / cell;
            int cellsY = h / cell;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    int cx = x / cell;
                    int cy = y / cell;
                    // Крупные квадраты: иногда блок 2x2 клеток одного оттенка (как на эскизе).
                    int bx = cx >> 1;
                    int by = cy >> 1;
                    bool big = Hash(bx, by, seed + 11) > 0.72f;
                    float n = big ? Hash(bx, by, seed + 12) : Hash(cx % cellsX, cy % cellsY, seed);
                    Color c;
                    if (n < 0.1f)
                    {
                        c = Color.Lerp(dark, mid, n / 0.1f);
                    }
                    else if (n < 0.45f)
                    {
                        c = Color.Lerp(mid, light, (n - 0.1f) / 0.35f);
                    }
                    else
                    {
                        c = light * (0.97f + 0.06f * (n - 0.45f) / 0.55f);
                    }

                    // Переплетение: мелкая сетка нитей и лёгкие пятна.
                    float weave = ((x & 1) ^ (y & 1)) == 0 ? 1.015f : 0.985f;
                    c *= weave * (0.95f + 0.08f * ValueNoise(x / 37f, y / 37f, seed + 5));

                    // Тонкие светлые кромки между квадратиками — мозаика читается и издали.
                    int fx = x % cell;
                    int fy = y % cell;
                    if (fx == 0 || fy == 0)
                    {
                        c *= 0.96f;
                    }

                    if (envelope)
                    {
                        // Швы клиньев по краям U (едва заметно) и шов по экватору со стежками.
                        float u = (x + 0.5f) / w;
                        float fu = u * 2f - Mathf.Floor(u * 2f);
                        float seamU = Mathf.Min(fu, 1f - fu) * (w / 2f);
                        if (seamU < 1.5f)
                        {
                            c = Color.Lerp(c, seamColor, 0.45f);
                        }
                        float seamDist = Mathf.Abs(v - seamV) * h;
                        if (seamDist < 1.2f)
                        {
                            c = Color.Lerp(c, seamColor, 0.7f);
                        }
                        else if (seamDist < 3.5f && (x % 12) < 5)
                        {
                            c = Color.Lerp(c, stitch, 0.45f);
                        }
                        // Копоть у горловины.
                        if (v < 0.06f)
                        {
                            c = Color.Lerp(c, soot, (0.06f - v) / 0.06f * 0.6f);
                        }
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        /// <summary>
        /// Полосатый лён купола драккара (эскиз «Грейд III»): красные и кремовые полосы поперёк длины,
        /// с той же «пиксельной» мозаикой квадратиков, что у льна карви, и швами со стежками на границах полос.
        /// По U — четверть окружности (повторяется 4 раза), по V — от кормового острия до носового;
        /// границы полос — в долях V (bounds), первая полоса — красная.
        /// </summary>
        public static Texture2D StripedLinen(int seed, float[] bounds)
        {
            const int w = 512;
            const int h = 2048;
            const int cell = 16;
            Texture2D tex = NewTexture("hab_striped_" + seed, w, h, false);
            var px = new Color32[w * h];
            var redLight = new Color(0.66f, 0.25f, 0.20f);
            var redDark = new Color(0.50f, 0.16f, 0.13f);
            var whiteLight = new Color(0.87f, 0.83f, 0.75f);
            var whiteDark = new Color(0.72f, 0.68f, 0.60f);
            var seamColor = new Color(0.30f, 0.20f, 0.14f);
            var stitch = new Color(0.62f, 0.52f, 0.38f);
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                int band = 0;
                float nearest = 1f;
                foreach (float b in bounds)
                {
                    if (v > b)
                    {
                        band++;
                    }
                    nearest = Mathf.Min(nearest, Mathf.Abs(v - b));
                }
                bool red = (band & 1) == 0;
                Color light = red ? redLight : whiteLight;
                Color dark = red ? redDark : whiteDark;
                float seamDist = nearest * h;
                for (int x = 0; x < w; x++)
                {
                    int cx = x / cell;
                    int cy = y / cell;
                    bool big = Hash(cx >> 1, cy >> 1, seed + 11) > 0.72f;
                    float n = big ? Hash(cx >> 1, cy >> 1, seed + 12) : Hash(cx, cy, seed);
                    Color c = Color.Lerp(dark, light, 0.35f + 0.65f * n);
                    c *= (((x & 1) ^ (y & 1)) == 0 ? 1.015f : 0.985f) * (0.95f + 0.08f * ValueNoise(x / 37f, y / 37f, seed + 5));
                    if (x % cell == 0 || y % cell == 0)
                    {
                        c *= 0.95f;
                    }
                    // Шов клиньев по краям U и швы между полосами со стежками.
                    float u = (x + 0.5f) / w;
                    float seamU = Mathf.Min(u, 1f - u) * w;
                    if (seamU < 1.5f)
                    {
                        c = Color.Lerp(c, seamColor, 0.4f);
                    }
                    if (seamDist < 2.2f)
                    {
                        c = Color.Lerp(c, seamColor, 0.85f);
                    }
                    else if (seamDist < 6f && (x % 14) < 6)
                    {
                        c = Color.Lerp(c, stitch, 0.55f);
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        /// <summary>
        /// Круглый щит (как у драккара на эскизе): доски, разделённые на четверти — красные и кремовые, тёмная кромка.
        /// UV — плоская развёртка диска: центр (0.5, 0.5), край — окружность радиуса 0.5.
        /// </summary>
        public static Texture2D Shield(int seed)
        {
            const int size = 256;
            Texture2D tex = NewTexture("hab_shield", size, size, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            var red = new Color(0.62f, 0.20f, 0.16f);
            var cream = new Color(0.85f, 0.80f, 0.70f);
            var rim = new Color(0.25f, 0.17f, 0.11f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size - 0.5f;
                    float v = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    bool quarterRed = (u > 0f) == (v > 0f);
                    Color c = quarterRed ? red : cream;
                    // Доски щита — вертикальные полосы с зазорами.
                    float board = (u + 0.5f) * 7f;
                    float fb = board - Mathf.Floor(board);
                    c *= 0.9f + 0.12f * Hash(Mathf.FloorToInt(board), 1, seed);
                    c *= 0.93f + 0.1f * ValueNoise(x / 6f, y / 40f, seed + 2);
                    if (fb < 0.04f || fb > 0.96f)
                    {
                        c *= 0.7f;
                    }
                    if (r > 0.9f)
                    {
                        c = Color.Lerp(c, rim, 0.8f);
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// Доски кадки-гондолы: горизонтальные доски по 0.17 м со смещёнными стыками («кирпичиком»), у каждой свой оттенок
        /// и волокна вдоль. Текстура покрывает 1.2 × 1.2 м (U — вдоль досок).
        /// </summary>
        public static Texture2D Planks(int seed)
        {
            const int size = 256;
            const int rows = 7;
            Texture2D tex = NewTexture("hab_planks", size, size, false);
            var px = new Color32[size * size];
            var baseColor = new Color(0.50f, 0.35f, 0.21f);
            var darkColor = new Color(0.36f, 0.24f, 0.14f);
            var gap = new Color(0.16f, 0.10f, 0.06f);
            float rowH = size / (float)rows;
            for (int y = 0; y < size; y++)
            {
                int row = Mathf.FloorToInt(y / rowH);
                float fy = y - row * rowH;
                // Длина досок в ряду: 3 доски на 1.2 м, со сдвигом стыков у соседних рядов.
                float offset = Hash(row, 3, seed) * size;
                for (int x = 0; x < size; x++)
                {
                    float xs = (x + offset) % size;
                    int board = Mathf.FloorToInt(xs / (size / 3f));
                    float fx = xs - board * (size / 3f);
                    float tint = 0.8f + 0.4f * Hash(row, board, seed + 1);
                    Color c = Color.Lerp(darkColor, baseColor, Hash(board, row, seed + 2)) * tint;
                    float grain = ValueNoise(xs / 18f, y / 1.6f + board * 7f, seed + 3);
                    c *= 0.86f + 0.24f * grain;
                    if (fy < 1.3f || fx < 1.3f)
                    {
                        c = gap;
                    }
                    else if (fy < 2.6f || fx < 2.6f || fy > rowH - 1.5f)
                    {
                        c *= 0.8f;
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Витой канат: три пряди по спирали. U — вокруг каната, V — вдоль.</summary>
        public static Texture2D Rope(int seed)
        {
            const int size = 128;
            Texture2D tex = NewTexture("hab_rope", size, size, false);
            var px = new Color32[size * size];
            var baseColor = new Color(0.60f, 0.46f, 0.30f);
            var groove = new Color(0.24f, 0.17f, 0.10f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float strand = u * 3f + v * 2f;
                    float f = strand - Mathf.Floor(strand);
                    float profile = Mathf.Sin(f * Mathf.PI);
                    Color c = Color.Lerp(groove, baseColor, Mathf.Pow(profile, 0.6f));
                    float fiber = ValueNoise(u * 24f + v * 8f, v * 64f, seed);
                    c *= 0.85f + 0.3f * fiber;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Плетёная корзина: высота плетения для цвета и карты нормалей.</summary>
        private static float WickerHeight(float u, float v, out bool isStake)
        {
            const float n = 8f;
            float cu = u * n;
            float cv = v * n * 2f;
            int col = Mathf.FloorToInt(cu);
            int row = Mathf.FloorToInt(cv);
            float fu = cu - col;
            float fv = cv - row;
            bool weftOver = ((row + col) & 1) == 0;
            float weft = Mathf.Sin(fv * Mathf.PI);
            float stakeProfile = Mathf.Clamp01(1f - Mathf.Abs(fu - 0.5f) * 3.2f);
            if (weftOver || stakeProfile <= 0f)
            {
                isStake = false;
                float arch = weftOver ? 0.75f + 0.25f * Mathf.Sin(fu * Mathf.PI) : 0.55f;
                return weft * arch;
            }
            isStake = true;
            return 0.35f + 0.5f * Mathf.Sqrt(stakeProfile);
        }

        public static Texture2D Wicker(int seed, bool dark = false)
        {
            const int size = 256;
            Texture2D tex = NewTexture(dark ? "hab_wicker_dark" : "hab_wicker", size, size, false);
            var px = new Color32[size * size];
            // Тёмный вариант — морёная лоза, как у корзины простого шара на эскизе.
            var straw = dark ? new Color(0.55f, 0.40f, 0.26f) : new Color(0.66f, 0.50f, 0.30f);
            var stake = dark ? new Color(0.44f, 0.31f, 0.19f) : new Color(0.52f, 0.37f, 0.21f);
            var gap = dark ? new Color(0.10f, 0.07f, 0.04f) : new Color(0.16f, 0.11f, 0.06f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float hgt = WickerHeight(u, v, out bool isStake);
                    Color c = Color.Lerp(gap, isStake ? stake : straw, Mathf.Clamp01(hgt * 1.4f));
                    c *= 0.9f + 0.2f * ValueNoise(u * 40f, v * 6f, seed);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        public static Texture2D WickerNormal()
        {
            const int size = 256;
            Texture2D tex = NewTexture("hab_wicker_n", size, size, true);
            var px = new Color32[size * size];
            float step = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float hl = WickerHeight(u - step, v, out _);
                    float hr = WickerHeight(u + step, v, out _);
                    float hd = WickerHeight(u, v - step, out _);
                    float hu = WickerHeight(u, v + step, out _);
                    var n = new Vector3((hl - hr) * 2.5f, (hd - hu) * 2.5f, 1f).normalized;
                    px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Кованое железо: тёмная сталь с разводами и следами ржавчины.</summary>
        public static Texture2D Iron(int seed)
        {
            const int size = 128;
            Texture2D tex = NewTexture("hab_iron", size, size, false);
            var px = new Color32[size * size];
            var steel = new Color(0.30f, 0.30f, 0.31f);
            var dark = new Color(0.17f, 0.17f, 0.18f);
            var rust = new Color(0.36f, 0.20f, 0.11f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;
                    float n = ValueNoise(u * 8f, v * 8f, seed) * 0.6f + ValueNoise(u * 32f, v * 32f, seed + 1) * 0.4f;
                    Color c = Color.Lerp(dark, steel, n);
                    float r = ValueNoise(u * 5f, v * 12f, seed + 2);
                    if (r > 0.72f)
                    {
                        c = Color.Lerp(c, rust, (r - 0.72f) * 2.5f);
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Плоская карта нормалей (подходит и для RG, и для AG-упаковки).</summary>
        public static Texture2D FlatNormal()
        {
            const int size = 8;
            Texture2D tex = NewTexture("hab_flat_n", size, size, true);
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(128, 128, 255, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>
        /// Значок якоря для корабельного HUD: белый силуэт на прозрачном фоне (кольцо, шток, поперечина, лапы с остриями),
        /// со сглаженными краями и тёмной обводкой, чтобы читался на любом фоне. Цвет задаёт Image.color.
        /// </summary>
        public static Texture2D AnchorIcon()
        {
            const int size = 64;
            Texture2D tex = NewTexture("hab_anchor_icon", size, size, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Координаты от -1 до 1, Y вверх.
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = AnchorDistance(u, v);
                    float fill = Mathf.Clamp01(0.5f - d * size * 0.5f);
                    float outline = Mathf.Clamp01(0.5f - (d - 0.07f) * size * 0.5f);
                    Color c = Color.Lerp(new Color(0.05f, 0.05f, 0.05f, outline * 0.85f), new Color(1f, 1f, 1f, 1f), fill);
                    c.a = Mathf.Max(fill, outline * 0.85f);
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Расстояние до силуэта якоря (отрицательное — внутри), в долях половины значка.</summary>
        private static float AnchorDistance(float u, float v)
        {
            float ring = Mathf.Abs(Mathf.Sqrt(u * u + (v - 0.68f) * (v - 0.68f)) - 0.16f) - 0.055f;
            float shank = SegmentDistance(u, v, 0f, 0.52f, 0f, -0.78f) - 0.07f;
            float stock = SegmentDistance(u, v, -0.36f, 0.36f, 0.36f, 0.36f) - 0.06f;
            // Лапы — дуга снизу.
            float r = Mathf.Sqrt(u * u + (v + 0.12f) * (v + 0.12f));
            float arc = Mathf.Abs(r - 0.66f) - 0.065f;
            if (v > -0.12f)
            {
                arc = Mathf.Max(arc, v + 0.12f - 0.4f * Mathf.Abs(u));
            }
            if (v > 0.05f)
            {
                arc = 1f;
            }
            // Острия на концах лап.
            float flukeL = TriangleDistance(u, v, -0.78f, -0.02f, -0.5f, -0.24f, -0.54f, 0.2f);
            float flukeR = TriangleDistance(u, v, 0.78f, -0.02f, 0.5f, -0.24f, 0.54f, 0.2f);
            return Mathf.Min(Mathf.Min(Mathf.Min(ring, shank), Mathf.Min(stock, arc)), Mathf.Min(flukeL, flukeR));
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / Mathf.Max(1e-6f, dx * dx + dy * dy));
            float qx = ax + dx * t - px;
            float qy = ay + dy * t - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        private static float TriangleDistance(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d = Mathf.Min(SegmentDistance(px, py, ax, ay, bx, by), Mathf.Min(SegmentDistance(px, py, bx, by, cx, cy), SegmentDistance(px, py, cx, cy, ax, ay)));
            float s1 = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
            float s2 = (cx - bx) * (py - by) - (cy - by) * (px - bx);
            float s3 = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
            bool inside = (s1 >= 0f && s2 >= 0f && s3 >= 0f) || (s1 <= 0f && s2 <= 0f && s3 <= 0f);
            return inside ? -d : d;
        }

        /// <summary>Запасная иконка, если отрисовка префаба не удалась.</summary>
        public static Sprite Icon(EnvelopeScheme s)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "hab_icon" };
            var px = new Color32[size * size];
            var basket = new Color(0.45f, 0.32f, 0.18f, 1f);
            var rope = new Color(0.2f, 0.15f, 0.1f, 1f);
            var center = new Vector2(64f, 78f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = new Color(0f, 0f, 0f, 0f);
                    float dx = (x - center.x) / 44f;
                    float dy = (y - center.y) / 46f;
                    float taper = dy < 0f ? 1f + dy * 0.55f : 1f;
                    if (dx * dx / (taper * taper) + dy * dy <= 1f && y > 30)
                    {
                        int stripe = Mathf.FloorToInt((dx / taper + 1f) * 3f);
                        c = (stripe & 1) == 0 ? s.A : s.B;
                        c.a = 1f;
                    }
                    else if (y >= 8 && y <= 24 && x >= 50 && x <= 78)
                    {
                        c = basket;
                    }
                    else if (y > 24 && y < 40 && (Mathf.Abs(x - (52 + (y - 24) * 0.2f)) < 1.2f || Mathf.Abs(x - (76 - (y - 24) * 0.2f)) < 1.2f))
                    {
                        c = rope;
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
