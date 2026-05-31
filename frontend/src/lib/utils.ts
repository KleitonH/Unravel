import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Helper canônico do shadcn — combina clsx (condicionais) com tailwind-merge
 * (resolve conflitos de classes Tailwind, ex.: "p-4 p-6" → "p-6").
 */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
