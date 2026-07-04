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

    // minDuration: opcional — garante que o arco (Sin) do pulo toque por pelo menos esse tempo
    // mesmo quando `to` acaba muito perto (ou igual) de `start`. Sem isso, distance/speed some
    // junto com a distância e o loop abaixo nunca roda (elapsed < duration já começa falso),
    // fazendo o personagem "teleportar" pra `to` num único frame sem nenhum pulinho visível —
    // bug real reportado pelo usuário: esquiva no limite da arena (ClampToArena deixando `to`
    // quase igual a `start`) não jogava a animação de pulo, só o popup de esquiva.
    public IEnumerator JumpTo(Vector2 to, float speed, float height, float minDuration = 0f)
    {
        Vector2 start = transform.position;
        float duration = Mathf.Max(Vector2.Distance(start, to) / speed, minDuration);
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
