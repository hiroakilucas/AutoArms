using UnityEngine;
using System.Collections;

public class MovementController : MonoBehaviour
{
    /// <summary>
    /// Move linear até 'to' com velocidade 'speed'.
    /// </summary>
    public IEnumerator MoveTo(Vector2 to, float speed)
    {
        while ((Vector2)transform.position != to)
        {
            transform.position = Vector2.MoveTowards(transform.position, to, speed * Time.deltaTime);
            yield return null;
        }
    }


    /// <summary>
    /// Salta em arco parabólico de onde o personagem está até 'to'.
    /// </summary>
    public IEnumerator JumpTo(Vector2 fromIgnored, Vector2 to, float speed, float height)
    {
        // Usa a posição real de início
        Vector2 start = transform.position;
        float distance = Vector2.Distance(start, to);
        float duration = distance / speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // interpola X/Y linearmente
            Vector2 pos = Vector2.Lerp(start, to, t);
            // adiciona componente vertical parabólico
            pos.y += Mathf.Sin(Mathf.PI * t) * height;
            transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // garante chegar exatamente em 'to'
        transform.position = to;
    }
}
