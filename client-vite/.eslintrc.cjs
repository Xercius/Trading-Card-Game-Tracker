/* @type {import("eslint").Linter.Config} */
module.exports = {
  root: true,
  parser: "@typescript-eslint/parser",
  parserOptions: { ecmaVersion: "latest", sourceType: "module" },
  env: { browser: true, es2023: true, node: true },
  settings: { react: { version: "detect" } },
  plugins: ["@typescript-eslint", "react", "react-hooks", "jsx-a11y", "prettier"],
  extends: [
    "eslint:recommended",
    "plugin:react/recommended",
    "plugin:react-hooks/recommended",
    "plugin:@typescript-eslint/recommended",
    "plugin:jsx-a11y/recommended",
    "prettier"
  ],
  rules: {
    "prettier/prettier": "error"
  },
  ignorePatterns: ["dist", "node_modules", "coverage", "vite-cache"]
};
