export type DeckCardItem = {
  printingId: number;
  cardId?: number;
  cardName: string;
  quantityInDeck: number;
  cardType?: string | null;
  side?: string | null;
  setName?: string | null;
  cost?: number | null;
  aspects?: Array<{ name: string; count: number }> | null;
  isStarting?: boolean;
};

export const SWU_ASPECT_IMAGES: Record<string, string> = {
  Aggression: "/images/swu/aspects/SWH_Aspects_Aggression.png",
  Command: "/images/swu/aspects/SWH_Aspects_Command.png",
  Cunning: "/images/swu/aspects/SWH_Aspects_Cunning.png",
  Heroism: "/images/swu/aspects/SWH_Aspects_Heroism.png",
  Vigilance: "/images/swu/aspects/SWH_Aspects_Vigilance.png",
  Villainy: "/images/swu/aspects/SWH_Aspects_Villainy.png",
};

export const SWU_ASPECT_NAMES = [
  "Aggression",
  "Command",
  "Cunning",
  "Heroism",
  "Vigilance",
  "Villainy",
] as const;

/**
 * Aggregates all aspects provided by the Leaders and Base of a Star Wars: Unlimited deck.
 */
export function computeDeckAspects(deckCards: DeckCardItem[]): Record<string, number> {
  const leaderAndBaseCards = deckCards.filter(
    (c) =>
      c.quantityInDeck > 0 &&
      (c.cardType?.toLowerCase() === "leader" || c.cardType?.toLowerCase() === "base")
  );

  const aspectsMap: Record<string, number> = {};
  for (const card of leaderAndBaseCards) {
    if (!card.aspects) continue;
    for (const asp of card.aspects) {
      const normalized = asp.name.trim();
      const properName =
        SWU_ASPECT_NAMES.find((n) => n.toLowerCase() === normalized.toLowerCase()) || normalized;
      aspectsMap[properName] = (aspectsMap[properName] || 0) + (asp.count || 1);
    }
  }

  return aspectsMap;
}

/**
 * Calculates the aspect penalty (+2 cost per missing aspect symbol) for a SWU card.
 */
export function calculateAspectPenalty(
  cardAspects: Array<{ name: string; count: number }> | null | undefined,
  deckAspects: Record<string, number>,
  cardCost?: number | null
): {
  missingCount: number;
  penaltyCost: number;
  effectiveCost: number | null;
  isOutOfAspect: boolean;
} {
  if (!cardAspects || cardAspects.length === 0) {
    return {
      missingCount: 0,
      penaltyCost: 0,
      effectiveCost: cardCost ?? null,
      isOutOfAspect: false,
    };
  }

  let missingCount = 0;
  for (const asp of cardAspects) {
    const normalized = asp.name.trim();
    const properName =
      SWU_ASPECT_NAMES.find((n) => n.toLowerCase() === normalized.toLowerCase()) || normalized;
    const available = deckAspects[properName] || 0;
    const needed = asp.count || 1;
    if (needed > available) {
      missingCount += needed - available;
    }
  }

  const penaltyCost = missingCount * 2;
  const effectiveCost = cardCost != null ? cardCost + penaltyCost : null;

  return {
    missingCount,
    penaltyCost,
    effectiveCost,
    isOutOfAspect: missingCount > 0,
  };
}

/**
 * Checks if a card matches the deck's available aspects (0 penalty).
 */
export function isCardInAspect(
  cardAspects: Array<{ name: string; count: number }> | null | undefined,
  deckAspects: Record<string, number>
): boolean {
  const { missingCount } = calculateAspectPenalty(cardAspects, deckAspects);
  return missingCount === 0;
}

export type SwuValidationResult = {
  isValid: boolean;
  issues: string[];
  leaderCount: number;
  requiredLeaderCount: number;
  baseCount: number;
  requiredBaseCount: number;
  otherCount: number;
  minOtherCount: number;
  maxCopiesAllowed: number;
  copyViolations: Array<{ cardName: string; count: number; max: number }>;
};

/**
 * Validates a Star Wars: Unlimited deck against Premier or Twin Suns rules.
 */
export function validateSwuDeck(
  deckCards: DeckCardItem[],
  format: string = "Premier"
): SwuValidationResult {
  const isTwinSuns = format.trim().toLowerCase() === "twin suns";
  const requiredLeaderCount = isTwinSuns ? 2 : 1;
  const requiredBaseCount = 1;
  const minOtherCount = isTwinSuns ? 80 : 50;
  const maxCopiesAllowed = isTwinSuns ? 1 : 3;

  const leaders = deckCards.filter(
    (c) => c.quantityInDeck > 0 && c.cardType?.toLowerCase() === "leader"
  );
  const bases = deckCards.filter(
    (c) => c.quantityInDeck > 0 && c.cardType?.toLowerCase() === "base"
  );
  const others = deckCards.filter(
    (c) =>
      c.quantityInDeck > 0 &&
      c.cardType?.toLowerCase() !== "leader" &&
      c.cardType?.toLowerCase() !== "base"
  );

  const leaderCount = leaders.reduce((sum, c) => sum + c.quantityInDeck, 0);
  const baseCount = bases.reduce((sum, c) => sum + c.quantityInDeck, 0);
  const otherCount = others.reduce((sum, c) => sum + c.quantityInDeck, 0);

  const issues: string[] = [];

  if (leaderCount !== requiredLeaderCount) {
    issues.push(
      isTwinSuns
        ? `Twin Suns decks must have exactly 2 Leaders (currently ${leaderCount}).`
        : `Premier decks must have exactly 1 Leader (currently ${leaderCount}).`
    );
  }

  if (baseCount !== requiredBaseCount) {
    issues.push(`Decks must include exactly 1 Base (currently ${baseCount}).`);
  }

  if (otherCount < minOtherCount) {
    issues.push(
      `Must include a minimum of ${minOtherCount} other cards (currently ${otherCount}).`
    );
  }

  // Check copy counts grouped by Card Name / ID
  const countsByName: Record<string, number> = {};
  for (const c of deckCards) {
    if (c.quantityInDeck <= 0) continue;
    countsByName[c.cardName] = (countsByName[c.cardName] || 0) + c.quantityInDeck;
  }

  const copyViolations: Array<{ cardName: string; count: number; max: number }> = [];
  for (const [name, count] of Object.entries(countsByName)) {
    if (count > maxCopiesAllowed) {
      copyViolations.push({ cardName: name, count, max: maxCopiesAllowed });
      issues.push(
        `"${name}" exceeds the limit of ${maxCopiesAllowed} ${
          maxCopiesAllowed === 1 ? "copy" : "copies"
        } (${count} present).`
      );
    }
  }

  return {
    isValid: issues.length === 0,
    issues,
    leaderCount,
    requiredLeaderCount,
    baseCount,
    requiredBaseCount,
    otherCount,
    minOtherCount,
    maxCopiesAllowed,
    copyViolations,
  };
}

/**
 * Returns true if the card is a Star Wars CCG Defensive Shield.
 */
export function isDefensiveShield(card: { cardType?: string | null; cardName?: string }): boolean {
  const type = card.cardType?.trim().toLowerCase() || "";
  return type === "defensive shield" || type.includes("shield");
}

/**
 * Returns true if the card belongs to a Virtual or Demo Deck set.
 */
export function isVirtualCard(card: { setName?: string | null; cardName?: string }): boolean {
  const set = card.setName?.trim().toLowerCase() || "";
  return set.includes("virtual") || set === "demo deck";
}

export type SwccgValidationResult = {
  isValid: boolean;
  issues: string[];
  mainDeckCount: number;
  defensiveShieldCount: number;
  startingCardsCount: number;
  sideViolations: string[];
  virtualViolations: string[];
  shieldViolations: string[];
};

/**
 * Validates a Star Wars CCG deck against Standard or Decipher Only rules.
 */
export function validateSwccgDeck(
  deckCards: DeckCardItem[],
  format: string = "Standard",
  selectedSide?: string | null
): SwccgValidationResult {
  const isDecipherOnly = format.trim().toLowerCase() === "decipher only";
  const issues: string[] = [];

  const defensiveShields = deckCards.filter((c) => c.quantityInDeck > 0 && isDefensiveShield(c));
  const mainCards = deckCards.filter((c) => c.quantityInDeck > 0 && !isDefensiveShield(c));

  const mainDeckCount = mainCards.reduce((sum, c) => sum + c.quantityInDeck, 0);
  const defensiveShieldCount = defensiveShields.reduce((sum, c) => sum + c.quantityInDeck, 0);
  const startingCardsCount = deckCards.filter((c) => c.quantityInDeck > 0 && c.isStarting).length;

  if (mainDeckCount < 60) {
    issues.push(
      `Deck must contain a minimum of 60 cards (currently ${mainDeckCount}, shields excluded).`
    );
  }

  // Side validation
  const sideViolations: string[] = [];
  if (selectedSide) {
    const expectedSide = selectedSide.trim().toLowerCase();
    for (const card of deckCards) {
      if (card.quantityInDeck > 0 && card.side) {
        if (card.side.trim().toLowerCase() !== expectedSide) {
          sideViolations.push(card.cardName);
        }
      }
    }
    if (sideViolations.length > 0) {
      issues.push(
        `Contains ${sideViolations.length} card(s) that do not match the ${selectedSide} side.`
      );
    }
  }

  // Decipher Only validation: no virtual sets
  const virtualViolations: string[] = [];
  if (isDecipherOnly) {
    for (const card of deckCards) {
      if (card.quantityInDeck > 0 && isVirtualCard(card)) {
        virtualViolations.push(card.cardName);
      }
    }
    if (virtualViolations.length > 0) {
      issues.push(
        `Decipher Only format does not allow Virtual set cards (${virtualViolations.length} found).`
      );
    }
  }

  // Defensive shields: max 1 copy per distinct shield
  const shieldViolations: string[] = [];
  for (const shield of defensiveShields) {
    if (shield.quantityInDeck > 1) {
      shieldViolations.push(shield.cardName);
      issues.push(
        `Defensive Shield "${shield.cardName}" exceeds the limit of 1 copy (${shield.quantityInDeck} present).`
      );
    }
  }

  return {
    isValid: issues.length === 0,
    issues,
    mainDeckCount,
    defensiveShieldCount,
    startingCardsCount,
    sideViolations,
    virtualViolations,
    shieldViolations,
  };
}
