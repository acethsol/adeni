export function filterByQuery<T>(
  items: T[],
  query: string,
  parts: (item: T) => Array<string | null | undefined>,
): T[] {
  const needle = query.trim().toLowerCase();
  if (!needle) {
    return items;
  }
  return items.filter((item) =>
    parts(item)
      .filter((part): part is string => Boolean(part))
      .join(" ")
      .toLowerCase()
      .includes(needle),
  );
}
