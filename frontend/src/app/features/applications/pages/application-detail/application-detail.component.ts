import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EnrollmentApiService } from '../../../../core/services/enrollment-api.service';
import { ApplicationEvaluationDto } from '../../../../core/models/enrollment.models';
import { ReadinessBadgeComponent } from '../../../../shared/readiness-badge/readiness-badge.component';
import { PayerDetailCardComponent } from '../../components/payer-detail-card/payer-detail-card.component';

@Component({
  selector: 'app-application-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    ReadinessBadgeComponent,
    PayerDetailCardComponent
  ],
  templateUrl: './application-detail.component.html',
  styleUrls: ['./application-detail.component.scss']
})
export class ApplicationDetailComponent implements OnInit {
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  application = signal<ApplicationEvaluationDto | null>(null);

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private enrollmentApi: EnrollmentApiService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('Application ID is required');
      return;
    }
    this.loadApplicationDetail(id);
  }

  onBackToList(): void {
    this.router.navigate(['/applications']);
  }

  getEvaluationSourceLabel(source: string): string {
    switch (source) {
      case 'SUBMITTED_ON':
        return 'Submission Date';
      case 'CONFIGURED':
        return 'Configured Date';
      case 'CURRENT':
        return 'System Date';
      default:
        return source;
    }
  }

  private loadApplicationDetail(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.enrollmentApi.getApplicationDetail(id).subscribe({
      next: (data) => {
        this.application.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load application detail:', err);
        if (err.status === 404) {
          this.error.set('Application not found');
        } else if (err.status === 400) {
          this.error.set('Invalid application ID');
        } else {
          this.error.set(
            err.error?.message || 'Failed to load application details. Please try again.'
          );
        }
        this.loading.set(false);
      }
    });
  }
}