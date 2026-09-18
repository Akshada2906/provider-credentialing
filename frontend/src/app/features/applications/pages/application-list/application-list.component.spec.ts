import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ApplicationListComponent } from './application-list.component';
import { EnrollmentApiService } from '../../../../core/services/enrollment-api.service';
import { of, throwError } from 'rxjs';
import { ApplicationListResponseDto } from '../../../../core/models/enrollment.models';

describe('ApplicationListComponent', () => {
  let component: ApplicationListComponent;
  let fixture: ComponentFixture<ApplicationListComponent>;
  let apiService: jasmine.SpyObj<EnrollmentApiService>;

  const mockResponse: ApplicationListResponseDto = {
    items: [
      {
        id: 'APP-001',
        providerId: 'PROV-001',
        providerName: 'Dr. John Smith',
        applicationType: 'NEW_ENROLLMENT',
        overallStatus: 'Ready to Submit',
        lastUpdated: '2026-09-18T10:00:00Z',
        payerCount: 3,
        hasEvaluationErrors: false
      }
    ],
    statusCounts: {
      readyToSubmit: 5,
      incomplete: 8,
      expiringSoon: 3
    },
    evaluationErrorCount: 1,
    configuredEvaluationDate: '2026-09-18'
  };

  beforeEach(async () => {
    const apiServiceSpy = jasmine.createSpyObj('EnrollmentApiService', ['getApplications']);

    await TestBed.configureTestingModule({
      imports: [ApplicationListComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: EnrollmentApiService, useValue: apiServiceSpy }
      ]
    }).compileComponents();

    apiService = TestBed.inject(EnrollmentApiService) as jasmine.SpyObj<EnrollmentApiService>;
    fixture = TestBed.createComponent(ApplicationListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load applications on init', () => {
    apiService.getApplications.and.returnValue(of(mockResponse));
    
    fixture.detectChanges();

    expect(apiService.getApplications).toHaveBeenCalledWith({
      sortBy: 'providerName',
      sortDirection: 'asc'
    });
    expect(component.response()).toEqual(mockResponse);
    expect(component.loading()).toBe(false);
  });

  it('should handle API error', () => {
    const errorResponse = { error: { message: 'Server error' } };
    apiService.getApplications.and.returnValue(throwError(() => errorResponse));

    fixture.detectChanges();

    expect(component.error()).toBe('Server error');
    expect(component.loading()).toBe(false);
  });

  it('should reload applications when query changes', () => {
    apiService.getApplications.and.returnValue(of(mockResponse));
    fixture.detectChanges();

    const newQuery = { status: 'Incomplete' as const, sortBy: 'lastUpdated' as const, sortDirection: 'desc' as const };
    component.onQueryChange(newQuery);

    expect(apiService.getApplications).toHaveBeenCalledWith(newQuery);
  });

  it('should display empty state when no applications returned', () => {
    const emptyResponse = { ...mockResponse, items: [] };
    apiService.getApplications.and.returnValue(of(emptyResponse));

    fixture.detectChanges();

    const compiled = fixture.nativeElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });
});