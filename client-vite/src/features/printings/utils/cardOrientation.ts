import type { PrintingListItem } from "../api/printings";

/**
 * Checks whether a card printing has horizontal artwork or is a horizontal card type (e.g. Leader, Base).
 */
export function isHorizontalPrinting(p: PrintingListItem): boolean {
  if (p.imageOrientation === "horizontal") return true;
  if (p.imageOrientation === "vertical") return false;
  return Boolean(
    p.cardTypes?.some((type) => {
      const lower = type.toLowerCase();
      return lower.includes("leader") || lower.includes("base");
    })
  );
}
