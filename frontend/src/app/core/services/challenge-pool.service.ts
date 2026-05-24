import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { ChallengePool } from "../models/challenge-pool.model";

/**
 * Cliente do pool de perguntas geradas pelo Forge (PR 4 do backend):
 *   GET /api/contents/{contentId}/challenge-pool?targetCount=N
 */
@Injectable({ providedIn: "root" })
export class ChallengePoolService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/api/contents`;

  pool(contentId: number, targetCount = 5): Observable<ChallengePool> {
    return this.http.get<ChallengePool>(
      `${this.base}/${contentId}/challenge-pool`,
      { params: { targetCount } },
    );
  }
}
