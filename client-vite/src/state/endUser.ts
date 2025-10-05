import { useContext } from "react";
import { UserContext } from "./UserContext";

export function useUser() {
  const ctx = useContext(UserContext);
  if (!ctx) throw new Error("useUser must be inside UserProvider");
  return ctx;
}
