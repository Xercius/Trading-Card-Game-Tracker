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
  quantitySideboard?: number;
  quantityIdea?: number;
  isCommander?: boolean;
  colorIdentity?: string[] | null;
  canBeCommander?: boolean;
  isBasicLand?: boolean;
  hasUnlimitedCopies?: boolean;
  maxAllowedCopies?: number | null;
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

export type GameCapabilities = {
  isSwu: boolean;
  isSwccg: boolean;
  isMtg: boolean;
  isPokemon: boolean;
  hasLeadersAndBase: boolean;
  hasStartingCards: boolean;
  hasDefensiveShields: boolean;
  hasCommander: boolean;
  hasSideboard: boolean;
};

/**
 * Returns game- and format-scoped deck building capabilities so UI zones and role controls
 * only appear where relevant.
 */
export function getGameCapabilities(game: string, format?: string | null): GameCapabilities {
  const g = (game || "").trim().toLowerCase();
  const f = (format || "").trim().toLowerCase();
  const isSwu = g.includes("unlimited");
  const isSwccg = g.includes("ccg");
  const isMtg = g.includes("magic");
  const isPokemon = g.includes("pokemon");
  const isCommander = isMtg && f === "commander";

  return {
    isSwu,
    isSwccg,
    isMtg,
    isPokemon,
    hasLeadersAndBase: isSwu,
    hasStartingCards: isSwccg,
    hasDefensiveShields: isSwccg,
    hasCommander: isCommander,
    hasSideboard: isMtg && !isCommander,
  };
}

export type DeckZoneKind = "main" | "sideboard" | "ideas";

export type DeckZoneGroup<T extends DeckCardItem = DeckCardItem> = {
  key: string;
  label: string;
  zone: DeckZoneKind;
  cards: T[];
  count: number;
  maxCount?: number;
};

function appendTypedGroups<T extends DeckCardItem>(
  groups: DeckZoneGroup<T>[],
  cards: T[],
  fallbackType: string
) {
  const byType = new Map<string, T[]>();
  for (const card of cards) {
    const type = card.cardType || fallbackType;
    const list = byType.get(type);
    if (list) list.push(card);
    else byType.set(type, [card]);
  }
  for (const [type, list] of byType.entries()) {
    groups.push({
      key: `type:${type.toLowerCase()}`,
      label: type,
      zone: "main",
      cards: list,
      count: list.reduce((sum, c) => sum + c.quantityInDeck, 0),
    });
  }
}

function getPokemonBucket(cardType?: string | null): string {
  const lower = (cardType || "").trim().toLowerCase();
  if (lower.includes("pokemon") || lower.includes("pokémon")) return "Pokémon";
  if (lower.includes("trainer")) return "Trainer";
  if (lower.includes("energy")) return "Energy";
  return cardType || "Other";
}

/**
 * Groups deck cards into ordered, game-appropriate zone sections (Leaders/Base, Commander,
 * Starting Cards, Defensive Shields, Main Deck categories, Sideboard, and Ideas).
 */
export function groupDeckCardsByZone<T extends DeckCardItem>(
  deckCards: T[],
  game: string,
  format?: string | null
): DeckZoneGroup<T>[] {
  const caps = getGameCapabilities(game, format);
  const mainCards = deckCards.filter((c) => c.quantityInDeck > 0);
  const groups: DeckZoneGroup<T>[] = [];

  if (caps.isSwccg) {
    const starting = mainCards.filter((c) => c.isStarting);
    const shields = mainCards.filter((c) => !c.isStarting && isDefensiveShield(c));
    const others = mainCards.filter((c) => !c.isStarting && !isDefensiveShield(c));
    if (starting.length > 0) {
      groups.push({
        key: "starting",
        label: "⭐ Starting Cards",
        zone: "main",
        cards: starting,
        count: starting.reduce((s, c) => s + c.quantityInDeck, 0),
      });
    }
    if (shields.length > 0) {
      groups.push({
        key: "shields",
        label: "🛡️ Defensive Shields",
        zone: "main",
        cards: shields,
        count: shields.reduce((s, c) => s + c.quantityInDeck, 0),
      });
    }
    appendTypedGroups(groups, others, "Other");
  } else if (caps.isMtg) {
    if (caps.hasCommander) {
      const commanders = mainCards.filter((c) => c.isCommander);
      if (commanders.length > 0) {
        groups.push({
          key: "commander",
          label: "★ Commander",
          zone: "main",
          cards: commanders,
          count: commanders.reduce((s, c) => s + c.quantityInDeck, 0),
        });
      }
    }
    const nonCommanders = mainCards.filter((c) => !(caps.hasCommander && c.isCommander));
    appendTypedGroups(groups, nonCommanders, "Other");

    const sideboard = deckCards.filter((c) => (c.quantitySideboard ?? 0) > 0);
    if (sideboard.length > 0) {
      groups.push({
        key: "sideboard",
        label: "Sideboard",
        zone: "sideboard",
        cards: sideboard,
        count: sideboard.reduce((s, c) => s + (c.quantitySideboard ?? 0), 0),
        maxCount: caps.hasSideboard ? 15 : 0,
      });
    }
  } else if (caps.isSwu) {
    const leaders = mainCards.filter((c) => c.cardType?.toLowerCase() === "leader");
    const bases = mainCards.filter((c) => c.cardType?.toLowerCase() === "base");
    const others = mainCards.filter(
      (c) => c.cardType?.toLowerCase() !== "leader" && c.cardType?.toLowerCase() !== "base"
    );
    if (leaders.length > 0) {
      groups.push({
        key: "leaders",
        label: "Leaders",
        zone: "main",
        cards: leaders,
        count: leaders.reduce((s, c) => s + c.quantityInDeck, 0),
      });
    }
    if (bases.length > 0) {
      groups.push({
        key: "base",
        label: "Base",
        zone: "main",
        cards: bases,
        count: bases.reduce((s, c) => s + c.quantityInDeck, 0),
      });
    }
    appendTypedGroups(groups, others, "Units & Events");
  } else if (caps.isPokemon) {
    const buckets = new Map<string, T[]>();
    for (const card of mainCards) {
      const bucket = getPokemonBucket(card.cardType);
      const list = buckets.get(bucket);
      if (list) list.push(card);
      else buckets.set(bucket, [card]);
    }
    const order = ["Pokémon", "Trainer", "Energy"];
    for (const name of order) {
      const list = buckets.get(name);
      if (list && list.length > 0) {
        groups.push({
          key: `pokemon:${name.toLowerCase()}`,
          label: name,
          zone: "main",
          cards: list,
          count: list.reduce((s, c) => s + c.quantityInDeck, 0),
        });
        buckets.delete(name);
      }
    }
    for (const [name, list] of buckets.entries()) {
      groups.push({
        key: `pokemon:${name.toLowerCase()}`,
        label: name,
        zone: "main",
        cards: list,
        count: list.reduce((s, c) => s + c.quantityInDeck, 0),
      });
    }
  } else if (mainCards.length > 0) {
    groups.push({
      key: "main",
      label: "Main Deck",
      zone: "main",
      cards: mainCards,
      count: mainCards.reduce((s, c) => s + c.quantityInDeck, 0),
    });
  }

  const ideas = deckCards.filter((c) => (c.quantityIdea ?? 0) > 0);
  if (ideas.length > 0) {
    groups.push({
      key: "ideas",
      label: "Ideas",
      zone: "ideas",
      cards: ideas,
      count: ideas.reduce((s, c) => s + (c.quantityIdea ?? 0), 0),
    });
  }

  return groups;
}

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

function formatZoneBreakdown(main: number, sideboard: number, ideas: number): string {
  if (sideboard === 0 && ideas === 0) return "";
  const parts: string[] = [];
  if (main > 0) parts.push(`${main} Main`);
  if (sideboard > 0) parts.push(`${sideboard} Sideboard`);
  if (ideas > 0) parts.push(`${ideas} Ideas`);
  return parts.length > 0 ? `: ${parts.join(", ")}` : "";
}
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

  // Check copy counts grouped by Card Name / ID (across Deck + Sideboard + Ideas)
  const countsByName: Record<string, { main: number; sb: number; ideas: number; total: number }> =
    {};
  for (const c of deckCards) {
    const main = c.quantityInDeck;
    const sb = c.quantitySideboard ?? 0;
    const ideas = c.quantityIdea ?? 0;
    const total = main + sb + ideas;
    if (total <= 0) continue;
    const existing = countsByName[c.cardName];
    countsByName[c.cardName] = {
      main: (existing?.main ?? 0) + main,
      sb: (existing?.sb ?? 0) + sb,
      ideas: (existing?.ideas ?? 0) + ideas,
      total: (existing?.total ?? 0) + total,
    };
  }

  const copyViolations: Array<{ cardName: string; count: number; max: number }> = [];
  for (const [name, info] of Object.entries(countsByName)) {
    if (info.total > maxCopiesAllowed) {
      copyViolations.push({ cardName: name, count: info.total, max: maxCopiesAllowed });
      const breakdown = formatZoneBreakdown(info.main, info.sb, info.ideas);
      issues.push(
        `"${name}" exceeds the limit of ${maxCopiesAllowed} ${
          maxCopiesAllowed === 1 ? "copy" : "copies"
        } (${info.total} present${breakdown}).`
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

export type MtgValidationResult = {
  isValid: boolean;
  issues: string[];
  mainDeckCount: number;
  sideboardCount: number;
  commanderCount: number;
  commanderColorIdentity: string[];
  colorIdentityViolations: string[];
  copyViolations: Array<{ cardName: string; count: number; max: number }>;
};

/**
 * Checks whether a card's MTG color identity is a subset of the Commander's color identity.
 */
export function isWithinColorIdentity(
  cardIdentity: string[] | null | undefined,
  commanderIdentity: string[] | null | undefined
): boolean {
  if (!cardIdentity || cardIdentity.length === 0) return true;
  const allowed = new Set((commanderIdentity ?? []).map((c) => c.trim().toUpperCase()));
  return cardIdentity.every((c) => allowed.has(c.trim().toUpperCase()));
}

/**
 * Validates a Magic: The Gathering deck against Constructed or Commander rules.
 */
export function validateMtgDeck(
  deckCards: DeckCardItem[],
  format: string = "Constructed"
): MtgValidationResult {
  const isCommander = format.trim().toLowerCase() === "commander";
  const active = deckCards.filter((c) => c.quantityInDeck > 0 || (c.quantitySideboard ?? 0) > 0);
  const mainDeckCount = active.reduce((sum, c) => sum + c.quantityInDeck, 0);
  const sideboardCount = active.reduce((sum, c) => sum + (c.quantitySideboard ?? 0), 0);
  const commanders = active.filter((c) => c.isCommander && c.quantityInDeck > 0);
  const commanderCount = commanders.reduce((sum, c) => sum + c.quantityInDeck, 0);
  const commanderColorIdentity = commanderCount === 1 ? (commanders[0].colorIdentity ?? []) : [];

  const issues: string[] = [];
  const colorIdentityViolations: string[] = [];
  const copyViolations: Array<{ cardName: string; count: number; max: number }> = [];

  if (isCommander) {
    if (commanderCount !== 1) {
      issues.push(
        `Commander decks must have exactly 1 designated Commander (currently ${commanderCount}).`
      );
    } else if (commanders[0].canBeCommander === false) {
      issues.push(`"${commanders[0].cardName}" is not eligible to be a Commander.`);
    }

    if (sideboardCount > 0) {
      issues.push(`Commander decks cannot have a sideboard (currently ${sideboardCount} cards).`);
    }

    if (mainDeckCount < 100) {
      issues.push(
        `Commander decks must contain at least 100 cards including the Commander (currently ${mainDeckCount}).`
      );
    }

    const countsByName: Record<
      string,
      {
        main: number;
        sb: number;
        ideas: number;
        count: number;
        isUnlimited: boolean;
        maxOverride: number | null;
      }
    > = {};
    for (const c of deckCards) {
      const main = c.quantityInDeck;
      const sb = c.quantitySideboard ?? 0;
      const ideas = c.quantityIdea ?? 0;
      const total = main + sb + ideas;
      if (total <= 0) continue;
      const existing = countsByName[c.cardName];
      countsByName[c.cardName] = {
        main: (existing?.main ?? 0) + main,
        sb: (existing?.sb ?? 0) + sb,
        ideas: (existing?.ideas ?? 0) + ideas,
        count: (existing?.count ?? 0) + total,
        isUnlimited: Boolean(existing?.isUnlimited || c.isBasicLand || c.hasUnlimitedCopies),
        maxOverride: existing?.maxOverride ?? c.maxAllowedCopies ?? null,
      };
    }

    for (const [name, info] of Object.entries(countsByName)) {
      if (info.isUnlimited) continue;
      const limit = info.maxOverride ?? 1;
      if (info.count > limit) {
        copyViolations.push({ cardName: name, count: info.count, max: limit });
        const breakdown = formatZoneBreakdown(info.main, info.sb, info.ideas);
        issues.push(
          `"${name}" exceeds the limit of ${limit} ${limit === 1 ? "copy" : "copies"} (${info.count} present${breakdown}).`
        );
      }
    }

    if (commanderCount === 1) {
      const allowedLabel =
        commanderColorIdentity.length === 0 ? "Colorless" : commanderColorIdentity.join("/");
      for (const c of active) {
        if (c.quantityInDeck <= 0 || c.isCommander) continue;
        if (!isWithinColorIdentity(c.colorIdentity, commanderColorIdentity)) {
          colorIdentityViolations.push(c.cardName);
          issues.push(
            `"${c.cardName}" (${(c.colorIdentity ?? []).join("/")}) is outside the Commander's color identity (${allowedLabel}).`
          );
        }
      }
    }
  } else {
    if (mainDeckCount < 60) {
      issues.push(
        `Constructed decks must contain at least 60 cards in the main deck (currently ${mainDeckCount}).`
      );
    }
    if (sideboardCount > 15) {
      issues.push(
        `Constructed sideboards may contain at most 15 cards (currently ${sideboardCount}).`
      );
    }

    const countsByName: Record<
      string,
      {
        main: number;
        sb: number;
        ideas: number;
        count: number;
        isUnlimited: boolean;
        maxOverride: number | null;
      }
    > = {};
    for (const c of deckCards) {
      const main = c.quantityInDeck;
      const sb = c.quantitySideboard ?? 0;
      const ideas = c.quantityIdea ?? 0;
      const total = main + sb + ideas;
      if (total <= 0) continue;
      const existing = countsByName[c.cardName];
      countsByName[c.cardName] = {
        main: (existing?.main ?? 0) + main,
        sb: (existing?.sb ?? 0) + sb,
        ideas: (existing?.ideas ?? 0) + ideas,
        count: (existing?.count ?? 0) + total,
        isUnlimited: Boolean(existing?.isUnlimited || c.isBasicLand || c.hasUnlimitedCopies),
        maxOverride: existing?.maxOverride ?? c.maxAllowedCopies ?? null,
      };
    }

    for (const [name, info] of Object.entries(countsByName)) {
      if (info.isUnlimited) continue;
      const limit = info.maxOverride ?? 4;
      if (info.count > limit) {
        copyViolations.push({ cardName: name, count: info.count, max: limit });
        const breakdown = formatZoneBreakdown(info.main, info.sb, info.ideas);
        issues.push(
          `"${name}" exceeds the limit of ${limit} ${limit === 1 ? "copy" : "copies"} (${info.count} present${breakdown}).`
        );
      }
    }
  }

  return {
    isValid: issues.length === 0,
    issues,
    mainDeckCount,
    sideboardCount,
    commanderCount,
    commanderColorIdentity,
    colorIdentityViolations,
    copyViolations,
  };
}
