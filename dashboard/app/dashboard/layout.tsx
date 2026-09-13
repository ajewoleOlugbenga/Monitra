"use client";

import React, { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";
import { getMe, tenantLogout } from "@/lib/api";
import {
  Activity,
  LogOut,
  Users,
  Monitor,
  Settings as SettingsIcon,
  LayoutDashboard,
} from "lucide-react";

interface Profile {
  id: string;
  fullName: string;
  email: string;
  role: "Owner" | "Admin" | "Viewer" | "ITSupport";
  tenantId: string;
}

const NAV_ITEMS = [
  { href: "/dashboard", label: "Overview", icon: LayoutDashboard, roles: ["Owner", "Admin", "Viewer", "ITSupport"] },
  { href: "/dashboard/employees", label: "Employees", icon: Users, roles: ["Owner", "Admin", "Viewer"] },
  { href: "/dashboard/devices", label: "Devices", icon: Monitor, roles: ["Owner", "Admin", "Viewer", "ITSupport"] },
  { href: "/dashboard/settings", label: "Settings", icon: SettingsIcon, roles: ["Owner", "Admin"] },
];

export default function DashboardLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    getMe()
      .then((data) => setProfile(data))
      .catch(() => router.push("/login"))
      .finally(() => setLoading(false));
  }, [router]);

  const handleLogout = async () => {
    try {
      await tenantLogout();
    } finally {
      router.push("/login");
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center flex-col space-y-4">
        <div className="h-8 w-8 border-4 border-slate-800 border-t-blue-500 rounded-full animate-spin" />
        <p className="text-sm text-slate-500">Loading your workspace...</p>
      </div>
    );
  }

  if (!profile) return null; // redirect already triggered

  const visibleNav = NAV_ITEMS.filter((item) => item.roles.includes(profile.role));

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex">
      {/* Sidebar */}
      <aside className="w-64 shrink-0 border-r border-slate-900 bg-slate-900/30 flex flex-col">
        <div className="px-5 py-5 flex items-center space-x-2.5 border-b border-slate-900">
          <Activity className="h-6 w-6 text-blue-500" />
          <span className="text-lg font-bold tracking-tight text-white">MONITRA</span>
        </div>

        <nav className="flex-1 px-3 py-4 space-y-1">
          {visibleNav.map((item) => {
            const active = pathname === item.href;
            const Icon = item.icon;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`flex items-center space-x-3 px-3 py-2.5 rounded-lg text-sm font-medium transition ${
                  active
                    ? "bg-blue-500/10 text-blue-400 border border-blue-500/20"
                    : "text-slate-400 hover:text-white hover:bg-slate-900"
                }`}
              >
                <Icon className="h-4 w-4" />
                <span>{item.label}</span>
              </Link>
            );
          })}
        </nav>

        <div className="px-3 py-4 border-t border-slate-900 space-y-3">
          <div className="px-3">
            <p className="text-sm font-semibold text-white truncate">{profile.fullName}</p>
            <p className="text-xs text-slate-500">
              {profile.role === "ITSupport" ? "IT Support" : profile.role}
            </p>
          </div>
          <button
            onClick={handleLogout}
            className="w-full flex items-center space-x-2 text-sm font-medium text-slate-400 hover:text-white bg-slate-900 hover:bg-slate-850 px-3 py-2 rounded-lg border border-slate-850 transition duration-200"
          >
            <LogOut className="h-4 w-4" />
            <span>Sign Out</span>
          </button>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 min-w-0 overflow-y-auto">
        <div className="max-w-6xl mx-auto p-6 md:p-8">{children}</div>
      </main>
    </div>
  );
}
