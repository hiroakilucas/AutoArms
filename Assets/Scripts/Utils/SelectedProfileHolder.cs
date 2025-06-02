using UnityEngine;

public class SelectedProfileHolder : MonoBehaviour
{
    public static SelectedProfileHolder Instance { get; private set; }

    [Header("Perfil Selecionado")]
    public PlayerProfile selectedProfile;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
