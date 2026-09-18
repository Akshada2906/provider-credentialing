import { Component, Input, Output, EventEmitter, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ApplicationQueryParams,
  ReadinessStatus,
  ApplicationType
} from '../../../../core/models/enrollment.models';

@Component({
  selector: 'app-application-filters',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './application-filters.component.html',
  styleUrls: ['./application-filters.component.scss']
})
export class ApplicationFiltersComponent {
  @Input() currentQuery: ApplicationQueryParams = {};
  @Output() queryChange = new EventEmitter<ApplicationQueryParams>();

  statusFilter = signal<string>('');
  payerIdFilter = signal<string>('');
  applicationTypeFilter = signal<string>('');
  searchTerm = signal<string>('');
  sortBy = signal<string>('providerName');
  sortDirection = signal<string>('asc');

  readonly statusOptions: ReadinessStatus[] = [
    'Ready to Submit',
    'Incomplete',
    'Expiring Soon'
  ];

  readonly applicationTypeOptions: { value: ApplicationType; label: string }[] = [
    { value: 'NEW_ENROLLMENT', label: 'New Enrollment' },
    { value: 'RE_CREDENTIALING', label: 'Re-Credentialing' }
  ];

  readonly sortByOptions = [
    { value: 'providerName', label: 'Provider Name' },
    { value: 'lastUpdated', label: 'Last Updated' },
    { value: 'readiness', label: 'Readiness Status' }
  ];

  ngOnInit(): void {
    // Initialize local state from current query
    this.statusFilter.set(this.currentQuery.status || '');
    this.payerIdFilter.set(this.currentQuery.payerId || '');
    this.applicationTypeFilter.set(this.currentQuery.applicationType || '');
    this.searchTerm.set(this.currentQuery.search || '');
    this.sortBy.set(this.currentQuery.sortBy || 'providerName');
    this.sortDirection.set(this.currentQuery.sortDirection || 'asc');
  }

  onStatusChange(value: string): void {
    this.statusFilter.set(value);
    this.emitQuery();
  }

  onPayerIdChange(value: string): void {
    this.payerIdFilter.set(value);
    this.emitQuery();
  }

  onApplicationTypeChange(value: string): void {
    this.applicationTypeFilter.set(value);
    this.emitQuery();
  }

  onSearchChange(value: string): void {
    this.searchTerm.set(value);
    this.emitQuery();
  }

  onSortByChange(value: string): void {
    this.sortBy.set(value);
    this.emitQuery();
  }

  onSortDirectionChange(value: string): void {
    this.sortDirection.set(value);
    this.emitQuery();
  }

  clearFilters(): void {
    this.statusFilter.set('');
    this.payerIdFilter.set('');
    this.applicationTypeFilter.set('');
    this.searchTerm.set('');
    this.sortBy.set('providerName');
    this.sortDirection.set('asc');
    this.emitQuery();
  }

  private emitQuery(): void {
    const query: ApplicationQueryParams = {
      sortBy: this.sortBy() as any,
      sortDirection: this.sortDirection() as any
    };

    if (this.statusFilter()) {
      query.status = this.statusFilter() as ReadinessStatus;
    }
    if (this.payerIdFilter()) {
      query.payerId = this.payerIdFilter();
    }
    if (this.applicationTypeFilter()) {
      query.applicationType = this.applicationTypeFilter() as ApplicationType;
    }
    if (this.searchTerm()) {
      query.search = this.searchTerm();
    }

    this.queryChange.emit(query);
  }
}
