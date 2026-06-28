import Link from "next/link";
import { Monitor, ShieldAlert, ArrowRight, ShieldCheck, Activity } from "lucide-react";

export default function Home() {
  return (
    <main className="relative min-h-screen flex flex-col items-center justify-center bg-gradient-to-br from-[#0f172a] via-[#1e293b] to-[#0f172a] overflow-hidden px-4">
      {/* Background Decorative Rings */}
      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl pointer-events-none" />
      <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-emerald-500/10 rounded-full blur-3xl pointer-events-none" />

      {/* Main Glassmorphic Card Container */}
      <div className="z-10 max-w-4xl w-full text-center space-y-8 bg-slate-900/40 backdrop-blur-md border border-slate-800 p-8 md:p-12 rounded-2xl shadow-2xl">
        <div className="flex items-center justify-center space-x-2">
          <Activity className="h-10 w-10 text-blue-500 animate-pulse" />
          <span className="text-3xl font-bold tracking-tight text-white">MONITRA</span>
        </div>

        <div className="space-y-4">
          <h1 className="text-4xl md:text-5xl font-extrabold tracking-tight text-transparent bg-clip-text bg-gradient-to-r from-blue-400 via-indigo-200 to-emerald-400">
            SaaS Employee Inactivity & Productivity Metrics
          </h1>
          <p className="max-w-2xl mx-auto text-lg text-slate-400 font-medium">
            Transparent, multi-tenant workforce analytics and schedule compliance dashboard. Built with strict cryptographic data isolation.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-6">
          {/* Tenant Console Card */}
          <div className="flex flex-col justify-between p-6 bg-slate-950/60 border border-slate-800 rounded-xl hover:border-blue-500/50 hover:shadow-[0_0_15px_rgba(59,130,246,0.15)] transition duration-300 text-left">
            <div className="space-y-3">
              <div className="h-12 w-12 bg-blue-500/10 rounded-lg flex items-center justify-center text-blue-400">
                <Monitor className="h-6 w-6" />
              </div>
              <h3 className="text-xl font-bold text-white">Organization Portal</h3>
              <p className="text-sm text-slate-400 leading-relaxed">
                Log in to your tenant dashboard to manage employee lists, registration tokens, check active window logs, review unexplained idle reasons, and export reports.
              </p>
            </div>
            <div className="pt-6">
              <Link
                href="/login"
                className="inline-flex items-center space-x-2 text-sm font-semibold text-blue-400 hover:text-blue-300 group"
              >
                <span>Access Tenant Dashboard</span>
                <ArrowRight className="h-4 w-4 group-hover:translate-x-1 transition-transform" />
              </Link>
            </div>
          </div>

          {/* Platform Console Card */}
          <div className="flex flex-col justify-between p-6 bg-slate-950/60 border border-slate-800 rounded-xl hover:border-emerald-500/50 hover:shadow-[0_0_15px_rgba(16,185,129,0.15)] transition duration-300 text-left">
            <div className="space-y-3">
              <div className="h-12 w-12 bg-emerald-500/10 rounded-lg flex items-center justify-center text-emerald-400">
                <ShieldCheck className="h-6 w-6" />
              </div>
              <h3 className="text-xl font-bold text-white">SaaS Admin Console</h3>
              <p className="text-sm text-slate-400 leading-relaxed">
                Dedicated interface for the SaaS system operator. Manage tenant subscription states, provision new tenants, suspension rules, and review global system statistics.
              </p>
            </div>
            <div className="pt-6">
              <Link
                href="/platform/login"
                className="inline-flex items-center space-x-2 text-sm font-semibold text-emerald-400 hover:text-emerald-300 group"
              >
                <span>Platform Console Login</span>
                <ArrowRight className="h-4 w-4 group-hover:translate-x-1 transition-transform" />
              </Link>
            </div>
          </div>
        </div>

        {/* Core Architectural Guarantees bar */}
        <div className="flex flex-wrap items-center justify-center gap-6 pt-8 border-t border-slate-800/80 text-xs text-slate-500">
          <div className="flex items-center space-x-1.5">
            <ShieldAlert className="h-4 w-4 text-emerald-500" />
            <span>Cryptographic Tenant Isolation</span>
          </div>
          <div className="flex items-center space-x-1.5">
            <ShieldAlert className="h-4 w-4 text-emerald-500" />
            <span>Automatic Schedule Enforcement</span>
          </div>
          <div className="flex items-center space-x-1.5">
            <ShieldAlert className="h-4 w-4 text-emerald-500" />
            <span>Zero Raw Keystroke Logger Policy</span>
          </div>
        </div>
      </div>
    </main>
  );
}
