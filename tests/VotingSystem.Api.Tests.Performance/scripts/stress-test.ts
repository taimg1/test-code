import { check, sleep } from "k6";
import type { Options } from "k6/options";
import { vote, getElection, getTurnout, prepareActiveElection } from "../helpers/api-client.ts";
import { voterEmail } from "../helpers/config.ts";

export const options: Options = {
  stages: [
    { duration: "10s", target: 20 },
    { duration: "20s", target: 60 },
    { duration: "20s", target: 100 },
    { duration: "10s", target: 0 },
  ],
  thresholds: {
    http_req_failed: ["rate<0.10"],
    http_req_duration: ["p(95)<1000", "p(99)<2000"],
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

  check(getTurnout(data.electionId), {
    "turnout 200": (r) => r.status === 200,
  });

  const voteRes = vote(data.electionId, voterEmail("stress"), [
    { candidateId: data.candidateId, rank: null },
  ]);
  check(voteRes, {
    "vote accepted or rejected gracefully": (r) =>
      r.status === 200 || r.status === 400,
  });

  sleep(0.1);
}
