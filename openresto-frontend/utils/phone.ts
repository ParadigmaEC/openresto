import {
  getCountries,
  getCountryCallingCode,
  parsePhoneNumberFromString,
  type CountryCode,
} from "libphonenumber-js";

export type PhoneCountry = CountryCode;

/** The dial code a phone field opens on. The product's home market is Ecuador. */
export const DEFAULT_PHONE_COUNTRY: PhoneCountry = "EC";

export interface PhoneCountryOption {
  label: string;
  value: PhoneCountry;
}

/**
 * The guest's number in E.164 (`+593991234567`), or null when it is not a valid number for the
 * selected country. The national format a guest is used to typing, trunk `0` included, is read
 * against that country; a number typed with a leading `+` is read as international and the
 * picker is ignored, since the guest has already said which country it belongs to.
 *
 * @see [phone.test.ts](../tests/utils/phone.test.ts) — pins the Ecuadorian mobile in both its
 * national forms, one digit short and one digit over, an explicit `+`, and garbage.
 */
export function toE164(country: PhoneCountry, input: string): string | null {
  const trimmed = input.trim();
  if (!trimmed) return null;
  const parsed = trimmed.startsWith("+")
    ? parsePhoneNumberFromString(trimmed)
    : parsePhoneNumberFromString(trimmed, country);
  return parsed?.isValid() ? parsed.number : null;
}

/** An E.164 number spaced for reading (`+593 99 123 4567`); anything unparseable as stored. */
export function formatPhoneForDisplay(value: string): string {
  const parsed = parsePhoneNumberFromString(value);
  return parsed ? parsed.formatInternational() : value;
}

type RegionNames = { of: (code: string) => string | undefined };

/**
 * Hermes ships without `Intl.DisplayNames`, and a locale it cannot resolve throws, so either
 * way the picker falls back to the bare ISO code rather than failing to render.
 */
function regionNames(locale: string | undefined): RegionNames | null {
  const DisplayNames = (Intl as { DisplayNames?: new (...args: unknown[]) => RegionNames })
    .DisplayNames;
  if (!DisplayNames) return null;
  try {
    return new DisplayNames(locale ? [locale] : undefined, { type: "region" });
  } catch {
    return null;
  }
}

/** A collator for the viewer's language, or the device's where that locale is refused. */
function collator(locale: string | undefined): Intl.Collator {
  try {
    return new Intl.Collator(locale);
  } catch {
    return new Intl.Collator();
  }
}

/**
 * Every country the metadata knows, labelled "Ecuador (+593)" in the viewer's language and
 * sorted by that label, so typeahead in the picker lands where a reader expects.
 *
 * @see [phone.test.ts](../tests/utils/phone.test.ts) — pins the label, the ordering, and the
 * ISO-code fallback where `Intl.DisplayNames` is missing.
 */
export function buildPhoneCountryOptions(locale?: string): PhoneCountryOption[] {
  const names = regionNames(locale);
  const { compare } = collator(locale);
  return getCountries()
    .map((code) => ({
      label: `${names?.of(code) ?? code} (+${getCountryCallingCode(code)})`,
      value: code,
    }))
    .sort((a, b) => compare(a.label, b.label));
}
