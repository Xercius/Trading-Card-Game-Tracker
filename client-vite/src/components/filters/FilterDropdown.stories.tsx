import type { Meta, StoryObj } from "@storybook/react";
import { useState } from "react";
import FilterDropdown from "@/components/filters/FilterDropdown";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

const meta = {
  title: "Components/FilterDropdown",
  component: FilterDropdown,
  tags: ["autodocs"],
  decorators: [
    (Story) => (
      <div className="p-8 min-h-48">
        <Story />
      </div>
    ),
  ],
  parameters: {
    docs: {
      description: {
        component:
          "A portal-rendered dropdown for filters with keyboard navigation, focus management, and click-outside-to-close behavior.",
      },
    },
  },
} satisfies Meta<typeof FilterDropdown>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    trigger: <Button variant="outline" size="sm">Rarity ▾</Button>,
    children: (
      <div className="p-2 space-y-1 min-w-40">
        {["Common", "Uncommon", "Rare", "Holo Rare", "Secret Rare"].map((r) => (
          <button key={r} className="w-full text-left px-3 py-1.5 text-sm hover:bg-white/10 rounded">
            {r}
          </button>
        ))}
      </div>
    ),
  },
};

export const WithActiveFilters: Story = {
  render: () => {
    const options = ["Fire", "Water", "Grass", "Electric", "Psychic"];
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [selected, setSelected] = useState<Set<string>>(new Set(["Fire", "Water"]));

    const toggle = (opt: string) => {
      setSelected((prev) => {
        const next = new Set(prev);
        if (next.has(opt)) next.delete(opt);
        else next.add(opt);
        return next;
      });
    };

    return (
      <div className="flex items-center gap-2">
        <FilterDropdown
          trigger={
            <Button variant="outline" size="sm">
              Type {selected.size > 0 && <Badge className="ml-1">{selected.size}</Badge>}
            </Button>
          }
        >
          <div className="p-2 space-y-1 min-w-40">
            {options.map((opt) => (
              <button
                key={opt}
                onClick={() => toggle(opt)}
                className={`w-full text-left px-3 py-1.5 text-sm rounded flex items-center gap-2 ${
                  selected.has(opt) ? "bg-white/20 font-medium" : "hover:bg-white/10"
                }`}
              >
                {selected.has(opt) && <span aria-hidden>✓</span>}
                {opt}
              </button>
            ))}
          </div>
        </FilterDropdown>
      </div>
    );
  },
};

export const RightAligned: Story = {
  decorators: [
    (Story) => (
      <div className="flex justify-end p-8 min-h-48">
        <Story />
      </div>
    ),
  ],
  args: {
    align: "right",
    trigger: <Button variant="outline" size="sm">Sort ▾</Button>,
    children: (
      <div className="p-2 space-y-1 min-w-40">
        {["Name A–Z", "Name Z–A", "Newest", "Price ↑", "Price ↓"].map((opt) => (
          <button key={opt} className="w-full text-left px-3 py-1.5 text-sm hover:bg-white/10 rounded">
            {opt}
          </button>
        ))}
      </div>
    ),
  },
};
