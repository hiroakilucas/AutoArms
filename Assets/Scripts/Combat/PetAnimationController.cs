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
    private bool _hasIdle, _hasRunning, _hasSlashing, _hasHurt, _hasJumping, _hasDying;

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
        {
            Debug.LogError($"[PetAnimationController] Nenhum Animator encontrado em {name}.");
            return;
        }

        // Confirma quais parâmetros existem de fato no controller antes de usá-los — evita
        // warnings de "parameter does not exist" se o Setup Pet Animators ainda não tiver
        // rodado, ou logar no Console qual clip/parâmetro está faltando.
        foreach (var p in _animator.parameters)
        {
            switch (p.name)
            {
                case "Idle": _hasIdle = true; break;
                case "Running": _hasRunning = true; break;
                case "Slashing": _hasSlashing = true; break;
                case "Hurt": _hasHurt = true; break;
                case "Jumping": _hasJumping = true; break;
                case "Dying": _hasDying = true; break;
            }
        }

        if (!_hasIdle || !_hasRunning || !_hasSlashing || !_hasHurt || !_hasJumping || !_hasDying)
            Debug.LogError($"[PetAnimationController] {name}: faltam parâmetros no Animator Controller — rode Tools/AutoArms/Setup Pet Animators.");
    }

    public void SetIdle(bool idle)
    {
        if (_hasIdle) _animator.SetBool("Idle", idle);
        if (idle && _hasRunning) _animator.SetBool("Running", false);
    }

    public void PlayRun(bool running)
    {
        if (_hasRunning) _animator.SetBool("Running", running);
    }

    public void PlaySlash()
    {
        if (_hasSlashing) _animator.SetTrigger("Slashing");
    }

    public void PlayHurt()
    {
        if (!_hasHurt) return;
        _animator.ResetTrigger("Hurt");
        _animator.SetTrigger("Hurt");
    }

    // Esquiva — usa o trigger "Jumping" (não "Jump_Loop"; ver nota no topo do arquivo).
    public void PlayJump()
    {
        if (_hasJumping) _animator.SetTrigger("Jumping");
    }

    // Morte — toca uma vez e trava no último frame (Loop Time=false no clipe, sem transição
    // de saída no controller, ver PetAnimatorSetup) — preparação pra Tamer (CLAUDE.md): o pet
    // fica caído no chão, GameObject nunca destruído.
    public void PlayDying()
    {
        if (_hasDying) _animator.SetTrigger("Dying");
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
