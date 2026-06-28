"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getMe, tenantLogout } from "@/lib/api";
import { Activity, LogOut, Users, ShieldAlert, Monitor, CheckCircle, BarChart3 } from "lucide-react";

export default function TenantDashboard() {
  const router = useRouter();
  const [profile, setProfile] = useState<any | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadProfile();
  }, []);

  const loadProfile = async () => {
    try {
      const data = await getMe();
      setProfile(data);
    } catch {
      // In case session expired or missing
      router.push("/login");
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = async () => {
    try {
      await tenantLogout();
      router.push("/login");
    } catch {
      router.push("/login");
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center flex-col space-y-4">
        <div className="h-8 w-8 border-4 border-slate-800 border-t-blue-500 rounded-full animate-spin" />
        <p className="text-sm text-slate-500">Loading your profile settings...</p>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col">
      {/* Header */}
      <header className="border-b border-slate-900 bg-slate-900/40 backdrop-blur-md px-6 py-4 flex items-center justify-between">
        <div className="flex items-center space-x-2.5">
          <Activity className="h-6 w-6 text-blue-500" />
          <span className="text-xl font-bold tracking-tight text-white">MONITRA</span>
          <span className="text-[10px] uppercase font-bold tracking-widest bg-blue-500/10 text-blue-400 px-2 py-0.5 rounded border border-blue-500/20">
            Console
          </span>
        </div>
        <div className="flex items-center space-x-4">
          <div className="text-right hidden md:block">
            <p className="text-sm font-semibold text-white">{profile?.fullName}</p>
            <p className="text-xs text-slate-500">Role: {profile?.role}</p>
          </div>
          <button
            onClick={handleLogout}
            className="flex items-center space-x-2 text-sm font-medium text-slate-400 hover:text-white bg-slate-900 hover:bg-slate-850 px-4 py-2 rounded-lg border border-slate-850 transition duration-200"
          >
            <LogOut className="h-4 w-4" />
            <span>Sign Out</span>
          </button>
        </div>
      </header>

      {/* Main Container */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-6 md:p-8 space-y-8">
        
        {/* Banner */}
        <section className="bg-gradient-to-r from-blue-900/20 to-slate-900/40 border border-blue-500/10 rounded-2xl p-6 md:p-8 flex flex-col md:flex-row md:items-center justify-between gap-6">
          <div className="space-y-2">
            <h2 className="text-2xl font-extrabold text-white">
              Welcome back to your Organization Portal!
            </h2>
            <p className="text-sm text-slate-400 max-w-xl">
              Strict multi-tenant security is actively isolating your data. Your organization ID is <span className="font-mono text-blue-400 bg-blue-500/10 px-1.5 py-0.5 rounded text-xs">{profile?.tenantId}</span>.
            </p>
          </div>
          <div className="flex items-center space-x-3 bg-slate-950/60 border border-slate-850 px-4 py-3 rounded-xl">
            <CheckCircle className="h-5 w-5 text-emerald-400" />
            <span className="text-xs font-semibold text-slate-300">Data Isolation Verified</span>
          </div>
        </section>

        {/* Overview cards */}
        <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Total Employees</span>
              <Users className="h-5 w-5 text-blue-400" />
            </div>
            <div className="text-3xl font-black">1</div>
            <p className="text-xs text-slate-500">Seed developer employee loaded</p>
          </div>

          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Active Devices</span>
              <Monitor className="h-5 w-5 text-violet-400" />
            </div>
            <div className="text-3xl font-black">0</div>
            <p className="text-xs text-slate-500">No workstation agent connected yet</p>
          </div>

          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Distraction Alerts</span>
              <ShieldAlert className="h-5 w-5 text-rose-400" />
            </div>
            <div className="text-3xl font-black text-rose-500">0</div>
            <p className="text-xs text-slate-500">Zero active alerts triggered</p>
          </div>
        </section>

        {/* Main layout placeholder */}
        <section className="grid grid-cols-1 md:grid-cols-2 gap-8">
          <div className="bg-slate-900/30 border border-slate-900 p-6 rounded-2xl flex flex-col items-center justify-center py-20 text-center space-y-3">
            <BarChart3 className="h-10 w-10 text-slate-700" />
            <h3 className="font-bold text-slate-300">Activity Telemetry Dashboard</h3>
            <p className="text-xs text-slate-500 max-w-sm">
              Workstation app monitoring, idle popup submissions, and daily attendance aggregations will appear here in Phase 2 & 3.
            </p>
          </div>

          <div className="bg-slate-900/30 border border-slate-900 p-6 rounded-2xl flex flex-col items-center justify-center py-20 text-center space-y-3">
            <Users className="h-10 w-10 text-slate-700" />
            <h3 className="font-bold text-slate-300">Workstation Installer Download</h3>
            <p className="text-xs text-slate-500 max-w-sm">
              Go to the installation instructions tab to download the MSI package and assign onboarding activation codes.
            </p>
          </div>
        </section>

      </main>
    </div>
  );
}
