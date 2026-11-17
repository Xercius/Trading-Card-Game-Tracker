# Changelog

All notable changes to Trading Card Game Tracker are documented in this file.

## [Unreleased]

## [v0.26.1] - 2026-11-17

### New Features

- Added game-specific card filters across all active games: Magic: The Gathering (Color Identity, Mana Value, Power, Toughness, Loyalty, and AND/OR/EXACT Color operators), Star Wars: Unlimited (Cost, Power, HP, Upgrade Power, Upgrade HP), and Pokémon TCG (Energy Type).

### Improvements

- Unified card filtering across Cards, Deck Construction, and Deck Builder views using game-scoped filter drawer sections.

## [v0.26.0] - 2026-11-17

### New Features

- Added an in-app "What's New" changelog flyout in the header with unread release tracking.

## [v0.25.3] - 2026-11-17

### Improvements

- Streamlined the application by removing legacy user-related state and context for a cleaner single-user experience.
- Updated GitHub security checks and agent guidance.

## [v0.25.2] - 2026-11-16

### Improvements

- GitHub security checks (#829).

## [v0.25.1] - 2026-11-16

### Improvements

- [Copilot Dreaming] Avoid invalid queries and wrong importer choices; remove stale and duplicate guidance (#822).

## [v0.25.0] - 2026-11-16

### New Features

- Added a two-step "Sync Sets First" workflow for Magic: The Gathering and Pokémon TCG importers.

### Improvements

- Updated Magic: The Gathering importer guidelines and agent instructions.

## [v0.23.0] - 2026-11-16

### New Features

- Added Magic: The Gathering deck validation rules service.

## [v0.22.2] - 2026-11-13

### New Features

- Added Magic: The Gathering card importer and set synchronization support.
- Added special types and "Evolves From" filters for Pokémon TCG cards.

### Improvements

- Refactored admin import and sync workflows for more consistent progress tracking.
