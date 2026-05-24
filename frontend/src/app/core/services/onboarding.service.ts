import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpParams } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  OnboardingResult,
  OnboardingSubmit,
  OnboardingTest,
} from "../models/onboarding.model";

/**
 * Cliente do onboarding com nivelamento (PR 6 do backend):
 *   POST /api/journey/onboarding/start   { trailIds }
 *   POST /api/journey/onboarding/submit?trailIds=1,3   { answers }
 */
@Injectable({ providedIn: "root" })
export class OnboardingService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/journey/onboarding`;

  start(trailIds: number[]): Observable<OnboardingTest> {
    return this.http.post<OnboardingTest>(`${this.base}/start`, { trailIds });
  }

  submit(
    trailIds: number[],
    body: OnboardingSubmit,
  ): Observable<OnboardingResult> {
    const params = new HttpParams().set("trailIds", trailIds.join(","));
    return this.http.post<OnboardingResult>(`${this.base}/submit`, body, {
      params,
    });
  }
}
