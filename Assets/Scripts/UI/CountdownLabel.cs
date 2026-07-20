using System;
using TMPro;
using UnityEngine;

// Texto que se auto-atualiza a cada `tickInterval` segundos chamando `formatter()` (2026-07-20,
// pedido do usuário — timer de "quando a energia vai recuperar") — usado tanto na fileira de
// energia do menu (MainMenuCharacterPreview) quanto no popup de energia zerada
// (MainMenuController), sem duplicar o polling em cada lugar. `formatter` decide o texto sozinho
// (ex: PlayerEconomyState.FormatEnergyCountdown) — este componente só chama e aplica, sem saber
// nada sobre energia/moeda especificamente.
public class CountdownLabel : MonoBehaviour
{
    private TMP_Text _text;
    private Func<string> _formatter;
    private float _tickInterval;
    private float _elapsed;
    private Func<bool> _isDoneCheck;
    private Action _onDone;
    private bool _donePending;

    // `isDoneCheck`/`onDone` são opcionais (2026-07-20, pedido do usuário — bug real: o countdown
    // de energia chegava em "0:00:00" e travava ali pra sempre, porque nada disparava um novo
    // `EnergyService.GetOrRegenAsync`). `onDone` dispara só UMA vez por ciclo (borda de subida de
    // `isDoneCheck`, controlada por `_donePending`) — sem esse debounce, chamaria de novo a cada
    // tick (1s) enquanto ficasse travado, inclusive durante o próprio re-sync assíncrono em voo.
    // Só reseta e permite disparar de novo quando `isDoneCheck` voltar a `false` (ex: energia
    // incrementada, novo ciclo de contagem começou).
    public void Init(TMP_Text text, Func<string> formatter, float tickInterval = 1f,
        Func<bool> isDoneCheck = null, Action onDone = null)
    {
        _text = text;
        _formatter = formatter;
        _tickInterval = tickInterval;
        _isDoneCheck = isDoneCheck;
        _onDone = onDone;
        _elapsed = tickInterval; // força 1 refresh já no primeiro Update, sem esperar o intervalo inteiro
        _donePending = false;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed < _tickInterval) return;
        _elapsed = 0f;
        Refresh();
    }

    private void Refresh()
    {
        if (_text != null && _formatter != null)
        {
            string value = _formatter();
            if (!string.IsNullOrEmpty(value)) _text.text = value;
        }

        if (_isDoneCheck == null || _onDone == null) return;
        bool done = _isDoneCheck();
        if (done && !_donePending)
        {
            _donePending = true;
            _onDone();
        }
        else if (!done)
        {
            _donePending = false;
        }
    }
}
