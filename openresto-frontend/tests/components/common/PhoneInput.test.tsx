import React, { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react-native";
import PhoneInput, {
  EMPTY_PHONE,
  phoneE164,
  type PhoneValue,
} from "@/components/common/PhoneInput";

jest.mock("@expo/vector-icons", () => ({ Ionicons: () => null }));
jest.mock("@/hooks/use-color-scheme", () => ({ useColorScheme: () => "light" }));
jest.mock("@/context/BrandContext", () => ({
  useBrand: () => ({ primaryColor: "#0a7ea4", appName: "Open Resto" }),
}));
jest.mock("@/utils/haptics", () => ({
  haptics: { selection: jest.fn(), press: jest.fn(), outcome: jest.fn() },
}));

const onValue = jest.fn();

function Harness({ invalidHint }: { invalidHint?: string }) {
  const [value, setValue] = useState<PhoneValue>(EMPTY_PHONE);
  return (
    <PhoneInput
      value={value}
      onChange={(next) => {
        onValue(next);
        setValue(next);
      }}
      numberAccessibilityLabel="Phone number"
      countryAccessibilityLabel="Phone country code"
      placeholder="099 123 4567"
      invalidHint={invalidHint}
      testID="phone"
    />
  );
}

beforeEach(() => onValue.mockClear());

describe("PhoneInput", () => {
  it("opens on Ecuador", () => {
    render(<Harness />);
    expect(screen.getByText("Ecuador (+593)")).toBeTruthy();
  }, 15000);

  it("reports the number as typed, against the picked country", () => {
    render(<Harness />);
    fireEvent.changeText(screen.getByLabelText("Phone number"), "0991234567");
    expect(onValue).toHaveBeenLastCalledWith({ country: "EC", national: "0991234567" });
  });

  it("changes country and keeps the number", () => {
    render(<Harness />);
    fireEvent.changeText(screen.getByLabelText("Phone number"), "4155552671");
    fireEvent.press(screen.getByText("Ecuador (+593)"));
    fireEvent.press(screen.getByText("United States (+1)"));
    expect(onValue).toHaveBeenLastCalledWith({ country: "US", national: "4155552671" });
    expect(screen.getByText("United States (+1)")).toBeTruthy();
  });

  it("carries the platform's phone keyboard and autofill hints", () => {
    render(<Harness />);
    const input = screen.getByTestId("phone");
    expect(input.props.keyboardType).toBe("phone-pad");
    expect(input.props.textContentType).toBe("telephoneNumber");
    expect(input.props.autoComplete).toBe("tel");
  });

  it("shows the hint for a typed number that is invalid, and not for a blank or valid one", () => {
    render(<Harness invalidHint="Not a valid number" />);
    expect(screen.queryByText("Not a valid number")).toBeNull();

    fireEvent.changeText(screen.getByLabelText("Phone number"), "099123456");
    expect(screen.getByText("Not a valid number")).toBeTruthy();

    fireEvent.changeText(screen.getByLabelText("Phone number"), "0991234567");
    expect(screen.queryByText("Not a valid number")).toBeNull();
  });

  it("shows no hint when the caller gives none", () => {
    render(<Harness />);
    fireEvent.changeText(screen.getByLabelText("Phone number"), "099");
    expect(screen.queryByRole("alert")).toBeNull();
  });
});

describe("phoneE164", () => {
  it("is the E.164 for a valid number and null otherwise", () => {
    expect(phoneE164({ country: "EC", national: "0991234567" })).toBe("+593991234567");
    expect(phoneE164(EMPTY_PHONE)).toBeNull();
  });
});
