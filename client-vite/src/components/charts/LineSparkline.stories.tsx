import type { Meta, StoryObj } from "@storybook/react";
import LineSparkline from "@/components/charts/LineSparkline";
import type { ValuePoint } from "@/types/value";

const risingPoints: ValuePoint[] = [
  { d: "2024-01-01", v: 10 },
  { d: "2024-02-01", v: 15 },
  { d: "2024-03-01", v: 12 },
  { d: "2024-04-01", v: 20 },
  { d: "2024-05-01", v: 28 },
  { d: "2024-06-01", v: 35 },
];

const fallingPoints: ValuePoint[] = [
  { d: "2024-01-01", v: 40 },
  { d: "2024-02-01", v: 35 },
  { d: "2024-03-01", v: 30 },
  { d: "2024-04-01", v: 22 },
  { d: "2024-05-01", v: 18 },
  { d: "2024-06-01", v: 10 },
];

const meta = {
  title: "Charts/LineSparkline",
  component: LineSparkline,
  tags: ["autodocs"],
  argTypes: {
    height: { control: { type: "range", min: 40, max: 200 } },
    stroke: { control: "color" },
    emptyLabel: { control: "text" },
  },
  decorators: [
    (Story) => (
      <div className="w-64 p-4 border rounded-lg bg-card">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof LineSparkline>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Rising: Story = {
  args: {
    points: risingPoints,
    ariaLabel: "Card value over time (rising)",
    height: 96,
  },
};

export const Falling: Story = {
  args: {
    points: fallingPoints,
    ariaLabel: "Card value over time (falling)",
    height: 96,
    stroke: "#ef4444",
  },
};

export const Empty: Story = {
  args: {
    points: [],
    ariaLabel: "Card value (no data)",
    height: 96,
    emptyLabel: "No price history",
  },
};

export const SinglePoint: Story = {
  args: {
    points: [{ d: "2024-01-01", v: 25 }],
    ariaLabel: "Card value (single point)",
    height: 96,
  },
};
