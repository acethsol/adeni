import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { t } from "../index";
import { enMessages } from "./en";
import { frMessages } from "./fr";
import { mergeMessageTrees, type MessageTree } from "../types";

function flatten(tree: MessageTree, prefix = "", out: Record<string, string> = {}): Record<string, string> {
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === "string") {
      out[path] = value;
    } else {
      flatten(value, path, out);
    }
  }
  return out;
}

const en = flatten(enMessages);
const fr = flatten(frMessages);

describe("message catalog parity (en/fr)", () => {
  it("fr defines every en key", () => {
    const missing = Object.keys(en).filter((key) => !(key in fr));
    expect(missing).toEqual([]);
  });

  it("en defines every fr key", () => {
    const extra = Object.keys(fr).filter((key) => !(key in en));
    expect(extra).toEqual([]);
  });

  it("keeps {placeholders} identical between locales", () => {
    const placeholders = (value: string) => [...value.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort();
    const mismatched = Object.keys(en).filter(
      (key) => key in fr && placeholders(en[key]).join() !== placeholders(fr[key]).join(),
    );
    expect(mismatched).toEqual([]);
  });

  it("has an errors.* entry in both locales for every backend ErrorCodes constant", () => {
    const source = readFileSync(
      fileURLToPath(new URL("../../../../../src/Adeni.Domain/Common/ErrorCodes.cs", import.meta.url)),
      "utf8",
    );
    const codes = [...source.matchAll(/public const string \w+ = "([a-z_]+(?:\.[a-z_]+)+)";/g)].map((m) => m[1]);
    expect(codes.length).toBeGreaterThan(40);
    const missingEn = codes.filter((code) => t("en", `errors.${code}`) === `errors.${code}`);
    const missingFr = codes.filter((code) => t("fr", `errors.${code}`) === `errors.${code}`);
    expect(missingEn).toEqual([]);
    expect(missingFr).toEqual([]);
  });
});

describe("mergeMessageTrees", () => {
  it("merges disjoint trees deeply", () => {
    expect(mergeMessageTrees({ a: { b: "1" } }, { a: { c: "2" }, d: "3" })).toEqual({
      a: { b: "1", c: "2" },
      d: "3",
    });
  });

  it("throws on duplicate leaf keys", () => {
    expect(() => mergeMessageTrees({ a: { b: "1" } }, { a: { b: "2" } })).toThrow(/duplicate/);
  });
});
