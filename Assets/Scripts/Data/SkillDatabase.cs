using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SkillDatabase", menuName = "Game/Skill Database", order = 102)]
public class SkillDatabase : ScriptableObject
{
    public List<SkillData> skills = new List<SkillData>();
}
