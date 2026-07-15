using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

// Inicializacao do Firebase (Fatia 1 do plano de contas/save na nuvem, 2026-07-14) - roda uma
// unica vez por processo (campos estaticos, InitializeAsync e idempotente), chamado no Start()
// de LoginController antes de qualquer chamada de Auth/Firestore. CheckAndFixDependenciesAsync
// confirma que as dependencias nativas (Android/iOS, resolvidas pelo External Dependency Manager)
// estao presentes - no Editor/Windows normalmente resolve rapido, ja que o SDK desktop do
// Firebase nao depende de bibliotecas nativas da mesma forma que mobile.
public static class FirebaseBootstrapper
{
    public static bool IsReady { get; private set; }
    public static string Error { get; private set; }

    private static Task<bool> _initTask;

    // Idempotente - chamar de novo so devolve a mesma Task ja em andamento/concluida, nao
    // reinicia a inicializacao (evita CheckAndFixDependenciesAsync rodar 2x se o LoginController
    // for recarregado ou algo mais tambem chamar isso).
    public static Task<bool> InitializeAsync()
    {
        if (_initTask != null) return _initTask;

        var tcs = new TaskCompletionSource<bool>();
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Exception != null)
            {
                Error = task.Exception.Flatten().InnerException?.Message ?? task.Exception.Message;
                Debug.LogError($"[FirebaseBootstrapper] {Error}");
            }
            else if (task.Result == DependencyStatus.Available)
            {
                IsReady = true;
            }
            else
            {
                Error = $"Dependencias do Firebase indisponiveis: {task.Result}";
                Debug.LogError($"[FirebaseBootstrapper] {Error}");
            }
            tcs.SetResult(IsReady);
        });

        _initTask = tcs.Task;
        return _initTask;
    }
}
