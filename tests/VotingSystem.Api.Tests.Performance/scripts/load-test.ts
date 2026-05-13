import { check, sleep } from "k6";
import type { Options } from "k6/options";
import { vote, getElections, getElection, prepareActiveElection } from "../helpers/api-client.ts";
import { voterEmail, DEFAULT_THRESHOLDS } from "../helpers/config.ts";

export const options: Options = {
  stages: [
    { duration: "15s", target: 10 },
    { duration: "30s", target: 30 },
    { duration: "15s", target: 0 },
  ],
  thresholds: {
    ...DEFAULT_THRESHOLDS,
    checks: ["rate>0.95"],
  },
};

export function setup(): { electionId: string; candidateId: string } {
  return prepareActiveElection({ type: 0 });
}

export default function (data: { electionId: string; candidateId: string }): void {
  if (!data || !data.electionId) return;

  check(getElection(data.electionId), {
    "get election 200": (r) => r.status === 200,
  });

  check(getElections(1), {
    "list active 200": (r) => r.status === 200,
  });

  const voteRes = vote(data.electionId, voterEmail("load"), [
    { candidateId: data.candidateId, rank: null },
  ]);
  check(voteRes, {
    "vote 200 or duplicate 400": (r) => r.status === 200 || r.status === 400,
  });

  sleep(0.2);
}
