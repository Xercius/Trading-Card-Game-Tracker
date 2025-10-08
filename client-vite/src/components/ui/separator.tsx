type SeparatorProps = React.HTMLAttributes<HTMLDivElement> & {
  orientation?: "horizontal" | "vertical";
};

export function Separator({
  orientation = "horizontal",
  className = "",
  ...props
}: SeparatorProps) {
  const base = "bg-border";
  const orientationClasses =
    orientation === "vertical" ? "mx-2 inline-block h-full w-px" : "my-2 h-px w-full";
  const classes = [base, orientationClasses, className].filter(Boolean).join(" ");
  return <div role="separator" aria-orientation={orientation} className={classes} {...props} />;
}

export type { SeparatorProps };
