const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

async function fetchJson(endpoint: string, options: RequestInit = {}) {
  const url = `${API_URL}${endpoint}`;
  
  // Set default headers and credentials (to include cookies)
  options.credentials = "include";
  options.headers = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  const response = await fetch(url, options);

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }

  const contentType = response.headers.get("content-type");
  if (contentType && contentType.includes("application/json")) {
    return await response.json();
  }
  return await response.text();
}

// 1. Tenant Admin Auth
export async function tenantLogin(payload: any) {
  return fetchJson("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function tenantLogout() {
  return fetchJson("/api/auth/logout", {
    method: "POST",
  });
}

export async function getMe() {
  return fetchJson("/api/auth/me");
}

// 2. Platform Admin Auth
export async function platformLogin(payload: any) {
  return fetchJson("/api/platform/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function platformLogout() {
  return fetchJson("/api/platform/auth/logout", {
    method: "POST",
  });
}

// 3. Platform Tenant Operations
export async function getPlatformTenants(page = 1, limit = 20) {
  return fetchJson(`/api/platform/tenants?page=${page}&limit=${limit}`);
}

export async function getPlatformTenantById(id: string) {
  return fetchJson(`/api/platform/tenants/${id}`);
}

export async function createTenant(payload: any) {
  return fetchJson("/api/platform/tenants", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function activateTenant(id: string) {
  return fetchJson(`/api/platform/tenants/${id}/activate`, {
    method: "POST",
  });
}

export async function suspendTenant(id: string) {
  return fetchJson(`/api/platform/tenants/${id}/suspend`, {
    method: "POST",
  });
}

export async function createTenantAdmin(tenantId: string, payload: any) {
  return fetchJson(`/api/platform/tenants/${tenantId}/admins`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function generateInstallToken(tenantId: string, payload: any) {
  return fetchJson(`/api/platform/tenants/${tenantId}/install-tokens`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
