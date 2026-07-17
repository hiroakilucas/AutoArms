using System.Collections;
using UnityEngine;

// Adicionado via AddComponent no pet instanciado (CombatSceneLoader) — resolve o Animator do
// prefab Spriter2UnityDX. Parâmetros esperados no Animator Controller (ver
// Assets/Editor/PetAnimatorSetup.cs, Tools/AutoArms/Setup Pet Animators): Idle (Bool),
// Running (Bool), Slashing (Trigger), Hurt (Trigger), Jumping (Trigger, esquiva — o clip se
// chama "Jumping", não "Jump_Loop"), Dying (Trigger, trava no último frame).
public class PetAnimationController : MonoBehaviour
{
    private Animator _animator;

    // Net visual — rede caída sobre o pet, oscilando lateralmente (mesma lógica de
    // PlayerCombat.ShowNetEnsnared / NetOscillateLoop, mas sem loop de face).
    private GameObject _netVisual;
    private Coroutine  _netOscillateRoutine;

    public void ShowNetEnsnared(Sprite netSprite, float scale = 1f)
    {
        if (_netVisual != null) return;
        _netVisual = new GameObject("NetVisual");
        _netVisual.transform.SetParent(transform);
        _netVisual.transform.localPosition = Vector3.zero;
        _netVisual.transform.localScale = new Vector3(Mathf.Sign(transform.localScale.x) * scale, scale, 1f);
        var sr = _netVisual.AddComponent<SpriteRenderer>();
        sr.sprite           = netSprite;
        sr.sortingLayerName = "Characters";  // acima dos sprites do pet (Default/Weapons)
        sr.sortingOrder     = 100;
        _netOscillateRoutine = StartCoroutine(NetOscillateLoop());
    }

    // Para oscilação e devolve o GameObject da rede sem destruí-lo — o chamador
    // (CombatPlayer) anima a dissipação antes de chamar Destroy, igual ao PlayerCombat.
    public GameObject ReleaseNet()
    {
        if (_netOscillateRoutine != null) { StopCoroutine(_netOscillateRoutine); _netOscillateRoutine = null; }
        var go = _netVisual;
        _netVisual = null;
        return go;
    }

    private IEnumerator NetOscillateLoop()
    {
        while (_netVisual != null)
        {
            var lp = _netVisual.transform.localPosition;
            lp.x = Mathf.Sin(Time.time * 2f) * 0.05f;
            _netVisual.transform.localPosition = lp;
            yield return null;
        }
    }

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            Debug.LogError($"[PetAnimationController] Nenhum Animator encontrado em {name}.");
    }

    // **Bug real corrigido (2026-07-17)**: até esta versão, `Awake()` enumerava
    // `_animator.parameters` pra cachear quais dos 6 parâmetros existiam (`_hasIdle`/
    // `_hasRunning`/etc.) e todo método abaixo só chamava `SetBool`/`SetTrigger` se a flag
    // correspondente tivesse dado `true` — pensado pra evitar warnings de "parameter does not
    // exist" caso `Tools/AutoArms/Setup Pet Animators` não tivesse rodado ainda. Só que esse
    // `Awake()` roda SINCRONAMENTE dentro de `gameObject.AddComponent<PetAnimationController>()`,
    // chamado por `PetCombatController.Awake()`, que por sua vez também roda sincronamente via
    // `petObj.AddComponent<PetCombatController>()` em `CombatSceneLoader.SpawnPets` — tudo no
    // MESMO frame do `Instantiate(prefab)` que criou o pet. `Animator.parameters`, lido tão cedo
    // (antes do Animator ter feito seu próprio bind/init interno, que a Unity só garante a
    // partir do primeiro `Update`/habilitação), pode devolver uma lista vazia mesmo com o
    // Controller corretamente configurado — todas as 6 flags ficavam `false`, e TODA chamada de
    // animação (`SetIdle`/`PlayRun`/`PlaySlash`/`PlayHurt`/`PlayJump`/`PlayDying`) virava no-op
    // silencioso pelo resto da luta, enquanto movimento (`MovementController`, componente
    // totalmente separado) e dano (`CombatSimulator`/`HealthSystem`) continuavam funcionando
    // normalmente — exatamente o sintoma reportado pelo usuário ("pet fica estático se movendo
    // até o oponente estático, ataca estático"). Não era específico do Macaco: a mesma cadeia de
    // `AddComponent` acontece igual pros 3 pets, então o bug é sistêmico, só que o usuário só
    // tinha testado o Macaco até então. `AnimationController.cs` (personagens principais, ver
    // `Assets/Scripts/Handler/`) nunca teve esse gate — só chama `SetBool`/`SetTrigger` direto
    // pelo nome, sem nenhuma checagem de existência prévia (Unity trata um nome de parâmetro
    // inexistente como no-op silencioso na própria chamada, sem lançar exceção) — por isso os
    // personagens principais nunca demonstraram esse bug. Fix: removida a checagem prévia por
    // completo, mesmo padrão simples/direto do `AnimationController` dos personagens.
    public void SetIdle(bool idle)
    {
        if (_animator == null) return;
        _animator.SetBool("Idle", idle);
        if (idle) _animator.SetBool("Running", false);
    }

    public void PlayRun(bool running)
    {
        if (_animator != null) _animator.SetBool("Running", running);
    }

    public void PlaySlash()
    {
        if (_animator != null) _animator.SetTrigger("Slashing");
    }

    public void PlayHurt()
    {
        if (_animator == null) return;
        _animator.ResetTrigger("Hurt");
        _animator.SetTrigger("Hurt");
    }

    // Esquiva — usa o trigger "Jumping" (não "Jump_Loop"; ver nota no topo do arquivo).
    public void PlayJump()
    {
        if (_animator != null) _animator.SetTrigger("Jumping");
    }

    // Morte — toca uma vez e trava no último frame (Loop Time=false no clipe, sem transição
    // de saída no controller, ver PetAnimatorSetup) — preparação pra Tamer (CLAUDE.md): o pet
    // fica caído no chão, GameObject nunca destruído.
    public void PlayDying()
    {
        if (_animator != null) _animator.SetTrigger("Dying");
    }

    // Desliga o Animator depois do clipe de Dying terminar de tocar (uma única passada, Loop
    // Time=false) — pet morto nunca é destruído (preparação pra Tamer, CLAUDE.md), então sem
    // isso o Animator continuaria avaliando esse estado parado pelo resto da luta sem nenhum
    // efeito visual a mais (já travado no último frame) — custo de CPU sem benefício, e com
    // várias lutas tendo pets sucessivos isso some, mas mesmo numa luta só, vários pets mortos
    // de ambos os lados (Net permanente + combate longo) somavam Animators ativos à toa.
    // Chamado por PetCombatController.PlayDeath, logo depois do trigger.
    public void DisableAfterDying()
    {
        if (_animator == null) return;
        float length = 1f;
        var clips = _animator.runtimeAnimatorController?.animationClips;
        if (clips != null)
            foreach (var c in clips)
                if (c != null && c.name == "Dying") { length = c.length; break; }
        StartCoroutine(DisableAfterDelay(length));
    }

    private IEnumerator DisableAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_animator != null) _animator.enabled = false;
    }
}
