# Test Utilities

This directory contains shared utilities for testing.

## cssEscape

The `cssEscape` module provides a `cssEscapeId` function for safely escaping ID strings when building CSS selectors.

### When to use

Use `cssEscapeId` whenever you need to construct a CSS selector with a dynamic ID:

```typescript
import { cssEscapeId } from "@/test/utils";

// ✓ Good - escaped
const element = document.querySelector(`#${cssEscapeId(dynamicId)}`);

// ✗ Bad - unescaped, will fail with special characters
const element = document.querySelector(`#${dynamicId}`);
```

### Why it's needed

CSS selectors have special characters that need to be escaped (colons, leading digits, etc.). The utility handles this automatically, using native `CSS.escape` when available and providing a fallback for older environments.

### Note

Prefer using Testing Library queries by role, label, or text when possible. This utility is for cases where `querySelector` with ID selectors is unavoidable.
