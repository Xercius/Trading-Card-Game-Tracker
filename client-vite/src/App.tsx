import { useState, useEffect } from 'react';
import './App.css';

interface CardDto {
  id: number;
  game: string;
  name: string;
  cardType: string;
  description?: string | null;
}

export default function App() {
  const [cards, setCards] = useState<CardDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/card')
      .then(res => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json();
      })
      .then((data: CardDto[]) => setCards(data))
      .catch(err => setError(err.message));
  }, []);

  return (
    <main style={{ padding: '1rem' }}>
      <h1>My Card Collection</h1>
      {error && <p style={{ color: 'red' }}>Error: {error}</p>}
      {cards.length === 0 && !error && <p>Loading…</p>}
      <ul>
        {cards.map(card => (
          <li key={card.id}>
            {card.name} ({card.game})
          </li>
        ))}
      </ul>
    </main>
  );
}
