import http, { type RefinedResponse, type ResponseType } from "k6/http";
import { ENDPOINTS, HEADERS, nowIso } from "./config.ts";

export interface CreateElectionOverrides {
  title?: string;
  description?: string;
  startDate?: string;
  endDate?: string;
  type?: number;
}

export function createElection(overrides: CreateElectionOverrides = {}): RefinedResponse<ResponseType> {
  const body = {
    title: overrides.title || "k6 election",
    description: overrides.description || "created from k6",
    startDate: overrides.startDate || nowIso(0),
    endDate: overrides.endDate || nowIso(1),
    type: overrides.type ?? 0,
  };
  return http.post(ENDPOINTS.elections, JSON.stringify(body), HEADERS);
}

export interface AddCandidateOverrides {
  name?: string;
  description?: string;
  party?: string;
  photoUrl?: string | null;
}

export function addCandidate(
  electionId: string,
  overrides: AddCandidateOverrides = {}
): RefinedResponse<ResponseType> {
  const body = {
    name: overrides.name || "k6 candidate",
    description: overrides.description || "auto-added",
    party: overrides.party || "Tech",
    photoUrl: overrides.photoUrl ?? null,
  };
  return http.post(ENDPOINTS.candidates(electionId), JSON.stringify(body), HEADERS);
}

export function openElection(electionId: string): RefinedResponse<ResponseType> {
  return http.post(ENDPOINTS.open(electionId), null, HEADERS);
}

export function closeElection(electionId: string): RefinedResponse<ResponseType> {
  return http.patch(ENDPOINTS.close(electionId), null, HEADERS);
}

export interface VoteItem {
  candidateId: string;
  rank: number | null;
}

export function vote(
  electionId: string,
  voterEmailValue: string,
  votes: VoteItem[]
): RefinedResponse<ResponseType> {
  const body = { voterEmail: voterEmailValue, votes };
  return http.post(ENDPOINTS.vote(electionId), JSON.stringify(body), HEADERS);
}

export function getElection(electionId: string): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.electionById(electionId), HEADERS);
}

export function getElections(statusFilter?: number): RefinedResponse<ResponseType> {
  const url =
    statusFilter !== undefined
      ? `${ENDPOINTS.elections}?status=${statusFilter}`
      : ENDPOINTS.elections;
  return http.get(url, HEADERS);
}

export function getResults(electionId: string): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.results(electionId), HEADERS);
}

export function getTurnout(electionId: string): RefinedResponse<ResponseType> {
  return http.get(ENDPOINTS.turnout(electionId), HEADERS);
}

export interface PrepareActiveElectionOpts {
  type?: number;
}

export interface PreparedElection {
  electionId: string;
  /** First candidate (e.g. for single-choice vote payload). */
  candidateId: string;
  secondCandidateId: string;
}

export function prepareActiveElection(opts: PrepareActiveElectionOpts = {}): PreparedElection {
  const createRes = createElection({ type: opts.type ?? 0 });
  if (createRes.status !== 201 && createRes.status !== 200) {
    throw new Error(`createElection failed: ${createRes.status} ${createRes.body}`);
  }
  const electionId = (JSON.parse(createRes.body as string) as { id: string }).id;

  const candRes1 = addCandidate(electionId, { name: "Setup Candidate A" });
  if (candRes1.status !== 200 && candRes1.status !== 201) {
    throw new Error(`addCandidate failed: ${candRes1.status} ${candRes1.body}`);
  }
  const candidateId = (JSON.parse(candRes1.body as string) as { id: string }).id;

  const candRes2 = addCandidate(electionId, { name: "Setup Candidate B" });
  if (candRes2.status !== 200 && candRes2.status !== 201) {
    throw new Error(`addCandidate failed: ${candRes2.status} ${candRes2.body}`);
  }
  const secondCandidateId = (JSON.parse(candRes2.body as string) as { id: string }).id;

  const openRes = openElection(electionId);
  if (openRes.status !== 204 && openRes.status !== 200) {
    throw new Error(`openElection failed: ${openRes.status} ${openRes.body}`);
  }

  return { electionId, candidateId, secondCandidateId };
}
