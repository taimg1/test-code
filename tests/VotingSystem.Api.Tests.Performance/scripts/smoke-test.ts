import { check, sleep } from "k6";
import type { Options } from "k6/options";
import {
  createElection,
  addCandidate,
  openElection,
  vote,
  closeElection,
  getResults,
  getTurnout,
  getElection,
  getElections,
} from "../helpers/api-client.ts";
import { voterEmail } from "../helpers/config.ts";

export const options: Options = {
  vus: 1,
  duration: "30s",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<300"],
    checks: ["rate>0.99"],
  },
};

export default function (): void {
  const createRes = createElection();
  check(createRes, { "create election 201": (r) => r.status === 201 });
  if (createRes.status !== 201) return;
  const electionId = JSON.parse(createRes.body as string).id as string;

  const candRes1 = addCandidate(electionId);
  check(candRes1, { "add candidate 1 — 200": (r) => r.status === 200 });
  if (candRes1.status !== 200) return;
  const candidateId = JSON.parse(candRes1.body as string).id as string;

  const candRes2 = addCandidate(electionId, { name: "Smoke Candidate B" });
  check(candRes2, { "add candidate 2 — 200": (r) => r.status === 200 });
  if (candRes2.status !== 200) return;

  const openRes = openElection(electionId);
  check(openRes, { "open election 204": (r) => r.status === 204 });

  check(getElection(electionId), { "get election 200": (r) => r.status === 200 });
  check(getElection(electionId), { "get election cached 200": (r) => r.status === 200 });

  check(getElections(1), { "list active 200": (r) => r.status === 200 });

  const voteRes = vote(electionId, voterEmail("smoke"), [{ candidateId, rank: null }]);
  check(voteRes, { "vote 200": (r) => r.status === 200 });

  check(closeElection(electionId), { "close 204": (r) => r.status === 204 });
  check(getResults(electionId), { "results 200": (r) => r.status === 200 });
  check(getTurnout(electionId), { "turnout 200": (r) => r.status === 200 });

  sleep(1);
}
