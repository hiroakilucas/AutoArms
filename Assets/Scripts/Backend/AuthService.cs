using System;
using System.Threading.Tasks;
using Firebase.Auth;
using Google;

// Wrapper fino sobre Firebase.Auth (Fatia 1, 2026-07-14: email/senha; Fatia 2, 2026-07-15:
// Google). Apple (Fatia 7, depende de Mac com Xcode) entra como metodo novo aqui depois, sem
// alterar os existentes.
//
// Versao do SDK confirmada por erro de compilacao real (2026-07-15) — e uma mistura, NAO uma
// regra uniforme: SignInWithEmailAndPasswordAsync/CreateUserWithEmailAndPasswordAsync retornam
// Task<AuthResult> nesta instalacao (confirmado funcionando desde a Fatia 1), mas
// SignInWithCredentialAsync (usado no login federado/Google, Fatia 2) ainda retorna
// Task<FirebaseUser> direto — API migrada método a método entre versões do SDK, não de uma vez
// só. Se o Apple Sign-In (Fatia 7) também passar por SignInWithCredentialAsync, ele também
// precisa do padrao FirebaseUser direto (sem .User), nao AuthResult.
public static class AuthService
{
    private static FirebaseAuth Auth => FirebaseAuth.DefaultInstance;

    public static FirebaseUser CurrentUser => Auth.CurrentUser;
    public static bool IsSignedIn => CurrentUser != null;

    // Web Client ID gerado automaticamente pelo Firebase Console (2026-07-15) ao ativar Google
    // como metodo de login em Authentication > Sign-in method — NAO e segredo (e um
    // identificador publico de OAuth, mesmo padrao de client IDs OAuth em geral).
    private const string GoogleWebClientId =
        "921890667694-s576t5t4jvi39e69dlobqjugl13csqdb.apps.googleusercontent.com";

    private static bool _googleConfigured;

    // GoogleSignIn.Configuration so pode ser setado UMA vez antes do 1º acesso a
    // GoogleSignIn.DefaultInstance (ver GoogleSignIn.cs do plugin) - guard evita configurar de
    // novo em chamadas repetidas de SignInWithGoogleAsync.
    private static void EnsureGoogleConfigured()
    {
        if (_googleConfigured) return;
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId = GoogleWebClientId,
            RequestIdToken = true,
        };
        _googleConfigured = true;
    }

    // So funciona em builds Android/iOS - GoogleSignIn.DefaultInstance lança SignInException
    // (DeveloperError) sincronamente em qualquer outra plataforma (Editor/Windows Standalone
    // incluidos), capturado abaixo e traduzido em FriendlyGoogleError. Chamador
    // (LoginController) tambem checa a plataforma antes de chamar, pra dar uma mensagem mais
    // clara sem precisar disparar a exception.
    public static async Task<(bool success, string error)> SignInWithGoogleAsync()
    {
        try
        {
            EnsureGoogleConfigured();
            GoogleSignInUser googleUser = await GoogleSignIn.DefaultInstance.SignIn();
            Credential credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
            FirebaseUser result = await Auth.SignInWithCredentialAsync(credential);
            return (result != null, null);
        }
        catch (GoogleSignIn.SignInException e)
        {
            return (false, FriendlyGoogleError(e.Status));
        }
        catch (Exception e)
        {
            return (false, FriendlyError(e));
        }
    }

    private static string FriendlyGoogleError(GoogleSignInStatusCode status)
    {
        switch (status)
        {
            case GoogleSignInStatusCode.Canceled: return "Login cancelado.";
            case GoogleSignInStatusCode.NetworkError: return "Erro de rede - verifique a conexao.";
            case GoogleSignInStatusCode.DeveloperError: return "Configuracao invalida (Web Client ID/SHA-1) - ver ARQUITETURA.md.";
            default: return $"Falha no login Google ({status}).";
        }
    }

    public static async Task<(bool success, string error)> SignInAsync(string email, string password)
    {
        try
        {
            AuthResult result = await Auth.SignInWithEmailAndPasswordAsync(email, password);
            return (result.User != null, null);
        }
        catch (Exception e)
        {
            return (false, FriendlyError(e));
        }
    }

    public static async Task<(bool success, string error)> SignUpAsync(string email, string password)
    {
        try
        {
            AuthResult result = await Auth.CreateUserWithEmailAndPasswordAsync(email, password);
            return (result.User != null, null);
        }
        catch (Exception e)
        {
            return (false, FriendlyError(e));
        }
    }

    public static void SignOut() => Auth.SignOut();

    // Traduz os erros mais comuns do Firebase Auth pra mensagem legivel em portugues -
    // FirebaseException.ErrorCode mapeia pro enum Firebase.Auth.AuthError.
    private static string FriendlyError(Exception e)
    {
        var inner = (e as AggregateException)?.Flatten().InnerException ?? e;
        if (inner is Firebase.FirebaseException fe)
        {
            switch ((AuthError)fe.ErrorCode)
            {
                case AuthError.WrongPassword: return "Senha incorreta.";
                case AuthError.UserNotFound: return "Conta nao encontrada.";
                case AuthError.EmailAlreadyInUse: return "Ja existe uma conta com esse email.";
                case AuthError.WeakPassword: return "Senha muito fraca (minimo 6 caracteres).";
                case AuthError.InvalidEmail: return "Email invalido.";
                default: return fe.Message;
            }
        }
        return inner.Message;
    }
}
