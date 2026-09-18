import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReadinessStatus, RequirementStatus } from '../../core/models/enrollment.models';

@Component({
  selector: 'app-readiness-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './readiness-badge.component.html',
  styleUrls: ['./readiness-badge.component.scss']
})
export class ReadinessBadgeComponent {
  @Input() status: ReadinessStatus | RequirementStatus | null = null;
  @Input() type: 'readiness' | 'requirement' = 'readiness';

  get badgeClass(): string {
    if (!this.status) {
      return 'badge-error';
    }

    const statusMap: Record<string, string> = {
      'Ready to Submit': 'badge-ready',
      'Incomplete': 'badge-incomplete',
      'Expiring Soon': 'badge-expiring',
      'Present & Valid': 'badge-valid',
      'Missing': 'badge-missing',
      'Expired': 'badge-expired'
    };

    return statusMap[this.status] || 'badge-default';
  }

  get displayText(): string {
    if (!this.status) {
      return 'Evaluation Error';
    }
    return this.status;
  }
}