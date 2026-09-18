import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PayerEvaluationDto } from '../../../../core/models/enrollment.models';
import { ReadinessBadgeComponent } from '../../../../shared/readiness-badge/readiness-badge.component';

@Component({
  selector: 'app-payer-detail-card',
  standalone: true,
  imports: [CommonModule, ReadinessBadgeComponent],
  templateUrl: './payer-detail-card.component.html',
  styleUrls: ['./payer-detail-card.component.scss']
})
export class PayerDetailCardComponent {
  @Input() payer!: PayerEvaluationDto;

  get hasEvaluationErrors(): boolean {
    return this.payer.evaluationErrors && this.payer.evaluationErrors.length > 0;
  }

  get hasRequirements(): boolean {
    return this.payer.requirements && this.payer.requirements.length > 0;
  }

  formatDate(dateString: string | null): string {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' });
  }

  getExpirationLabel(daysUntilExpiration: number | null): string {
    if (daysUntilExpiration === null) return '';
    if (daysUntilExpiration < 0) return 'Expired';
    if (daysUntilExpiration === 0) return 'Expires today';
    if (daysUntilExpiration === 1) return 'Expires in 1 day';
    return `Expires in ${daysUntilExpiration} days`;
  }
}