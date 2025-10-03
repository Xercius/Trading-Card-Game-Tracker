import { forwardRef } from "react";

type BadgeProps = React.HTMLAttributes<HTMLSpanElement> & {
  variant?: "default" | "secondary" | "outline";
};

const base = "inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold transition-colors";

const variants: Record<NonNullable<BadgeProps["variant"]>, string> = {
  default: "border-transparent bg-primary text-primary-foreground hover:bg-primary/80",
  secondary: "border-transparent bg-secondary text-secondary-foreground hover:bg-secondary/80",
  outline: "text-foreground",
};

export const Badge = forwardRef<HTMLSpanElement, BadgeProps>(function Badge(
  { className = "", variant = "default", ...props },
  ref,
) {
  const classes = [base, variants[variant], className].filter(Boolean).join(" ");
  return <span ref={ref} className={classes} {...props} />;
});

export type { BadgeProps };
