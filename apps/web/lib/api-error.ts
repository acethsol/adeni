"use client";

import { useCallback } from "react";
import {
  localizeErrorFromResponse,
  localizeErrorResponse,
  type ApiErrorResponse,
} from "@adeni/shared";
import { useTranslation } from "@/components/locale-provider";

export function useApiErrorMessage() {
  const { locale } = useTranslation();

  const formatApiError = useCallback(
    (response: ApiErrorResponse | unknown, fallback?: string) => {
      const message = localizeErrorResponse(locale, response);
      return message === "Something went wrong." && fallback ? fallback : message;
    },
    [locale],
  );

  const formatApiErrorResponse = useCallback(
    (response: Response, fallback?: string) =>
      localizeErrorFromResponse(locale, response, fallback),
    [locale],
  );

  return { formatApiError, formatApiErrorResponse };
}
