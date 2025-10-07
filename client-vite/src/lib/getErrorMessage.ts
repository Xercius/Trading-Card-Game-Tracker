import { isAxiosError } from "axios";

export function getErrorMessage(error: unknown, fallback = "Something went wrong.") {
  if (isAxiosError(error)) {
    const data = error.response?.data as
      | { detail?: string; title?: string; error?: string; message?: string }
      | undefined;

    return (
      data?.detail?.toString().trim() ||
      data?.title?.toString().trim() ||
      data?.error?.toString().trim() ||
      data?.message?.toString().trim() ||
      error.message ||
      fallback
    );
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallback;
}
