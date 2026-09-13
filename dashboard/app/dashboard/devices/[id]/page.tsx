"use client";

import React, { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { getDeviceHealthHistory, getDeviceLogs } from "@/lib/api";
import { ArrowLeft, Activity, FileText, Cpu } from "lucide-react";

const LOG_LEVEL_STYLE = ["text-slate-400", "text-amber-400", "text-rose-400"];
const LOG_LEVEL_NAME = ["Info", "Warning", "Error"];

export default function DeviceDetailPage() {
  const params = useParams();
  const deviceId = params.id as string;

  const [health, setHealth] = useState<any[] | null>(null);
  const [logs, setLogs] = useState<any[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getDeviceHealthHistory(deviceId).then(setHealth).catch((e) => setError(e.message));
    getDeviceLogs(deviceId).then(setLogs).catch(() => setLogs([]));
  }, [deviceId]);

  const latest = health?.[0];
  const topProcesses: { ProcessName: string; MemoryMb: number }[] = latest?.topProcessesJson
    ? JSON.parse(latest.topProcessesJson)
    : [];

  return (
    <div className="space-y-8">
      <div>
        <Link href="/dashboard/devices" className="inline-flex items-center gap-1.5 text-xs text-slate-400 hover:text-white mb-4">
          <ArrowLeft className="h-3.5 w-3.5" /> Back to devices
        </Link>
        <h1 className="text-2xl font-extrabold text-white">Device health</h1>
      </div>

      {error && (
        <div className="p-4 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-sm">{error}</div>
      )}

      {latest && (
        <section className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <Metric label="CPU" value={`${latest.cpuUsagePercent}%`} />
          <Metric label="Memory" value={`${latest.memoryUsagePercent}%`} />
          <Metric label="Disk free" value={`${latest.diskFreeGb} GB`} />
          <Metric label="Battery" value={latest.batteryPercent != null ? `${latest.batteryPercent}%` : "N/A"} />
        </section>
      )}

      <section className="bg-slate-900/40 border border-slate-850 rounded-xl p-5">
        <h2 className="text-sm font-bold text-white flex items-center gap-2 mb-3">
          <Cpu className="h-4 w-4 text-violet-400" /> Top resource-consuming apps
        </h2>
        {topProcesses.length === 0 ? (
          <p className="text-xs text-slate-500">No process data reported yet.</p>
        ) : (
          <div className="space-y-2">
            {topProcesses.map((p, i) => (
              <div key={i} className="flex items-center justify-between text-sm">
                <span className="text-slate-300">{p.ProcessName}</span>
                <span className="text-slate-500 font-mono text-xs">{p.MemoryMb} MB</span>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="bg-slate-900/40 border border-slate-850 rounded-xl p-5">
        <h2 className="text-sm font-bold text-white flex items-center gap-2 mb-3">
          <Activity className="h-4 w-4 text-blue-400" /> Health history
        </h2>
        {health === null ? (
          <p className="text-xs text-slate-500">Loading...</p>
        ) : health.length === 0 ? (
          <p className="text-xs text-slate-500">No health snapshots yet.</p>
        ) : (
          <div className="divide-y divide-slate-900">
            {health.slice(0, 20).map((h) => (
              <div key={h.id} className="py-2 flex items-center justify-between text-xs">
                <span className="text-slate-500">{new Date(h.capturedAt).toLocaleString()}</span>
                <span className="text-slate-400">
                  CPU {h.cpuUsagePercent}% · RAM {h.memoryUsagePercent}% · Disk free {h.diskFreeGb} GB
                </span>
              </div>
            ))}
          </div>
        )}
      </section>

      <section className="bg-slate-900/40 border border-slate-850 rounded-xl p-5">
        <h2 className="text-sm font-bold text-white flex items-center gap-2 mb-3">
          <FileText className="h-4 w-4 text-slate-400" /> Recent logs
        </h2>
        {logs === null ? (
          <p className="text-xs text-slate-500">Loading...</p>
        ) : logs.length === 0 ? (
          <p className="text-xs text-slate-500">No logs reported yet.</p>
        ) : (
          <div className="divide-y divide-slate-900 font-mono">
            {logs.slice(0, 50).map((l) => (
              <div key={l.id} className="py-2 flex items-start gap-3 text-xs">
                <span className="text-slate-600 shrink-0">{new Date(l.createdAt).toLocaleTimeString()}</span>
                <span className={`shrink-0 font-semibold ${LOG_LEVEL_STYLE[l.level]}`}>
                  {LOG_LEVEL_NAME[l.level]}
                </span>
                <span className="text-slate-400">{l.message}</span>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-slate-900/40 border border-slate-850 p-4 rounded-xl text-center">
      <div className="text-xl font-black text-white">{value}</div>
      <div className="text-xs text-slate-500 mt-1">{label}</div>
    </div>
  );
}
