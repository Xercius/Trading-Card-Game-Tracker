import { describe, expect, it } from "vitest";
import {
  computeDeckAspects,
  calculateAspectPenalty,
  getGameCapabilities,
  groupDeckCardsByZone,
  isCardInAspect,
  validateSwuDeck,
  validateSwccgDeck,
  validateMtgDeck,
  isWithinColorIdentity,
  isDefensiveShield,
  isVirtualCard,
} from "../deckRules";

describe("deckRules - Star Wars Unlimited", () => {
  it("computes combined aspects from Leader and Base", () => {
    const deckCards = [
      {
        printingId: 1,
        cardName: "Sabine Wren",
        quantityInDeck: 1,
        cardType: "Leader",
        aspects: [
          { name: "Aggression", count: 1 },
          { name: "Heroism", count: 1 },
        ],
      },
      {
        printingId: 2,
        cardName: "Chopper Base",
        quantityInDeck: 1,
        cardType: "Base",
        aspects: [{ name: "Cunning", count: 1 }],
      },
    ];

    const aspects = computeDeckAspects(deckCards);
    expect(aspects).toEqual({
      Aggression: 1,
      Heroism: 1,
      Cunning: 1,
    });
  });

  it("calculates aspect penalty of +2 cost per missing aspect symbol", () => {
    const deckAspects = { Aggression: 1, Villainy: 1, Cunning: 1 };

    // 1. In-aspect card: Aggression 1, Cunning 1 -> +0
    const inAspect = calculateAspectPenalty(
      [
        { name: "Aggression", count: 1 },
        { name: "Cunning", count: 1 },
      ],
      deckAspects,
      3
    );
    expect(inAspect.missingCount).toBe(0);
    expect(inAspect.penaltyCost).toBe(0);
    expect(inAspect.effectiveCost).toBe(3);
    expect(inAspect.isOutOfAspect).toBe(false);

    // 2. Out-of-aspect card: Vigilance 1 -> +2 cost
    const vigilanceCard = calculateAspectPenalty([{ name: "Vigilance", count: 1 }], deckAspects, 3);
    expect(vigilanceCard.missingCount).toBe(1);
    expect(vigilanceCard.penaltyCost).toBe(2);
    expect(vigilanceCard.effectiveCost).toBe(5);
    expect(vigilanceCard.isOutOfAspect).toBe(true);

    // 3. Double-aspect card when deck only has 1: Cunning 2 -> +2 cost
    const doubleCunning = calculateAspectPenalty([{ name: "Cunning", count: 2 }], deckAspects, 4);
    expect(doubleCunning.missingCount).toBe(1);
    expect(doubleCunning.penaltyCost).toBe(2);
    expect(doubleCunning.effectiveCost).toBe(6);

    // 4. Two missing aspects: Heroism 1 + Command 1 -> +4 cost
    const heroismCommand = calculateAspectPenalty(
      [
        { name: "Heroism", count: 1 },
        { name: "Command", count: 1 },
      ],
      deckAspects,
      2
    );
    expect(heroismCommand.missingCount).toBe(2);
    expect(heroismCommand.penaltyCost).toBe(4);
    expect(heroismCommand.effectiveCost).toBe(6);

    // 5. Neutral card: no aspects -> +0 cost
    const neutralCard = calculateAspectPenalty([], deckAspects, 1);
    expect(neutralCard.missingCount).toBe(0);
    expect(neutralCard.penaltyCost).toBe(0);
    expect(neutralCard.effectiveCost).toBe(1);
  });

  it("validates Premier deck rules (1 Leader, 1 Base, min 50 other cards, max 3 copies)", () => {
    const validCards = [
      { printingId: 1, cardName: "Leader 1", quantityInDeck: 1, cardType: "Leader" },
      { printingId: 2, cardName: "Base 1", quantityInDeck: 1, cardType: "Base" },
      ...Array.from({ length: 50 }, (_, i) => ({
        printingId: 10 + i,
        cardName: `Card ${i}`,
        quantityInDeck: 1,
        cardType: "Unit",
      })),
    ];

    const validResult = validateSwuDeck(validCards, "Premier");
    expect(validResult.isValid).toBe(true);
    expect(validResult.issues).toHaveLength(0);

    // Violations
    const invalidCards = [
      { printingId: 1, cardName: "Leader 1", quantityInDeck: 1, cardType: "Leader" },
      { printingId: 2, cardName: "Leader 2", quantityInDeck: 1, cardType: "Leader" }, // 2 leaders in Premier
      // 0 base
      { printingId: 10, cardName: "Over-copied Card", quantityInDeck: 4, cardType: "Unit" }, // > 3 copies
      ...Array.from({ length: 20 }, (_, i) => ({
        printingId: 20 + i,
        cardName: `Unit ${i}`,
        quantityInDeck: 1,
        cardType: "Unit",
      })), // only 24 other cards
    ];

    const invalidResult = validateSwuDeck(invalidCards, "Premier");
    expect(invalidResult.isValid).toBe(false);
    expect(invalidResult.issues.some((i) => i.includes("exactly 1 Leader"))).toBe(true);
    expect(invalidResult.issues.some((i) => i.includes("exactly 1 Base"))).toBe(true);
    expect(invalidResult.issues.some((i) => i.includes("minimum of 50 other cards"))).toBe(true);
    expect(invalidResult.issues.some((i) => i.includes("exceeds the limit of 3 copies"))).toBe(
      true
    );
  });

  it("validates Twin Suns deck rules (2 Leaders, 1 Base, min 80 other cards, singleton max 1 copy)", () => {
    const validTwinSunsCards = [
      { printingId: 1, cardName: "Leader 1", quantityInDeck: 1, cardType: "Leader" },
      { printingId: 2, cardName: "Leader 2", quantityInDeck: 1, cardType: "Leader" },
      { printingId: 3, cardName: "Base 1", quantityInDeck: 1, cardType: "Base" },
      ...Array.from({ length: 80 }, (_, i) => ({
        printingId: 10 + i,
        cardName: `Card ${i}`,
        quantityInDeck: 1,
        cardType: "Unit",
      })),
    ];

    const validResult = validateSwuDeck(validTwinSunsCards, "Twin Suns");
    expect(validResult.isValid).toBe(true);

    const invalidTwinSunsCards = [
      { printingId: 1, cardName: "Leader 1", quantityInDeck: 1, cardType: "Leader" }, // only 1 leader
      { printingId: 2, cardName: "Base 1", quantityInDeck: 1, cardType: "Base" },
      { printingId: 10, cardName: "Duplicate Card", quantityInDeck: 2, cardType: "Unit" }, // > 1 copy in Twin Suns
      ...Array.from({ length: 60 }, (_, i) => ({
        printingId: 20 + i,
        cardName: `Unit ${i}`,
        quantityInDeck: 1,
        cardType: "Unit",
      })),
    ];

    const invalidResult = validateSwuDeck(invalidTwinSunsCards, "Twin Suns");
    expect(invalidResult.isValid).toBe(false);
    expect(invalidResult.issues.some((i) => i.includes("exactly 2 Leaders"))).toBe(true);
    expect(invalidResult.issues.some((i) => i.includes("minimum of 80 other cards"))).toBe(true);
    expect(invalidResult.issues.some((i) => i.includes("exceeds the limit of 1 copy"))).toBe(true);
  });
});

describe("deckRules - Star Wars CCG", () => {
  it("excludes Defensive Shields from the 60-card deck minimum", () => {
    const cards = [
      ...Array.from({ length: 55 }, (_, i) => ({
        printingId: 10 + i,
        cardName: `Character ${i}`,
        quantityInDeck: 1,
        cardType: "Character",
        side: "Dark",
      })),
      ...Array.from({ length: 5 }, (_, i) => ({
        printingId: 100 + i,
        cardName: `Shield ${i}`,
        quantityInDeck: 1,
        cardType: "Defensive Shield",
        side: "Dark",
      })),
    ];

    const result = validateSwccgDeck(cards, "Standard", "Dark");
    expect(result.mainDeckCount).toBe(55);
    expect(result.defensiveShieldCount).toBe(5);
    expect(result.isValid).toBe(false); // 55 < 60
    expect(result.issues.some((i) => i.includes("minimum of 60 cards"))).toBe(true);
  });

  it("enforces side alignment", () => {
    const cards = [
      ...Array.from({ length: 60 }, (_, i) => ({
        printingId: 10 + i,
        cardName: `Card ${i}`,
        quantityInDeck: 1,
        cardType: "Character",
        side: "Light",
      })),
      {
        printingId: 999,
        cardName: "Darth Vader",
        quantityInDeck: 1,
        cardType: "Character",
        side: "Dark",
      },
    ];

    const result = validateSwccgDeck(cards, "Standard", "Light");
    expect(result.isValid).toBe(false);
    expect(result.sideViolations).toContain("Darth Vader");
  });

  it("disallows Virtual set cards in Decipher Only format", () => {
    const cards = [
      ...Array.from({ length: 60 }, (_, i) => ({
        printingId: 10 + i,
        cardName: `Card ${i}`,
        quantityInDeck: 1,
        cardType: "Character",
        setName: "Premiere",
        side: "Light",
      })),
      {
        printingId: 999,
        cardName: "Virtual Luke",
        quantityInDeck: 1,
        cardType: "Character",
        setName: "Virtual Set 1",
        side: "Light",
      },
    ];

    const standardResult = validateSwccgDeck(cards, "Standard", "Light");
    expect(standardResult.isValid).toBe(true); // Allowed in Standard

    const decipherOnlyResult = validateSwccgDeck(cards, "Decipher Only", "Light");
    expect(decipherOnlyResult.isValid).toBe(false); // Disallowed in Decipher Only
    expect(decipherOnlyResult.virtualViolations).toContain("Virtual Luke");
  });

  it("caps Defensive Shields at max 1 copy per shield", () => {
    const cards = [
      ...Array.from({ length: 60 }, (_, i) => ({
        printingId: 10 + i,
        cardName: `Card ${i}`,
        quantityInDeck: 1,
        cardType: "Character",
        setName: "A New Hope",
        side: "Light",
      })),
      {
        printingId: 501,
        cardName: "Battle Order",
        quantityInDeck: 2, // 2 copies of same shield
        cardType: "Defensive Shield",
        setName: "Reflections III",
        side: "Light",
      },
    ];

    const result = validateSwccgDeck(cards, "Standard", "Light");
    expect(result.isValid).toBe(false);
    expect(result.shieldViolations).toContain("Battle Order");
  });

  it("checks helper utilities for cards, aspects, shields, and virtual sets", () => {
    const deckAspects = { Aggression: 1, Heroism: 1 };
    expect(isCardInAspect([{ name: "Aggression", count: 1 }], deckAspects)).toBe(true);
    expect(isCardInAspect([{ name: "Villainy", count: 1 }], deckAspects)).toBe(false);

    expect(isDefensiveShield({ cardType: "Defensive Shield" })).toBe(true);
    expect(isDefensiveShield({ cardType: "Character" })).toBe(false);

    expect(isVirtualCard({ setName: "Virtual Set 2" })).toBe(true);
    expect(isVirtualCard({ setName: "Demo Deck" })).toBe(true);
    expect(isVirtualCard({ setName: "Premiere" })).toBe(false);
  });
});

describe("deckRules - Magic: The Gathering", () => {
  it("validates Constructed 60+ main deck, <=15 sideboard, and 4-copy limit across both", () => {
    const valid = validateMtgDeck(
      [
        { printingId: 1, cardName: "Mountain", quantityInDeck: 56, isBasicLand: true },
        {
          printingId: 2,
          cardName: "Lightning Bolt",
          quantityInDeck: 4,
          quantitySideboard: 0,
        },
        {
          printingId: 3,
          cardName: "Abrade",
          quantityInDeck: 0,
          quantitySideboard: 4,
        },
        {
          printingId: 4,
          cardName: "Island",
          quantityInDeck: 0,
          quantitySideboard: 11,
          isBasicLand: true,
        },
      ],
      "Constructed"
    );
    expect(valid.isValid).toBe(true);

    const invalid = validateMtgDeck(
      [
        { printingId: 1, cardName: "Mountain", quantityInDeck: 55, isBasicLand: true },
        {
          printingId: 2,
          cardName: "Lightning Bolt",
          quantityInDeck: 3,
          quantitySideboard: 2,
        },
        {
          printingId: 3,
          cardName: "Island",
          quantityInDeck: 0,
          quantitySideboard: 14,
          isBasicLand: true,
        },
      ],
      "Constructed"
    );
    expect(invalid.isValid).toBe(false);
    expect(invalid.copyViolations).toHaveLength(1);
    expect(invalid.issues.some((i) => i.includes("at least 60 cards"))).toBe(true);
    expect(invalid.issues.some((i) => i.includes("at most 15 cards"))).toBe(true);
  });

  it("validates Commander 100+ cards, 1 commander, singleton, and color identity", () => {
    const valid = validateMtgDeck(
      [
        {
          printingId: 1,
          cardName: "Prossh, Skyraider of Kher",
          quantityInDeck: 1,
          isCommander: true,
          canBeCommander: true,
          colorIdentity: ["B", "R", "G"],
        },
        {
          printingId: 2,
          cardName: "Sol Ring",
          quantityInDeck: 1,
          colorIdentity: [],
        },
        {
          printingId: 3,
          cardName: "Forest",
          quantityInDeck: 98,
          isBasicLand: true,
          colorIdentity: ["G"],
        },
      ],
      "Commander"
    );
    expect(valid.isValid).toBe(true);

    const invalid = validateMtgDeck(
      [
        {
          printingId: 1,
          cardName: "Prossh, Skyraider of Kher",
          quantityInDeck: 1,
          isCommander: true,
          canBeCommander: true,
          colorIdentity: ["B", "R", "G"],
        },
        {
          printingId: 2,
          cardName: "Counterspell",
          quantityInDeck: 2,
          colorIdentity: ["U"],
        },
        {
          printingId: 3,
          cardName: "Forest",
          quantityInDeck: 95,
          isBasicLand: true,
          colorIdentity: ["G"],
        },
      ],
      "Commander"
    );
    expect(invalid.isValid).toBe(false);
    expect(invalid.colorIdentityViolations).toContain("Counterspell");
    expect(invalid.copyViolations).toHaveLength(1);
    expect(isWithinColorIdentity(["B", "G"], ["B", "R", "G"])).toBe(true);
    expect(isWithinColorIdentity(["W", "B"], ["B", "R", "G"])).toBe(false);
  });

  it("respects hasUnlimitedCopies and maxAllowedCopies overrides", () => {
    const valid = validateMtgDeck(
      [
        { printingId: 1, cardName: "Swamp", quantityInDeck: 30, isBasicLand: true },
        {
          printingId: 2,
          cardName: "Relentless Rats",
          quantityInDeck: 21,
          hasUnlimitedCopies: true,
        },
        { printingId: 3, cardName: "Nazgûl", quantityInDeck: 9, maxAllowedCopies: 9 },
      ],
      "Constructed"
    );
    expect(valid.isValid).toBe(true);
    expect(valid.copyViolations).toHaveLength(0);

    const invalid = validateMtgDeck(
      [
        { printingId: 1, cardName: "Swamp", quantityInDeck: 30, isBasicLand: true },
        {
          printingId: 2,
          cardName: "Relentless Rats",
          quantityInDeck: 20,
          hasUnlimitedCopies: true,
        },
        { printingId: 3, cardName: "Nazgûl", quantityInDeck: 10, maxAllowedCopies: 9 },
      ],
      "Constructed"
    );
    expect(invalid.isValid).toBe(false);
    expect(invalid.copyViolations).toEqual([{ cardName: "Nazgûl", count: 10, max: 9 }]);
  });
});

describe("deckRules - Zone grouping & game capabilities", () => {
  it("scopes capabilities accurately by game and format", () => {
    expect(getGameCapabilities("Magic The Gathering", "Constructed")).toMatchObject({
      hasSideboard: true,
      hasCommander: false,
      hasStartingCards: false,
      hasLeadersAndBase: false,
    });
    expect(getGameCapabilities("Magic The Gathering", "Commander")).toMatchObject({
      hasSideboard: false,
      hasCommander: true,
    });
    expect(getGameCapabilities("Star Wars CCG", "Standard")).toMatchObject({
      hasStartingCards: true,
      hasDefensiveShields: true,
      hasCommander: false,
      hasSideboard: false,
    });
    expect(getGameCapabilities("Star Wars Unlimited", "Premier")).toMatchObject({
      hasLeadersAndBase: true,
      hasStartingCards: false,
    });
  });

  it("groups cards by game-specific zones including Sideboard and Ideas", () => {
    const mtgGroups = groupDeckCardsByZone(
      [
        {
          printingId: 1,
          cardName: "Lightning Bolt",
          quantityInDeck: 3,
          quantitySideboard: 1,
          quantityIdea: 1,
          cardType: "Instant",
        },
      ],
      "Magic The Gathering",
      "Constructed"
    );
    expect(mtgGroups.map((g) => g.label)).toEqual(["Instant", "Sideboard", "Ideas"]);

    const swccgGroups = groupDeckCardsByZone(
      [
        {
          printingId: 1,
          cardName: "Heading For The Medical Frigate",
          quantityInDeck: 1,
          isStarting: true,
          cardType: "Effect",
        },
        {
          printingId: 2,
          cardName: "Battle Order",
          quantityInDeck: 1,
          cardType: "Defensive Shield",
        },
        { printingId: 3, cardName: "Luke Skywalker", quantityInDeck: 2, cardType: "Character" },
      ],
      "Star Wars CCG"
    );
    expect(swccgGroups.map((g) => g.label)).toEqual([
      "⭐ Starting Cards",
      "🛡️ Defensive Shields",
      "Character",
    ]);

    const pokemonGroups = groupDeckCardsByZone(
      [
        { printingId: 1, cardName: "Fire Energy", quantityInDeck: 10, cardType: "Energy" },
        { printingId: 2, cardName: "Pikachu", quantityInDeck: 4, cardType: "Pokemon" },
        { printingId: 3, cardName: "Professor's Research", quantityInDeck: 4, cardType: "Trainer" },
      ],
      "Pokemon TCG"
    );
    expect(pokemonGroups.map((g) => g.label)).toEqual(["Pokémon", "Trainer", "Energy"]);
  });

  it("includes zone breakdown when copy violations span Main, Sideboard, and Ideas", () => {
    const res = validateMtgDeck(
      [
        {
          printingId: 1,
          cardName: "Lightning Bolt",
          quantityInDeck: 2,
          quantitySideboard: 2,
          quantityIdea: 1,
        },
      ],
      "Constructed"
    );
    expect(res.issues.some((i) => i.includes("2 Main, 2 Sideboard, 1 Ideas"))).toBe(true);
  });
});
