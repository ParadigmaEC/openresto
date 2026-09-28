import {
  buildPhoneCountryOptions,
  DEFAULT_PHONE_COUNTRY,
  formatPhoneForDisplay,
  toE164,
} from "@/utils/phone";

describe("toE164", () => {
  it("defaults to Ecuador", () => {
    expect(DEFAULT_PHONE_COUNTRY).toBe("EC");
  });

  it("reads the Ecuadorian national format, trunk 0 included", () => {
    expect(toE164("EC", "0991234567")).toBe("+593991234567");
  });

  it("reads an Ecuadorian mobile typed without the trunk 0", () => {
    expect(toE164("EC", "99 123 4567")).toBe("+593991234567");
  });

  it("rejects an Ecuadorian mobile one digit short", () => {
    expect(toE164("EC", "099123456")).toBeNull();
  });

  it("rejects an Ecuadorian mobile one digit over", () => {
    expect(toE164("EC", "09912345678")).toBeNull();
  });

  it("reads a number against the country picked, not Ecuador", () => {
    expect(toE164("US", "(415) 555-2671")).toBe("+14155552671");
  });

  it("rejects a number that is not valid for the country picked", () => {
    expect(toE164("US", "0991234567")).toBeNull();
  });

  it("honours an explicit + as international, whatever the picker says", () => {
    expect(toE164("EC", "+1 415 555 2671")).toBe("+14155552671");
  });

  it("rejects an explicit + number that is not valid", () => {
    expect(toE164("EC", "+1 415")).toBeNull();
  });

  it("rejects garbage and blanks", () => {
    expect(toE164("EC", "not a phone")).toBeNull();
    expect(toE164("EC", "   ")).toBeNull();
  });
});

describe("formatPhoneForDisplay", () => {
  it("spaces an E.164 number for reading", () => {
    expect(formatPhoneForDisplay("+593991234567")).toBe("+593 99 123 4567");
  });

  it("keeps a value it cannot parse as stored", () => {
    expect(formatPhoneForDisplay("call the desk")).toBe("call the desk");
  });
});

describe("buildPhoneCountryOptions", () => {
  it("labels each country with its localized name and dial code", () => {
    const options = buildPhoneCountryOptions("en");
    expect(options.find((o) => o.value === "EC")?.label).toBe("Ecuador (+593)");
    expect(options.find((o) => o.value === "US")?.label).toBe("United States (+1)");
  });

  it("follows the viewer's language", () => {
    const options = buildPhoneCountryOptions("es");
    expect(options.find((o) => o.value === "US")?.label).toBe("Estados Unidos (+1)");
  });

  it("sorts by label", () => {
    const labels = buildPhoneCountryOptions("en").map((o) => o.label);
    expect(labels).toEqual([...labels].sort((a, b) => a.localeCompare(b, "en")));
  });

  it("falls back to the ISO code where Intl.DisplayNames is missing", () => {
    const intl = Intl as { DisplayNames?: unknown };
    const original = intl.DisplayNames;
    delete intl.DisplayNames;
    try {
      const options = buildPhoneCountryOptions("en");
      expect(options.find((o) => o.value === "EC")?.label).toBe("EC (+593)");
    } finally {
      intl.DisplayNames = original;
    }
  });

  it("falls back to the ISO code for a locale DisplayNames refuses", () => {
    const options = buildPhoneCountryOptions("not a locale!");
    expect(options.find((o) => o.value === "EC")?.label).toBe("EC (+593)");
  });

  it("uses the device locale when none is given", () => {
    expect(buildPhoneCountryOptions().some((o) => o.value === "EC")).toBe(true);
  });
});
