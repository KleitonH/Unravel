import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { ProfileResponse } from "../models/profile.model";

export interface RankingEntry {
  id: string;
  name: string;
  xp: number;
  streakDays: number;
  activeTitle: string | null;
  badgeCount: number;
}

@Injectable({ providedIn: "root" })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api`;

  getProfile(): Observable<ProfileResponse> {
    return this.http.get<ProfileResponse>(`${this.api}/profile`);
  }

  getRanking(top = 10): Observable<RankingEntry[]> {
    return this.http.get<RankingEntry[]>(
      `${this.api}/profile/ranking?top=${top}`,
    );
  }

  updateTitle(title: string | null): Observable<{ activeTitle: string | null }> {
    return this.http.put<{ activeTitle: string | null }>(
      `${this.api}/profile/title`,
      { title },
    );
  }
}
