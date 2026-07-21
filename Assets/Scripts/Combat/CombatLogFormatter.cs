using System.Collections.Generic;
using System.Text;

// Builds a single human-readable multi-line summary of a pre-calculated fight,
// printed once before CombatPlayer starts replaying the events as animation.
public static class CombatLogFormatter
{
    public static string Format(string p1Name, string p2Name, List<CombatEvent> events,
        List<PetData> p1Pets = null, List<PetData> p2Pets = null)
    {
        string Name(int idx) => idx == 0 ? p1Name : p2Name;

        // Pets (Fase 3) — nome de exibição (Rato/Macaco/Javali) resolvido pelo tipo na lista do
        // dono (p1Pets/p2Pets, passadas pelo CombatSceneLoader a partir de profile.pets); sem
        // essas listas (chamadas antigas, sem os 2 parâmetros novos) cai no fallback "Pet".
        string PetName(int ownerIdx, int petIdx)
        {
            var list = ownerIdx == 0 ? p1Pets : p2Pets;
            if (list != null && petIdx >= 0 && petIdx < list.Count && list[petIdx] != null)
                return PetState.DisplayName(list[petIdx].petType);
            return "Pet";
        }

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

                case CombatEventType.HitSpeedSkip:
                    sb.AppendLine($"  {Name(e.playerIndex)} não age neste turno (arma lenta)");
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

                    // Pets como alvo válido (Ajuste 1): targetIndex aqui é sempre o DONO do pet,
                    // não o pet em si (mesma convenção de PetAttack) — sem isso, o log mostrava
                    // "{Atacante} acerta {Dono}" mesmo quando o alvo real era o pet, e nunca
                    // achava o HealthChanged emparelhado (pets não emitem esse evento — o Hit já
                    // carrega newTargetHp/newTargetMaxHp direto), então a linha saía sem HP
                    // nenhum, parecendo que o golpe não tinha efeito.
                    if (e.targetIsPet)
                    {
                        string petTargetName = PetName(e.targetIndex, e.targetPetIndex);
                        sb.AppendLine($"  {Name(e.playerIndex)} acerta [{petTargetName}] (pet de {Name(e.targetIndex)}): {e.damage} dano{tag} (HP: {e.newTargetHp}/{e.newTargetMaxHp})");
                        break;
                    }

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
                    sb.AppendLine(e.targetIsPet
                        ? $"  [{PetName(e.targetIndex, e.targetPetIndex)}] (pet de {Name(e.targetIndex)}) esquiva do ataque de {Name(e.playerIndex)}"
                        : $"  {Name(e.targetIndex)} esquiva do ataque de {Name(e.playerIndex)}");
                    break;

                case CombatEventType.Block:
                    sb.AppendLine(e.isThrow
                        ? $"  {Name(e.targetIndex)} bloqueia o arremesso de {Name(e.playerIndex)}"
                        : $"  {Name(e.targetIndex)} bloqueia o ataque de {Name(e.playerIndex)}");
                    break;

                case CombatEventType.Miss:
                    sb.AppendLine(e.targetIsPet
                        ? $"  {Name(e.playerIndex)} erra o arremesso contra [{PetName(e.targetIndex, e.targetPetIndex)}] (pet de {Name(e.targetIndex)})"
                        : $"  {Name(e.playerIndex)} erra o arremesso contra {Name(e.targetIndex)}");
                    break;

                case CombatEventType.Mimic:
                    sb.AppendLine($"  [MIMIC] {Name(e.playerIndex)} copia e usa a skill do oponente: {e.weaponName}");
                    break;

                case CombatEventType.Repulse:
                {
                    string repTag = e.isCrit ? " [CRÍTICO]" : "";
                    string repHp  = "";
                    if (i + 1 < events.Count
                        && events[i + 1].type == CombatEventType.HealthChanged
                        && events[i + 1].playerIndex == e.targetIndex)
                    {
                        repHp = $" (HP: {events[i + 1].newHp}/{events[i + 1].maxHp})";
                        i++;
                    }
                    sb.AppendLine($"  [REPULSE] {Name(e.playerIndex)} deflecte {e.weaponName} de volta para {Name(e.targetIndex)}: {e.damage} dano{repTag}{repHp}");
                    break;
                }

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
                {
                    string hasteTargetName = e.targetIsPet ? $"[{PetName(e.targetIndex, e.targetPetIndex)}] (pet de {Name(e.targetIndex)})" : Name(e.targetIndex);
                    if (e.isDodged)
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE — {hasteTargetName} esquiva do dash");
                    else if (e.isBlocked)
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE — {hasteTargetName} bloqueia o dash");
                    else if (e.targetIsPet)
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE e acerta {hasteTargetName}: {e.damage} dano{(e.isCrit ? " [CRÍTICO]" : "")} (HP: {e.newTargetHp}/{e.newTargetMaxHp})");
                    else
                        sb.AppendLine($"  {Name(e.playerIndex)} ativa HASTE e acerta {hasteTargetName}: {e.damage} dano{(e.isCrit ? " [CRÍTICO]" : "")} (HP: {e.newHp}/{e.maxHp})");
                    break;
                }

                case CombatEventType.PiledriverAttack:
                    sb.AppendLine(e.targetIsPet
                        ? $"  {Name(e.playerIndex)} ativa PILEDRIVER e acerta [{PetName(e.targetIndex, e.targetPetIndex)}] (pet de {Name(e.targetIndex)}): {e.damage} dano{(e.isCrit ? " [CRÍTICO]" : "")} (HP: {e.newTargetHp}/{e.newTargetMaxHp})"
                        : $"  {Name(e.playerIndex)} ativa PILEDRIVER e acerta {Name(e.targetIndex)}: {e.damage} dano{(e.isCrit ? " [CRÍTICO]" : "")} (HP: {e.newHp}/{e.maxHp})");
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

                case CombatEventType.ChefPizzaThrow:
                    sb.AppendLine($"  {Name(e.playerIndex)} ativa CHEF e arremessa uma pizza envenenada em {Name(e.targetIndex)}");
                    break;

                case CombatEventType.PoisonDamage:
                    sb.AppendLine($"  {Name(e.playerIndex)} sofre {e.damage} dano de veneno (Chef, HP: {e.newHp})");
                    break;

                case CombatEventType.VampirismAttack:
                    sb.AppendLine(e.targetIsPet
                        ? $"  {Name(e.playerIndex)} ativa VAMPIRISM e morde [{PetName(e.targetIndex, e.targetPetIndex)}] (pet de {Name(e.targetIndex)}, mordida garantida): {e.damage} dano (HP: {e.newTargetHp}/{e.newTargetMaxHp}) e cura {e.healAmount} HP (HP: {e.newAttackerHp})"
                        : $"  {Name(e.playerIndex)} ativa VAMPIRISM e morde {Name(e.targetIndex)} (mordida garantida): {e.damage} dano (HP: {e.newDefenderHp}) e cura {e.healAmount} HP (HP: {e.newAttackerHp})");
                    break;

                case CombatEventType.CombatEnd:
                    sb.AppendLine($"========== VENCEDOR: {Name(e.playerIndex)} ==========");
                    break;

                case CombatEventType.PetTurnStart:
                    sb.AppendLine($"--- Turno de {PetName(e.playerIndex, e.petIndex)} (pet de {Name(e.playerIndex)}) ---");
                    break;

                case CombatEventType.PetAttack:
                {
                    string petName = PetName(e.playerIndex, e.petIndex);
                    if (e.shieldIntercept)
                    {
                        sb.AppendLine($"  [{petName}] intercepta o golpe (escudo vivo) e sofre {e.damage} dano (HP: {e.newTargetHp}/{e.newTargetMaxHp})");
                        break;
                    }
                    string targetName = e.targetIsPet ? PetName(e.targetIndex, e.targetPetIndex) : Name(e.targetIndex);
                    if (e.isDodged)
                        sb.AppendLine($"  [{petName}] ataque esquivado por {targetName}");
                    else
                    {
                        int newHp = e.targetIsPet ? e.newTargetHp : e.newHp;
                        int maxHp = e.targetIsPet ? e.newTargetMaxHp : e.maxHp;
                        sb.AppendLine($"  [{petName}] {e.damage} de dano em {targetName} (HP: {newHp}/{maxHp})");
                    }
                    break;
                }

                case CombatEventType.PetNetSkip:
                    sb.AppendLine($"  [{PetName(e.playerIndex, e.petIndex)}] preso na rede — turno pulado");
                    break;

                case CombatEventType.PetDeath:
                    sb.AppendLine($"  [{PetName(e.playerIndex, e.petIndex)}] foi nocauteado!");
                    break;

                case CombatEventType.PetDisarm:
                    sb.AppendLine($"  [{PetName(e.playerIndex, e.petIndex)}] desarmou {Name(e.targetIndex)}!");
                    break;

                case CombatEventType.CryOfTheDamned:
                    sb.AppendLine($"  {Name(e.playerIndex)} soltou o Grito dos Condenados!");
                    break;

                case CombatEventType.PetFlee:
                    sb.AppendLine($"  [{PetName(e.playerIndex, e.petIndex)}] fugiu apavorado e abandonou a partida!");
                    break;

                case CombatEventType.Hypnosis:
                    sb.AppendLine($"  {Name(e.playerIndex)} ativou HIPNOSE!");
                    break;

                case CombatEventType.PetHypnotized:
                    sb.AppendLine($"  [{PetName(e.playerIndex, e.petIndex)}] foi hipnotizado e trocou de lado para {Name(e.targetIndex)}!");
                    break;

                case CombatEventType.TamerEat:
                    sb.AppendLine($"  {Name(e.playerIndex)} (TAMER) come [{PetName(e.targetIndex, e.petIndex)}] e recupera {e.healAmount} HP (HP: {e.newHp}/{e.maxHp})");
                    break;

                // RunToDefender, TurnEnd, PetTurnEnd, and any unconsumed HealthChanged carry no
                // line of their own.
                default:
                    break;
            }
        }

        return sb.ToString();
    }
}
