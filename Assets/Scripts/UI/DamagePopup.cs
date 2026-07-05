using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    const float Lifetime  = 1f;
    const float RiseSpeed = 1.5f;

    TextMeshPro label;
    float elapsed;
    Vector3 origin;
    Color baseColor;

    // Pool de popups — Profiler confirmou freezes pontuais durante o combate coincidindo com
    // GC.Alloc; cada popup fazia `new GameObject` + `AddComponent<DamagePopup>` +
    // `AddComponent<TextMeshPro>` e `Destroy` 1s depois (TextMeshPro aloca buffers de mesh/
    // material internamente em Add/Destroy, caro o bastante pra empacar quando vários popups
    // nascem em sequência rápida — combo longo, Flash Flood com 3 arremessos, burst de 10 curas
    // do Fast Metabolism). Mesmo padrão de `CombatPlayer._ghostPool`/`RentGhost`: cresce sob
    // demanda, nunca destrói, recicla via `SetActive(true/false)` — `AddComponent<TextMeshPro>`
    // só roda 1x por objeto, na primeira vez que ele é criado.
    private static readonly List<DamagePopup> _pool = new List<DamagePopup>();

    private static DamagePopup Rent(Vector3 worldPos)
    {
        // _pool é static — sobrevive a um SceneManager.LoadScene (Combate → MainMenu → Combate
        // de novo, dentro da mesma sessão de Play), diferente dos GameObjects que ele referencia
        // (destruídos junto da cena anterior). `p == null` usa o operator overload do
        // UnityEngine.Object (retorna true pra uma referência já destruída, "fake null") — sem
        // essa purga, a próxima luta tentava reusar um popup morto e lançava
        // MissingReferenceException em `p.gameObject`.
        _pool.RemoveAll(p => p == null);

        foreach (var p in _pool)
        {
            if (!p.gameObject.activeSelf)
            {
                p.gameObject.SetActive(true);
                p.transform.position = worldPos;
                return p;
            }
        }

        var go = new GameObject("DamagePopup (pooled)");
        go.transform.position = worldPos;
        var popup = go.AddComponent<DamagePopup>();
        popup.label = go.AddComponent<TextMeshPro>();
        popup.label.alignment      = TextAlignmentOptions.Center;
        popup.label.sortingLayerID = SortingLayer.NameToID("Characters");
        popup.label.sortingOrder   = 50;
        popup.label.fontStyle      = FontStyles.Bold;
        _pool.Add(popup);
        return popup;
    }

    private void Begin(string text, float fontSize, Color color)
    {
        label.text     = text;
        label.fontSize = fontSize;
        baseColor      = color;
        label.color    = baseColor;
        origin         = transform.position;
        elapsed        = 0f;
    }

    public static void Spawn(Vector3 worldPos, int damage, bool isCrit)
    {
        var p = Rent(worldPos);
        if (isCrit) p.Begin($"CRIT!\n{damage}", 5f, Color.red);
        else        p.Begin(damage.ToString(), 3.5f, new Color(1f, 0.92f, 0.2f));
    }

    public static void SpawnDodge(Vector3 worldPos) =>
        Rent(worldPos).Begin("ESQUIVA!", 4f, new Color(0.3f, 0.7f, 1f));

    public static void SpawnBlock(Vector3 worldPos) =>
        Rent(worldPos).Begin("BLOCK!", 4f, new Color(1f, 0.84f, 0f));

    public static void SpawnMiss(Vector3 worldPos) =>
        Rent(worldPos).Begin("MISS!", 4f, new Color(0.65f, 0.65f, 0.65f));

    // Counter e Reversal compartilham o mesmo visual (roxo, pra distinguir de crit/normal/
    // disarm/drop), só o texto do título muda.
    public static void SpawnCounter(Vector3 worldPos, int damage, bool isCrit) =>
        SpawnRetaliation(worldPos, "CONTRA-ATAQUE!", damage, isCrit);

    public static void SpawnReversal(Vector3 worldPos, int damage, bool isCrit) =>
        SpawnRetaliation(worldPos, "REVERSAL!", damage, isCrit);

    public static void SpawnMimic(Vector3 worldPos, string skillName)
    {
        string text = string.IsNullOrEmpty(skillName) ? "MIMIC!" : $"MIMIC!\n{skillName}";
        Rent(worldPos).Begin(text, 4f, new Color(0.85f, 0.35f, 0.95f)); // magenta
    }

    public static void SpawnRepulse(Vector3 worldPos, int damage, bool isCrit)
    {
        string text = isCrit ? $"REPULSE!\nCRIT! {damage}" : $"REPULSE!\n{damage}";
        Rent(worldPos).Begin(text, 4f, new Color(0.2f, 0.85f, 0.95f)); // cyan
    }

    private static void SpawnRetaliation(Vector3 worldPos, string title, int damage, bool isCrit)
    {
        string text = isCrit ? $"{title}\nCRIT! {damage}" : $"{title}\n{damage}";
        Rent(worldPos).Begin(text, 4f, new Color(0.7f, 0.3f, 0.9f));
    }

    public static void SpawnDisarm(Vector3 worldPos) =>
        Rent(worldPos).Begin("DISARM!", 4f, new Color(1f, 0.5f, 0f));

    public static void SpawnSabotage(Vector3 worldPos) =>
        Rent(worldPos).Begin("SABOTAGE!", 4f, new Color(1f, 0.5f, 0f));

    // Chaining: popup instantâneo no momento exato do 3º hit que estuna — diferente da label
    // persistente "ATORDOADO!" (PlayerCombat.ShowStunLabel, fica presa acima da cabeça até o
    // stun ser consumido), esse aqui sobe e desaparece como qualquer outro popup, só pra marcar
    // visualmente o instante em que o stun foi recebido.
    public static void SpawnStun(Vector3 worldPos) =>
        Rent(worldPos).Begin("ESTUNADO!", 4f, new Color(1f, 0.95f, 0.2f));

    public static void SpawnDrop(Vector3 worldPos) =>
        Rent(worldPos).Begin("DROP!", 4f, new Color(1f, 0.5f, 0f));

    public static void SpawnRapido(Vector3 worldPos) =>
        Rent(worldPos).Begin("RAPIDO!", 4f, new Color(1f, 1f, 0f));

    // hitSpeed < 100% da arma equipada — débito de CombatSimulator.ResolveHitSpeedUnits ainda não
    // fechou 1.0, então o personagem não age neste turno. Cinza-azulado pra não ser confundido
    // com MISS (cinza puro) nem com nenhum popup de dano.
    public static void SpawnSlow(Vector3 worldPos) =>
        Rent(worldPos).Begin("LENTO!", 4f, new Color(0.55f, 0.6f, 0.75f));

    // Fierce Brute: mesmo popup de dano normal, mas com uma linha "×2!" abaixo do número e cor
    // laranja intensa em vez do amarelo padrão — pedido explícito do usuário pra diferenciar
    // visualmente o hit que consumiu o buff.
    public static void SpawnFierceBrute(Vector3 worldPos, int damage, bool isCrit)
    {
        string text = isCrit ? $"CRIT! {damage}\n×2!" : $"{damage}\n×2!";
        Rent(worldPos).Begin(text, isCrit ? 5f : 4f, new Color(1f, 0.35f, 0f));
    }

    // Tragic Potion: popup verde de cura, "+<quantidade>" — fonte maior que o popup normal de
    // dano (fontSize 3.5f -> 6.5f, +3 pedido pelo usuário) pra se destacar dos popups de combate.
    // fontSize default (6.5) é o popup "normal" de cura (Tragic Potion, pulso de Fast
    // Metabolism); a regeneração passiva de Fast Metabolism passa um valor menor explícito
    // (ver CombatPlayer.PlayFastMetabolismRegen) pra não competir visualmente com o popup do
    // pulso, que deve parecer mais impactante.
    public static void SpawnHeal(Vector3 worldPos, int amount, float fontSize = 6.5f) =>
        Rent(worldPos).Begin($"+{amount}", fontSize, new Color(0.2f, 0.9f, 0.2f));

    // Skill Chef — dano do veneno aplicado no fim do turno do envenenado. Mesmo padrão de
    // SpawnHeal (texto simples, sem prefixo "+"/"-", já que é dano negativo de qualquer forma —
    // mesma convenção do popup normal/Spawn), mas verde escuro pra diferenciar de um hit comum.
    public static void SpawnPoison(Vector3 worldPos, int amount) =>
        Rent(worldPos).Begin(amount.ToString(), 3.5f, new Color(0.1f, 0.7f, 0.1f, 1f));

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / Lifetime;

        transform.position = origin + Vector3.up * (RiseSpeed * elapsed);

        baseColor.a = 1f - t;
        label.color = baseColor;

        if (elapsed >= Lifetime)
            gameObject.SetActive(false);
    }
}
