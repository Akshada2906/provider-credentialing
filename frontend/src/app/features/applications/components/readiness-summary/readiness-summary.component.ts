import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReadinessSummaryDto } from '../../../../core/models/enrollment.models';

@Component({
  selector: 'app-readiness-summary',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './readiness-summary.component.html',
  styleUrls: ['./readiness-summary.component.scss']
})
export class ReadinessSummaryComponent {
  @Input() statusCounts: ReadinessSummaryDto = {
    readyToSubmit: 0,
    incomplete: 0,
    expiringSoon: 0
  };
  @Input() evaluationErrorCount: number = 0;
}