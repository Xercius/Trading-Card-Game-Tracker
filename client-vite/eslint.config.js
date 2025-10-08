import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
  globalIgnores(["dist"]),
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs["recommended-latest"],
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      "no-restricted-syntax": [
        "error",
        {
          selector:
            "CallExpression[callee.object.name='http'][arguments.0.type='Literal'][arguments.0.value^='/']",
          message: "Leading '/' bypasses baseURL—use a relative path.",
        },
        {
          selector:
            "CallExpression[callee.object.name='http'][arguments.0.type='TemplateLiteral'][arguments.0.quasis.0.value.raw^='/']",
          message: "Leading '/' bypasses baseURL—use a relative path.",
        },
      ],
    },
  },
]);
