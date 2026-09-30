using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using HotAirBalloons.Visuals;
using UnityEngine;

namespace HotAirBalloons.Preview
{
    /// <summary>
    /// Выгружает процедурную модель шара в OBJ (+ текстуры PNG), чтобы посмотреть её без игры:
    /// dotnet run --project tests/ModelPreview -- out_dir [simple|medium|large] [sails: 0|1|2] [rudder -1..1] [people]
    /// </summary>
    internal static class Program
    {
        private sealed class Part
        {
            public string Name;
            public MeshBuilder Mesh;
            public string Texture;
            public Func<Vector3, Vector3> Position = p => p;
            public Func<Vector3, Vector3, Vector3> Normal = (p, n) => n;
        }

        private static int Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string outDir = args.Length > 0 ? args[0] : "preview_out";
            string kind = args.Length > 1 ? args[1] : "simple";
            int sails = args.Length > 2 ? int.Parse(args[2]) : 2;
            float rudder = args.Length > 3 ? float.Parse(args[3]) : 0f;
            bool people = args.Length > 4 && args[4] == "people";
            Directory.CreateDirectory(outDir);

            var parts = new List<Part>();
            var markers = new StringBuilder();
            WritePng(Path.Combine(outDir, "iron.png"), TextureFactory.Iron(11));
            WritePng(Path.Combine(outDir, "rope.png"), TextureFactory.Rope(5));
            WritePng(Path.Combine(outDir, "wood.png"), PlainWood());
            WritePng(Path.Combine(outDir, "person.png"), Solid(new Color(0.35f, 0.45f, 0.55f)));
            WritePng(Path.Combine(outDir, "anchor_icon.png"), TextureFactory.AnchorIcon());

            if (kind == "large")
            {
                DrakkarModel d = DrakkarModel.Build();
                WritePng(Path.Combine(outDir, "striped.png"), TextureFactory.StripedLinen(3, d.StripeV));
                WritePng(Path.Combine(outDir, "planks.png"), TextureFactory.Planks(9));
                WritePng(Path.Combine(outDir, "shield.png"), TextureFactory.Shield(4));
                parts.Add(new Part { Name = "envelope", Mesh = d.Envelope, Texture = "striped.png" });
                parts.Add(new Part { Name = "hull", Mesh = d.Hull, Texture = "planks.png" });
                parts.Add(new Part { Name = "deck", Mesh = d.Deck, Texture = "planks.png" });
                parts.Add(new Part { Name = "wood", Mesh = d.Wood, Texture = "wood.png" });
                parts.Add(new Part { Name = "iron", Mesh = d.Iron, Texture = "iron.png" });
                parts.Add(new Part { Name = "rope", Mesh = d.Rope, Texture = "rope.png" });
                parts.Add(new Part { Name = "shields", Mesh = d.Shields, Texture = "shield.png" });
                // Руль: баллер с пером и руль-палкой поворачивается вокруг вертикали (BalloonController, m_rudderPivots).
                Quaternion ry = Quaternion.Euler(0f, -rudder * 18f, 0f);
                foreach (var (name, mb, tex) in new[] { ("rudder_wood", d.RudderWood, "wood.png"), ("rudder_iron", d.RudderIron, "iron.png") })
                {
                    parts.Add(new Part { Name = name, Mesh = mb, Texture = tex, Position = p => d.RudderPivot + ry * p, Normal = (p, n) => ry * n });
                }
                Quaternion spin = Quaternion.Euler(0f, 0f, 20f);
                foreach (var (name, mb, tex) in new[] { ("prop_wood", d.PropellerWood, "wood.png"), ("prop_iron", d.PropellerIron, "iron.png") })
                {
                    parts.Add(new Part { Name = name, Mesh = mb, Texture = tex, Position = p => d.PropellerPivot + spin * p, Normal = (p, n) => spin * n });
                }
                int ci = 0;
                foreach (CrankSpec c in d.Cranks)
                {
                    Quaternion r = c.CrankRotation * Quaternion.Euler(0f, 0f, ci * 60f);
                    Vector3 pivot = c.CrankPivot;
                    parts.Add(new Part { Name = "crank_iron" + ci, Mesh = d.CrankIron, Texture = "iron.png", Position = p => pivot + r * p, Normal = (p, n) => r * n });
                    parts.Add(new Part { Name = "crank_wood" + ci, Mesh = d.CrankWood, Texture = "wood.png", Position = p => pivot + r * p, Normal = (p, n) => r * n });
                    ci++;
                }
                if (people)
                {
                    var figures = new MeshBuilder();
                    StandingFigure(figures, d.FireOperatorPosition, d.FireOperatorYaw);
                    SittingFigure(figures, d.HelmPosition, d.HelmYaw);
                    foreach (CrankSpec c in d.Cranks)
                    {
                        SittingFigure(figures, c.SeatPosition, c.SeatYaw);
                    }
                    parts.Add(new Part { Name = "people", Mesh = figures, Texture = "person.png" });
                }
            }
            else if (kind == "medium")
            {
                MediumBalloonModel m = MediumBalloonModel.Build();
                WritePng(Path.Combine(outDir, "linen.png"), TextureFactory.Linen(2, m.EquatorV));
                WritePng(Path.Combine(outDir, "sailcloth.png"), TextureFactory.Linen(3, -1f));
                WritePng(Path.Combine(outDir, "planks.png"), TextureFactory.Planks(9));
                parts.Add(new Part { Name = "envelope", Mesh = m.Envelope, Texture = "linen.png" });
                parts.Add(new Part { Name = "planks", Mesh = m.Planks, Texture = "planks.png" });
                parts.Add(new Part { Name = "wood", Mesh = m.Wood, Texture = "wood.png" });
                parts.Add(new Part { Name = "iron", Mesh = m.Iron, Texture = "iron.png" });
                parts.Add(new Part { Name = "rope", Mesh = m.Rope, Texture = "rope.png" });

                // Руль-палка наклоняется вбок, как в игре (BalloonController.LateUpdate).
                Quaternion tilt = Quaternion.Euler(0f, 0f, -rudder * 14f);
                foreach (var (name, mb, tex) in new[] { ("lever_wood", m.LeverWood, "wood.png"), ("lever_iron", m.LeverIron, "iron.png"), ("lever_rope", m.LeverRope, "rope.png") })
                {
                    parts.Add(new Part { Name = name, Mesh = mb, Texture = tex, Position = p => m.LeverPivot + tilt * p, Normal = (p, n) => tilt * n });
                }

                // Паруса: складывание к средней рее (BalloonSail) и поворот рулём вокруг оси крепления.
                float k = SailFold.Factor(sails);
                int si = 0;
                foreach (SailParts sp in m.Sails)
                {
                    Quaternion yaw = Quaternion.Euler(0f, sp.BaseYaw + rudder * 30f, 0f);
                    Quaternion up = Quaternion.Euler(0f, 0f, SailFold.DeltaDeg(sp.UpperAngle, sp.MiddleAngle, k));
                    Quaternion low = Quaternion.Euler(0f, 0f, SailFold.DeltaDeg(sp.LowerAngle, sp.MiddleAngle, k));
                    Vector3 pivot = sp.Pivot;
                    float mid = sp.MiddleAngle;
                    string s = "sail" + si++ + "_";
                    parts.Add(new Part
                    {
                        Name = s + "cloth", Mesh = sp.Cloth, Texture = "sailcloth.png",
                        Position = p => pivot + yaw * SailFold.Point(p, mid, k), Normal = (p, n) => yaw * SailFold.Normal(p, n, mid, k),
                    });
                    parts.Add(new Part
                    {
                        Name = s + "batten", Mesh = sp.Batten, Texture = "wood.png",
                        Position = p => pivot + yaw * SailFold.Point(p, mid, k), Normal = (p, n) => yaw * SailFold.Normal(p, n, mid, k),
                    });
                    foreach (var (name, mb, tex) in new[] { ("hub", sp.Hub, "iron.png"), ("midwood", sp.MiddleWood, "wood.png"), ("midrope", sp.MiddleRope, "rope.png") })
                    {
                        parts.Add(new Part { Name = s + name, Mesh = mb, Texture = tex, Position = p => pivot + yaw * p, Normal = (p, n) => yaw * n });
                    }
                    foreach (var (name, mb, tex, rot) in new[]
                             {
                                 ("upwood", sp.UpperWood, "wood.png", up), ("uprope", sp.UpperRope, "rope.png", up),
                                 ("lowwood", sp.LowerWood, "wood.png", low), ("lowrope", sp.LowerRope, "rope.png", low),
                             })
                    {
                        Quaternion r = rot;
                        parts.Add(new Part { Name = s + name, Mesh = mb, Texture = tex, Position = p => pivot + yaw * (r * p), Normal = (p, n) => yaw * (r * n) });
                    }
                }

                if (people)
                {
                    var figures = new MeshBuilder();
                    StandingFigure(figures, m.FireOperatorPosition, m.FireOperatorYaw);
                    StandingFigure(figures, m.HelmPosition, m.HelmYaw);
                    foreach (SeatSpec seat in m.Seats)
                    {
                        SittingFigure(figures, seat.Position, seat.Yaw);
                    }
                    parts.Add(new Part { Name = "people", Mesh = figures, Texture = "person.png" });
                }
                markers.Append($"fire {m.FirePosition.x} {m.FirePosition.y} {m.FirePosition.z}\n");
                markers.Append($"fireop {m.FireOperatorPosition.x} {m.FireOperatorPosition.y} {m.FireOperatorPosition.z}\n");
                markers.Append($"helm {m.HelmPosition.x} {m.HelmPosition.y} {m.HelmPosition.z}\n");
                markers.Append($"equatorV {m.EquatorV}\n");
            }
            else
            {
                SimpleBalloonModel model = SimpleBalloonModel.Build();
                WritePng(Path.Combine(outDir, "trollcloth.png"), TextureFactory.TrollCloth(1));
                WritePng(Path.Combine(outDir, "wicker.png"), TextureFactory.Wicker(7, dark: true));
                parts.Add(new Part { Name = "envelope", Mesh = model.Envelope, Texture = "trollcloth.png" });
                parts.Add(new Part { Name = "wicker", Mesh = model.Wicker, Texture = "wicker.png" });
                parts.Add(new Part { Name = "wood", Mesh = model.Wood, Texture = "wood.png" });
                parts.Add(new Part { Name = "iron", Mesh = model.Iron, Texture = "iron.png" });
                parts.Add(new Part { Name = "rope", Mesh = model.Rope, Texture = "rope.png" });
                markers.Append($"fire {model.FirePosition.x} {model.FirePosition.y} {model.FirePosition.z}\n");
                markers.Append($"operator {model.OperatorPosition.x} {model.OperatorPosition.y} {model.OperatorPosition.z}\n");
            }

            var obj = new StringBuilder();
            var mtl = new StringBuilder();
            obj.AppendLine("mtllib model.mtl");
            int vBase = 1;
            int triangles = 0;
            foreach (Part part in parts)
            {
                if (part.Mesh.IsEmpty)
                {
                    continue;
                }
                Mesh mesh = part.Mesh.ToMesh(part.Name);
                mtl.AppendLine($"newmtl {part.Name}\nmap_Kd {part.Texture}\n");
                obj.AppendLine($"o {part.Name}\nusemtl {part.Name}");
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    Vector3 v = part.Position(mesh.Vertices[i]);
                    obj.AppendLine($"v {v.x:0.#####} {v.y:0.#####} {v.z:0.#####}");
                }
                foreach (Vector2 uv in mesh.Uvs)
                {
                    obj.AppendLine($"vt {uv.x:0.#####} {uv.y:0.#####}");
                }
                for (int i = 0; i < mesh.Normals.Count; i++)
                {
                    Vector3 n = part.Normal(mesh.Vertices[i], mesh.Normals[i]);
                    obj.AppendLine($"vn {n.x:0.####} {n.y:0.####} {n.z:0.####}");
                }
                for (int i = 0; i < mesh.Triangles.Count; i += 3)
                {
                    int a = mesh.Triangles[i] + vBase, b = mesh.Triangles[i + 1] + vBase, c = mesh.Triangles[i + 2] + vBase;
                    obj.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
                }
                vBase += mesh.Vertices.Count;
                triangles += mesh.Triangles.Count / 3;
                Console.WriteLine($"{part.Name}: {mesh.Vertices.Count} verts, {mesh.Triangles.Count / 3} tris");
            }
            File.WriteAllText(Path.Combine(outDir, "model.obj"), obj.ToString());
            File.WriteAllText(Path.Combine(outDir, "model.mtl"), mtl.ToString());
            File.WriteAllText(Path.Combine(outDir, "markers.txt"), markers.ToString());
            Console.WriteLine($"total tris: {triangles}");
            return 0;
        }

        /// <summary>Манекен 1.8 м (стоит): видно, помещаются ли люди и где их руки относительно мачты/палки.</summary>
        private static void StandingFigure(MeshBuilder mb, Vector3 feet, float yaw)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            mb.AddBox(feet + r * new Vector3(-0.11f, 0.45f, 0f), new Vector3(0.14f, 0.9f, 0.16f), r, 1f);
            mb.AddBox(feet + r * new Vector3(0.11f, 0.45f, 0f), new Vector3(0.14f, 0.9f, 0.16f), r, 1f);
            mb.AddBox(feet + r * new Vector3(0f, 1.2f, 0f), new Vector3(0.44f, 0.62f, 0.24f), r, 1f);
            mb.AddSphere(feet + r * new Vector3(0f, 1.66f, 0f), new Vector3(0.12f, 0.14f, 0.12f), 10, 6);
            // Левая рука тянется к мачте: 0.73 м влево, 0.47 м вперёд, на высоту плеча.
            Vector3 shoulder = feet + r * new Vector3(-0.24f, 1.45f, 0f);
            Vector3 hand = feet + r * (MediumBalloonModel.MastGripOffset + new Vector3(0f, 1.35f, 0f));
            mb.AddBeam(shoulder, hand, 0.09f, 1f);
            mb.AddBox(feet + r * new Vector3(0.26f, 1.15f, 0f), new Vector3(0.09f, 0.6f, 0.09f), r, 1f);
        }

        private static void SittingFigure(MeshBuilder mb, Vector3 feet, float yaw)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            mb.AddBox(feet + r * new Vector3(0f, 0.52f, -0.05f), new Vector3(0.36f, 0.16f, 0.44f), r, 1f);
            mb.AddBox(feet + r * new Vector3(0f, 0.26f, 0.2f), new Vector3(0.3f, 0.5f, 0.14f), r, 1f);
            mb.AddBox(feet + r * new Vector3(0f, 0.9f, -0.15f), new Vector3(0.44f, 0.62f, 0.24f), r, 1f);
            mb.AddSphere(feet + r * new Vector3(0f, 1.36f, -0.15f), new Vector3(0.12f, 0.14f, 0.12f), 10, 6);
        }

        /// <summary>Заглушка под ванильную текстуру досок (в игре — Planks5c_low).</summary>
        private static Texture2D PlainWood()
        {
            var t = new Texture2D(64, 64);
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float grain = 0.85f + 0.15f * Mathf.Sin(y * 0.9f + Mathf.Sin(x * 0.2f) * 2f);
                    px[y * 64 + x] = new Color(0.55f * grain, 0.41f * grain, 0.26f * grain, 1f);
                }
            }
            t.SetPixels32(px);
            return t;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(4, 4);
            var px = new Color32[16];
            for (int i = 0; i < 16; i++)
            {
                px[i] = c;
            }
            t.SetPixels32(px);
            return t;
        }

        private static void WritePng(string path, Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            var raw = new byte[h * (w * 4 + 1)];
            int o = 0;
            for (int y = h - 1; y >= 0; y--)
            {
                raw[o++] = 0;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = tex.Pixels[y * w + x];
                    raw[o++] = c.r;
                    raw[o++] = c.g;
                    raw[o++] = c.b;
                    raw[o++] = c.a;
                }
            }
            using var fs = File.Create(path);
            fs.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w);
            WriteBE(ihdr, 4, h);
            ihdr[8] = 8;
            ihdr[9] = 6;
            Chunk(fs, "IHDR", ihdr);
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                {
                    z.Write(raw, 0, raw.Length);
                }
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        private static void WriteBE(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            WriteBE(len, 0, data.Length);
            s.Write(len);
            byte[] t = Encoding.ASCII.GetBytes(type);
            s.Write(t);
            s.Write(data);
            uint crc = Crc(t, data);
            var c = new byte[4];
            WriteBE(c, 0, (int)crc);
            s.Write(c);
        }

        private static uint Crc(byte[] a, byte[] b)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte[] arr in new[] { a, b })
            {
                foreach (byte x in arr)
                {
                    crc ^= x;
                    for (int k = 0; k < 8; k++)
                    {
                        crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
                    }
                }
            }
            return crc ^ 0xFFFFFFFF;
        }
    }
}
