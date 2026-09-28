import { StyleSheet } from "react-native";
import { theme } from "@/theme/theme";

/** Wide enough for "Ecuador (+593)" before the row gives up and wraps the number underneath. */
const COUNTRY_MIN_WIDTH = 150;
/** A national number plus a little air; narrower than this and the row wraps instead. */
const NUMBER_MIN_WIDTH = 140;

export const styles = StyleSheet.create({
  container: {
    gap: theme.spacing.xs,
  },
  row: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: theme.spacing.sm,
  },
  country: {
    flexGrow: 1,
    flexBasis: COUNTRY_MIN_WIDTH,
    minWidth: COUNTRY_MIN_WIDTH,
    maxWidth: "100%",
  },
  number: {
    flexGrow: 2,
    flexBasis: NUMBER_MIN_WIDTH,
    minWidth: NUMBER_MIN_WIDTH,
  },
  hint: {
    fontSize: 12,
  },
});
