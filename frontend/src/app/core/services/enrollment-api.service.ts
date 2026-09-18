import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api-config';
import {
  ApplicationQueryParams,
  ApplicationListResponseDto,
  ApplicationEvaluationDto
} from '../models/enrollment.models';

@Injectable({
  providedIn: 'root'
})
export class EnrollmentApiService {
  private readonly baseUrl = API_CONFIG.baseUrl;

  constructor(private http: HttpClient) {}

  /**
   * Retrieves the active application work queue with optional filtering, searching, and sorting.
   * Returns items, status counts across all active applications before filtering, evaluation error count,
   * and the configured evaluation date.
   */
  getApplications(query: ApplicationQueryParams): Observable<ApplicationListResponseDto> {
    let params = new HttpParams();

    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.payerId) {
      params = params.set('payerId', query.payerId);
    }
    if (query.applicationType) {
      params = params.set('applicationType', query.applicationType);
    }
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }
    if (query.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }

    return this.http.get<ApplicationListResponseDto>(`${this.baseUrl}/applications`, { params });
  }

  /**
   * Retrieves full evaluation details for a single application, including all payer-specific
   * requirement breakdowns, evaluation date basis, and any evaluation errors.
   */
  getApplicationDetail(id: string): Observable<ApplicationEvaluationDto> {
    return this.http.get<ApplicationEvaluationDto>(`${this.baseUrl}/applications/${id}`);
  }
}