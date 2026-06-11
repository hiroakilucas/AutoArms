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
