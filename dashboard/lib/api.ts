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

// 4. Employees
export async function getEmployees() {
  return fetchJson("/api/employees");
}

export async function getEmployee(id: string) {
  return fetchJson(`/api/employees/${id}`);
}

export async function createEmployee(payload: any) {
  return fetchJson("/api/employees", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function getEmployeeInactivityIncidents(id: string) {
  return fetchJson(`/api/employees/${id}/inactivity-incidents`);
}

export async function reviewInactivityIncident(incidentId: string, payload: any) {
  return fetchJson(`/api/employees/inactivity-incidents/${incidentId}/review`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}

export async function getEmployeeBreaks(id: string) {
  return fetchJson(`/api/employees/${id}/breaks`);
}

export async function getEmployeeActions(id: string) {
  return fetchJson(`/api/employees/${id}/actions`);
}

export async function createEmployeeAction(id: string, payload: any) {
  return fetchJson(`/api/employees/${id}/actions`, {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

// 5. Devices (IT)
export async function getDevices() {
  return fetchJson("/api/devices");
}

export async function getDeviceHealthHistory(id: string) {
  return fetchJson(`/api/devices/${id}/health`);
}

export async function getDeviceLogs(id: string) {
  return fetchJson(`/api/devices/${id}/logs`);
}

export async function assignDeviceEmployee(id: string, employeeId: string | null) {
  return fetchJson(`/api/devices/${id}/assign-employee`, {
    method: "PUT",
    body: JSON.stringify({ employeeId }),
  });
}

// 6. Tenant settings
export async function getTenantSettings() {
  return fetchJson("/api/tenant/settings");
}

export async function updateTenantSettings(payload: any) {
  return fetchJson("/api/tenant/settings", {
    method: "PUT",
    body: JSON.stringify(payload),
  });
}
