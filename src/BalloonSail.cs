using HotAirBalloons.Visuals;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Боковой парус-веер среднего шара: плавно складывается и раскрывается по положению парусов из ZDO
    /// (сложены → наполовину → полностью), как парус корабля. Полотно и поперечная планка сходятся к средней рее
    /// (вершины мешей пересчитываются только во время движения), верхняя и нижняя реи поворачиваются к ней целиком.
    /// Поворот паруса рулём делает BalloonController (этот объект — одна из его m_rudderPivots).
    /// </summary>
    public class BalloonSail : MonoBehaviour
    {
        public MeshFilter[] m_deform = new MeshFilter[0];
        public Transform m_upper;
        public Transform m_lower;
        public float m_upperAngle = 29f;
        public float m_middleAngle = -4f;
        public float m_lowerAngle = -29f;
        public float m_foldSpeed = 0.7f;

        private BalloonController m_ctrl;
        private Mesh[] m_meshes = new Mesh[0];
        private Vector3[][] m_baseVerts = new Vector3[0][];
        private Vector3[][] m_baseNormals = new Vector3[0][];
        private Vector3[][] m_verts = new Vector3[0][];
        private Vector3[][] m_normals = new Vector3[0][];
        private float m_fold = -1f;

        private void Awake()
        {
            m_ctrl = GetComponentInParent<BalloonController>();
            int n = m_deform.Length;
            m_meshes = new Mesh[n];
            m_baseVerts = new Vector3[n][];
            m_baseNormals = new Vector3[n][];
            m_verts = new Vector3[n][];
            m_normals = new Vector3[n][];
            for (int i = 0; i < n; i++)
            {
                if (m_deform[i] == null || m_deform[i].sharedMesh == null)
                {
                    continue;
                }
                // Свой экземпляр меша: общий меш префаба складывался бы сразу у всех шаров.
                m_meshes[i] = Instantiate(m_deform[i].sharedMesh);
                m_meshes[i].MarkDynamic();
                m_deform[i].sharedMesh = m_meshes[i];
                m_baseVerts[i] = m_meshes[i].vertices;
                m_baseNormals[i] = m_meshes[i].normals;
                m_verts[i] = new Vector3[m_baseVerts[i].Length];
                m_normals[i] = new Vector3[m_baseNormals[i].Length];
            }
        }

        private void OnDestroy()
        {
            foreach (Mesh mesh in m_meshes)
            {
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }
        }

        private void Update()
        {
            if (m_ctrl == null)
            {
                return;
            }
            float target = SailFold.Factor((int)m_ctrl.GetSails());
            float fold = m_fold < 0f ? target : Mathf.MoveTowards(m_fold, target, m_foldSpeed * Time.deltaTime);
            if (Mathf.Approximately(fold, m_fold))
            {
                return;
            }
            m_fold = fold;
            Apply(fold);
        }

        private void Apply(float k)
        {
            if (m_upper != null)
            {
                m_upper.localRotation = Quaternion.Euler(0f, 0f, SailFold.DeltaDeg(m_upperAngle, m_middleAngle, k));
            }
            if (m_lower != null)
            {
                m_lower.localRotation = Quaternion.Euler(0f, 0f, SailFold.DeltaDeg(m_lowerAngle, m_middleAngle, k));
            }
            for (int i = 0; i < m_meshes.Length; i++)
            {
                if (m_meshes[i] == null)
                {
                    continue;
                }
                Vector3[] bv = m_baseVerts[i];
                Vector3[] bn = m_baseNormals[i];
                Vector3[] v = m_verts[i];
                Vector3[] n = m_normals[i];
                for (int j = 0; j < bv.Length; j++)
                {
                    v[j] = SailFold.Point(bv[j], m_middleAngle, k);
                    n[j] = SailFold.Normal(bv[j], bn[j], m_middleAngle, k);
                }
                m_meshes[i].vertices = v;
                m_meshes[i].normals = n;
                m_meshes[i].RecalculateBounds();
            }
        }
    }
}
