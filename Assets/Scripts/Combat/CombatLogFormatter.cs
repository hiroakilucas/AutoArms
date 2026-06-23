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

                case CombatEventType.WeaponSwap:
                    sb.AppendLine($"  {Name(e.playerIndex)} troca de arma, larga: {e.weaponName}");
                    break;

                case CombatEventType.Thief:
                    sb.AppendLine($"  {Name(e.playerIndex)} rouba a arma de {Name(e.targetIndex)}: {e.weaponName}");
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
                    sb.AppendLine(e.isThrow
                        ? $"  {Name(e.targetIndex)} bloqueia o arremesso de {Name(e.playerIndex)}"
                        : $"  {Name(e.targetIndex)} bloqueia o ataque de {Name(e.playerIndex)}");
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

                case CombatEventType.Stunned:
                    sb.AppendLine($"  {Name(e.playerIndex)} encadeia 3 golpes e estuna {Name(e.targetIndex)}");
                    break;

                case CombatEventType.StunSkip:
                    sb.AppendLine($"  {Name(e.playerIndex)} está estunado e perde a ação");
                    break;

                case CombatEventType.Saboteur:
                    sb.AppendLine($"  {Name(e.playerIndex)} sabota {Name(e.targetIndex)}: destrói {e.weaponName}");
                    break;

                case CombatEventType.SaboteurBreak:
                    sb.AppendLine($"  {Name(e.targetIndex)} puxa {e.weaponName} e ela quebra na hora (Saboteur de {Name(e.playerIndex)})");
                    break;

                case CombatEventType.FlashFlood:
                {
                    sb.AppendLine($"  {Name(e.playerIndex)} ativa FLASH FLOOD contra {Name(e.targetIndex)}!");
                    if (e.ffWeapons != null)
                        for (int j = 0; j < e.ffWeapons.Count; j++)
                            sb.AppendLine($"    arremessa {e.ffWeapons[j]}: {e.ffDamages[j]} dano (SEMPRE ACERTA) (HP: {e.ffHpAfter[j]}/{e.maxHp})");
                    break;
                }

                case CombatEventType.HasteAttack:
                    if (e.isDodged)
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE — {Name(e.targetIndex)} esquiva do dash");
                    else if (e.isBlocked)
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE — {Name(e.targetIndex)} bloqueia o dash");
                    else
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE e acerta {Name(e.targetIndex)}: {e.damage} dano{(e.isCrit ? " [CRÍTICO]" : "")} (HP: {e.newHp}/{e.maxHp})");
                    break;

                case CombatEventType.PiledriverAttack:
                    sb.AppendLine($"  {Name(e.playerIndex)} ativa PILEDRIVER e acerta {Name(e.targetIndex)}: {e.damage} dano{(e.isCrit ? " [CRÍTICO]" : "")} (HP: {e.newHp}/{e.maxHp})");
                    break;

                case CombatEventType.NetThrow:
                    sb.AppendLine($"  {Name(e.playerIndex)} ativa NET e enreda {Name(e.targetIndex)}");
                    break;

                case CombatEventType.NetEnsnaredSkip:
                    sb.AppendLine($"  {Name(e.playerIndex)} está enredado pela rede e perde a ação");
                    break;

                case CombatEventType.NetFreed:
                    sb.AppendLine($"  {Name(e.playerIndex)} se solta da rede");
                    break;

                case CombatEventType.FierceBruteActivated:
                    sb.AppendLine($"  {Name(e.playerIndex)} ativa FIERCE BRUTE (próximo golpe dobrado)");
                    break;

                case CombatEventType.BombThrow:
                {
                    sb.AppendLine($"  {Name(e.playerIndex)} ativa BOMB!");
                    if (e.bombTargets != null)
                        for (int j = 0; j < e.bombTargets.Count; j++)
                        {
                            int targetIdx = e.bombTargets[j];
                            int maxHp = 0;
                            if (i + 1 < events.Count
                                && events[i + 1].type == CombatEventType.HealthChanged
                                && events[i + 1].playerIndex == targetIdx)
                            {
                                maxHp = events[i + 1].maxHp;
                                i++; // consome o HealthChanged paralelo deste alvo
                            }
                            string netTag = (e.netFreedTargets != null && e.netFreedTargets.Contains(targetIdx)) ? " (rede quebrada)" : "";
                            sb.AppendLine($"    explosão acerta {Name(targetIdx)}: {e.bombTargetDamages[j]} dano (IGNORA DODGE/BLOCK/ARMOR){netTag} (HP: {e.bombTargetHp[j]}/{maxHp})");
                        }
                    break;
                }

                case CombatEventType.TragicPotionUse:
                    sb.AppendLine($"  {Name(e.playerIndex)} bebe TRAGIC POTION e recupera {e.healAmount} HP (HP: {e.newHp}/{e.maxHp})");
                    break;

                case CombatEventType.FastMetabolismRegen:
                    sb.AppendLine($"  {Name(e.playerIndex)} regenera {e.healAmount} HP (Fast Metabolism, HP: {e.newHp})");
                    break;

                case CombatEventType.FastMetabolismPulse:
                    sb.AppendLine($"  {Name(e.playerIndex)} pulso de Fast Metabolism cura {e.healAmount} HP ({e.pulseCount}/10, HP: {e.newHp})");
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
