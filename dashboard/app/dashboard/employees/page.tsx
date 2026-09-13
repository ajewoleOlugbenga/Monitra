"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { getEmployees, createEmployee } from "@/lib/api";
import { Users, Monitor, Plus, X, ArrowRight } from "lucide-react";

interface EmployeeRow {
  id: string;
  fullName: string;
  email: string;
  employeeCode: string;
  status: string;
  device: { id: string; deviceName: string; deviceStatus: number; lastSeenAt: string | null } | null;
}

export default function EmployeesPage() {
  const [employees, setEmployees] = useState<EmployeeRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const load = () => {
    getEmployees()
      .then(setEmployees)
      .catch((e) => setError(e.message));
  };

  useEffect(load, []);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-extrabold text-white">Employees</h1>
          <p className="text-sm text-slate-400 mt-1">Everyone monitored under your organization.</p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="flex items-center space-x-2 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium px-4 py-2.5 rounded-lg transition"
        >
          <Plus className="h-4 w-4" />
          <span>Add employee</span>
        </button>
      </div>

      {error && (
        <div className="p-4 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-sm">
          {error}
        </div>
      )}

      <div className="bg-slate-900/40 border border-slate-850 rounded-xl overflow-hidden">
        {employees === null ? (
          <div className="p-10 text-center text-sm text-slate-500">Loading employees...</div>
        ) : employees.length === 0 ? (
          <div className="p-10 text-center text-sm text-slate-500">
            No employees yet. Add one to get started.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-850 text-left text-xs uppercase tracking-wider text-slate-500">
                <th className="px-5 py-3 font-semibold">Name</th>
                <th className="px-5 py-3 font-semibold">Email</th>
                <th className="px-5 py-3 font-semibold">Code</th>
                <th className="px-5 py-3 font-semibold">Device</th>
                <th className="px-5 py-3 font-semibold"></th>
              </tr>
            </thead>
            <tbody>
              {employees.map((emp) => (
                <tr key={emp.id} className="border-b border-slate-900 last:border-0 hover:bg-slate-900/40">
                  <td className="px-5 py-3 font-medium text-white">{emp.fullName}</td>
                  <td className="px-5 py-3 text-slate-400">{emp.email}</td>
                  <td className="px-5 py-3 text-slate-500 font-mono text-xs">{emp.employeeCode || "—"}</td>
                  <td className="px-5 py-3">
                    {emp.device ? (
                      <span className="inline-flex items-center gap-1.5 text-slate-300">
                        <Monitor className="h-3.5 w-3.5 text-violet-400" />
                        {emp.device.deviceName}
                      </span>
                    ) : (
                      <span className="text-slate-600">Not assigned</span>
                    )}
                  </td>
                  <td className="px-5 py-3 text-right">
                    <Link
                      href={`/dashboard/employees/${emp.id}`}
                      className="inline-flex items-center gap-1 text-blue-400 hover:text-blue-300 text-xs font-medium"
                    >
                      View <ArrowRight className="h-3 w-3" />
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showCreate && (
        <CreateEmployeeModal
          onClose={() => setShowCreate(false)}
          onCreated={() => {
            setShowCreate(false);
            load();
          }}
        />
      )}
    </div>
  );
}

function CreateEmployeeModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [employeeCode, setEmployeeCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await createEmployee({ fullName, email, employeeCode: employeeCode || undefined });
      onCreated();
    } catch (err: any) {
      setError(err.message || "Could not create employee.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 w-full max-w-md space-y-5">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-bold text-white flex items-center gap-2">
            <Users className="h-5 w-5 text-blue-400" /> Add employee
          </h3>
          <button onClick={onClose} className="text-slate-500 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>

        {error && (
          <div className="p-3 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-xs">
            {error}
          </div>
        )}

        <form onSubmit={submit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">Full name</label>
            <input
              required
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg py-2.5 px-3 text-sm text-white focus:outline-none focus:border-blue-500"
            />
          </div>
          <div className="space-y-1.5">
            <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">Email</label>
            <input
              required
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg py-2.5 px-3 text-sm text-white focus:outline-none focus:border-blue-500"
            />
          </div>
          <div className="space-y-1.5">
            <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">Employee code (optional)</label>
            <input
              value={employeeCode}
              onChange={(e) => setEmployeeCode(e.target.value)}
              className="w-full bg-slate-950 border border-slate-800 rounded-lg py-2.5 px-3 text-sm text-white focus:outline-none focus:border-blue-500"
            />
          </div>
          <button
            type="submit"
            disabled={saving}
            className="w-full bg-blue-600 hover:bg-blue-500 text-white font-medium py-2.5 rounded-lg text-sm transition disabled:opacity-50"
          >
            {saving ? "Adding..." : "Add employee"}
          </button>
        </form>
      </div>
    </div>
  );
}
