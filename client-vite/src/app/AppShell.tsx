import Header from "@/components/app/Header";
import { Outlet } from "react-router-dom";

export default function AppShell() {
  return (
    <div className="min-h-dvh flex flex-col">
      <Header />
      <main className="flex-1">
        <div className="mx-auto w-full p-4">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
