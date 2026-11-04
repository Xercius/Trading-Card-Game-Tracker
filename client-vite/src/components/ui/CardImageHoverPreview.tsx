import React, { useState, useRef, useEffect } from "react";
import { createPortal } from "react-dom";
import { resolveImageUrl } from "@/lib/http";

export type CardImageHoverPreviewProps = {
  imageUrl?: string | null;
  cardName: string;
  className?: string;
  isHorizontal?: boolean;
  thumbnailWidth?: number;
  thumbnailHeight?: number;
};

/**
 * Renders a tiny thumbnail image of a card that shows a larger preview popover on hover.
 * Automatically respects vertical vs. horizontal card orientation and keeps the popover
 * fully within the viewport bounds.
 */
export const CardImageHoverPreview: React.FC<CardImageHoverPreviewProps> = ({
  imageUrl,
  cardName,
  className = "",
  isHorizontal: initialIsHorizontal = false,
  thumbnailWidth,
  thumbnailHeight,
}) => {
  const [isHovered, setIsHovered] = useState(false);
  const [imgError, setImgError] = useState(false);
  const [detectedHorizontal, setDetectedHorizontal] = useState(initialIsHorizontal);
  const [coords, setCoords] = useState<{ top: number; left: number } | null>(null);
  const triggerRef = useRef<HTMLDivElement>(null);

  // Sync if prop changes
  useEffect(() => {
    setDetectedHorizontal(initialIsHorizontal);
  }, [initialIsHorizontal]);

  const isHorizontal = detectedHorizontal;

  // Compute thumbnail dimensions matching the aspect ratio
  const computedThumbWidth = thumbnailWidth ?? (isHorizontal ? 44 : 32);
  const computedThumbHeight = thumbnailHeight ?? (isHorizontal ? 32 : 44);

  const updatePopoverPosition = () => {
    if (!triggerRef.current) return;
    const rect = triggerRef.current.getBoundingClientRect();
    const popoverWidth = isHorizontal ? 320 : 230;
    const popoverHeight = isHorizontal ? 245 : 335;

    // Default: position to the right of the thumbnail
    let left = rect.right + 12;
    // If popover would clip the right edge of the viewport, position to the left of the thumbnail
    if (left + popoverWidth > window.innerWidth - 12) {
      left = Math.max(12, rect.left - popoverWidth - 12);
    }

    // Vertically center popover with trigger thumbnail
    let top = rect.top + rect.height / 2 - popoverHeight / 2;
    // Clamp so popover never overflows viewport top or bottom
    const maxTop = window.innerHeight - popoverHeight - 12;
    top = Math.max(12, Math.min(top, Math.max(12, maxTop)));

    setCoords({ top, left });
  };

  const handleMouseEnter = () => {
    updatePopoverPosition();
    setIsHovered(true);
  };

  const handleMouseLeave = () => {
    setIsHovered(false);
  };

  // Auto-detect horizontal orientation on image load if naturalWidth > naturalHeight
  const handleImageLoad = (e: React.SyntheticEvent<HTMLImageElement>) => {
    const img = e.currentTarget;
    if (img.naturalWidth && img.naturalHeight && img.naturalWidth > img.naturalHeight) {
      setDetectedHorizontal(true);
    }
  };

  if (!imageUrl || imgError) {
    return (
      <div
        className={`flex items-center justify-center rounded bg-zinc-800 text-zinc-500 text-[10px] select-none border border-zinc-700 ${className}`}
        style={{ width: computedThumbWidth, height: computedThumbHeight }}
        title={cardName}
      >
        <span>N/A</span>
      </div>
    );
  }

  const resolvedImageUrl = resolveImageUrl(imageUrl);

  return (
    <>
      <div
        ref={triggerRef}
        className={`relative inline-flex items-center justify-center ${className}`}
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
      >
        <img
          src={resolvedImageUrl}
          alt={cardName}
          onError={() => setImgError(true)}
          onLoad={handleImageLoad}
          className="rounded border border-zinc-700/80 object-cover shadow-sm transition-transform hover:scale-105 cursor-pointer"
          style={{ width: computedThumbWidth, height: computedThumbHeight }}
          loading="lazy"
        />
      </div>

      {isHovered &&
        coords &&
        typeof document !== "undefined" &&
        createPortal(
          <div
            className="fixed z-[9999] pointer-events-none rounded-xl border border-zinc-700 bg-zinc-950 p-2 shadow-2xl transition-opacity duration-150"
            style={{
              top: `${coords.top}px`,
              left: `${coords.left}px`,
              width: `${isHorizontal ? 320 : 230}px`,
            }}
          >
            <img
              src={resolvedImageUrl}
              alt={cardName}
              className={`w-full rounded-lg object-contain shadow-md ${
                isHorizontal ? "max-h-60" : "max-h-80"
              }`}
            />
            <div className="mt-1.5 text-center text-xs font-semibold text-zinc-200 truncate px-1">
              {cardName}
            </div>
          </div>,
          document.body
        )}
    </>
  );
};
