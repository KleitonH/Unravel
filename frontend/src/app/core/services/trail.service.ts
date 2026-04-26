import { Injectable, inject } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../../environments/environment";
import {
  TrailResponse,
  ContentResponse,
  ProgressResponse,
} from "../models/trail.model";

@Injectable({ providedIn: "root" })
export class TrailService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/api`;

  getAll(): Observable<TrailResponse[]> {
    return this.http.get<TrailResponse[]>(`${this.api}/trails`);
  }

  getById(id: number): Observable<TrailResponse> {
    return this.http.get<TrailResponse>(`${this.api}/trails/${id}`);
  }

  getSequence(id: number): Observable<ContentResponse[]> {
    return this.http.get<ContentResponse[]>(
      `${this.api}/trails/${id}/sequence`,
    );
  }

  enroll(id: number): Observable<ProgressResponse> {
    return this.http.post<ProgressResponse>(
      `${this.api}/trails/${id}/enroll`,
      {},
    );
  }

  getProgress(id: number): Observable<ProgressResponse> {
    return this.http.get<ProgressResponse>(`${this.api}/trails/${id}/progress`);
  }

  completeContent(contentId: number): Observable<ProgressResponse> {
    return this.http.post<ProgressResponse>(
      `${this.api}/trails/complete-content`,
      { contentId },
    );
  }
}
