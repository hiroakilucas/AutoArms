using UnityEngine;
using System;

public class HealthSystem : MonoBehaviour
{
    // Y offset above the character's transform (feet pivot). Adjust per character if needed.
    private const float BarYOffset = 2.5f;

    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public event Action OnDeath;

    private HealthBar healthBar;

    public void Initialize(int maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;

        if (healthBar != null) Destroy(healthBar.gameObject);
        healthBar = HealthBar.Create(transform, new Vector3(0, BarYOffset, 0));
        healthBar.UpdateBar(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        healthBar?.UpdateBar(CurrentHealth, MaxHealth);
        Debug.Log($"[HealthSystem] {gameObject.name} tomou {damage} de dano. HP: {CurrentHealth}/{MaxHealth}");
        if (IsDead) OnDeath?.Invoke();
    }

    private void OnDestroy()
    {
        if (healthBar != null)
            Destroy(healthBar.gameObject);
    }
}
