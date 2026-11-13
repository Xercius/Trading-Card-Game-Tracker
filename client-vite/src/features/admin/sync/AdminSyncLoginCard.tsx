import { useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";

type Props = {
  title: string;
  isPending: boolean;
  error: unknown;
  onSubmit: (credentials: { username: string; password: string }) => Promise<void>;
};

/** Controlled administrator sign-in card shared by admin sync panels. */
export function AdminSyncLoginCard({ title, isPending, error, onSubmit }: Props) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!username.trim() || !password) {
      setValidationError("Username and password are required.");
      return;
    }

    setValidationError(null);
    try {
      await onSubmit({ username: username.trim(), password });
      setUsername("");
      setPassword("");
    } catch {
      setPassword("");
    }
  };

  const errorText =
    validationError ?? (error ? "Sign-in failed. Check your administrator credentials." : null);

  return (
    <section className="space-y-3 rounded border p-4">
      <h2 className="text-lg font-semibold">{title}</h2>
      <p>Sign in with an administrator account to manage sync.</p>
      <form className="space-y-3" onSubmit={handleSubmit} noValidate>
        <label className="block">
          Username
          <input
            className="block rounded border p-2"
            name="username"
            autoComplete="username"
            required
            maxLength={200}
            value={username}
            onChange={(e) => setUsername(e.target.value)}
          />
        </label>
        <label className="block">
          Password
          <input
            className="block rounded border p-2"
            name="password"
            type="password"
            autoComplete="current-password"
            required
            maxLength={1024}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
        </label>
        {errorText && (
          <p role="alert" className="text-sm text-destructive">
            {errorText}
          </p>
        )}
        <Button type="submit" disabled={isPending}>
          {isPending ? "Signing in…" : "Sign in"}
        </Button>
      </form>
    </section>
  );
}
