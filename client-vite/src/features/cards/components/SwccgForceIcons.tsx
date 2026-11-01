import * as React from "react";

export type LightsaberBladeProps = {
  side: "dark" | "light";
  size?: "sm" | "md" | "lg";
  className?: string;
};

/**
 * Renders an illuminated diagonal lightsaber blade SVG matching the Star Wars CCG
 * Force icon appearance (red blade for Dark Side, blue blade for Light Side).
 */
export function LightsaberBlade({ side, size = "md", className = "" }: LightsaberBladeProps) {
  const isDark = side === "dark";
  const glowColor = isDark ? "#ef4444" : "#38bdf8";
  const outerGlow = isDark ? "#991b1b" : "#0369a1";

  const width = size === "sm" ? 10 : size === "lg" ? 18 : 14;
  const height = size === "sm" ? 18 : size === "lg" ? 28 : 22;

  return (
    <svg
      width={width}
      height={height}
      viewBox="0 0 14 24"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={`inline-block shrink-0 transform -rotate-12 ${className}`}
      aria-hidden="true"
    >
      {/* Outer blurred glow */}
      <line
        x1="10"
        y1="2"
        x2="4"
        y2="22"
        stroke={glowColor}
        strokeWidth="5"
        strokeLinecap="round"
        style={{ filter: `drop-shadow(0 0 2px ${glowColor}) drop-shadow(0 0 4px ${outerGlow})` }}
      />
      {/* Saturated colored halo */}
      <line
        x1="10"
        y1="2"
        x2="4"
        y2="22"
        stroke={glowColor}
        strokeWidth="3.5"
        strokeLinecap="round"
      />
      {/* Pure white core blade */}
      <line
        x1="10"
        y1="2"
        x2="4"
        y2="22"
        stroke="#FFFFFF"
        strokeWidth="1.8"
        strokeLinecap="round"
      />
    </svg>
  );
}

export type ForceIconBadgeProps = {
  side: "dark" | "light";
  count: number;
};

/**
 * Renders a Force generation badge with count and glowing lightsaber blade(s).
 */
export function ForceIconBadge({ side, count }: ForceIconBadgeProps) {
  const isDark = side === "dark";
  const label = isDark ? "Dark Icons" : "Light Icons";
  const borderColor = isDark ? "border-red-500/30 bg-red-950/20" : "border-blue-500/30 bg-blue-950/20";
  const textColor = isDark ? "text-red-400" : "text-blue-400";
  const title = `${count} ${isDark ? "Dark Side" : "Light Side"} Force icon${count === 1 ? "" : "s"}`;

  return (
    <div
      className={`flex flex-col items-center justify-center rounded border ${borderColor} px-2.5 py-1 min-w-[55px]`}
      title={title}
      aria-label={`${isDark ? "Dark Side" : "Light Side"} Icons: ${count}`}
    >
      <span className={`text-[10px] uppercase font-bold ${textColor}`}>{label}</span>
      <div className="flex items-center gap-1.5 mt-0.5">
        <span className={`text-base font-black ${textColor}`}>{count}</span>
        {count > 0 && (
          <div className="flex items-center -space-x-1" aria-hidden="true">
            {Array.from({ length: Math.min(count, 5) }).map((_, i) => (
              <LightsaberBlade key={i} side={side} size="sm" />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
