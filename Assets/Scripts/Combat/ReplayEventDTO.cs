using System.Collections.Generic;

// Formato de replay salvo no Firestore — deliberadamente DESACOPLADO de CombatEvent (a classe de
// runtime usada por CombatSimulator/CombatPlayer). CombatEvent muda com frequência real (todo
// rebalance/skill nova mexe nela — ver histórico de mudanças em CombatSimulator.cs) e se
// serializássemos ela direto, qualquer campo novo passaria a fazer parte do formato salvo sem
// ninguém decidir isso conscientemente, e replays antigos podiam quebrar silenciosamente na
// leitura. Este arquivo (+ CombatEventReplayConverter) é o único lugar que precisa mudar quando um
// evento novo precisar persistir um campo novo.
//
// `type` é gravado como STRING (nome do enum), não o int subjacente que CombatEventType usaria
// por padrão — o enum já tem ~50 valores e cresce a cada Super/skill nova; uma inserção no meio
// dele mudaria os índices numéricos de todos os valores seguintes e corromperia silenciosamente
// todo replay já salvo se o tipo fosse persistido como número.
//
// A leveza de fato (só os campos relevantes por tipo de evento) acontece na camada de
// serialização (ReplayEventDTOMap.ToMap), não aqui — esta classe tem o espelho completo dos
// campos de CombatEvent porque qualquer um deles pode ser necessário pra CombatPlayer reproduzir
// a animação certa; ToMap só grava no Firestore os campos que fogem do valor-padrão daquele
// evento específico.
[System.Serializable]
public class ReplayEventDTO
{
    public string type;
    public int  playerIndex;
    public int  targetIndex;
    public int  damage;
    public bool isCrit;
    public bool isCombo;
    public bool isThrow;
    public bool isRetaliation;
    public bool isDodged;
    public bool isBlocked;
    public bool isFierceBrute;
    public int  newHp;
    public int  maxHp;
    public int  extraActions;
    public int  healAmount;
    public int  pulseCount;
    public int  newDefenderHp;
    public int  newAttackerHp;
    public string weaponName;

    public List<string> ffWeapons;
    public List<int>    ffDamages;
    public List<int>    ffHpAfter;

    public List<int> bombTargets;
    public List<int> bombTargetDamages;
    public List<int> bombTargetHp;
    public List<int> netFreedTargets;

    public List<int> bombPetIndexes;
    public List<int> bombPetHp;

    public int  petIndex = -1;
    public int  targetPetIndex = -1;
    public bool targetIsPet;
    public int  newTargetHp;
    public int  newTargetMaxHp;
    public bool shieldIntercept;
    public bool petShieldAbsorb;
}
