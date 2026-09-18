import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { EnrollmentApiService } from '../../../../core/services/enrollment-api.service';
import {
  ApplicationQueryParams,
  ApplicationListResponseDto,
  ApplicationListItemDto
} from '../../../../core/models/enrollment.models';
import { ReadinessBadgeComponent } from '../../../../shared/readiness-badge/readiness-badge.component';
import { ReadinessSummaryComponent } from '../../components/readiness-summary/readiness-summary.component';
import { ApplicationFiltersComponent } from '../../components/application-filters/application-filters.component';

@Component({
  selector: 'app-application-list',
  standalone: true,
  imports: [
    CommonModule,
    ReadinessBadgeComponent,
    ReadinessSummaryComponent,
    ApplicationFiltersComponent
  ],
  templateUrl: './application-list.component.html',
  styleUrls: ['./application-list.component.scss']
})
export class ApplicationListComponent implements OnInit {
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  response = signal<ApplicationListResponseDto | null>(null);
  query = signal<ApplicationQueryParams>({
    sortBy: 'providerName',
    sortDirection: 'asc'
  });

  constructor(
    private enrollmentApi: EnrollmentApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadApplications();
  }

  onQueryChange(newQuery: ApplicationQueryParams): void {
    this.query.set(newQuery);
    this.loadApplications();
  }

  onApplicationClick(application: ApplicationListItemDto): void {
    // Navigation to detail view will be implemented in S2
    console.log('Application clicked:', application.id);
  }

  private loadApplications(): void {
    this.loading.set(true);
    this.error.set(null);

    this.enrollmentApi.getApplications(this.query()).subscribe({
      next: (data) => {
        this.response.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load applications:', err);
        this.error.set(
          err.error?.message || 'Failed to load applications. Please try again.'
        );
        this.loading.set(false);
      }
    });
  }
}