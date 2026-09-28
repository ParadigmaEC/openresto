import { memo } from "react";
import { View } from "react-native";
import { useTranslation } from "react-i18next";
import Input from "@/components/common/Input";
import Select from "@/components/common/Select";
import { ThemedText } from "@/components/themed-text";
import { useAppTheme } from "@/hooks/use-app-theme";
import {
  buildPhoneCountryOptions,
  DEFAULT_PHONE_COUNTRY,
  toE164,
  type PhoneCountry,
} from "@/utils/phone";
import { styles } from "./PhoneInput.styles";

/** What the guest has entered: the dial-code pick and the number exactly as typed. */
export interface PhoneValue {
  country: PhoneCountry;
  national: string;
}

export const EMPTY_PHONE: PhoneValue = { country: DEFAULT_PHONE_COUNTRY, national: "" };

/** The value in E.164, or null while it is blank or not a number in the picked country. */
export function phoneE164(value: PhoneValue): string | null {
  return toE164(value.country, value.national);
}

/**
 * A dial-code picker beside the number, in one row that wraps the number underneath when the
 * two will not fit side by side. It holds no state of its own; callers keep the `PhoneValue`
 * and derive the E.164 through `phoneE164`. The invalid hint appears only once something has
 * been typed, so an untouched required field does not open on an error.
 *
 * @see [PhoneInput.test.tsx](../../tests/components/common/PhoneInput.test.tsx) — pins the
 * Ecuador default, a country change, and the hint showing only for a typed, invalid number.
 */
export default memo(PhoneInput);

/** Built once per language: 245 localized labels are not worth recomputing per keystroke. */
const optionsByLocale = new Map<string, ReturnType<typeof buildPhoneCountryOptions>>();
function countryOptions(locale: string) {
  let options = optionsByLocale.get(locale);
  if (!options) {
    options = buildPhoneCountryOptions(locale);
    optionsByLocale.set(locale, options);
  }
  return options;
}

function PhoneInput({
  value,
  onChange,
  numberAccessibilityLabel,
  countryAccessibilityLabel,
  placeholder,
  invalidHint,
  testID,
}: {
  value: PhoneValue;
  onChange: (value: PhoneValue) => void;
  numberAccessibilityLabel: string;
  countryAccessibilityLabel: string;
  placeholder?: string;
  /** Shown under the row while the typed number is not valid for the picked country. */
  invalidHint?: string;
  testID?: string;
}) {
  const { i18n } = useTranslation();
  const { colors } = useAppTheme();
  const options = countryOptions(i18n.language);
  const showInvalid = !!invalidHint && value.national.trim() !== "" && !phoneE164(value);

  return (
    <View style={styles.container}>
      <View style={styles.row}>
        <View style={styles.country}>
          <Select
            icon="call-outline"
            accessibilityLabel={countryAccessibilityLabel}
            selectedValue={value.country}
            options={options}
            onSelect={(country) => onChange({ ...value, country: country as PhoneCountry })}
          />
        </View>
        <View style={styles.number}>
          <Input
            testID={testID}
            placeholder={placeholder}
            accessibilityLabel={numberAccessibilityLabel}
            value={value.national}
            onChangeText={(national) => onChange({ ...value, national })}
            keyboardType="phone-pad"
            textContentType="telephoneNumber"
            autoComplete="tel"
            returnKeyType="next"
            blurOnSubmit={false}
          />
        </View>
      </View>
      {showInvalid && (
        <ThemedText
          style={[styles.hint, { color: colors.error }]}
          role="alert"
          accessibilityLiveRegion="polite"
        >
          {invalidHint}
        </ThemedText>
      )}
    </View>
  );
}
