import { render, screen } from '@testing-library/react';
import App from './App';

const originalFetch = global.fetch;

afterEach(() => {
  jest.resetAllMocks();
  if (originalFetch) {
    global.fetch = originalFetch;
  } else {
    delete global.fetch;
  }
});

test('renders cards fetched from the API', async () => {
  const mockCards = [
    { id: 1, name: 'Black Lotus', game: 'Magic: The Gathering' },
    { id: 2, name: 'Elsa, Snow Queen', game: 'Lorcana' }
  ];

  global.fetch = jest.fn().mockResolvedValue({
    ok: true,
    json: async () => mockCards
  });

  render(<App />);

  expect(screen.getByRole('heading', { name: /cards from api/i })).toBeInTheDocument();
  expect(global.fetch).toHaveBeenCalledWith('/api/card');

  for (const card of mockCards) {
    expect(await screen.findByText(`${card.name} (${card.game})`)).toBeInTheDocument();
  }
});
