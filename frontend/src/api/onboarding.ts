import { api } from "./client"
import type { OnboardingResult, OnboardingSubmit, OnboardingTest } from "@/types/api"

export const onboardingApi = {
  start: (trailIds: number[]) =>
    api
      .post<OnboardingTest>("/api/journey/onboarding/start", { trailIds })
      .then((r) => r.data),

  submit: (trailIds: number[], body: OnboardingSubmit) =>
    api
      .post<OnboardingResult>("/api/journey/onboarding/submit", body, {
        params: { trailIds: trailIds.join(",") },
      })
      .then((r) => r.data),
}
