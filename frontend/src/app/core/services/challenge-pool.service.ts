import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { ChallengePool } from "../models/challenge-pool.model";

export type SubmitPoolChallengeRequest = {
  generatedChallengeId: number;
  selectedOptionIndex: number;
};

export type SubmitPoolChallengeResponse = {
  isCorrect: boolean;
  correctOptionIndex: number;
  explanation: string | null;
  newMasteryScore: number;
  newMasteryConfidence: number;
  // PR 15 — gamificação
  xpEarned: number;
  coinsEarned: number;
  starsEarned: number;
  lifeDelta: number;       // -1 em erro, 0 em acerto
  totalXp: number;
  totalCoins: number;
  totalStars: number;
  totalLives: number;
  streakDays: number;
};

/**
 * Cliente do pool de perguntas geradas pelo Forge.
 *   GET  /api/contents/{contentId}/challenge-pool?targetCount=N   (PR 4)
 *   POST /api/contents/{contentId}/challenge-pool/submit          (PR 13)
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

  submit(
    contentId: number,
    body: SubmitPoolChallengeRequest,
  ): Observable<SubmitPoolChallengeResponse> {
    return this.http.post<SubmitPoolChallengeResponse>(
      `${this.base}/${contentId}/challenge-pool/submit`,
      body,
    );
  }
}
