import { useEffect, useRef, useState } from "react";

type Props = {
  src: string;
  alt: string;
  className?: string;
  onLoad?: () => void;
  skeletonClassName?: string;
};

export default function LazyImage({ src, alt, className, onLoad, skeletonClassName }: Props) {
  const imgRef = useRef<HTMLImageElement | null>(null);
  const [isInView, setIsInView] = useState(false);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    const el = imgRef.current;
    if (!el) return;
    if ("loading" in HTMLImageElement.prototype) {
      setIsInView(true);
      return;
    }
    const io = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) { setIsInView(true); io.disconnect(); }
    }, { rootMargin: "400px" });
    io.observe(el);
    return () => io.disconnect();
  }, []);

  return (
    <div className={`relative ${className ?? ""}`}>
      {!loaded && (
        <div className={`absolute inset-0 animate-pulse bg-muted ${skeletonClassName ?? ""}`} />
      )}
      <img
        ref={imgRef}
        loading="lazy"
        decoding="async"
        src={isInView ? src : undefined}
        alt={alt}
        className={`h-full w-full object-cover ${loaded ? "" : "opacity-0"}`}
        onLoad={() => { setLoaded(true); onLoad?.(); }}
      />
    </div>
  );
}
