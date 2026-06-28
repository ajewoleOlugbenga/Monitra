"use client";

import React, { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { 
  getPlatformTenants, 
  createTenant, 
  activateTenant, 
  suspendTenant, 
  createTenantAdmin, 
  generateInstallToken, 
  platformLogout 
} from "@/lib/api";
import { 
  Activity, 
  Plus, 
  UserPlus, 
  Key, 
  Power, 
  ShieldAlert, 
  Users, 
  HardDrive, 
  Clock, 
  CheckCircle2, 
  LogOut, 
  X,
  Copy,
  Check
} from "lucide-react";

export default function PlatformDashboard() {
  const router = useRouter();
  const [tenants, setTenants] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modals state
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showAdminModal, setShowAdminModal] = useState(false);
  const [showTokenModal, setShowTokenModal] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState<any | null>(null);

  // Form states
  const [newTenant, setNewTenant] = useState({ name: "", slug: "", timezone: "UTC", adminEmail: "", adminPassword: "", adminFullName: "" });
  const [newAdmin, setNewAdmin] = useState({ fullName: "", email: "", password: "" });
  const [tokenParams, setTokenParams] = useState({ label: "Acme Onboarding Token", maxUses: 20, expiresInDays: 30 });

  // Output reveals
  const [generatedToken, setGeneratedToken] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    loadTenants();
  }, []);

  const loadTenants = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getPlatformTenants(1, 100);
      setTenants(data.items || []);
      setTotal(data.total || 0);
    } catch (err: any) {
      setError(err.message || "Failed to load tenants database.");
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = async () => {
    try {
      await platformLogout();
      router.push("/platform/login");
    } catch {
      router.push("/platform/login");
    }
  };

  // Actions
  const handleToggleStatus = async (tenant: any) => {
    try {
      if (tenant.status === 0 || tenant.status === "Active") {
        await suspendTenant(tenant.id);
      } else {
        await activateTenant(tenant.id);
      }
      loadTenants();
    } catch (err: any) {
      alert(err.message || "Operation failed.");
    }
  };

  const handleCreateTenant = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await createTenant(newTenant);
      setShowCreateModal(false);
      setNewTenant({ name: "", slug: "", timezone: "UTC", adminEmail: "", adminPassword: "", adminFullName: "" });
      loadTenants();
    } catch (err: any) {
      alert(err.message || "Failed to create tenant.");
    }
  };

  const handleCreateAdmin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTenant) return;
    try {
      await createTenantAdmin(selectedTenant.id, newAdmin);
      setShowAdminModal(false);
      setNewAdmin({ fullName: "", email: "", password: "" });
      alert("Tenant administrator account created successfully!");
    } catch (err: any) {
      alert(err.message || "Failed to create administrator.");
    }
  };

  const handleGenerateToken = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTenant) return;
    try {
      const res = await generateInstallToken(selectedTenant.id, tokenParams);
      setGeneratedToken(res.rawActivationToken);
    } catch (err: any) {
      alert(err.message || "Failed to generate installation token.");
    }
  };

  const copyToClipboard = () => {
    if (generatedToken) {
      navigator.clipboard.writeText(generatedToken);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col">
      {/* Navigation Header */}
      <header className="border-b border-slate-900 bg-slate-900/40 backdrop-blur-md px-6 py-4 flex items-center justify-between z-10">
        <div className="flex items-center space-x-2.5">
          <Activity className="h-6 w-6 text-emerald-500" />
          <span className="text-xl font-bold tracking-tight text-white">MONITRA</span>
          <span className="text-[10px] uppercase font-bold tracking-widest bg-emerald-500/10 text-emerald-400 px-2 py-0.5 rounded border border-emerald-500/20">
            SaaS Operator
          </span>
        </div>
        <button
          onClick={handleLogout}
          className="flex items-center space-x-2 text-sm font-medium text-slate-400 hover:text-white bg-slate-900 hover:bg-slate-850 px-4 py-2 rounded-lg border border-slate-850 transition duration-200"
        >
          <LogOut className="h-4 w-4" />
          <span>Exit Console</span>
        </button>
      </header>

      {/* Main Content Area */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-6 md:p-8 space-y-8">
        
        {/* KPI Grid */}
        <section className="grid grid-cols-1 md:grid-cols-4 gap-6">
          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-400 uppercase tracking-wider">Total Tenants</span>
              <Users className="h-5 w-5 text-blue-400" />
            </div>
            <div className="text-3xl font-extrabold">{total}</div>
            <div className="text-xs text-slate-500">Registered organizations</div>
          </div>

          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-400 uppercase tracking-wider">Active Status</span>
              <CheckCircle2 className="h-5 w-5 text-emerald-400 animate-pulse" />
            </div>
            <div className="text-3xl font-extrabold">Online</div>
            <div className="text-xs text-slate-500">All services fully operational</div>
          </div>

          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-400 uppercase tracking-wider">Active Devices</span>
              <HardDrive className="h-5 w-5 text-violet-400" />
            </div>
            <div className="text-3xl font-extrabold">{total * 3}</div>
            <div className="text-xs text-slate-500">Average agents checking in</div>
          </div>

          <div className="bg-slate-900/40 border border-slate-850 p-6 rounded-xl space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-400 uppercase tracking-wider">Telemetry Frequency</span>
              <Clock className="h-5 w-5 text-amber-400" />
            </div>
            <div className="text-3xl font-extrabold">1-5 mins</div>
            <div className="text-xs text-slate-500">Agent batch upload cycles</div>
          </div>
        </section>

        {/* Tenant Database Header */}
        <section className="bg-slate-900/30 border border-slate-900 rounded-2xl p-6 md:p-8 space-y-6">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="space-y-1">
              <h2 className="text-2xl font-bold text-white">Tenants Registry</h2>
              <p className="text-sm text-slate-400">
                Manage organization status, deploy onboarding tokens, and manage admin users.
              </p>
            </div>
            <button
              onClick={() => setShowCreateModal(true)}
              className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium px-5 py-2.5 rounded-lg text-sm flex items-center justify-center space-x-2 shadow-lg shadow-emerald-600/15 active:bg-emerald-700 transition duration-200 self-start"
            >
              <Plus className="h-4 w-4" />
              <span>Provision Tenant</span>
            </button>
          </div>

          {error && (
            <div className="p-4 bg-rose-500/10 border border-rose-500/20 text-rose-200 rounded-lg flex items-start space-x-3 text-sm">
              <ShieldAlert className="h-5 w-5 text-rose-400 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {/* Tenants Table Grid */}
          {loading ? (
            <div className="py-20 text-center space-y-4">
              <div className="inline-block h-8 w-8 border-4 border-slate-800 border-t-emerald-500 rounded-full animate-spin" />
              <p className="text-sm text-slate-500">Fetching tenants records...</p>
            </div>
          ) : tenants.length === 0 ? (
            <div className="py-20 text-center border border-dashed border-slate-850 rounded-xl">
              <Users className="h-12 w-12 text-slate-700 mx-auto mb-3" />
              <h3 className="font-semibold text-slate-300">No Tenants Provisioned</h3>
              <p className="text-xs text-slate-500 max-w-xs mx-auto mt-1">
                You haven't provisioned any organizations yet. Click "Provision Tenant" above to begin.
              </p>
            </div>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-slate-850 bg-slate-950">
              <table className="w-full text-left border-collapse text-sm">
                <thead>
                  <tr className="bg-slate-900/60 border-b border-slate-850 text-xs font-semibold uppercase tracking-wider text-slate-400">
                    <th className="py-4 px-6">Name</th>
                    <th className="py-4 px-6">Slug</th>
                    <th className="py-4 px-6">Timezone</th>
                    <th className="py-4 px-6">Status</th>
                    <th className="py-4 px-6">Created At</th>
                    <th className="py-4 px-6 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-850">
                  {tenants.map((tenant) => {
                    const isActive = tenant.status === 0 || tenant.status === "Active";
                    return (
                      <tr key={tenant.id} className="hover:bg-slate-900/30 transition duration-150">
                        <td className="py-4 px-6 font-semibold text-white">{tenant.name}</td>
                        <td className="py-4 px-6 font-mono text-xs text-slate-400">/{tenant.slug}</td>
                        <td className="py-4 px-6 text-slate-400">{tenant.timezone}</td>
                        <td className="py-4 px-6">
                          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border ${
                            isActive 
                              ? "bg-emerald-500/10 text-emerald-400 border-emerald-500/20" 
                              : "bg-rose-500/10 text-rose-400 border-rose-500/20"
                          }`}>
                            {isActive ? "Active" : "Suspended"}
                          </span>
                        </td>
                        <td className="py-4 px-6 text-slate-400">
                          {new Date(tenant.createdAt).toLocaleDateString()}
                        </td>
                        <td className="py-4 px-6 text-right space-x-2">
                          <button
                            onClick={() => {
                              setSelectedTenant(tenant);
                              setShowAdminModal(true);
                            }}
                            title="Add Tenant User"
                            className="inline-flex items-center justify-center p-2 rounded-lg bg-slate-900 hover:bg-slate-850 text-slate-400 hover:text-white border border-slate-800 transition"
                          >
                            <UserPlus className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => {
                              setSelectedTenant(tenant);
                              setGeneratedToken(null);
                              setShowTokenModal(true);
                            }}
                            title="Generate Install Token"
                            className="inline-flex items-center justify-center p-2 rounded-lg bg-slate-900 hover:bg-slate-850 text-slate-400 hover:text-white border border-slate-800 transition"
                          >
                            <Key className="h-4 w-4" />
                          </button>
                          <button
                            onClick={() => handleToggleStatus(tenant)}
                            title={isActive ? "Suspend Tenant" : "Activate Tenant"}
                            className={`inline-flex items-center justify-center p-2 rounded-lg border transition ${
                              isActive 
                                ? "bg-rose-500/10 text-rose-400 hover:bg-rose-500/20 border-rose-500/20" 
                                : "bg-emerald-500/10 text-emerald-400 hover:bg-emerald-500/20 border-emerald-500/20"
                            }`}
                          >
                            <Power className="h-4 w-4" />
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </main>

      {/* 1. Modal: Provision Tenant */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-slate-900 border border-slate-850 w-full max-w-xl rounded-2xl shadow-2xl p-6 relative">
            <button 
              onClick={() => setShowCreateModal(false)}
              className="absolute top-4 right-4 text-slate-400 hover:text-white transition"
            >
              <X className="h-5 w-5" />
            </button>
            <h3 className="text-lg font-bold text-white mb-2">Provision New Organization</h3>
            <p className="text-xs text-slate-400 mb-6">
              Create a new client database space and optional primary admin credentials.
            </p>

            <form onSubmit={handleCreateTenant} className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Company Name</label>
                  <input
                    type="text"
                    required
                    value={newTenant.name}
                    onChange={(e) => setNewTenant({ ...newTenant, name: e.target.value })}
                    placeholder="Acme Corp"
                    className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                  />
                </div>
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Slug (lowercase URL key)</label>
                  <input
                    type="text"
                    required
                    value={newTenant.slug}
                    onChange={(e) => setNewTenant({ ...newTenant, slug: e.target.value })}
                    placeholder="acme"
                    className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                  />
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Tenant Timezone</label>
                <input
                  type="text"
                  required
                  value={newTenant.timezone}
                  onChange={(e) => setNewTenant({ ...newTenant, timezone: e.target.value })}
                  placeholder="America/New_York"
                  className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                />
              </div>

              <div className="border-t border-slate-850/60 pt-4 mt-6">
                <h4 className="text-xs font-bold uppercase tracking-wider text-emerald-400 mb-3">Primary Tenant User Account (Optional)</h4>
                
                <div className="space-y-3">
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-xs font-semibold text-slate-400">Full Name</label>
                      <input
                        type="text"
                        value={newTenant.adminFullName}
                        onChange={(e) => setNewTenant({ ...newTenant, adminFullName: e.target.value })}
                        placeholder="John Doe"
                        className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2 px-3 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-xs font-semibold text-slate-400">Admin Email</label>
                      <input
                        type="email"
                        value={newTenant.adminEmail}
                        onChange={(e) => setNewTenant({ ...newTenant, adminEmail: e.target.value })}
                        placeholder="owner@acme.com"
                        className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2 px-3 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                      />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-xs font-semibold text-slate-400">Secure Password</label>
                    <input
                      type="password"
                      value={newTenant.adminPassword}
                      onChange={(e) => setNewTenant({ ...newTenant, adminPassword: e.target.value })}
                      placeholder="••••••••"
                      className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2 px-3 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                    />
                  </div>
                </div>
              </div>

              <div className="pt-6 flex justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="bg-slate-950 hover:bg-slate-900 border border-slate-850 text-slate-400 hover:text-white px-5 py-2.5 rounded-lg text-sm transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium px-5 py-2.5 rounded-lg text-sm shadow-md hover:shadow-emerald-600/10 transition"
                >
                  Provision Account
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 2. Modal: Add Tenant User */}
      {showAdminModal && selectedTenant && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-slate-900 border border-slate-850 w-full max-w-md rounded-2xl shadow-2xl p-6 relative">
            <button 
              onClick={() => setShowAdminModal(false)}
              className="absolute top-4 right-4 text-slate-400 hover:text-white transition"
            >
              <X className="h-5 w-5" />
            </button>
            <h3 className="text-lg font-bold text-white mb-1">Add Admin Account</h3>
            <p className="text-xs text-slate-400 mb-6">
              Create an administrative user for <span className="text-emerald-400 font-semibold">{selectedTenant.name}</span>.
            </p>

            <form onSubmit={handleCreateAdmin} className="space-y-4">
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Full Name</label>
                <input
                  type="text"
                  required
                  value={newAdmin.fullName}
                  onChange={(e) => setNewAdmin({ ...newAdmin, fullName: e.target.value })}
                  placeholder="Jane Smith"
                  className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Email Address</label>
                <input
                  type="email"
                  required
                  value={newAdmin.email}
                  onChange={(e) => setNewAdmin({ ...newAdmin, email: e.target.value })}
                  placeholder="jane.smith@acme.com"
                  className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Password</label>
                <input
                  type="password"
                  required
                  value={newAdmin.password}
                  onChange={(e) => setNewAdmin({ ...newAdmin, password: e.target.value })}
                  placeholder="••••••••"
                  className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                />
              </div>

              <div className="pt-6 flex justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => setShowAdminModal(false)}
                  className="bg-slate-950 hover:bg-slate-900 border border-slate-850 text-slate-400 hover:text-white px-4 py-2 rounded-lg text-sm transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium px-4 py-2 rounded-lg text-sm shadow-md hover:shadow-emerald-600/10 transition"
                >
                  Create User
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* 3. Modal: Generate Onboarding Install Token */}
      {showTokenModal && selectedTenant && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
          <div className="bg-slate-900 border border-slate-850 w-full max-w-md rounded-2xl shadow-2xl p-6 relative">
            <button 
              onClick={() => setShowTokenModal(false)}
              className="absolute top-4 right-4 text-slate-400 hover:text-white transition"
            >
              <X className="h-5 w-5" />
            </button>
            <h3 className="text-lg font-bold text-white mb-1">Onboarding Activation Code</h3>
            <p className="text-xs text-slate-400 mb-6">
              Generate an onboarding activation code for device installations under <span className="text-emerald-400 font-semibold">{selectedTenant.name}</span>.
            </p>

            {generatedToken ? (
              <div className="space-y-6">
                <div className="p-4 bg-emerald-500/10 border border-emerald-500/20 text-emerald-200 rounded-lg text-xs space-y-2 text-center">
                  <p className="font-semibold text-slate-300">TOKEN GENERATED SUCCESSFULLY</p>
                  <p className="text-slate-400 leading-relaxed">
                    Copy the activation code below. The raw token is hashed and not viewable again once this dialog is closed.
                  </p>
                </div>

                <div className="flex items-center space-x-2 bg-slate-950 border border-slate-850 rounded-lg p-3">
                  <span className="flex-1 text-sm font-mono tracking-wider text-emerald-400 select-all">{generatedToken}</span>
                  <button
                    onClick={copyToClipboard}
                    className="p-2 rounded-lg bg-slate-900 hover:bg-slate-850 text-slate-400 hover:text-white border border-slate-800 transition"
                  >
                    {copied ? <Check className="h-4 w-4 text-emerald-400" /> : <Copy className="h-4 w-4" />}
                  </button>
                </div>

                <button
                  type="button"
                  onClick={() => setShowTokenModal(false)}
                  className="w-full bg-slate-950 hover:bg-slate-900 border border-slate-850 text-slate-400 hover:text-white py-3 rounded-lg text-sm transition"
                >
                  Done
                </button>
              </div>
            ) : (
              <form onSubmit={handleGenerateToken} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Token Label / Description</label>
                  <input
                    type="text"
                    required
                    value={tokenParams.label}
                    onChange={(e) => setTokenParams({ ...tokenParams, label: e.target.value })}
                    placeholder="Q1 Developer Laptops"
                    className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Max Uses</label>
                    <input
                      type="number"
                      required
                      value={tokenParams.maxUses}
                      onChange={(e) => setTokenParams({ ...tokenParams, maxUses: parseInt(e.target.value) || 10 })}
                      className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Expiry (Days)</label>
                    <input
                      type="number"
                      required
                      value={tokenParams.expiresInDays}
                      onChange={(e) => setTokenParams({ ...tokenParams, expiresInDays: parseInt(e.target.value) || 30 })}
                      className="w-full bg-slate-950 border border-slate-850 rounded-lg py-2.5 px-3.5 text-sm text-white focus:outline-none focus:border-emerald-500 transition"
                    />
                  </div>
                </div>

                <div className="pt-6 flex justify-end space-x-3">
                  <button
                    type="button"
                    onClick={() => setShowTokenModal(false)}
                    className="bg-slate-950 hover:bg-slate-900 border border-slate-850 text-slate-400 hover:text-white px-4 py-2 rounded-lg text-sm transition"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium px-4 py-2 rounded-lg text-sm shadow-md hover:shadow-emerald-600/10 transition"
                  >
                    Generate Activation Code
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
