"use client";

import React, { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import {
  getEmployee,
  getEmployeeInactivityIncidents,
  getEmployeeBreaks,
  getEmployeeActions,
  createEmployeeAction,
  reviewInactivityIncident,
} from "@/lib/api";
import {
  ArrowLeft,
  AlertTriangle,
  Coffee,
  MessageSquare,
  Send,
  X,
  CheckCircle2,
  XCircle,
} from "lucide-react";

const INACTIVITY_STATUS = ["Pending review", "Justified", "Unjustified", "Escalated"];
const BREAK_STATUS = ["Approved", "Over quota", "Completed", "Cancelled"];
const ACTION_STATUS = ["Sent", "Delivered", "Acknowledged"];
const ACTION_SEVERITY_STYLE = [
  "text-slate-300 border-slate-700", // Info
  "text-amber-300 border-amber-500/30", // Warning
  "text-rose-300 border-rose-500/30", // Critical
];

export default function EmployeeDetailPage() {
  const params = useParams();
  const employeeId = params.id as string;

  const [employee, setEmployee] = useState<any>(null);
  const [incidents, setIncidents] = useState<any[] | null>(null);
  const [breaks, setBreaks] = useState<any[] | null>(null);
  const [actions, setActions] = useState<any[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showComposer, setShowComposer] = useState(false);

  const loadAll = () => {
    getEmployee(employeeId).then(setEmployee).catch((e) => setError(e.message));
    getEmployeeInactivityIncidents(employeeId).then((r) => setIncidents(r.items)).catch(() => setIncidents([]));
    getEmployeeBreaks(employeeId).then((r) => setBreaks(r.items)).catch(() => setBreaks([]));
    getEmployeeActions(employeeId).then((r) => setActions(r.items)).catch(() => setActions([]));
  };

  useEffect(loadAll, [employeeId]);

  const handleReview = async (incidentId: string, status: "Justified" | "Unjustified") => {
    try {
      await reviewInactivityIncident(incidentId, { status });
      loadAll();
    } catch (e: any) {
      setError(e.message);
    }
  };

  if (error) {
    return (
      <div className="p-4 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-sm">
        {error}
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div>
        <Link href="/dashboard/employees" className="inline-flex items-center gap-1.5 text-xs text-slate-400 hover:text-white mb-4">
          <ArrowLeft className="h-3.5 w-3.5" /> Back to employees
        </Link>
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-extrabold text-white">{employee?.fullName ?? "Loading..."}</h1>
            <p className="text-sm text-slate-400 mt-1">{employee?.email}</p>
          </div>
          <button
            onClick={() => setShowComposer(true)}
            className="flex items-center space-x-2 bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium px-4 py-2.5 rounded-lg transition"
          >
            <Send className="h-4 w-4" />
            <span>Issue action</span>
          </button>
        </div>
      </div>

      {/* Inactivity incidents */}
      <Section title="Inactivity incidents" icon={AlertTriangle} iconColor="text-amber-400">
        {incidents === null ? (
          <Loading />
        ) : incidents.length === 0 ? (
          <Empty text="No inactivity incidents recorded." />
        ) : (
          <div className="divide-y divide-slate-900">
            {incidents.map((inc) => (
              <div key={inc.id} className="py-3 flex items-start justify-between gap-4">
                <div className="min-w-0">
                  <p className="text-sm text-slate-200">
                    Idle for <span className="font-semibold">{inc.idleMinutes} min</span> on{" "}
                    {new Date(inc.detectedAt).toLocaleString()}
                  </p>
                  {inc.employeeReason && (
                    <p className="text-xs text-slate-400 mt-1">Reason: "{inc.employeeReason}"</p>
                  )}
                  {inc.hrNote && <p className="text-xs text-slate-500 mt-1">HR note: {inc.hrNote}</p>}
                </div>
                <div className="flex items-center gap-2 shrink-0">
                  <StatusPill text={INACTIVITY_STATUS[inc.status] ?? "Unknown"} />
                  {inc.status === 0 && (
                    <>
                      <button
                        onClick={() => handleReview(inc.id, "Justified")}
                        title="Mark justified"
                        className="text-emerald-400 hover:text-emerald-300"
                      >
                        <CheckCircle2 className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => handleReview(inc.id, "Unjustified")}
                        title="Mark unjustified"
                        className="text-rose-400 hover:text-rose-300"
                      >
                        <XCircle className="h-4 w-4" />
                      </button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </Section>

      {/* Breaks */}
      <Section title="Break history" icon={Coffee} iconColor="text-teal-400">
        {breaks === null ? (
          <Loading />
        ) : breaks.length === 0 ? (
          <Empty text="No breaks taken yet." />
        ) : (
          <div className="divide-y divide-slate-900">
            {breaks.map((b) => (
              <div key={b.id} className="py-3 flex items-center justify-between">
                <p className="text-sm text-slate-200">
                  {b.plannedDurationMinutes} min · requested {new Date(b.requestedAt).toLocaleString()}
                </p>
                <StatusPill text={BREAK_STATUS[b.status] ?? "Unknown"} />
              </div>
            ))}
          </div>
        )}
      </Section>

      {/* Actions history */}
      <Section title="Actions &amp; notices" icon={MessageSquare} iconColor="text-blue-400">
        {actions === null ? (
          <Loading />
        ) : actions.length === 0 ? (
          <Empty text="No actions issued yet." />
        ) : (
          <div className="divide-y divide-slate-900">
            {actions.map((a) => (
              <div key={a.id} className="py-3">
                <div className="flex items-center justify-between">
                  <span className={`text-xs font-semibold uppercase tracking-wide border rounded px-2 py-0.5 ${ACTION_SEVERITY_STYLE[a.severity] ?? ""}`}>
                    {a.actionType}
                  </span>
                  <StatusPill text={ACTION_STATUS[a.status] ?? "Unknown"} />
                </div>
                <p className="text-sm text-slate-200 mt-2">{a.message}</p>
                <p className="text-xs text-slate-500 mt-1">{new Date(a.createdAt).toLocaleString()}</p>
              </div>
            ))}
          </div>
        )}
      </Section>

      {showComposer && (
        <ActionComposer
          employeeId={employeeId}
          onClose={() => setShowComposer(false)}
          onSent={() => {
            setShowComposer(false);
            loadAll();
          }}
        />
      )}
    </div>
  );
}

function Section({
  title,
  icon: Icon,
  iconColor,
  children,
}: {
  title: string;
  icon: any;
  iconColor: string;
  children: React.ReactNode;
}) {
  return (
    <section className="bg-slate-900/40 border border-slate-850 rounded-xl p-5">
      <h2 className="text-sm font-bold text-white flex items-center gap-2 mb-1">
        <Icon className={`h-4 w-4 ${iconColor}`} /> {title}
      </h2>
      <div className="mt-3">{children}</div>
    </section>
  );
}

function Loading() {
  return <p className="text-xs text-slate-500 py-2">Loading...</p>;
}

function Empty({ text }: { text: string }) {
  return <p className="text-xs text-slate-500 py-2">{text}</p>;
}

function StatusPill({ text }: { text: string }) {
  return (
    <span className="text-[10px] font-semibold uppercase tracking-wide text-slate-400 bg-slate-950 border border-slate-800 rounded-full px-2.5 py-1 whitespace-nowrap">
      {text}
    </span>
  );
}

function ActionComposer({
  employeeId,
  onClose,
  onSent,
}: {
  employeeId: string;
  onClose: () => void;
  onSent: () => void;
}) {
  const [actionType, setActionType] = useState("Message");
  const [severity, setSeverity] = useState("Info");
  const [message, setMessage] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSending(true);
    setError(null);
    try {
      await createEmployeeAction(employeeId, { actionType, severity, message });
      onSent();
    } catch (err: any) {
      setError(err.message || "Could not send action.");
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-50 p-4">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 w-full max-w-lg space-y-5">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-bold text-white flex items-center gap-2">
            <Send className="h-5 w-5 text-blue-400" /> Issue action
          </h3>
          <button onClick={onClose} className="text-slate-500 hover:text-white">
            <X className="h-5 w-5" />
          </button>
        </div>
        <p className="text-xs text-slate-500">
          Delivered to the employee's desktop agent immediately if they're connected, or on
          their next reconnect otherwise.
        </p>

        {error && (
          <div className="p-3 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg text-xs">
            {error}
          </div>
        )}

        <form onSubmit={submit} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">Type</label>
              <select
                value={actionType}
                onChange={(e) => setActionType(e.target.value)}
                className="w-full bg-slate-950 border border-slate-800 rounded-lg py-2.5 px-3 text-sm text-white focus:outline-none focus:border-blue-500"
              >
                <option value="Message">Message</option>
                <option value="Warning">Warning</option>
                <option value="CoachingNote">Coaching note</option>
                <option value="Escalation">Escalation</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">Severity</label>
              <select
                value={severity}
                onChange={(e) => setSeverity(e.target.value)}
                className="w-full bg-slate-950 border border-slate-800 rounded-lg py-2.5 px-3 text-sm text-white focus:outline-none focus:border-blue-500"
              >
                <option value="Info">Info</option>
                <option value="Warning">Warning</option>
                <option value="Critical">Critical</option>
              </select>
            </div>
          </div>
          <div className="space-y-1.5">
            <label className="text-xs font-semibold uppercase tracking-wider text-slate-400">Message</label>
            <textarea
              required
              rows={4}
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Please review today's attendance flag."
              className="w-full bg-slate-950 border border-slate-800 rounded-lg py-2.5 px-3 text-sm text-white placeholder-slate-600 focus:outline-none focus:border-blue-500"
            />
          </div>
          <button
            type="submit"
            disabled={sending}
            className="w-full bg-blue-600 hover:bg-blue-500 text-white font-medium py-2.5 rounded-lg text-sm transition disabled:opacity-50"
          >
            {sending ? "Sending..." : "Send to employee"}
          </button>
        </form>
      </div>
    </div>
  );
}
