import { resolveImageUrl } from "@/lib/http";
import type { CardPrintingImages } from "../api/types";

type CardImagesProps = CardPrintingImages & {
  name: string;
};

export default function CardImages({ name, imageUrl, backImageUrl }: CardImagesProps) {
  const images = [
    { url: imageUrl, face: "front" },
    { url: backImageUrl, face: "back" },
  ].filter((image) => image.url);

  return (
    <div
      role="group"
      aria-label="Card images"
      className={`grid h-full min-h-0 w-full min-w-0 gap-2 overflow-hidden rounded-xl border bg-muted p-2 [@media(max-height:500px)]:p-1 ${
        images.length === 2 ? "grid-cols-1 grid-rows-2" : "grid-cols-1 grid-rows-1"
      }`}
    >
      {images.length ? (
        images.map(({ url, face }) => (
          <img
            key={`${face}-${url}`}
            loading="lazy"
            decoding="async"
            src={resolveImageUrl(url!)}
            alt={images.length === 2 ? `${name} (${face})` : name}
            className="h-full min-h-0 w-full min-w-0 object-contain"
          />
        ))
      ) : (
        <div className="flex h-full w-full items-center justify-center text-sm text-muted-foreground">
          No image available
        </div>
      )}
    </div>
  );
}
