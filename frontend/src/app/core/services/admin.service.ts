import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";

/**
 * DTO de retorno do POST /api/admin/replan-now — espelha
 * Application/Journey/DailyReplanReport.
 */
export type DailyReplanReport = {
  asOf: string;
  processed: number;
  failures: number;
  yesterdayGoalMet: number;
};

@Injectable({ providedIn: "root" })
export class AdminService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/admin`;

  /** Dispara o cron de replanejamento diário no instante atual.
   *  Requer role Moderator (backend valida via [Authorize(Roles="Moderator")]). */
  replanNow(): Observable<DailyReplanReport> {
    return this.http.post<DailyReplanReport>(`${this.base}/replan-now`, null);
  }
}
