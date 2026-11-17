# Changelog

All notable changes to Trading Card Game Tracker are documented in this file.

## [v1.4.0] - 2026-11-17

### New Features

- Added an in-app "What's New" changelog flyout in the header with unread release tracking.
- Added a two-step "Sync Sets First" workflow for Magic: The Gathering and Pokémon TCG importers.
- Added Magic: The Gathering deck validation rules service.

### Improvements

- Streamlined the application by removing legacy user-related state and context for a cleaner single-user experience.
- Updated importer guidelines and agent documentation for Magic: The Gathering.

### Bug Fixes

- Fixed invalid query handling and importer selection edge cases.

## [v1.3.0] - 2026-11-05

### New Features

- Added Magic: The Gathering card importer and set synchronization support.
- Added special types and "Evolves From" filters for Pokémon TCG cards.

### Improvements

- Refactored admin import and sync workflows for more consistent progress tracking.

### Bug Fixes

- Resolved filter state synchronization issues when switching between games.

## [v1.2.0] - 2026-10-20

### New Features

- Added Star Wars: Unlimited and Star Wars CCG collection value history charts.
- Added proxy quantity toggle with persistent preference across sessions.

### Improvements

- Improved card grid virtualization performance for large collections.

### Bug Fixes

- Fixed proxy quantities being included in total collection value calculations.

## [v1.1.0] - 2026-10-01

### New Features

- Added wishlist management and deck-building support across supported games.
- Added inline card image hover preview in list and deck views.

### Improvements

- Enhanced header navigation with quick game switching and debounced search.
