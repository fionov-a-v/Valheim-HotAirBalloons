namespace HotAirBalloons
{
    /// <summary>
    /// Руль-палка. У «Летучего карви»: W/S — сложить / раскрыть боковые паруса (сложены → наполовину → полностью),
    /// A/D — повернуть паруса рулём. У «Небесного драккара» (парусов нет): W/S — огонь, A/D — поворот корабля.
    /// ПКМ — якорь.
    /// </summary>
    public class BalloonTiller : BalloonStation
    {
        public override int StationIndex => Helm;

        protected override void ChangeLevel(int delta)
        {
            m_nview.InvokeRPC(m_ctrl.HasSails ? "HAB_ChangeSail" : "HAB_ChangeMode", delta);
        }

        /// <summary>Огонь рядом: топливо с панели быстрого доступа уходит в чашу, не отпуская руля.</summary>
        public override bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return Valid && m_ctrl.m_burner != null && m_ctrl.m_burner.UseItem(user, item);
        }

        public override string GetHoverText()
        {
            if (!Valid)
            {
                return "";
            }
            if (!InUseDistance(Player.m_localPlayer))
            {
                return Localization.instance.Localize("<color=#888888>$piece_toofar</color>");
            }
            string state = m_ctrl.HasSails ? $"$hab_hud_sails: {BalloonHud.SailText(m_ctrl)}" : $"$hab_hud_fire: {BalloonHud.ModeText(m_ctrl)}";
            string text = $"{m_name}\n{state}" +
                          $"\n[<color=yellow><b>$KEY_Use</b></color>] {m_useText}";
            return Localization.instance.Localize(text);
        }
    }
}
