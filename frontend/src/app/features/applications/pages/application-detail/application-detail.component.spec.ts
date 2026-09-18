import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApplicationDetailComponent } from './application-detail.component';
import { EnrollmentApiService } from '../../../../core/services/enrollment-api.service';
import { ApplicationEvaluationDto } from '../../../../core/models/enrollment.models';

describe('ApplicationDetailComponent', () => {
  let component: ApplicationDetailComponent;
  let fixture: ComponentFixture<ApplicationDetailComponent>;
  let mockEnrollmentApi: jasmine.SpyObj<EnrollmentApiService>;
  let mockRouter: jasmine.SpyObj<Router>;
  let mockActivatedRoute: any;

  const mockApplication: ApplicationEvaluationDto = {
    id: 'app-001',
    providerId: 'prov-001',
    providerName: 'Dr. Jane Smith',
    applicationType: 'NEW_ENROLLMENT',
    isActive: true,
    submittedOn: null,
    lastUpdated: '2024-01-15T10:30:00Z',
    overallStatus: 'Incomplete',
    evaluationDate: '2024-01-15T00:00:00Z',
    evaluationDateSource: 'CONFIGURED',
    evaluationErrors: [],
    payers: [
      {
        payerId: 'payer-001',
        payerName: 'Medicare',
        status: 'Incomplete',
        ruleVersionId: 'rule-v1',
        effectiveFrom: '2024-01-01T00:00:00Z',
        effectiveTo: null,
        evaluationErrors: [],
        requirements: [
          {
            key: 'medical_license',
            label: 'Medical License',
            kind: 'DOCUMENT',
            status: 'Missing',
            reason: 'Document not provided',
            requiredAction: 'Upload medical license',
            documentId: null,
            expiresOn: null,
            daysUntilExpiration: null
          }
        ],
        blockingDeficiencyCount: 1,
        expirationWarningCount: 0
      }
    ]
  };

  beforeEach(async () => {
    mockEnrollmentApi = jasmine.createSpyObj('EnrollmentApiService', ['getApplicationDetail']);
    mockRouter = jasmine.createSpyObj('Router', ['navigate']);
    mockActivatedRoute = {
      snapshot: {
        paramMap: {
          get: jasmine.createSpy('get').and.returnValue('app-001')
        }
      }
    };

    await TestBed.configureTestingModule({
      imports: [ApplicationDetailComponent],
      providers: [
        { provide: EnrollmentApiService, useValue: mockEnrollmentApi },
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ApplicationDetailComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load application detail on init', () => {
    mockEnrollmentApi.getApplicationDetail.and.returnValue(of(mockApplication));
    
    fixture.detectChanges();

    expect(mockEnrollmentApi.getApplicationDetail).toHaveBeenCalledWith('app-001');
    expect(component.application()).toEqual(mockApplication);
    expect(component.loading()).toBe(false);
    expect(component.error()).toBeNull();
  });

  it('should handle 404 error', () => {
    const error404 = { status: 404, error: { message: 'Not found' } };
    mockEnrollmentApi.getApplicationDetail.and.returnValue(throwError(() => error404));
    
    fixture.detectChanges();

    expect(component.error()).toBe('Application not found');
    expect(component.loading()).toBe(false);
  });

  it('should handle 400 error', () => {
    const error400 = { status: 400, error: { message: 'Bad request' } };
    mockEnrollmentApi.getApplicationDetail.and.returnValue(throwError(() => error400));
    
    fixture.detectChanges();

    expect(component.error()).toBe('Invalid application ID');
    expect(component.loading()).toBe(false);
  });

  it('should navigate back to list', () => {
    component.onBackToList();
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/applications']);
  });

  it('should display error when application ID is missing', () => {
    mockActivatedRoute.snapshot.paramMap.get.and.returnValue(null);
    
    fixture.detectChanges();

    expect(component.error()).toBe('Application ID is required');
  });
});