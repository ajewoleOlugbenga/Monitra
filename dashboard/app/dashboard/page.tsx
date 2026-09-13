"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { getMe, getEmployees, getDevices } from "@/lib/api";
import { Users, Monitor, ShieldAlert, ArrowRight, BarChart3 } from "lucide-react";

export default function DashboardOverview() {
  const [role, setRole] = useState<string | null>(null);
  const [employeeCount, setEmployeeCount] = useState<number | null>(null);
  const [deviceStats, setDeviceStats] = useState<{ total: number; degraded: number } | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getMe().then((me) => setRole(me.role));

    getEmployees()
      .then((list) => setEmployeeCount(Array.isArray(list) ? list.length : 0))
      .catch(() => setEmployeeCount(null)); // IT role gets 403 here - expected, not an error to show

    getDevices()
      .then((list) => {
        const total = Array.isArray(list) ? list.length : 0;
        const degraded = Array.isArray(list)
          ? list.filter((d: any) => d.health && d.health.status !== 0).length
          : 0;
        setDeviceStats({ total, degraded });
      })
      .catch((e) => setError(e.message));
  }, []);

  return (
    <div className="space-y-8">
      <section>
        <h1 className="text-2xl font-extrabold text-white">Welcome back</h1>
        <p className="text-sm text-slate-400 mt-1">
          Here's what's happening across your organization right now.
        </p>
      </section>

      <section className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {role !== "ITSupport" && (
          <Link
            href="/dashboard/employees"
            className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-3 hover:border-blue-500/30 transition group"
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Employees</span>
              <Users className="h-5 w-5 text-blue-400" />
            </div>
            <div className="text-3xl font-black">{employeeCount ?? "—"}</div>
            <p className="text-xs text-slate-500 flex items-center gap-1 group-hover:text-blue-400">
              View directory <ArrowRight className="h-3 w-3" />
            </p>
          </Link>
        )}

        <Link
          href="/dashboard/devices"
          className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-3 hover:border-blue-500/30 transition group"
        >
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Devices</span>
            <Monitor className="h-5 w-5 text-violet-400" />
          </div>
          <div className="text-3xl font-black">{deviceStats?.total ?? "—"}</div>
          <p className="text-xs text-slate-500 flex items-center gap-1 group-hover:text-blue-400">
            View fleet health <ArrowRight className="h-3 w-3" />
          </p>
        </Link>

        <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-xs font-bold uppercase tracking-wider text-slate-400">Needs Attention</span>
            <ShieldAlert className="h-5 w-5 text-rose-400" />
          </div>
          <div className="text-3xl font-black text-rose-500">{deviceStats?.degraded ?? "—"}</div>
          <p className="text-xs text-slate-500">Devices reporting degraded or critical health</p>
        </div>
      </section>

      <section className="bg-slate-900/30 border border-slate-900 p-6 rounded-2xl flex flex-col items-center justify-center py-16 text-center space-y-3">
        <BarChart3 className="h-10 w-10 text-slate-700" />
        <h3 className="font-bold text-slate-300">Attendance &amp; Productivity Reports</h3>
        <p className="text-xs text-slate-500 max-w-sm">
          Reporting on activity, attendance, and productivity trends requires the aggregation
          pipeline (Phase 3 of the roadmap), which hasn't been built yet - this isn't a loading
          state, there's no data to show.
        </p>
      </section>

      {error && (
        <p className="text-xs text-rose-400">Could not load device data: {error}</p>
      )}
    </div>
  );
}
