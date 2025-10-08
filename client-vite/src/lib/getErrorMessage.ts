import { getProblemDetailsMessage } from "./problemDetails";

export function getErrorMessage(error: unknown, fallback = "Something went wrong.") {
  return getProblemDetailsMessage(error, fallback);
}
