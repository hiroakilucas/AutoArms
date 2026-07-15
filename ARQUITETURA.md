# AutoArms — Regras Arquiteturais Permanentes

Este arquivo guarda decisões de arquitetura que são **regra fixa do projeto**, não anotação de
sessão — o que está aqui vale pra qualquer implementação futura, mesmo que o sistema descrito
ainda não exista. Ler antes de desenhar qualquer sistema que envolva dinheiro real, contas de
usuário ou persistência online.

## Moeda premium (diamantes) — regra de segurança inegociável

Contexto: o sistema de diamantes (Fase 4 — Monetização, ver `ROADMAP_FUTURO.md`) ainda não foi
implementado. Esta regra foi registrada **antes** da implementação, a pedido explícito do
usuário, justamente pra nascer certa desde o desenho inicial — decidida durante o planejamento do
backend de contas/save na nuvem (2026-07-14), quando ficou claro que o modelo de dados do
Firestore para progressão de personagem precisava já prever isso.

**Regras, sem exceção:**

1. **O campo de diamantes nunca pode ter permissão de escrita direta pelo cliente no Firestore.**
   Regra de segurança: `allow write: if false;` no documento/campo de moeda — mesmo pro próprio
   dono da conta. Não existe caminho em que o SDK client-side grava um novo saldo de diamante
   diretamente.
2. **Toda alteração de diamante passa obrigatoriamente por uma Cloud Function** — ganhar por
   vitória, gastar em reroll de level-up, reset de build, comprar pacote, o que for. O cliente
   nunca envia "novo saldo": envia a **intenção** (ex.: "gastar 50 diamantes no reroll do slot X"),
   e a function calcula o resultado e escreve.
3. **Compra com dinheiro real exige validação server-side do recibo** (Google Play Billing /
   Apple StoreKit) dentro da Cloud Function, **antes** de creditar qualquer diamante. Nunca
   confiar no cliente dizer "comprei o pacote X".
4. **Consequência**: diamantes precisam de Cloud Functions **desde o dia 1** da implementação da
   monetização — não dá pra esperar a Fase 8 (anti-cheat/validação server-side geral) pra isso
   especificamente, porque aqui envolve dinheiro real. O resto do jogo (combate, level, skills)
   pode continuar no modelo "cliente confiável" por mais tempo — moeda premium não.

**Não implementado ainda** (correto deixar pra quando a Fase 4 for de fato construída) — só a
regra em si é fixa e não deve ser esquecida/relaxada quando esse dia chegar.

## Regras de segurança do Firestore — `users/{uid}/characters`

Implementado na Fatia 3 do plano de contas/save na nuvem (2026-07-15). `allow write: if
request.auth.uid == uid` sozinho só verifica QUEM escreve, não O QUE está sendo escrito —
deixaria qualquer dono mandar `str: 9999`/`level: 9999` direto pelo SDK client-side (pedido
explícito do usuário, feito antes de aprovar o plano original). As regras abaixo validam formato
e faixa também, amarradas ao próprio `level` do documento (não um teto fixo solto) — margem
generosa de propósito, só pra barrar valor grosseiramente fora da curva de progressão real
(level-up dá no máximo +8 HP / +2 STR/AGI/SPD por escolha, ver `CombatResultPanel.ApplyBonus`).
Os valores exatos das constantes (`20 +`, `4 *`, `9999`, os tetos de array) são um ponto de
partida — ajustar se o balanceamento mudar, mas a regra em si (amarrada ao level, com teto de
array) deve continuar existindo.

Colar em **Firebase Console > Firestore Database > Regras** (substitui o "modo de teste" usado
até aqui):

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{uid} {
      allow read, write: if request.auth != null && request.auth.uid == uid;

      match /characters/{characterId} {
        allow read: if request.auth != null && request.auth.uid == uid;
        allow write: if request.auth != null && request.auth.uid == uid
          && request.resource.data.level is int
          && request.resource.data.level >= 1
          && request.resource.data.level <= 9999
          && request.resource.data.str is number
          && request.resource.data.str <= 20 + 4 * request.resource.data.level
          && request.resource.data.agility is number
          && request.resource.data.agility <= 20 + 4 * request.resource.data.level
          && request.resource.data.speed is number
          && request.resource.data.speed <= 20 + 4 * request.resource.data.level
          && request.resource.data.maxHealth is number
          && request.resource.data.maxHealth <= 80 + 16 * request.resource.data.level
          && request.resource.data.weapons is list
          && request.resource.data.weapons.size() <= 50
          && request.resource.data.skills is list
          && request.resource.data.skills.size() <= 100;
      }

      match /matchHistory/{opponentId} {
        allow read, write: if request.auth != null && request.auth.uid == uid;
      }
    }

    // opponents_index (Fatia 5+, ainda não implementada): qualquer usuário autenticado pode ler
    // (é a superfície pública de busca de adversário), só o dono pode escrever o próprio doc.
    match /opponents_index/{characterId} {
      allow read: if request.auth != null;
      allow create: if request.auth != null && request.auth.uid == request.resource.data.ownerUid;
      allow update, delete: if request.auth != null && request.auth.uid == resource.data.ownerUid;
    }
  }
}
```

Isso não substitui validação server-side de verdade (Cloud Function, Fase 8, correto deixar pra
depois) — só fecha o caso mais grosseiro de edição direta de documento sem precisar de Cloud
Function agora.
