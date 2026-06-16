using UnityEngine;

public struct CharacterStats
{
    public int maxHealth;
    public int str;
    public int agility;
    public int speed;
}

public static class CharacterCreation
{
    const int BASE_HP  = 55;
    const int BASE_STR = 2;
    const int BASE_AGI = 2;
    const int BASE_SPD = 2;
    const int POOL     = 9;

    // Distribui POOL pontos aleatoriamente: HP+5, STR+1, AGI+1 ou SPD+1 (25% cada).
    public static CharacterStats GenerateLevel1Stats()
    {
        var stats = new CharacterStats
        {
            maxHealth = BASE_HP,
            str       = BASE_STR,
            agility   = BASE_AGI,
            speed     = BASE_SPD
        };

        for (int i = 0; i < POOL; i++)
        {
            switch (Random.Range(0, 4))
            {
                case 0: stats.maxHealth += 5; break;
                case 1: stats.str++;           break;
                case 2: stats.agility++;       break;
                case 3: stats.speed++;         break;
            }
        }

        return stats;
    }
}
