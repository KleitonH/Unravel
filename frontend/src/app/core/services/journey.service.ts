import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { JourneyPlan } from "../models/journey.model";

/**
 * Cliente dos endpoints do Journey Planner (PR 3 do backend):
 *   GET  /api/journey/today?trailId=X
 *   POST /api/journey/replan?trailId=X
 */
@Injectable({ providedIn: "root" })
export class JourneyService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/journey`;

  today(trailId: number): Observable<JourneyPlan> {
    return this.http.get<JourneyPlan>(`${this.base}/today`, {
      params: { trailId },
    });
  }

  replan(trailId: number): Observable<JourneyPlan> {
    return this.http.post<JourneyPlan>(`${this.base}/replan`, null, {
      params: { trailId },
    });
  }
}
