import { isAxiosError } from "axios";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function toNumber(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string") {
    const parsed = Number.parseInt(value, 10);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }
  return undefined;
}

function toStringOrUndefined(value: unknown): string | undefined {
  if (typeof value === "string") {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : undefined;
  }
  return undefined;
}

function toStringArray(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map((item) => toStringOrUndefined(item))
      .filter((item): item is string => typeof item === "string");
  }
  const single = toStringOrUndefined(value);
  return single ? [single] : [];
}

export type ProblemDetailsErrorEntry = {
  field: string | null;
  messages: string[];
};

export type ParsedProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors: ProblemDetailsErrorEntry[];
  raw: unknown;
};

export function parseProblemDetails(data: unknown): ParsedProblemDetails | null {
  if (!isRecord(data)) return null;

  const type = toStringOrUndefined(data.type);
  const title = toStringOrUndefined(data.title);
  const detail = toStringOrUndefined(data.detail ?? data.message ?? data.error);
  const instance = toStringOrUndefined(data.instance);
  const status = toNumber(data.status);
  const traceId = toStringOrUndefined(data.traceId);

  const errorsSource = data.errors;
  const errors: ProblemDetailsErrorEntry[] = [];

  if (isRecord(errorsSource)) {
    for (const [key, value] of Object.entries(errorsSource)) {
      const messages = toStringArray(value);
      if (messages.length > 0) {
        errors.push({ field: key || null, messages });
      }
    }
  }

  return {
    type,
    title,
    status,
    detail,
    instance,
    traceId,
    errors,
    raw: data,
  } satisfies ParsedProblemDetails;
}

function buildProblemMessage(problem: ParsedProblemDetails | null, fallback: string): string {
  if (!problem) return fallback;

  const seen = new Set<string>();
  const candidates: string[] = [];

  if (problem.detail) {
    const trimmed = problem.detail.trim();
    if (trimmed.length > 0) {
      candidates.push(trimmed);
      seen.add(trimmed);
    }
  }

  if (problem.title) {
    const trimmed = problem.title.trim();
    if (trimmed.length > 0 && !seen.has(trimmed)) {
      candidates.push(trimmed);
      seen.add(trimmed);
    }
  }

  for (const entry of problem.errors) {
    for (const message of entry.messages) {
      const trimmed = message.trim();
      if (!trimmed) continue;
      if (!seen.has(trimmed)) {
        candidates.push(trimmed);
        seen.add(trimmed);
      }
    }
  }

  if (candidates.length > 0) {
    return candidates[0];
  }

  return fallback;
}

function defaultMessageForStatus(status?: number): string | undefined {
  switch (status) {
    case 400:
      return "Unable to process the request.";
    case 401:
      return "Authentication required.";
    case 403:
      return "You do not have permission to perform this action.";
    case 404:
      return "Not found.";
    case 409:
      return "The request could not be completed due to a conflict.";
    case 422:
      return "The request data is invalid.";
    case 500:
      return "A server error occurred.";
    default:
      return undefined;
  }
}

export class ProblemDetailsError extends Error {
  status?: number;
  problem: ParsedProblemDetails | null;
  traceId?: string;
  override cause?: unknown;

  constructor(message: string, options: {
    status?: number;
    problem?: ParsedProblemDetails | null;
    traceId?: string;
    cause?: unknown;
  } = {}) {
    super(message);
    this.name = "ProblemDetailsError";
    this.status = options.status ?? options.problem?.status;
    this.problem = options.problem ?? null;
    this.traceId = options.traceId ?? options.problem?.traceId;
    if (options.cause !== undefined) {
      this.cause = options.cause;
    }
  }
}

export function toProblemDetailsError(error: unknown, fallback = "Something went wrong."): ProblemDetailsError {
  if (error instanceof ProblemDetailsError) {
    return error;
  }

  if (isAxiosError(error)) {
    const status = error.response?.status;
    const problem = parseProblemDetails(error.response?.data);
    const statusFallback = defaultMessageForStatus(status) ?? fallback;
    const message = buildProblemMessage(problem, statusFallback);
    return new ProblemDetailsError(message, {
      status: status ?? problem?.status,
      problem,
      cause: error,
    });
  }

  if (error instanceof Error) {
    const message = error.message && error.message.trim().length > 0 ? error.message : fallback;
    return new ProblemDetailsError(message, { cause: error });
  }

  return new ProblemDetailsError(fallback, { cause: error });
}

export function getProblemDetailsMessage(error: unknown, fallback = "Something went wrong.") {
  const normalized = toProblemDetailsError(error, fallback);
  return normalized.message;
}
