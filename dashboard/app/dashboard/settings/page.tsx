"use client";

import React, { useEffect, useState } from "react";
import { getTenantSettings, updateTenantSettings } from "@/lib/api";
import { Save, CheckCircle } from "lucide-react";

const WEEKDAYS = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

export default function SettingsPage() {
  const [form, setForm] = useState<any>(null);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    getTenantSettings()
      .then((s) =>
        setForm({
          ...s,
          workDaysSet: new Set((s.workDays as string).split(",").filter(Boolean)),
        })
      )
      .catch((e) => setError(e.message));
  }, []);

  const toggleWorkDay = (day: string) => {
    setForm((prev: any) => {
      const next = new Set(prev.workDaysSet);
      if (next.has(day)) next.delete(day);
      else next.add(day);
      return { ...prev, workDaysSet: next };
    });
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      await updateTenantSettings({
        defaultWorkStartTime: form.defaultWorkStartTime,
        defaultWorkEndTime: form.defaultWorkEndTime,
        workDays: Array.from(form.workDaysSet).join(","),
        trackWeekends: form.trackWeekends,
        idleThresholdMinutes: Number(form.idleThresholdMinutes),
        urlTrackingMode: form.urlTrackingMode,
        keystrokeTrackingMode: form.keystrokeTrackingMode,
        dataRetentionDays: Number(form.dataRetentionDays),
        breaksPerDay: Number(form.breaksPerDay),
        breakDurationMinutes: Number(form.breakDurationMinutes),
        maxUnjustifiedInactivityBeforeEscalation: Number(form.maxUnjustifiedInactivityBeforeEscalation),
      });
      setSaved(true);
    } catch (err: any) {
      setError(err.message || "Could not save settings.");
    } finally {
      setSaving(false);
    }
  };

  if (!form) {
    return <p className="text-sm text-slate-500">Loading settings...</p>;
  }

  return (
    <div className="space-y-6 max-w-2xl">
      <div>
        <h1 className="text-2xl font-extrabold text-white">Settings</h1>
        <p className="text-sm text-slate-400 mt-1">
          Configure your organization's tracking window, break policy, and inactivity thresholds.
        </p>
      </div>

      {error && (
        <div className="p-4 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-sm">{error}</div>
      )}
      {saved && (
        <div className="p-4 bg-emerald-500/10 border border-emerald-500/20 text-emerald-200 rounded-lg text-sm flex items-center gap-2">
          <CheckCircle className="h-4 w-4" /> Settings saved.
        </div>
      )}

      <form onSubmit={submit} className="space-y-6">
        <FormSection title="Tracking window">
          <div className="grid grid-cols-2 gap-4">
            <Field label="Work starts at">
              <input
                type="time"
                value={form.defaultWorkStartTime?.slice(0, 5)}
                onChange={(e) => setForm({ ...form, defaultWorkStartTime: `${e.target.value}:00` })}
                className="input"
              />
            </Field>
            <Field label="Work ends at">
              <input
                type="time"
                value={form.defaultWorkEndTime?.slice(0, 5)}
                onChange={(e) => setForm({ ...form, defaultWorkEndTime: `${e.target.value}:00` })}
                className="input"
              />
            </Field>
          </div>
          <Field label="Work days">
            <div className="flex flex-wrap gap-2">
              {WEEKDAYS.map((day) => (
                <button
                  type="button"
                  key={day}
                  onClick={() => toggleWorkDay(day)}
                  className={`text-xs font-medium px-3 py-1.5 rounded-lg border transition ${
                    form.workDaysSet.has(day)
                      ? "bg-blue-500/10 border-blue-500/30 text-blue-300"
                      : "bg-slate-950 border-slate-800 text-slate-500"
                  }`}
                >
                  {day.slice(0, 3)}
                </button>
              ))}
            </div>
          </Field>
          <Toggle
            label="Also track weekends outside the configured work days"
            checked={form.trackWeekends}
            onChange={(v) => setForm({ ...form, trackWeekends: v })}
          />
        </FormSection>

        <FormSection title="Inactivity &amp; escalation">
          <Field label="Idle minutes before an inactivity prompt">
            <input
              type="number"
              min={1}
              value={form.idleThresholdMinutes}
              onChange={(e) => setForm({ ...form, idleThresholdMinutes: e.target.value })}
              className="input"
            />
          </Field>
          <Field label="Unjustified incidents before escalation">
            <input
              type="number"
              min={0}
              value={form.maxUnjustifiedInactivityBeforeEscalation}
              onChange={(e) => setForm({ ...form, maxUnjustifiedInactivityBeforeEscalation: e.target.value })}
              className="input"
            />
          </Field>
        </FormSection>

        <FormSection title="Breaks">
          <div className="grid grid-cols-2 gap-4">
            <Field label="Breaks per day">
              <input
                type="number"
                min={0}
                value={form.breaksPerDay}
                onChange={(e) => setForm({ ...form, breaksPerDay: e.target.value })}
                className="input"
              />
            </Field>
            <Field label="Break duration (minutes)">
              <input
                type="number"
                min={0}
                value={form.breakDurationMinutes}
                onChange={(e) => setForm({ ...form, breakDurationMinutes: e.target.value })}
                className="input"
              />
            </Field>
          </div>
        </FormSection>

        <FormSection title="Tracking scope">
          <Field label="URL tracking">
            <select
              value={form.urlTrackingMode}
              onChange={(e) => setForm({ ...form, urlTrackingMode: e.target.value })}
              className="input"
            >
              <option value="DomainOnly">Domain only</option>
              <option value="FullUrl">Full URL</option>
            </select>
          </Field>
          <Field label="Keystroke tracking">
            <select
              value={form.keystrokeTrackingMode}
              onChange={(e) => setForm({ ...form, keystrokeTrackingMode: e.target.value })}
              className="input"
            >
              <option value="CountsOnly">Counts only (never actual keys)</option>
            </select>
          </Field>
          <Field label="Data retention (days)">
            <input
              type="number"
              min={1}
              value={form.dataRetentionDays}
              onChange={(e) => setForm({ ...form, dataRetentionDays: e.target.value })}
              className="input"
            />
          </Field>
        </FormSection>

        <button
          type="submit"
          disabled={saving}
          className="flex items-center gap-2 bg-blue-600 hover:bg-blue-500 text-white font-medium px-5 py-2.5 rounded-lg text-sm transition disabled:opacity-50"
        >
          <Save className="h-4 w-4" />
          {saving ? "Saving..." : "Save settings"}
        </button>
      </form>

      <style jsx global>{`
        .input {
          width: 100%;
          background: rgb(2 6 23);
          border: 1px solid rgb(30 41 59);
          border-radius: 0.5rem;
          padding: 0.625rem 0.75rem;
          font-size: 0.875rem;
          color: white;
        }
        .input:focus {
          outline: none;
          border-color: rgb(59 130 246);
        }
      `}</style>
    </div>
  );
}

function FormSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="bg-slate-900/40 border border-slate-850 rounded-xl p-5 space-y-4">
      <h2 className="text-sm font-bold text-white">{title}</h2>
      {children}
    </section>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">{label}</label>
      {children}
    </div>
  );
}

function Toggle({ label, checked, onChange }: { label: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-center gap-3 text-sm text-slate-300 cursor-pointer">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="h-4 w-4" />
      {label}
    </label>
  );
}
