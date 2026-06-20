using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Replays a pre-calculated event list from CombatSimulator as animation.
// Bridges the pure-logic simulator with the existing MonoBehaviour components.
public class CombatPlayer : MonoBehaviour
{
    [HideInInspector] public PlayerCombat    p1Combat;
    [HideInInspector] public PlayerCombat    p2Combat;
    [HideInInspector] public AttackSequencer sequencer;
    [HideInInspector] public WeaponHUD       p1WeaponHUD;
    [HideInInspector] public WeaponHUD       p2WeaponHUD;

    private List<CombatEvent> _events;
    private float             _playbackSpeed = 1f;
    private bool              _is2x = false;
    private bool              _skipRequested;
    private HealthSystem      _h1, _h2;

    // Called by CombatSceneLoader after both players have landed.
    public void PlayCombat(List<CombatEvent> events)
    {
        _events = events;
        _h1     = p1Combat.GetComponent<HealthSystem>();
        _h2     = p2Combat.GetComponent<HealthSystem>();
        StartCoroutine(PlayEvents());
    }

    // Toggles between 1x and 1.5x playback. Returns the new state (true = now at 1.5x).
    public bool ToggleSpeed()
    {
        if (!_is2x)
        {
            _playbackSpeed = 1.5f;
            _is2x = true;
        }
        else
        {
            _playbackSpeed = 1f;
            _is2x = false;
        }
        return _is2x;
    }

    public void RequestSkip()
    {
        _skipRequested = true;
    }

    private IEnumerator PlayEvents()
    {
        foreach (var evt in _events)
        {
            if (_skipRequested)
            {
                // Fast-forward: apply all remaining HealthChanged events, then fire CombatEnd
                foreach (var remaining in _events)
                {
                    if (remaining.type == CombatEventType.HealthChanged)
                        ApplyHealthChanged(remaining);
                }
                var endEvt = _events.FindLast(e => e.type == CombatEventType.CombatEnd);
                if (endEvt != null) TriggerCombatEnd(endEvt);
                yield break;
            }

            yield return StartCoroutine(ExecuteEvent(evt));
        }
    }

    private IEnumerator ExecuteEvent(CombatEvent evt)
    {
        var attacker = GetCombat(evt.playerIndex);
        var defender = GetCombat(evt.targetIndex);
        float t = 1f / _playbackSpeed;

        switch (evt.type)
        {
            case CombatEventType.Saboteur:
                // Pré-fight, emitido antes do 1º TurnStart — popup "SABOTAGE!" acima da
                // vítima logo no início da animação, antes de qualquer ação de combate. A
                // arma destruída também cai do próprio ícone na WeaponHUD (abaixo da barra de
                // vida) até o chão, igual a um Disarm normal — sem isso, o ícone continuava
                // visível mesmo com a arma já removida do loadout do simulador.
                if (defender != null)
                {
                    // Força o layout group a recalcular antes de ler a posição do ícone —
                    // RectTransform.position só reflete o resultado final do HorizontalLayoutGroup
                    // depois de um passe de layout; sem isso, ler a posição logo após o frame em
                    // que a WeaponHUD foi montada podia pegar um valor desatualizado/zerado.
                    Canvas.ForceUpdateCanvases();

                    var weaponHud  = GetWeaponHUD(evt.targetIndex);
                    var weaponData = FindWeaponByName(defender.weaponHandler.loadout, evt.weaponName);
                    Vector3? iconScreenPos = weaponHud?.GetIconScreenPosition(evt.weaponName);

                    if (weaponHud != null && weaponData != null && iconScreenPos.HasValue && Camera.main != null)
                    {
                        weaponHud.RemoveWeapon(weaponData);
                        float depth = defender.transform.position.z - Camera.main.transform.position.z;
                        Vector3 startWorldPos = Camera.main.ScreenToWorldPoint(
                            new Vector3(iconScreenPos.Value.x, iconScreenPos.Value.y, depth));
                        StartCoroutine(PlayerCombat.DropWeaponFromHud(defender, weaponData, startWorldPos));
                    }
                    else
                    {
                        Debug.LogError($"[Saboteur] Não foi possível animar a queda da arma destruída ({evt.weaponName}): weaponHud={weaponHud != null}, weaponData={weaponData != null}, iconScreenPos={iconScreenPos.HasValue}, Camera.main={Camera.main != null}");
                    }
                }
                DamagePopup.SpawnSabotage((defender?.transform.position ?? Vector3.zero) + Vector3.up * 1.5f);
                yield return new WaitForSeconds(0.6f * t);
                break;

            case CombatEventType.TurnStart:
                // Pequeno buffer antes de qualquer PickupWeapon/CatchWeapon deste turno.
                // A transição "Idle → Catch Weapon" no Animator só existe a partir do
                // estado Idle (não AnyState) — em turnos extras por velocidade, o TurnEnd
                // anterior chama SetIdle(true) e o próximo TurnStart rodava 1 frame depois,
                // sem tempo do Animator de fato concluir a transição pro estado Idle antes
                // do trigger CatchWeapon ser setado. O trigger ficava pendente e só disparava
                // quando o Animator finalmente entrava em Idle — tarde, parecendo a animação
                // de pegar arma rodando no fim do turno em vez do início.
                yield return new WaitForSeconds(0.1f * t);
                break;

            case CombatEventType.RunToDefender:
                if (attacker != null && defender != null)
                {
                    Vector2 attackPos = CalcAttackPosition(attacker, defender);
                    yield return StartCoroutine(
                        attacker.animationController.PlayRun(attackPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
                }
                break;

            case CombatEventType.WeaponSwap:
                // Troca de arma estando armado — joga a atual no chão (mesmo pêndulo de
                // DropWeapon/WeaponDrop, remoção permanente do loadout — some do WeaponHUD,
                // não pode ser sacada de novo, igual a qualquer outra arma largada). Fire-and-
                // forget: o PickupWeapon que vem logo a seguir na lista de eventos já cobre a
                // animação de pegar a nova arma, não precisa esperar a queda terminar.
                if (attacker != null)
                    StartCoroutine(PlayerCombat.DropWeapon(attacker, isDisarm: false));
                yield return null;
                break;

            case CombatEventType.PickupWeapon:
                if (attacker != null)
                {
                    // Equipa exatamente a arma que o CombatSimulator sorteou para este evento
                    // (evt.weaponName) — EquipRandom() fazia um sorteio independente aqui,
                    // que podia equipar visualmente uma arma diferente da usada no cálculo
                    // de dano daquele turno.
                    var weaponToEquip = FindWeaponByName(attacker.weaponHandler.loadout, evt.weaponName);
                    if (weaponToEquip != null) attacker.weaponHandler.EquipSpecific(weaponToEquip);
                    else attacker.weaponHandler.EquipRandom();
                    yield return StartCoroutine(attacker.animationController.PlayCatchWeapon(0.6f * t));
                }
                break;

            case CombatEventType.Thief:
                // PlayerCombat.StealWeapon cobre o visual inteiro (pula nas costas do
                // adversário, balançam juntos, desce com a arma) e a troca de dono no
                // WeaponHandler/loadout de ambos — bloqueante (yield return StartCoroutine,
                // não fire-and-forget) porque o resto deste mesmo turno (Throw/Run/Melee logo
                // abaixo) depende do ladrão já estar armado quando a coroutine termina.
                if (attacker != null && defender != null)
                    yield return StartCoroutine(PlayerCombat.StealWeapon(attacker, defender));
                break;

            case CombatEventType.Hit:
                if (defender != null)
                {
                    float swingMult = SwingSpeedMultiplier(attacker);
                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / swingMult;

                    // Thrown-weapon hits already had their "windup" during ThrowWeapon's flight —
                    // the attacker has no weapon in hand anymore, so don't re-trigger a melee
                    // swing or wait out slashHalf again; apply the impact immediately instead.
                    if (!evt.isThrow)
                    {
                        yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));

                        string trigger = SwingTrigger(attacker);
                        if (swingMult != 1f) attacker?.animationController.SetSpeed(swingMult);
                        attacker?.GetComponent<Animator>()?.SetTrigger(trigger);
                        yield return new WaitForSeconds(slashHalf);
                    }

                    // Impact moment: damage, hurt animation, knockback, and popup all fire
                    // together here instead of being staggered — health used to only update
                    // once the separate HealthChanged event was reached afterward, and the
                    // popup only appeared once PlayHurt finished, both visibly lagging the hit.
                    Vector2 pushDir = ComputePushDir(attacker, defender);
                    float   kbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   kbDur   = attacker?.settings?.hurtDuration ?? 0.07f;

                    ApplyHealthDelta(evt.targetIndex, evt.newHp);

                    Vector3 popupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.Spawn(popupPos, evt.damage, evt.isCrit);

                    StartCoroutine(defender.Knockback(pushDir, kbDist, kbDur * t));
                    yield return StartCoroutine(defender.animationController.PlayHurt(kbDur * t));

                    // Let the slash clip finish its second half before anything else can
                    // re-trigger it — without this, combo hits retrigger mid-clip and the
                    // animation snaps/restarts instead of playing through.
                    if (!evt.isThrow)
                    {
                        yield return new WaitForSeconds(slashHalf);
                        if (swingMult != 1f) attacker?.animationController.SetSpeed(1f);
                    }
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            // Counter (defensor bate antes do hit do atacante conectar) e Reversal (defensor
            // bate de volta depois de já ter tomado o hit) — em ambos, playerIndex é quem
            // retalia (o "attacker" deste evento) e targetIndex é o atacante original (que
            // agora toma o dano). Mesmo fluxo do Hit, só troca o popup.
            case CombatEventType.Counter:
            case CombatEventType.Reversal:
                if (defender != null)
                {
                    // Sem RepositionIfNeeded aqui: quem retalia (attacker deste evento) nunca
                    // saiu do lugar — é o atacante original (defender deste evento) que correu
                    // até ele. Counter/Reversal disparam antes de qualquer dano nesta troca
                    // (ver SimulateHit), então não há knockback prévio que o tenha deslocado.

                    float retSwingMult = SwingSpeedMultiplier(attacker);
                    float retSlashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / retSwingMult;
                    string retTrigger = SwingTrigger(attacker);
                    if (retSwingMult != 1f) attacker?.animationController.SetSpeed(retSwingMult);
                    attacker?.GetComponent<Animator>()?.SetTrigger(retTrigger);
                    yield return new WaitForSeconds(retSlashHalf);

                    Vector2 retPushDir = ComputePushDir(attacker, defender);
                    float   retKbDist  = attacker?.settings?.knockbackDistance ?? 0.5f;
                    float   retKbDur   = attacker?.settings?.hurtDuration ?? 0.07f;

                    ApplyHealthDelta(evt.targetIndex, evt.newHp);

                    Vector3 retPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    if (evt.type == CombatEventType.Counter)
                        DamagePopup.SpawnCounter(retPopupPos, evt.damage, evt.isCrit);
                    else
                        DamagePopup.SpawnReversal(retPopupPos, evt.damage, evt.isCrit);

                    StartCoroutine(defender.Knockback(retPushDir, retKbDist, retKbDur * t));
                    yield return StartCoroutine(defender.animationController.PlayHurt(retKbDur * t));

                    yield return new WaitForSeconds(retSlashHalf);
                    if (retSwingMult != 1f) attacker?.animationController.SetSpeed(1f);

                    // Os estados Slashing/SlashingDagger/SlashingHeavy só têm UMA transição de
                    // saída no Animator Controller: pro estado Jump Start (via bool JumpStart),
                    // que por sua vez só sai pro Idle quando JumpStart volta a false. Isso nunca
                    // foi um problema antes porque todo combo termina em TurnEnd, que passa por
                    // PlayJumpStart + JumpTo (volta ao spawn) — e quem retalia num Counter/
                    // Reversal nunca tem um TurnEnd próprio nesta troca. Um SetIdle(true) sozinho
                    // não adianta nada aqui: a transição de saída do Slashing nem olha pro bool
                    // Idle, só pro JumpStart. Solução: disparar o mesmo toggle de JumpStart que o
                    // TurnEnd usa, mas sem chamar movement.JumpTo() — fica parado no lugar.
                    float retJumpDur = attacker?.settings?.jumpStartDuration ?? 0.02f;
                    if (attacker != null)
                        yield return StartCoroutine(attacker.animationController.PlayJumpStart(retJumpDur * t));
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.HealthChanged:
                ApplyHealthChanged(evt);
                yield return null;
                break;

            case CombatEventType.Dodge:
                if (defender != null)
                {
                    // Dodge always follows a melee swing attempt (never a throw) — the attacker
                    // swings, and exactly when it would have landed, the defender leaps away.
                    yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));

                    string trigger = SwingTrigger(attacker);
                    float dodgeSwingMult = SwingSpeedMultiplier(attacker);
                    if (dodgeSwingMult != 1f) attacker?.animationController.SetSpeed(dodgeSwingMult);
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);

                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / dodgeSwingMult;
                    yield return new WaitForSeconds(slashHalf);

                    Vector3 dodgePopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnDodge(dodgePopupPos);
                    Vector2 dodgeDir = ComputePushDir(attacker, defender);
                    float   dodgeDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(dodgeDir, dodgeDist));

                    yield return new WaitForSeconds(slashHalf);
                    if (dodgeSwingMult != 1f) attacker?.animationController.SetSpeed(1f);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Block:
                if (defender != null)
                {
                    string trigger = SwingTrigger(attacker);
                    float blockSwingMult = SwingSpeedMultiplier(attacker);
                    float slashHalf = (attacker?.settings?.slashingDuration ?? 0.5f) * 0.5f * t / blockSwingMult;

                    // Same reasoning as Dodge: sync the attacker's swing with the moment of impact.
                    yield return StartCoroutine(RepositionIfNeeded(attacker, defender, t));
                    if (blockSwingMult != 1f) attacker?.animationController.SetSpeed(blockSwingMult);
                    attacker?.GetComponent<Animator>()?.SetTrigger(trigger);
                    yield return new WaitForSeconds(slashHalf);

                    Vector3 blockPopupPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnBlock(blockPopupPos);

                    float kbDist = (attacker?.settings?.knockbackDistance ?? 0.5f) * 0.5f;
                    float kbDur  = (attacker?.settings?.hurtDuration ?? 0.07f) * t;
                    Vector2 blockDir = ComputePushDir(attacker, defender);

                    // Block animation and knockback in parallel
                    StartCoroutine(defender.Knockback(blockDir, kbDist, kbDur));
                    yield return StartCoroutine(defender.animationController.PlayBlock(0.36666667f * t));

                    yield return new WaitForSeconds(slashHalf);
                    if (blockSwingMult != 1f) attacker?.animationController.SetSpeed(1f);
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Miss:
                if (defender != null)
                {
                    Vector3 missPos = defender.transform.position + Vector3.up * 1.5f
                        + Vector3.right * Random.Range(-0.3f, 0.3f);
                    DamagePopup.SpawnMiss(missPos);
                    Vector2 missDir = ComputePushDir(attacker, defender);
                    float missDist = attacker?.settings?.knockbackDistance ?? 0.5f;
                    yield return StartCoroutine(defender.DodgeLeap(missDir, missDist));
                }
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.Disarm:
                // PlayerCombat.DropWeapon já cobre popup + UnequipPermanent + a queda em
                // pêndulo amortecido até o chão (ver Drop de Arma no CLAUDE.md) — antes este
                // case só tirava a arma e mostrava o popup, sem nenhum visual de queda.
                if (defender != null)
                    StartCoroutine(PlayerCombat.DropWeapon(defender, isDisarm: true));
                yield return null;
                break;

            case CombatEventType.WeaponDrop:
                if (attacker != null)
                    StartCoroutine(PlayerCombat.DropWeapon(attacker, isDisarm: false));
                yield return null;
                break;

            case CombatEventType.ShieldDisarm:
                // PlayerCombat.DropShield cobre popup + RemoveShield + a mesma queda em pêndulo
                // amortecido de DropWeapon (ver CLAUDE.md, Desarmar do Escudo) — ao tomar hit,
                // popup "DISARM!" (mesma convenção de Disarm de arma).
                if (defender != null)
                    StartCoroutine(PlayerCombat.DropShield(defender, isDisarm: true));
                yield return null;
                break;

            case CombatEventType.ShieldDrop:
                // Espelha WeaponDrop: escudo cai pelo próprio impacto de um bloqueio bem-sucedido,
                // não por um desarme ativo do atacante — popup "DROP!" em vez de "DISARM!".
                if (attacker != null)
                    StartCoroutine(PlayerCombat.DropShield(attacker, isDisarm: false));
                yield return null;
                break;

            case CombatEventType.ThrowWeapon:
                if (attacker != null)
                {
                    // Capture sprite/position/scale before Unequip destroys the in-hand weapon object.
                    var weaponData   = attacker.weaponHandler.CurrentWeaponData;
                    var inHandWeapon = attacker.weaponHandler.CurrentWeapon;
                    Vector3 launchPos = attacker.weaponHandler.handBone != null
                        ? attacker.weaponHandler.handBone.position
                        : (inHandWeapon != null ? inHandWeapon.transform.position : attacker.transform.position + Vector3.up * 0.5f);
                    Vector3 projectileScale = inHandWeapon != null
                        ? inHandWeapon.transform.lossyScale
                        : (attacker.weaponHandler.handBone != null ? attacker.weaponHandler.handBone.lossyScale : Vector3.one) * (weaponData?.scale ?? 1f);

                    // Mirrors CombatSimulator.SimulateThrow: a arma some da mão (Unequip), mas
                    // Hideaway faz ela ficar no loadout igual a uma arma Thrown normal (pode ser
                    // pega de novo num pickup futuro) mesmo sem ter a tag. Sem a skill e sem a
                    // tag Thrown, sai do loadout pra sempre (UnequipPermanent — some do WeaponHUD).
                    if (weaponData != null && !WeaponData.HasType(weaponData, WeaponType.Thrown) && !attacker.HasSkill("Hideaway"))
                        attacker.weaponHandler.UnequipPermanent();
                    else
                        attacker.weaponHandler.Unequip();
                    // Idle=false antes do trigger (mesmo padrão de PlayCatchWeapon) — garante que
                    // "Any State → Throwing" dispara mesmo que o frame anterior já tivesse
                    // deixado Idle=true de um throw anterior neste mesmo turno.
                    attacker.animationController.SetIdle(false);
                    attacker.GetComponent<Animator>()?.SetTrigger("Throwing");

                    if (weaponData?.inHandSprite != null)
                    {
                        Vector3 targetPos = defender != null
                            ? defender.transform.position
                            : attacker.transform.position + (attacker.isPlayer1 ? Vector3.right : Vector3.left) * 5f;

                        Vector3 flightDir   = (targetPos - launchPos).normalized;
                        float   flightAngle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;

                        var flyingWeapon = new GameObject("FlyingWeapon");
                        flyingWeapon.transform.position   = launchPos;
                        flyingWeapon.transform.localScale = new Vector3(
                            Mathf.Abs(projectileScale.x), Mathf.Abs(projectileScale.y), Mathf.Abs(projectileScale.z));
                        flyingWeapon.transform.rotation = Quaternion.Euler(0, 0, flightAngle);
                        var sr = flyingWeapon.AddComponent<SpriteRenderer>();
                        sr.sprite           = weaponData.inHandSprite;
                        sr.sortingLayerName = "Weapons";
                        sr.sortingOrder     = 10;

                        bool  rotate = WeaponData.HasType(weaponData, WeaponType.Thrown);
                        float arc    = rotate ? 0.5f : 0f;
                        yield return StartCoroutine(attacker.FlyWeapon(flyingWeapon.transform, launchPos, targetPos, 0.45f * t, rotate, arc));
                        Destroy(flyingWeapon);
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.45f * t);
                    }

                    // Sai do estado Throwing assim que o arremesso termina — a transição
                    // Throwing → Idle exige Idle=true e CanTransitionToSelf=0 (não reentra em si
                    // mesma). Mantido por segurança mesmo agora que Hideaway voltou a ser um
                    // throw probabilístico só (sem combo de arremesso, no máximo 1 ThrowWeapon
                    // por turno) — era essencial enquanto existiu um combo de throws no mesmo
                    // turno (bug real reportado pelo usuário, "parece que está com parkinson").
                    attacker.animationController.SetIdle(true);
                }
                break;

            case CombatEventType.SpeedBonus:
                for (int i = 0; i < evt.extraActions; i++)
                    DamagePopup.SpawnRapido((attacker?.transform.position ?? Vector3.zero) + Vector3.up * 2f);
                yield return null;
                break;

            case CombatEventType.Stunned:
                // Chaining: 3º hit consecutivo do streak — liga a label + Hurt em loop no
                // alvo (defender, quem ficou estunado). Fire-and-forget: o resto do turno do
                // atacante (mais combo, TurnEnd) continua normalmente em paralelo.
                defender?.ShowStunLabel();
                yield return null;
                break;

            case CombatEventType.StunSkip:
                // Ação consumida pelo stun — sem Thief/pickup/throw/melee neste turno. Desliga
                // a label/Hurt (volta ao normal) e segue pro TurnEnd como um turno qualquer.
                attacker?.HideStunLabel();
                yield return new WaitForSeconds((attacker?.settings?.comboDelay ?? 0.15f) * t);
                break;

            case CombatEventType.TurnEnd:
                // Return attacker to spawn (jump-back) — mirrors ReturnToSpawn.
                // RandomSpawnPos sorteia um ponto NOVO a cada chamada (não a posição original
                // do personagem) — só faz sentido pular pra lá se o atacante de fato saiu da
                // própria zona de spawn neste turno (correu até o adversário). Monk (guarda,
                // hitSpeed = 0) nunca corre até o adversário (RunToDefender é pulado em
                // CombatSimulator), então o guard `attacker.hitSpeed > 0f` cobre o caso comum dele
                // já estar dentro da zona — mas isso só por si só não bastava: Monk pode ser
                // empurrado por knockback PRA FORA da zona enquanto defende nos turnos do
                // adversário (toma hit, bloqueia, etc.), e como ele nunca corre de volta sozinho,
                // o turno seguinte DELE ainda o achava fora da zona e disparava um jump-back pra
                // um ponto aleatório — um "pulinho" sem nenhuma ação visível no turno (bug real
                // reportado pelo usuário). Por isso o guard agora é incondicional por hitSpeed,
                // não só pela zona: Monk nunca jump-back no próprio TurnEnd, ponto final — ele é
                // um guarda estacionário, não decide se reposicionar sozinho.
                if (attacker != null && attacker.hitSpeed > 0f && !InSpawnZone(attacker.transform.position, attacker.isPlayer1))
                {
                    float jsDur = attacker.settings?.jumpStartDuration ?? 0.02f;
                    float jh    = attacker.settings?.jumpHeight ?? 2f;
                    float spd   = (attacker.settings?.runSpeed ?? 35f) * t;
                    Vector2 spawnPos = RandomSpawnPos(attacker.isPlayer1);
                    yield return StartCoroutine(attacker.animationController.PlayJumpStart(jsDur * t));
                    yield return StartCoroutine(attacker.movement.JumpTo(spawnPos, spd, jh));
                }
                attacker?.animationController.SetIdle(true);
                break;

            case CombatEventType.CombatEnd:
                TriggerCombatEnd(evt);
                yield return null;
                break;

            default:
                yield return null;
                break;
        }
    }

    // --- Helpers ---

    private PlayerCombat GetCombat(int index) => index == 0 ? p1Combat : p2Combat;
    private WeaponHUD GetWeaponHUD(int index) => index == 0 ? p1WeaponHUD : p2WeaponHUD;

    private static WeaponData FindWeaponByName(PlayerLoadout loadout, string name)
    {
        if (loadout == null || string.IsNullOrEmpty(name)) return null;
        foreach (var w in loadout.Weapons)
            if (w != null && w.weaponName == name) return w;
        return null;
    }

    // Mirrors ComboStrikeRoutine's reposition step (legacy path) — só a 1ª ação do turno tinha
    // RunToDefender; combo hits após uma esquiva ou knockback do hit anterior deixavam o
    // defensor mais longe, e nada recolocava o atacante perto antes do próximo swing/dodge/block.
    // No-op quando a distância já é pequena (ex: 1ª ação do turno, attacker já parado em posição).
    private IEnumerator RepositionIfNeeded(PlayerCombat attacker, PlayerCombat defender, float t)
    {
        if (attacker == null || defender == null) yield break;
        Vector2 attackPos = CalcAttackPosition(attacker, defender);
        if (Vector2.Distance(attacker.transform.position, attackPos) > 0.3f)
            yield return StartCoroutine(
                attacker.animationController.PlayRun(attackPos, (attacker.settings?.runSpeed ?? 35f) * t, attacker.movement));
    }

    private void ApplyHealthChanged(CombatEvent evt) => ApplyHealthDelta(evt.playerIndex, evt.newHp);

    // Compartilhado por Hit/Counter/Reversal/HealthChanged — sempre sincroniza a HealthSystem
    // pro newHp já resolvido pelo simulador (que já leva Survival em conta), em vez de aplicar
    // o dano bruto do evento direto. TakeDamage só entende dano relativo (subtração), então o
    // delta é calculado aqui antes de chamar.
    private void ApplyHealthDelta(int targetIndex, int newHp)
    {
        var hs = targetIndex == 0 ? _h1 : _h2;
        if (hs == null) return;
        int delta = hs.CurrentHealth - newHp;
        if (delta > 0) hs.TakeDamage(delta);
    }

    private void TriggerCombatEnd(CombatEvent evt)
    {
        if (sequencer != null)
        {
            PlayerCombat winner = evt.playerIndex == 0 ? p1Combat : p2Combat;
            sequencer.OnCombatEnd(winner);
        }
    }

    // Heavy e Sharp/default não são somados (não faz sentido físico somar dois alcances de
    // categoria inteiros) — Heavy tem prioridade, depois Fast desconta — mesma regra de
    // PlayerCombat.AttackPosition, ver tabela em CLAUDE.md.
    private static Vector2 CalcAttackPosition(PlayerCombat attacker, PlayerCombat defender)
    {
        float reach;
        if (attacker.weaponHandler.CurrentWeapon == null)
        {
            reach = 0.8f;
        }
        else
        {
            reach = WeaponData.HasType(attacker.weaponHandler.CurrentWeaponData, WeaponType.Heavy) ? 2.8f : 2.0f;
            if (WeaponData.HasType(attacker.weaponHandler.CurrentWeaponData, WeaponType.Fast)) reach -= 0.5f;
        }
        Vector2 defPos = defender.transform.position;
        Vector2 attPos = attacker.transform.position;
        Vector2 dir    = (defPos - attPos).normalized;
        return defPos - dir * reach;
    }

    // Trigger de swing por prioridade Heavy > Fast > default — uma arma só toca uma animação,
    // ainda que tenha múltiplas tags (ex: Heavy|Blunt entra em SlashingHeavy; Sharp|Fast em
    // SlashingDagger). attacker null (ex: Monk guardando) cai no default "Slashing".
    private static string SwingTrigger(PlayerCombat attacker)
    {
        var data = attacker?.weaponHandler.CurrentWeaponData;
        if (WeaponData.HasType(data, WeaponType.Heavy)) return "SlashingHeavy";
        if (WeaponData.HasType(data, WeaponType.Fast))  return "SlashingDagger";
        return "Slashing";
    }

    // Bodybuilder: +40% velocidade de swing, só enquanto empunha arma Heavy — puramente visual
    // (CombatSimulator não usa hitSpeed pra nada além do guard do Monk, então o bônus precisa
    // ser aplicado aqui na reprodução, não no cálculo de chances/dano).
    private static float SwingSpeedMultiplier(PlayerCombat attacker)
    {
        if (attacker == null) return 1f;
        bool heavy = WeaponData.HasType(attacker.weaponHandler.CurrentWeaponData, WeaponType.Heavy);
        return (heavy && attacker.HasSkill("Bodybuilder")) ? 1.4f : 1f;
    }

    private static Vector2 ComputePushDir(PlayerCombat attacker, PlayerCombat defender)
    {
        if (attacker == null || defender == null) return Vector2.right;
        return ((Vector2)defender.transform.position - (Vector2)attacker.transform.position).normalized;
    }

    private static Vector2 RandomSpawnPos(bool isPlayer1)
    {
        float x = isPlayer1 ? Random.Range(-7.25f, -4.79f) : Random.Range(4.79f, 7.25f);
        float y = Random.Range(-3.90f, -0.81f);
        return new Vector2(x, y);
    }

    private static bool InSpawnZone(Vector2 pos, bool isPlayer1)
    {
        float xMin = isPlayer1 ? -7.25f : 4.79f;
        float xMax = isPlayer1 ? -4.79f : 7.25f;
        return pos.x >= xMin && pos.x <= xMax && pos.y >= -3.90f && pos.y <= -0.81f;
    }
}
