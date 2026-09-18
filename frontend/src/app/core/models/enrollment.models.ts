// Readiness status constants matching backend contract
export type ReadinessStatus = 'Ready to Submit' | 'Incomplete' | 'Expiring Soon';
export type RequirementStatus = 'Present & Valid' | 'Missing' | 'Expired' | 'Expiring Soon';
export type RequirementKind = 'DOCUMENT' | 'FIELD';
export type ApplicationType = 'NEW_ENROLLMENT' | 'RE_CREDENTIALING';
export type EvaluationDateSource = 'SUBMITTED_ON' | 'CONFIGURED' | 'CURRENT';

// Query parameters for application list endpoint
export interface ApplicationQueryParams {
  status?: ReadinessStatus;
  payerId?: string;
  applicationType?: ApplicationType;
  search?: string;
  sortBy?: 'providerName' | 'lastUpdated' | 'readiness';
  sortDirection?: 'asc' | 'desc';
}

// Application list item DTO
export interface ApplicationListItemDto {
  id: string;
  providerId: string;
  providerName: string;
  applicationType: ApplicationType;
  overallStatus: ReadinessStatus | null;
  lastUpdated: string;
  payerCount: number;
  hasEvaluationErrors: boolean;
}

// Readiness summary counts
export interface ReadinessSummaryDto {
  readyToSubmit: number;
  incomplete: number;
  expiringSoon: number;
}

// Application list response
export interface ApplicationListResponseDto {
  items: ApplicationListItemDto[];
  statusCounts: ReadinessSummaryDto;
  evaluationErrorCount: number;
  configuredEvaluationDate: string;
}

// Requirement evaluation detail (used in detail view, S2)
export interface RequirementEvaluationDto {
  key: string;
  label: string;
  kind: RequirementKind;
  status: RequirementStatus;
  reason: string | null;
  requiredAction: string | null;
  documentId: string | null;
  expiresOn: string | null;
  daysUntilExpiration: number | null;
}

// Payer evaluation detail (used in detail view, S2)
export interface PayerEvaluationDto {
  payerId: string;
  payerName: string;
  status: ReadinessStatus | null;
  ruleVersionId: string | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  evaluationErrors: string[];
  requirements: RequirementEvaluationDto[];
  blockingDeficiencyCount: number;
  expirationWarningCount: number;
}

// Full application evaluation (used in detail view, S2)
export interface ApplicationEvaluationDto {
  id: string;
  providerId: string;
  providerName: string;
  applicationType: ApplicationType;
  isActive: boolean;
  submittedOn: string | null;
  lastUpdated: string;
  overallStatus: ReadinessStatus | null;
  evaluationDate: string;
  evaluationDateSource: EvaluationDateSource;
  evaluationErrors: string[];
  payers: PayerEvaluationDto[];
}
```

---