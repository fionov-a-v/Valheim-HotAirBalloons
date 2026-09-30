using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HotAirBalloons.Visuals
{
    /// <summary>Сборщик процедурных мешей: ящики (доски, стойки, железо) и тело вращения (оболочка шара).</summary>
    internal sealed class MeshBuilder
    {
        private readonly List<Vector3> m_verts = new List<Vector3>();
        private readonly List<Vector3> m_normals = new List<Vector3>();
        private readonly List<Vector2> m_uvs = new List<Vector2>();
        private readonly List<int> m_tris = new List<int>();

        public bool IsEmpty => m_verts.Count == 0;

        public int VertexCount => m_verts.Count;

        /// <summary>Повернуть и сдвинуть вершины, добавленные начиная с from (например, лёгший на бок купол).</summary>
        public void Transform(int from, Quaternion rotation, Vector3 offset)
        {
            for (int i = from; i < m_verts.Count; i++)
            {
                m_verts[i] = rotation * m_verts[i] + offset;
                m_normals[i] = rotation * m_normals[i];
            }
        }

        /// <summary>Четырёхугольник; порядок обхода подбирается так, чтобы лицевая сторона смотрела по normal.</summary>
        public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            int i = m_verts.Count;
            m_verts.Add(p0);
            m_verts.Add(p1);
            m_verts.Add(p2);
            m_verts.Add(p3);
            m_normals.Add(normal);
            m_normals.Add(normal);
            m_normals.Add(normal);
            m_normals.Add(normal);
            m_uvs.Add(uv0);
            m_uvs.Add(uv1);
            m_uvs.Add(uv2);
            m_uvs.Add(uv3);
            AddTri(i, i + 1, i + 2, normal);
            AddTri(i, i + 2, i + 3, normal);
        }

        private void AddTri(int a, int b, int c, Vector3 normal)
        {
            // В Unity лицевая грань — обход по часовой стрелке; Cross(b-a, c-a) смотрит наружу лицевой стороны.
            Vector3 n = Vector3.Cross(m_verts[b] - m_verts[a], m_verts[c] - m_verts[a]);
            if (Vector3.Dot(n, normal) >= 0f)
            {
                m_tris.Add(a);
                m_tris.Add(b);
                m_tris.Add(c);
            }
            else
            {
                m_tris.Add(a);
                m_tris.Add(c);
                m_tris.Add(b);
            }
        }

        /// <summary>Ящик с UV в метрах * uvScale, чтобы текстура не растягивалась на длинных досках.</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, float uvScale = 1f)
        {
            Vector3 h = size * 0.5f;
            Vector3 ax = rotation * Vector3.right;
            Vector3 ay = rotation * Vector3.up;
            Vector3 az = rotation * Vector3.forward;

            AddFace(center, ax, ay, az, h.x, h.y, h.z, size.z, size.y, uvScale);
            AddFace(center, -ax, ay, -az, h.x, h.y, h.z, size.z, size.y, uvScale);
            AddFace(center, ay, az, ax, h.y, h.z, h.x, size.x, size.z, uvScale);
            AddFace(center, -ay, az, -ax, h.y, h.z, h.x, size.x, size.z, uvScale);
            AddFace(center, az, ay, -ax, h.z, h.y, h.x, size.x, size.y, uvScale);
            AddFace(center, -az, ay, ax, h.z, h.y, h.x, size.x, size.y, uvScale);
        }

        public void AddBox(Vector3 center, Vector3 size, float uvScale = 1f)
        {
            AddBox(center, size, Quaternion.identity, uvScale);
        }

        /// <summary>Грань ящика: n — нормаль, v и u — оси грани, hn/hv/hu — полуразмеры.</summary>
        private void AddFace(Vector3 c, Vector3 n, Vector3 v, Vector3 u, float hn, float hv, float hu, float lenU, float lenV, float uvScale)
        {
            Vector3 fc = c + n * hn;
            Vector3 du = u * hu;
            Vector3 dv = v * hv;
            float su = lenU * uvScale;
            float sv = lenV * uvScale;
            AddQuad(fc - du - dv, fc + du - dv, fc + du + dv, fc - du + dv, n,
                new Vector2(0f, 0f), new Vector2(su, 0f), new Vector2(su, sv), new Vector2(0f, sv));
        }

        /// <summary>Балка между двумя точками (квадратное сечение).</summary>
        public void AddBeam(Vector3 from, Vector3 to, float thickness, float uvScale = 1f)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-4f)
            {
                return;
            }
            Quaternion rot = Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);
            AddBox((from + to) * 0.5f, new Vector3(thickness, thickness, len), rot, uvScale);
        }

        /// <summary>Двусторонняя оболочка по профилю — изнутри корзины купол тоже виден.</summary>
        public void AddEnvelope(EnvelopeProfile profile, int segments, int lowerRings, int upperRings, float gorePairs, float baseY)
        {
            List<Vector2> points = profile.Points(lowerRings, upperRings);
            AddLathe(points, segments, gorePairs, -1f, baseY, doubleSided: true, closedTop: true);
        }

        /// <summary>
        /// Тело вращения по профилю (r, y). Нормали — (t.y, -t.x) от касательной: профиль нужно обходить так,
        /// чтобы материал был справа (для внешней стороны — снизу вверх).
        /// uScale — сколько раз текстура повторяется по кругу; vPerMeter &lt; 0 — V от 0 до 1 по всей длине профиля.
        /// </summary>
        public void AddLathe(List<Vector2> profile, int segments, float uScale, float vPerMeter, float baseY, bool doubleSided = false,
            bool closedTop = false, Vector3 offset = default)
        {
            var vCoord = new float[profile.Count];
            float total = 0f;
            for (int k = 1; k < profile.Count; k++)
            {
                total += Vector2.Distance(profile[k], profile[k - 1]);
                vCoord[k] = total;
            }

            var normals2D = new Vector2[profile.Count];
            for (int k = 0; k < profile.Count; k++)
            {
                Vector2 prev = profile[Mathf.Max(0, k - 1)];
                Vector2 next = profile[Mathf.Min(profile.Count - 1, k + 1)];
                Vector2 tangent = (next - prev).normalized;
                normals2D[k] = new Vector2(tangent.y, -tangent.x);
            }
            if (closedTop)
            {
                normals2D[profile.Count - 1] = new Vector2(0f, 1f);
            }

            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                bool inner = side == 1;
                int start = m_verts.Count;
                for (int k = 0; k < profile.Count; k++)
                {
                    float v = vPerMeter < 0f ? (total > 0f ? vCoord[k] / total : 0f) : vCoord[k] * vPerMeter;
                    for (int j = 0; j <= segments; j++)
                    {
                        float phi = (float)j / segments * Mathf.PI * 2f;
                        float sn = Mathf.Sin(phi);
                        float cs = Mathf.Cos(phi);
                        Vector2 p = profile[k];
                        Vector2 n2 = normals2D[k];
                        Vector3 normal = new Vector3(n2.x * sn, n2.y, n2.x * cs).normalized;
                        m_verts.Add(offset + new Vector3(p.x * sn, baseY + p.y, p.x * cs));
                        m_normals.Add(inner ? -normal : normal);
                        m_uvs.Add(new Vector2((float)j / segments * uScale, v));
                    }
                }
                int row = segments + 1;
                for (int k = 0; k < profile.Count - 1; k++)
                {
                    for (int j = 0; j < segments; j++)
                    {
                        int a = start + k * row + j;
                        int b = a + 1;
                        int c = a + row + 1;
                        int d = a + row;
                        Vector3 outward = (m_normals[a] + m_normals[c]).normalized;
                        if (outward.sqrMagnitude < 0.01f)
                        {
                            outward = m_normals[a].sqrMagnitude > 0.01f ? m_normals[a] : (inner ? Vector3.down : Vector3.up);
                        }
                        AddTri(a, b, c, outward);
                        AddTri(a, c, d, outward);
                    }
                }
            }
        }

        /// <summary>Труба (канат) вдоль ломаной; кольца сечения ориентированы переносом рамки вдоль пути.</summary>
        public void AddTube(IList<Vector3> path, float radius, int sides, float vPerMeter)
        {
            if (path.Count < 2)
            {
                return;
            }
            Vector3 t0 = (path[1] - path[0]).normalized;
            Vector3 normal = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            int start = m_verts.Count;
            float length = 0f;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tangent;
                if (i == 0)
                {
                    tangent = (path[1] - path[0]).normalized;
                }
                else if (i == path.Count - 1)
                {
                    tangent = (path[i] - path[i - 1]).normalized;
                }
                else
                {
                    tangent = ((path[i + 1] - path[i]).normalized + (path[i] - path[i - 1]).normalized).normalized;
                }
                if (i > 0)
                {
                    length += Vector3.Distance(path[i], path[i - 1]);
                }
                // Перенос рамки: убираем из нормали составляющую вдоль новой касательной.
                normal = (normal - tangent * Vector3.Dot(normal, tangent)).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                for (int j = 0; j <= sides; j++)
                {
                    float a = (float)j / sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    m_verts.Add(path[i] + dir * radius);
                    m_normals.Add(dir);
                    m_uvs.Add(new Vector2((float)j / sides, length * vPerMeter));
                }
            }
            int row = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * row + j;
                    int b = a + 1;
                    int c = a + row + 1;
                    int d = a + row;
                    Vector3 outward = (m_normals[a] + m_normals[c]).normalized;
                    AddTri(a, b, c, outward);
                    AddTri(a, c, d, outward);
                }
            }
        }

        /// <summary>
        /// Сетка точек [i, j] (полотно паруса): нормали — по соседним точкам, лицевая сторона смотрит по front
        /// (для двусторонней — обе стороны).
        /// </summary>
        public void AddGrid(Vector3[,] points, Vector2[,] uvs, Vector3 front, bool doubleSided)
        {
            AddGrid(points, uvs, _ => front, doubleSided);
        }

        /// <summary>Сетка точек, у которой «наружу» своё для каждой точки (корпус корабля: от оси корпуса к обшивке).</summary>
        public void AddGrid(Vector3[,] points, Vector2[,] uvs, System.Func<Vector3, Vector3> outwardAt, bool doubleSided)
        {
            int ni = points.GetLength(0);
            int nj = points.GetLength(1);
            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                int start = m_verts.Count;
                for (int i = 0; i < ni; i++)
                {
                    for (int j = 0; j < nj; j++)
                    {
                        Vector3 facing = side == 0 ? outwardAt(points[i, j]) : -outwardAt(points[i, j]);
                        Vector3 di = points[Mathf.Min(ni - 1, i + 1), j] - points[Mathf.Max(0, i - 1), j];
                        Vector3 dj = points[i, Mathf.Min(nj - 1, j + 1)] - points[i, Mathf.Max(0, j - 1)];
                        Vector3 n = Vector3.Cross(di, dj).normalized;
                        if (n.sqrMagnitude < 0.5f)
                        {
                            n = facing.normalized;
                        }
                        if (Vector3.Dot(n, facing) < 0f)
                        {
                            n = -n;
                        }
                        m_verts.Add(points[i, j]);
                        m_normals.Add(n);
                        m_uvs.Add(uvs[i, j]);
                    }
                }
                for (int i = 0; i < ni - 1; i++)
                {
                    for (int j = 0; j < nj - 1; j++)
                    {
                        int a = start + i * nj + j;
                        int b = a + 1;
                        int c = a + nj + 1;
                        int d = a + nj;
                        Vector3 outward = (m_normals[a] + m_normals[c]).normalized;
                        AddTri(a, b, c, outward);
                        AddTri(a, c, d, outward);
                    }
                }
            }
        }

        /// <summary>Сфера (эллипсоид при scale != 1) — узлы сетки.</summary>
        public void AddSphere(Vector3 center, Vector3 scale, int segments, int rings)
        {
            int start = m_verts.Count;
            for (int r = 0; r <= rings; r++)
            {
                float theta = (float)r / rings * Mathf.PI;
                for (int j = 0; j <= segments; j++)
                {
                    float phi = (float)j / segments * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Cos(phi));
                    m_verts.Add(center + Vector3.Scale(n, scale));
                    m_normals.Add(new Vector3(n.x / Mathf.Max(scale.x, 1e-4f), n.y / Mathf.Max(scale.y, 1e-4f), n.z / Mathf.Max(scale.z, 1e-4f)).normalized);
                    m_uvs.Add(new Vector2((float)j / segments, (float)r / rings));
                }
            }
            int row = segments + 1;
            for (int r = 0; r < rings; r++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = start + r * row + j;
                    int b = a + 1;
                    int c = a + row + 1;
                    int d = a + row;
                    Vector3 outward = (m_verts[a] + m_verts[b] + m_verts[c] + m_verts[d]) * 0.25f - center;
                    AddTri(a, b, c, outward);
                    AddTri(a, c, d, outward);
                }
            }
        }

        /// <summary>
        /// Тор: кольцо по эллипсу с полуосями radiusX/radiusZ в плоскости XZ (с учётом rotation), сечение — круг tube
        /// (или эллипс tube × tubeHeight для приплюснутых ободов).
        /// </summary>
        public void AddTorus(Vector3 center, Quaternion rotation, float radiusX, float radiusZ, float tube, float tubeHeight,
            int segments, int sides, float uScale = 1f)
        {
            int start = m_verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                var ring = new Vector3(Mathf.Sin(a) * radiusX, 0f, Mathf.Cos(a) * radiusZ);
                Vector3 outDir = new Vector3(Mathf.Sin(a) / Mathf.Max(radiusX, 1e-4f), 0f, Mathf.Cos(a) / Mathf.Max(radiusZ, 1e-4f)).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float b = (float)j / sides * Mathf.PI * 2f;
                    Vector3 local = outDir * (Mathf.Cos(b) * tube) + Vector3.up * (Mathf.Sin(b) * tubeHeight);
                    Vector3 n = (outDir * (Mathf.Cos(b) / Mathf.Max(tube, 1e-4f)) + Vector3.up * (Mathf.Sin(b) / Mathf.Max(tubeHeight, 1e-4f))).normalized;
                    m_verts.Add(center + rotation * (ring + local));
                    m_normals.Add(rotation * n);
                    m_uvs.Add(new Vector2((float)i / segments * uScale, (float)j / sides));
                }
            }
            int row = sides + 1;
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < sides; j++)
                {
                    int a = start + i * row + j;
                    int b = a + 1;
                    int c = a + row + 1;
                    int d = a + row;
                    Vector3 outward = (m_normals[a] + m_normals[c]).normalized;
                    AddTri(a, b, c, outward);
                    AddTri(a, c, d, outward);
                }
            }
        }

        /// <summary>Цилиндр (сплошной, с крышками) — днище корзины.</summary>
        public void AddCylinder(Vector3 bottomCenter, float radius, float height, int segments, float uvPerMeter)
        {
            var profile = new List<Vector2>
            {
                new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius, 0f), new Vector2(radius, height),
                new Vector2(radius, height), new Vector2(0f, height),
            };
            AddLatheCapped(profile, segments, bottomCenter, uvPerMeter);
        }

        /// <summary>Цилиндрическая стенка с толщиной (снаружи, внутри и торцы) — плетёная корзина.</summary>
        public void AddCylinderShell(Vector3 bottomCenter, float outerRadius, float innerRadius, float height, int segments, float uvPerMeter)
        {
            var profile = new List<Vector2>
            {
                new Vector2(innerRadius, 0f), new Vector2(outerRadius, 0f), new Vector2(outerRadius, 0f), new Vector2(outerRadius, height),
                new Vector2(outerRadius, height), new Vector2(innerRadius, height), new Vector2(innerRadius, height), new Vector2(innerRadius, 0f),
            };
            AddLatheCapped(profile, segments, bottomCenter, uvPerMeter);
        }

        /// <summary>
        /// Тело вращения из отрезков (пары точек), у каждого — своя плоская нормаль: даёт острые рёбра у цилиндров.
        /// Профиль обходится против часовой стрелки вокруг сечения (материал справа).
        /// </summary>
        private void AddLatheCapped(List<Vector2> segmentsProfile, int segments, Vector3 origin, float uvPerMeter)
        {
            for (int k = 0; k + 1 < segmentsProfile.Count; k += 2)
            {
                Vector2 p0 = segmentsProfile[k];
                Vector2 p1 = segmentsProfile[k + 1];
                Vector2 tangent = (p1 - p0).normalized;
                var n2 = new Vector2(tangent.y, -tangent.x);
                int start = m_verts.Count;
                bool horizontal = Mathf.Abs(p1.y - p0.y) < 1e-5f;
                for (int e = 0; e < 2; e++)
                {
                    Vector2 p = e == 0 ? p0 : p1;
                    for (int j = 0; j <= segments; j++)
                    {
                        float phi = (float)j / segments * Mathf.PI * 2f;
                        float sn = Mathf.Sin(phi);
                        float cs = Mathf.Cos(phi);
                        var pos = new Vector3(p.x * sn, p.y, p.x * cs);
                        m_verts.Add(origin + pos);
                        m_normals.Add(new Vector3(n2.x * sn, n2.y, n2.x * cs).normalized);
                        // Торцы — планарная развёртка, боковины — по окружности и высоте.
                        m_uvs.Add(horizontal ? new Vector2(pos.x, pos.z) * uvPerMeter
                            : new Vector2((float)j / segments * Mathf.PI * 2f * p.x * uvPerMeter, p.y * uvPerMeter));
                    }
                }
                int row = segments + 1;
                for (int j = 0; j < segments; j++)
                {
                    int a = start + j;
                    int b = a + 1;
                    int c = a + row + 1;
                    int d = a + row;
                    Vector3 outward = (m_normals[a] + m_normals[c]).normalized;
                    AddTri(a, b, c, outward);
                    AddTri(a, c, d, outward);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (m_verts.Count > 65000)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(m_verts);
            mesh.SetNormals(m_normals);
            mesh.SetUVs(0, m_uvs);
            mesh.SetTriangles(m_tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
