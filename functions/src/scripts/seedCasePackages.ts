/**
 * Script um-tiro (Admin SDK) pra popular casePackages/ com o catálogo estático da Loja —
 * MONETIZACAO.md seções 5 e 7. Não roda como parte do app nem da function; o desenvolvedor
 * executa manualmente (`npm run seed`, dentro de functions/) sempre que os pacotes mudarem
 * (preço, limite, odds). Precisa de `GOOGLE_APPLICATION_CREDENTIALS` apontando pra uma service
 * account do projeto autoarms-c248f, OU rodar autenticado via `gcloud auth application-default
 * login` antes — não requer estar dentro do runtime de uma Cloud Function.
 */
import * as admin from "firebase-admin";
import { CasePackageDoc } from "../casePackageTypes";

admin.initializeApp();
const db = admin.firestore();

// CharacterRarity (int): 0 Normal, 1 Uncommon, 2 Rare, 3 Legendary, 4 Immortal.
const RARITY_RARE = 2;
const RARITY_LEGENDARY = 3;
const RARITY_IMMORTAL = 4;

const packages: CasePackageDoc[] = [
  {
    packageId: "case_rare",
    isRarityLocked: true,
    rarityTier: RARITY_RARE,
    cashPrice: 99.0,
    currencyCost: 0,
    purchaseLimitPerPlayer: 10,
    tierWeights: null,
  },
  {
    packageId: "case_legendary",
    isRarityLocked: true,
    rarityTier: RARITY_LEGENDARY,
    cashPrice: 199.0,
    currencyCost: 0,
    purchaseLimitPerPlayer: 3,
    tierWeights: null,
  },
  {
    packageId: "case_immortal",
    isRarityLocked: true,
    rarityTier: RARITY_IMMORTAL,
    cashPrice: 249.0,
    currencyCost: 0,
    purchaseLimitPerPlayer: 1,
    tierWeights: null,
  },
  // "case_moeda_geral" (Case Geral, diamante) removido (2026-07-26) — substituído por "Próximo
  // Personagem" (Coins, functions/src/purchaseNextCharacter.ts, preço progressivo por jogador em
  // vez de preço fixo por pacote — não cabe no schema `casePackages` de preço único). O doc antigo
  // em casePackages/case_moeda_geral fica órfão no Firestore (inofensivo, nada mais o lê) — não
  // precisa ser apagado manualmente.
];

async function main() {
  const batch = db.batch();
  for (const pkg of packages) {
    batch.set(db.collection("casePackages").doc(pkg.packageId), pkg);
  }
  await batch.commit();
  console.log(`Seed concluído — ${packages.length} pacotes gravados em casePackages/.`);
}

main().catch((err) => {
  console.error("Falha no seed de casePackages:", err);
  process.exit(1);
});
