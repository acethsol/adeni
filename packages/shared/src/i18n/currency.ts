export function getCurrencySymbol(currency: string): string {
  const symbols: Record<string, string> = {
    NGN: "₦",
    CAD: "C$",
    USD: "$",
  };

  return symbols[currency] ?? currency;
}
