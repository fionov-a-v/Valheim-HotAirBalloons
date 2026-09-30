using HotAirBalloons.Sim;
using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>Лебёдка якоря на борту корзины: бросить/поднять якорь может любой, кто рядом.</summary>
    public class BalloonAnchor : MonoBehaviour, Hoverable, Interactable
    {
        public Transform m_ropeStart;
        public Transform m_model;
        public Transform m_modelRing;
        public Vector3 m_stowedLocalPos;
        public Quaternion m_stowedLocalRot = Quaternion.identity;
        public LineRenderer m_rope;
        public float m_useDistance = 3f;

        private BalloonController m_ctrl;
        private ZNetView m_nview;
        private float m_visualDrop;

        private void Awake()
        {
            m_ctrl = GetComponentInParent<BalloonController>();
            m_nview = GetComponentInParent<ZNetView>();
        }

        private bool Valid => m_ctrl != null && m_nview != null && m_nview.IsValid();

        private void LateUpdate()
        {
            if (!Valid || m_model == null || m_ropeStart == null)
            {
                return;
            }
            Transform root = m_ctrl.transform;
            Quaternion yaw = Quaternion.Euler(0f, root.eulerAngles.y, 0f);
            AnchorState state = m_ctrl.GetAnchorState();
            bool anchorOut = m_ctrl.IsAnchorOut();

            if (state == AnchorState.Holding)
            {
                // Лежит на земле в точке крепления.
                Vector3 point = m_ctrl.GetAnchorPoint();
                m_model.position = point + Vector3.up * 0.15f;
                m_model.rotation = yaw * Quaternion.Euler(0f, 0f, 80f);
                m_visualDrop = Vector3.Distance(m_ropeStart.position, point);
            }
            else
            {
                // Висит под корзиной: канат вытравлен на ropeOut ниже дна корзины.
                float extra = m_ropeStart.position.y - root.position.y;
                float target = anchorOut || state == AnchorState.Hanging ? m_ctrl.GetRopeOut() + extra : 0f;
                m_visualDrop = Mathf.MoveTowards(m_visualDrop, target, Time.deltaTime * 8f);
                if (m_visualDrop < 0.05f && !anchorOut)
                {
                    m_model.localPosition = m_stowedLocalPos;
                    m_model.localRotation = m_stowedLocalRot;
                }
                else
                {
                    m_model.rotation = yaw;
                    Vector3 ringOffset = m_modelRing != null ? m_modelRing.position - m_model.position : Vector3.zero;
                    m_model.position = m_ropeStart.position + Vector3.down * m_visualDrop - ringOffset;
                }
            }

            if (m_rope != null)
            {
                bool show = m_visualDrop > 0.05f || state == AnchorState.Holding;
                m_rope.enabled = show;
                if (show)
                {
                    m_rope.SetPosition(0, m_ropeStart.position);
                    m_rope.SetPosition(1, m_modelRing != null ? m_modelRing.position : m_model.position);
                }
            }
        }

        public string GetHoverText()
        {
            if (!Valid)
            {
                return "";
            }
            if (Player.m_localPlayer == null || Vector3.Distance(Player.m_localPlayer.transform.position, transform.position) > m_useDistance)
            {
                return Localization.instance.Localize("<color=#888888>$piece_toofar</color>");
            }
            string action = m_ctrl.IsAnchorOut() ? "$hab_anchor_raise" : "$hab_anchor_drop";
            string text = $"$hab_anchor ( {BalloonHud.AnchorText(m_ctrl)} )\n[<color=yellow><b>$KEY_Use</b></color>] {action}";
            return Localization.instance.Localize(text);
        }

        public string GetHoverName()
        {
            return Localization.instance.Localize("$hab_anchor");
        }

        public float GetHoverOffset()
        {
            return 0f;
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid || user == null)
            {
                return false;
            }
            if (Vector3.Distance(user.transform.position, transform.position) > m_useDistance)
            {
                return false;
            }
            m_nview.InvokeRPC("HAB_ToggleAnchor");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }
}
