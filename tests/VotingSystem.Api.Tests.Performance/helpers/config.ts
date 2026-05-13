import type { RefinedResponse, ResponseType } from "k6/http";

export const BASE_URL: string = __ENV.BASE_URL || "http://localhost:5032";

export const ENDPOINTS = {
  elections: `${BASE_URL}/api/elections`,
  electionById: (id: string) => `${BASE_URL}/api/elections/${id}`,
  candidates: (id: string) => `${BASE_URL}/api/elections/${id}/candidates`,
  open: (id: string) => `${BASE_URL}/api/elections/${id}/open`,
  close: (id: string) => `${BASE_URL}/api/elections/${id}/close`,
  vote: (id: string) => `${BASE_URL}/api/elections/${id}/vote`,
  results: (id: string) => `${BASE_URL}/api/elections/${id}/results`,
  turnout: (id: string) => `${BASE_URL}/api/elections/${id}/turnout`,
} as const;

export const HEADERS = {
  headers: { "Content-Type": "application/json" },
};

export const DEFAULT_THRESHOLDS: Record<string, string[]> = {
  http_req_failed: ["rate<0.05"],
  http_req_duration: ["p(95)<500", "p(99)<1000"],
};

export const THRESHOLDS = DEFAULT_THRESHOLDS;

export function voterEmail(prefix = "voter"): string {
  return `${prefix}_${__VU}_${__ITER}_${Math.random().toString(36).slice(2, 8)}@example.com`;
}

export function nowIso(offsetDays = 0): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() + offsetDays);
  return d.toISOString();
}

export function parseBody<T>(res: RefinedResponse<ResponseType>): T {
  return JSON.parse(res.body as string) as T;
}
