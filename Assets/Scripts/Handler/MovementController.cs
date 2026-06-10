using UnityEngine;
using System.Collections;

public class MovementController : MonoBehaviour
{
    public IEnumerator MoveTo(Vector2 to, float speed)
    {
        while ((Vector2)transform.position != to)
        {
            transform.position = Vector2.MoveTowards(transform.position, to, speed * Time.deltaTime);
            yield return null;
        }
    }

    public IEnumerator JumpTo(Vector2 to, float speed, float height)
    {
        Vector2 start = transform.position;
        float duration = Vector2.Distance(start, to) / speed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector2 pos = Vector2.Lerp(start, to, t);
            pos.y += Mathf.Sin(Mathf.PI * t) * height; // parabolic arc via sin curve
            transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = to;
    }
}
