# AutoArms — Game Vision

## Game Vision

AutoArms é inspirado no My Brute (jogo browser francês de 2008 da Motion Twin).
Referência jogável: https://brute.eternaltwin.org/

### Conceito central
- Combate automático entre dois personagens — o jogador não controla as ações, apenas monta o personagem
- Progressão por XP e level: ao subir de nível, o jogador escolhe 1 bônus (atributo, skill ou arma)
- Personagem com stats aleatórios ao criar: vida, força, agilidade, velocidade
- Limite de batalhas por dia (energia) — incentiva retorno diário

### Mecânicas de combate inspiradas no My Brute
- Combo: chance de atacar mais de uma vez seguida
- Crítico: chance de causar dano dobrado
- Esquiva: chance de desviar do ataque baseada em agilidade
- Parry: chance de bloquear o dano com arma ou escudo
- Knockback: ao tomar hit, personagem recua levemente
- Jogar arma: chance de arremessar a arma no adversário
- Derrubar arma: chance de desarmar o adversário no golpe
- Troca de arma: personagem troca de arma durante o combate

### Tipos de arma inspirados no My Brute (26 armas no original)
- Fast: maior chance de ataque extra, mais difícil de esquivar
- Slow: menor chance de ataque duplo e bloqueio
- Heavy: alto dano, penalidade de velocidade
- Thrown: pode ser arremessada no adversário
- Block: chance de bloquear dano recebido

### Pets planejados (roster original, substituído na implementação — ver Fase 3/Combat Systems)
- Cachorro — meat shield inicial, combatente fraco
- Lobo — versão mais forte do cachorro
- Águia — ataque à distância
- Urso — mais poderoso, alta vida própria

**Implementado de fato**: Rato (Mouse, equivalente ao Cachorro), Macaco (Monkey, equivalente
ao Lobo/uma versão mais ágil), Javali (Boar, equivalente ao Urso) — os assets de arte
(`Assets/Data/UI/Pets/{Boar,Monkey,Mouse}/`) já existiam nesse roster antes da implementação
de código, então o roster real seguiu os assets disponíveis em vez do texto original. Sem
Águia (ataque à distância) — nenhum dos 3 pets ataca a distância.

### Progressão e XP
- Vitória: +3 XP
- Derrota: +1 XP
- XP necessário por nível: level × 20 (ex: level 2→3 = 40 XP)
- Ao subir de nível: escolher 1 entre 3 opções sorteadas (atributo, skill ou arma)

### Monetização planejada
- Diamantes: moeda premium
- Energia: comprar recargas para lutar mais vezes
- Personagens: desbloquear com diamante

### Código Fonte de Referência
- LaBrute (remake open source do My Brute): https://github.com/Zenoo/labrute
- Pasta de lógica de combate: core/src/
- IMPORTANTE: Licença PolyForm Noncommercial — estudar lógica apenas, não copiar código
- Quando implementar uma skill ou mecânica, consultar o repositório para entender a lógica original

