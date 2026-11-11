import { describe, expect, it } from "vitest";
import {
  computeDeckAspects,
  calculateAspectPenalty,
  isCardInAspect,
  validateSwuDeck,
  validateSwccgDeck,
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
