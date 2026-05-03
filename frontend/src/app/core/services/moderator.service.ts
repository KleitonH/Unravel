import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import { ContentResponse } from "../models/trail.model";

export interface ModeratorMetrics {
  totalStudents: number;
  totalTrails: number;
  totalContents: number;
  totalChallenges: number;
  totalXpDistributed: number;
  totalEnrollments: number;
  totalCompletions: number;
}

export interface CreateContentDto {
  trailId: number;
  title: string;
  body: string;
  externalUrl?: string | null;
  type: number;
  level: number;
  order: number;
}

export interface UpdateContentDto {
  title?: string;
  body?: string;
  externalUrl?: string | null;
  type?: number;
  level?: number;
  order?: number;
}

@Injectable({ providedIn: "root" })
export class ModeratorService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api/moderator`;

  getContents(trailId?: number): Observable<ContentResponse[]> {
    const url = trailId
      ? `${this.api}/contents?trailId=${trailId}`
      : `${this.api}/contents`;
    return this.http.get<ContentResponse[]>(url);
  }

  createContent(dto: CreateContentDto): Observable<ContentResponse> {
    return this.http.post<ContentResponse>(`${this.api}/contents`, dto);
  }

  updateContent(id: number, dto: UpdateContentDto): Observable<ContentResponse> {
    return this.http.put<ContentResponse>(`${this.api}/contents/${id}`, dto);
  }

  deleteContent(id: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/contents/${id}`);
  }

  getMetrics(): Observable<ModeratorMetrics> {
    return this.http.get<ModeratorMetrics>(`${this.api}/metrics`);
  }
}
