import Header from "@/components/app/Header";
import { Outlet } from "react-router-dom";

export default function AppShell() {
  return (
    <div className="min-h-dvh flex flex-col">
      <Header />
      <main className="flex-1">
        <div className="w-full px-4 sm:px-6 py-4">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
