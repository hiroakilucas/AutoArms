using System.Collections.Generic;
using System.Text;

// Builds a single human-readable multi-line summary of a pre-calculated fight,
// printed once before CombatPlayer starts replaying the events as animation.
public static class CombatLogFormatter
{
    public static string Format(string p1Name, string p2Name, List<CombatEvent> events)
    {
        string Name(int idx) => idx == 0 ? p1Name : p2Name;

        var sb = new StringBuilder();
        sb.AppendLine($"========== LOG DE COMBATE: {p1Name} (P1) vs {p2Name} (P2) ==========");

        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            switch (e.type)
            {
                case CombatEventType.TurnStart:
                    sb.AppendLine($"--- Turno de {Name(e.playerIndex)} ---");
                    break;

                case CombatEventType.PickupWeapon:
                    sb.AppendLine($"  {Name(e.playerIndex)} pega arma: {e.weaponName}");
                    break;

                case CombatEventType.WeaponEquipped:
                    sb.AppendLine($"  {Name(e.playerIndex)} equipa: {e.weaponName}");
                    break;

                case CombatEventType.ThrowWeapon:
                    sb.AppendLine($"  {Name(e.playerIndex)} arremessa {e.weaponName} em {Name(e.targetIndex)}");
                    break;

                case CombatEventType.Hit:
                {
                    string tag = e.isCrit ? " [CRÍTICO]" : e.isCombo ? " [COMBO]" : "";
                    string hp  = "";
                    if (i + 1 < events.Count
                        && events[i + 1].type == CombatEventType.HealthChanged
                        && events[i + 1].playerIndex == e.targetIndex)
                    {
                        hp = $" (HP: {events[i + 1].newHp}/{events[i + 1].maxHp})";
                        i++; // consume the paired HealthChanged event
                    }
                    sb.AppendLine($"  {Name(e.playerIndex)} acerta {Name(e.targetIndex)}: {e.damage} dano{tag}{hp}");
                    break;
                }

                case CombatEventType.Counter:
                case CombatEventType.Reversal:
                {
                    string label = e.type == CombatEventType.Counter ? "CONTRA-ATAQUE" : "REVERSAL";
                    string tag = e.isCrit ? " [CRÍTICO]" : "";
                    string hp  = "";
                    if (i + 1 < events.Count
                        && events[i + 1].type == CombatEventType.HealthChanged
                        && events[i + 1].playerIndex == e.targetIndex)
                    {
                        hp = $" (HP: {events[i + 1].newHp}/{events[i + 1].maxHp})";
                        i++;
                    }
                    sb.AppendLine($"  [{label}] {Name(e.playerIndex)} acerta {Name(e.targetIndex)}: {e.damage} dano{tag}{hp}");
                    break;
                }

                case CombatEventType.Dodge:
                    sb.AppendLine($"  {Name(e.targetIndex)} esquiva do ataque de {Name(e.playerIndex)}");
                    break;

                case CombatEventType.Block:
                    sb.AppendLine($"  {Name(e.targetIndex)} bloqueia o ataque de {Name(e.playerIndex)}");
                    break;

                case CombatEventType.Miss:
                    sb.AppendLine($"  {Name(e.playerIndex)} erra o arremesso contra {Name(e.targetIndex)}");
                    break;

                case CombatEventType.Disarm:
                    sb.AppendLine($"  {Name(e.playerIndex)} desarma {Name(e.targetIndex)} (perdeu: {e.weaponName})");
                    break;

                case CombatEventType.WeaponDrop:
                    sb.AppendLine($"  {Name(e.playerIndex)} deixa cair a arma: {e.weaponName}");
                    break;

                case CombatEventType.SpeedBonus:
                    sb.AppendLine($"  {Name(e.playerIndex)} ganha {e.extraActions} ação(ões) extra(s) por velocidade");
                    break;

                case CombatEventType.CombatEnd:
                    sb.AppendLine($"========== VENCEDOR: {Name(e.playerIndex)} ==========");
                    break;

                // RunToDefender, TurnEnd, and any unconsumed HealthChanged carry no line of their own.
                default:
                    break;
            }
        }

        return sb.ToString();
    }
}
