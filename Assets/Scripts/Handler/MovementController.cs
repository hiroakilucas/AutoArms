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
    /// Salta em arco parabólico de 'from' até 'to'.
    /// </summary>
    public IEnumerator JumpTo(Vector2 from, Vector2 to, float speed, float height)
    {
        float distance = Vector2.Distance(from, to);
        float duration = distance / speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector2 pos = Vector2.Lerp(from, to, t);
            pos.y += Mathf.Sin(Mathf.PI * t) * height;
            transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = to;
    }
}
