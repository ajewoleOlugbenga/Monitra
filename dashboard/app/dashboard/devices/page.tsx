"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { getDevices } from "@/lib/api";
import { Monitor, ArrowRight, HeartPulse } from "lucide-react";

const HEALTH_STATUS = ["Healthy", "Degraded", "Critical"];
const HEALTH_STYLE = [
  "text-emerald-400 border-emerald-500/30 bg-emerald-500/5",
  "text-amber-400 border-amber-500/30 bg-amber-500/5",
  "text-rose-400 border-rose-500/30 bg-rose-500/5",
];

export default function DevicesPage() {
  const [devices, setDevices] = useState<any[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getDevices().then(setDevices).catch((e) => setError(e.message));
  }, []);

  const counts = { healthy: 0, degraded: 0, critical: 0 };
  devices?.forEach((d) => {
    if (!d.health) return;
    if (d.health.status === 0) counts.healthy++;
    else if (d.health.status === 1) counts.degraded++;
    else counts.critical++;
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-extrabold text-white">Devices</h1>
        <p className="text-sm text-slate-400 mt-1">
          Fleet health across every registered workstation - no behavioral data here, just machine health.
        </p>
      </div>

      {error && (
        <div className="p-4 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-sm">
          {error}
        </div>
      )}

      <div className="grid grid-cols-3 gap-4">
        <div className="bg-slate-900/40 border border-slate-850 p-4 rounded-xl text-center">
          <div className="text-2xl font-black text-emerald-400">{counts.healthy}</div>
          <div className="text-xs text-slate-500 mt-1">Healthy</div>
        </div>
        <div className="bg-slate-900/40 border border-slate-850 p-4 rounded-xl text-center">
          <div className="text-2xl font-black text-amber-400">{counts.degraded}</div>
          <div className="text-xs text-slate-500 mt-1">Degraded</div>
        </div>
        <div className="bg-slate-900/40 border border-slate-850 p-4 rounded-xl text-center">
          <div className="text-2xl font-black text-rose-400">{counts.critical}</div>
          <div className="text-xs text-slate-500 mt-1">Critical</div>
        </div>
      </div>

      <div className="bg-slate-900/40 border border-slate-850 rounded-xl overflow-hidden">
        {devices === null ? (
          <div className="p-10 text-center text-sm text-slate-500">Loading devices...</div>
        ) : devices.length === 0 ? (
          <div className="p-10 text-center text-sm text-slate-500">
            No devices registered yet. Generate an install token from the platform console to onboard one.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-850 text-left text-xs uppercase tracking-wider text-slate-500">
                <th className="px-5 py-3 font-semibold">Device</th>
                <th className="px-5 py-3 font-semibold">OS</th>
                <th className="px-5 py-3 font-semibold">Health</th>
                <th className="px-5 py-3 font-semibold">CPU / RAM</th>
                <th className="px-5 py-3 font-semibold">Last seen</th>
                <th className="px-5 py-3 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {devices.map((d) => (
                <tr key={d.id} className="border-b border-slate-900 last:border-0 hover:bg-slate-900/40">
                  <td className="px-5 py-3 font-medium text-white flex items-center gap-2">
                    <Monitor className="h-4 w-4 text-violet-400" /> {d.deviceName}
                  </td>
                  <td className="px-5 py-3 text-slate-400">{d.operatingSystem}</td>
                  <td className="px-5 py-3">
                    {d.health ? (
                      <span className={`text-[10px] font-semibold uppercase tracking-wide border rounded-full px-2.5 py-1 ${HEALTH_STYLE[d.health.status]}`}>
                        {HEALTH_STATUS[d.health.status]}
                      </span>
                    ) : (
                      <span className="text-slate-600 text-xs">No data</span>
                    )}
                  </td>
                  <td className="px-5 py-3 text-slate-400 text-xs">
                    {d.health ? `${d.health.cpuUsagePercent}% / ${d.health.memoryUsagePercent}%` : "—"}
                  </td>
                  <td className="px-5 py-3 text-slate-500 text-xs">
                    {d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleString() : "Never"}
                  </td>
                  <td className="px-5 py-3 text-right">
                    <Link
                      href={`/dashboard/devices/${d.id}`}
                      className="inline-flex items-center gap-1 text-blue-400 hover:text-blue-300 text-xs font-medium"
                    >
                      <HeartPulse className="h-3 w-3" /> Details <ArrowRight className="h-3 w-3" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
