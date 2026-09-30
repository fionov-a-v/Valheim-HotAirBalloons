using UnityEngine;

namespace HotAirBalloons
{
    /// <summary>
    /// Сиденье с рукоятью драккара (рядом с ванильным Chair на том же объекте): кто сидит — крутит винт.
    /// Кто где сидит, каждый клиент пишет в ZDO своего персонажа (BalloonHud), владелец шара собирает это
    /// в маску сидений (BalloonController); здесь — только анимация: рукоять крутится, пока сиденье занято.
    /// </summary>
    public class BalloonCrankSeat : MonoBehaviour
    {
        public int m_index;

        /// <summary>Кривошип: крутится вокруг своей оси Z (ось вала).</summary>
        public Transform m_crank;
        public float m_turnsPerSecond = 0.9f;

        private BalloonController m_ctrl;
        private float m_speed;

        private void Awake()
        {
            m_ctrl = GetComponentInParent<BalloonController>();
        }

        private void Update()
        {
            if (m_ctrl == null || m_crank == null)
            {
                return;
            }
            bool busy = (m_ctrl.GetCrankMask() & (1 << m_index)) != 0;
            m_speed = Mathf.MoveTowards(m_speed, busy ? m_turnsPerSecond : 0f, Time.deltaTime * 2f);
            if (m_speed > 0.001f)
            {
                m_crank.Rotate(0f, 0f, m_speed * 360f * Time.deltaTime, Space.Self);
            }
        }
    }
}
