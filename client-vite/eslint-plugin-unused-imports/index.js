import tsEslintPlugin from "@typescript-eslint/eslint-plugin";

function removeSpecifierWithComma(fixer, sourceCode, specifier) {
  let [start, end] = specifier.range;
  const nextToken = sourceCode.getTokenAfter(specifier, { includeComments: true });
  const prevToken = sourceCode.getTokenBefore(specifier, { includeComments: true });

  if (nextToken && nextToken.value === ",") {
    end = nextToken.range[1];
    const tokenAfterComma = sourceCode.getTokenAfter(nextToken, { includeComments: true });
    if (tokenAfterComma) {
      const textBetween = sourceCode.text.slice(end, tokenAfterComma.range[0]);
      const whitespaceMatch = textBetween.match(/^[\t \f\v]+/u);
      if (whitespaceMatch) {
        end += whitespaceMatch[0].length;
      }
    } else {
      const trailingWhitespace = sourceCode.text.slice(end).match(/^[\t \f\v]+/u);
      if (trailingWhitespace) {
        end += trailingWhitespace[0].length;
      }
    }
  } else if (prevToken && prevToken.value === ",") {
    start = prevToken.range[0];
  }

  return fixer.removeRange([start, end]);
}

const noUnusedImportsRule = {
  meta: {
    type: "problem",
    docs: {
      description: "Disallow unused imports and provide autofixes to remove them.",
      recommended: false,
    },
    fixable: "code",
    schema: [
      {
        type: "object",
        properties: {
          ignoreTypeImports: { type: "boolean" },
        },
        additionalProperties: false,
      },
    ],
    messages: {
      allUnused: "All imports from '{{module}}' are unused.",
      partiallyUnused:
        "The following imports are unused and can be removed: {{names}}.",
    },
  },
  create(context) {
    const sourceCode = context.sourceCode ?? context.getSourceCode();
    const option = context.options[0] ?? {};

    return {
      ImportDeclaration(node) {
        if (option.ignoreTypeImports && node.importKind === "type") {
          return;
        }

        const declaredVariables = context.getDeclaredVariables(node);
        const unusedVariables = declaredVariables.filter(
          (variable) => variable.references.length === 0
        );

        if (unusedVariables.length === 0) {
          return;
        }

        const unusedSpecifiers = node.specifiers.filter((specifier) =>
          unusedVariables.some((variable) => {
            const identifier = variable.identifiers[0];
            return identifier && identifier.name === specifier.local.name;
          })
        );

        if (unusedSpecifiers.length === 0) {
          return;
        }

        const names = unusedSpecifiers.map((specifier) => specifier.local.name);
        const allUnused = unusedSpecifiers.length === node.specifiers.length;

        context.report({
          node,
          messageId: allUnused ? "allUnused" : "partiallyUnused",
          data: {
            module: node.source?.value ?? "module",
            names: names.join(", "),
          },
          fix(fixer) {
            if (allUnused) {
              return fixer.remove(node);
            }

            return unusedSpecifiers.map((specifier) =>
              removeSpecifierWithComma(fixer, sourceCode, specifier)
            );
          },
        });
      },
    };
  },
};

const plugin = {
  rules: {
    "no-unused-imports": noUnusedImportsRule,
    "no-unused-vars": tsEslintPlugin.rules["no-unused-vars"],
  },
};

export default plugin;
