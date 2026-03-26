import type { Meta, StoryObj } from "@storybook/react";
import CardTile, { type CardSummary } from "@/components/CardTile";

const mockCard: CardSummary = {
  id: 1,
  primaryPrintingId: 1,
  name: "Charizard",
  game: "Pokemon",
  cardType: "Fire",
  setName: "Base Set",
  number: "4/102",
  rarity: "Holo Rare",
  imageUrl: null,
};

const meta = {
  title: "Components/CardTile",
  component: CardTile,
  tags: ["autodocs"],
  argTypes: {
    onClick: { action: "clicked" },
  },
  decorators: [
    (Story) => (
      <div className="w-40">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof CardTile>;

export default meta;
type Story = StoryObj<typeof meta>;

export const WithoutImage: Story = {
  args: {
    card: mockCard,
  },
};

export const WithImage: Story = {
  args: {
    card: {
      ...mockCard,
      imageUrl: "https://images.pokemontcg.io/base1/4_hires.png",
    },
  },
};

export const LongName: Story = {
  args: {
    card: {
      ...mockCard,
      name: "Mega Charizard EX",
      setName: "Flashfire",
      number: "107/106",
      rarity: "Secret Rare",
    },
  },
};

export const GridOfCards: Story = {
  render: () => (
    <div className="grid grid-cols-4 gap-3 p-4">
      {Array.from({ length: 8 }, (_, i) => ({
        ...mockCard,
        id: i + 1,
        name: `Card ${i + 1}`,
      })).map((card) => (
        <CardTile key={card.id} card={card} />
      ))}
    </div>
  ),
};
