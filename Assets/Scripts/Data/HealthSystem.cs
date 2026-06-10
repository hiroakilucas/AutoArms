using UnityEngine;
using System;

public class HealthSystem : MonoBehaviour
{
    public int CurrentHealth { get; private set; }
    public int MaxHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0;

    public event Action OnDeath;
    public event Action<int, int> OnHealthChanged; // current, max

    public void Initialize(int maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
        Debug.Log($"[HealthSystem] {gameObject.name} tomou {damage} de dano. HP: {CurrentHealth}/{MaxHealth}");
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        if (IsDead) OnDeath?.Invoke();
    }
}
