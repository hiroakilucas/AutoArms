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

    public static void Spawn(Vector3 worldPos, int damage, bool isCrit)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().Init(damage, isCrit);
    }

    public static void SpawnDodge(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitDodge();
    }

    public static void SpawnBlock(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitBlock();
    }

    public static void SpawnMiss(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitMiss();
    }

    public static void SpawnCounter(Vector3 worldPos, int damage, bool isCrit)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitRetaliation("CONTRA-ATAQUE!", damage, isCrit);
    }

    public static void SpawnReversal(Vector3 worldPos, int damage, bool isCrit)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitRetaliation("REVERSAL!", damage, isCrit);
    }

    // Counter e Reversal compartilham o mesmo visual (roxo, pra distinguir de crit/normal/
    // disarm/drop), só o texto do título muda.
    void InitRetaliation(string title, int damage, bool isCrit)
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = isCrit ? $"{title}\nCRIT! {damage}" : $"{title}\n{damage}";
        label.fontSize       = 4f;
        baseColor            = new Color(0.7f, 0.3f, 0.9f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    public static void SpawnDisarm(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitDisarm();
    }

    void InitDisarm()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "DISARM!";
        label.fontSize       = 4f;
        baseColor            = new Color(1f, 0.5f, 0f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    public static void SpawnSabotage(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitSabotage();
    }

    void InitSabotage()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "SABOTAGE!";
        label.fontSize       = 4f;
        baseColor            = new Color(1f, 0.5f, 0f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    // Chaining: popup instantâneo no momento exato do 3º hit que estuna — diferente da label
    // persistente "ATORDOADO!" (PlayerCombat.ShowStunLabel, fica presa acima da cabeça até o
    // stun ser consumido), esse aqui sobe e desaparece como qualquer outro popup, só pra marcar
    // visualmente o instante em que o stun foi recebido.
    public static void SpawnStun(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitStun();
    }

    void InitStun()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "ESTUNADO!";
        label.fontSize       = 4f;
        baseColor            = new Color(1f, 0.95f, 0.2f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    public static void SpawnDrop(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitDrop();
    }

    public static void SpawnRapido(Vector3 worldPos)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitRapido();
    }

    void InitRapido()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "RAPIDO!";
        label.fontSize       = 4f;
        baseColor            = new Color(1f, 1f, 0f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    void InitDrop()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "DROP!";
        label.fontSize       = 4f;
        baseColor            = new Color(1f, 0.5f, 0f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    void InitBlock()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "BLOCK!";
        label.fontSize       = 4f;
        baseColor            = new Color(1f, 0.84f, 0f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    void InitMiss()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "MISS!";
        label.fontSize       = 4f;
        baseColor            = new Color(0.65f, 0.65f, 0.65f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    void InitDodge()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = "ESQUIVA!";
        label.fontSize       = 4f;
        baseColor            = new Color(0.3f, 0.7f, 1f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    void Init(int damage, bool isCrit)
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;

        if (isCrit)
        {
            label.text     = $"CRIT!\n{damage}";
            label.fontSize = 5f;
            baseColor      = Color.red;
        }
        else
        {
            label.text     = damage.ToString();
            label.fontSize = 3.5f;
            baseColor      = new Color(1f, 0.92f, 0.2f);
        }

        label.color = baseColor;
        origin = transform.position;
    }

    // Fierce Brute: mesmo popup de dano normal, mas com uma linha "×2!" abaixo do número e cor
    // laranja intensa em vez do amarelo padrão — pedido explícito do usuário pra diferenciar
    // visualmente o hit que consumiu o buff.
    public static void SpawnFierceBrute(Vector3 worldPos, int damage, bool isCrit)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = worldPos;
        go.AddComponent<DamagePopup>().InitFierceBrute(damage, isCrit);
    }

    void InitFierceBrute(int damage, bool isCrit)
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.alignment      = TextAlignmentOptions.Center;
        label.sortingLayerID = SortingLayer.NameToID("Characters");
        label.sortingOrder   = 50;
        label.fontStyle      = FontStyles.Bold;
        label.text           = isCrit ? $"CRIT! {damage}\n×2!" : $"{damage}\n×2!";
        label.fontSize       = isCrit ? 5f : 4f;
        baseColor            = new Color(1f, 0.35f, 0f);
        label.color          = baseColor;
        origin               = transform.position;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float t = elapsed / Lifetime;

        transform.position = origin + Vector3.up * (RiseSpeed * elapsed);

        baseColor.a = 1f - t;
        label.color = baseColor;

        if (elapsed >= Lifetime)
            Destroy(gameObject);
    }
}
