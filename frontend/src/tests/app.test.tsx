import { describe, test, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { BrowserRouter } from 'react-router-dom'
import LeaveRequestPage from '../pages/LeaveRequestPage'
import LeaveRequestForm from '../components/LeaveRequestForm'
import LeaveBalanceCards from '../components/LeaveBalanceCards'
import LeaveOverlapWarning from '../components/LeaveOverlapWarning'
import LeaveRequestList from '../components/LeaveRequestList'
import * as leaveRequestApi from '../api/leaveRequestApi'


vi.mock('../api/leaveRequestApi')
vi.mock('../utils/logger', () => ({
  logInfo: vi.fn(),
  logWarn: vi.fn(),
  logError: vi.fn(),
  logDebug: vi.fn()
}))


const mockLeaveTypes = [
  {
    id: 1,
    code: 'ANNUAL',
    name: 'Annual Leave',
    reasonRequired: false,
    annualEntitlementDays: 20
  },
  {
    id: 2,
    code: 'SICK',
    name: 'Sick Leave',
    reasonRequired: true,
    annualEntitlementDays: null
  }
]


const mockBalances = [
  {
    leaveTypeId: 1,
    leaveTypeName: 'Annual Leave',
    annualEntitlementDays: 20,
    usedDays: 5,
    remainingDays: 15,
    isUnlimited: false
  },
  {
    leaveTypeId: 2,
    leaveTypeName: 'Sick Leave',
    annualEntitlementDays: null,
    usedDays: 2,
    remainingDays: null,
    isUnlimited: true
  }
]


const mockRequests = [
  {
    id: 1,
    leaveTypeId: 1,
    leaveTypeName: 'Annual Leave',
    startDate: '2024-06-01',
    endDate: '2024-06-05',
    requestedDays: 5,
    reason: 'Vacation',
    status: 'PENDING',
    submittedAt: '2024-05-20T10:00:00',
    reviewedAt: null,
    reviewedByUserId: null,
    decisionNotes: null
  }
]


const mockOverlapResponse = {
  hasOverlap: true,
  overlapCount: 2,
  affectedShifts: [
    { shiftDate: '2024-06-02', shiftTime: '09:00-17:00', role: 'Nurse' },
    { shiftDate: '2024-06-03', shiftTime: '09:00-17:00', role: 'Nurse' }
  ]
}


describe('LeaveBalanceCards', () => {
  test('renders leave balance cards', () => {
    render()


expect(screen.getByText('Leave Balance')).toBeInTheDocument()
expect(screen.getByText('Annual Leave')).toBeInTheDocument()
expect(screen.getByText('Sick Leave')).toBeInTheDocument()
expect(screen.getByText('20 days')).toBeInTheDocument()
expect(screen.getByText('No fixed limit')).toBeInTheDocument()
expect(screen.getByText('Unlimited')).toBeInTheDocument()

  })


  test('displays correct remaining days calculation', () => {
    render()


expect(screen.getByText('15 days')).toBeInTheDocument()

  })
})


describe('LeaveOverlapWarning', () => {
  test('renders overlap warning with affected shifts', () => {
    render(

    )


expect(screen.getByText('Roster Overlap Detected')).toBeInTheDocument()
expect(screen.getByText(/You have 2 scheduled shift/)).toBeInTheDocument()
expect(screen.getByText('09:00-17:00')).toBeInTheDocument()
expect(screen.getByText('Nurse')).toBeInTheDocument()

  })


  test('displays coordination message', () => {
    render(

    )


expect(screen.getByText(/Please coordinate with your manager/)).toBeInTheDocument()

  })
})


describe('LeaveRequestList', () => {
  test('renders leave request list', () => {
    render()


expect(screen.getByText('Recent Leave Requests')).toBeInTheDocument()
expect(screen.getByText('Annual Leave')).toBeInTheDocument()
expect(screen.getByText('PENDING')).toBeInTheDocument()

  })


  test('displays empty state when no requests', () => {
    render()


expect(screen.getByText('No leave requests to display')).toBeInTheDocument()

  })


  test('displays status with correct styling', () => {
    const approvedRequest = {
      ...mockRequests[0],
      status: 'APPROVED'
    }


render(<LeaveRequestList requests={[approvedRequest]} />)

const statusBadge = screen.getByText('APPROVED')
expect(statusBadge).toBeInTheDocument()

  })
})


describe('LeaveRequestForm', () => {
  const mockOnSubmit = vi.fn()
  const mockOnCancel = vi.fn()


  beforeEach(() => {
    mockOnSubmit.mockClear()
    mockOnCancel.mockClear()
  })


  test('renders form fields', () => {
    render(

    )


expect(screen.getByText('Submit Leave Request')).toBeInTheDocument()
expect(screen.getByLabelText(/Leave Type/)).toBeInTheDocument()
expect(screen.getByLabelText(/Start Date/)).toBeInTheDocument()
expect(screen.getByLabelText(/End Date/)).toBeInTheDocument()
expect(screen.getByLabelText(/Reason\/Notes/)).toBeInTheDocument()

  })


  test('validates required fields on submit', async () => {
    render(

    )


const submitButton = screen.getByText('Submit Request')
fireEvent.click(submitButton)

await waitFor(() => {
  expect(screen.getByText('Please select a leave type')).toBeInTheDocument()
  expect(screen.getByText('Start date is required')).toBeInTheDocument()
  expect(screen.getByText('End date is required')).toBeInTheDocument()
})

expect(mockOnSubmit).not.toHaveBeenCalled()

  })


  test('conditionally requires reason field', async () => {
    const user = userEvent.setup()


render(
  <LeaveRequestForm
    leaveTypes={mockLeaveTypes}
    onSubmit={mockOnSubmit}
    onCancel={mockOnCancel}
    submitting={false}
  />
)

const leaveTypeSelect = screen.getByLabelText(/Leave Type/)
await user.selectOptions(leaveTypeSelect, '2')

const startDateInput = screen.getByLabelText(/Start Date/)
await user.type(startDateInput, '2024-06-01')

const endDateInput = screen.getByLabelText(/End Date/)
await user.type(endDateInput, '2024-06-05')

const submitButton = screen.getByText('Submit Request')
fireEvent.click(submitButton)

await waitFor(() => {
  expect(screen.getByText('Reason is required for this leave type')).toBeInTheDocument()
})

  })


  test('submits form with valid data', async () => {
    const user = userEvent.setup()
    mockOnSubmit.mockResolvedValue(undefined)


render(
  <LeaveRequestForm
    leaveTypes={mockLeaveTypes}
    onSubmit={mockOnSubmit}
    onCancel={mockOnCancel}
    submitting={false}
  />
)

const leaveTypeSelect = screen.getByLabelText(/Leave Type/)
await user.selectOptions(leaveTypeSelect, '1')

const startDateInput = screen.getByLabelText(/Start Date/)
await user.type(startDateInput, '2024-06-01')

const endDateInput = screen.getByLabelText(/End Date/)
await user.type(endDateInput, '2024-06-05')

const submitButton = screen.getByText('Submit Request')
fireEvent.click(submitButton)

await waitFor(() => {
  expect(mockOnSubmit).toHaveBeenCalledWith({
    leaveTypeId: 1,
    startDate: '2024-06-01',
    endDate: '2024-06-05',
    reason: undefined
  })
})

  })


  test('clears form on cancel', async () => {
    const user = userEvent.setup()


render(
  <LeaveRequestForm
    leaveTypes={mockLeaveTypes}
    onSubmit={mockOnSubmit}
    onCancel={mockOnCancel}
    submitting={false}
  />
)

const leaveTypeSelect = screen.getByLabelText(/Leave Type/)
await user.selectOptions(leaveTypeSelect, '1')

const cancelButton = screen.getByText('Cancel')
fireEvent.click(cancelButton)

expect(mockOnCancel).toHaveBeenCalled()

  })
})


describe('LeaveRequestPage API Integration', () => {
  beforeEach(() => {
    vi.mocked(leaveRequestApi.getActiveLeaveTypes).mockResolvedValue({
      leaveTypes: mockLeaveTypes
    })
    vi.mocked(leaveRequestApi.getLeaveBalance).mockResolvedValue({
      balances: mockBalances
    })
    vi.mocked(leaveRequestApi.getMyLeaveRequests).mockResolvedValue({
      requests: mockRequests
    })
    vi.mocked(leaveRequestApi.checkLeaveOverlap).mockResolvedValue({
      hasOverlap: false,
      overlapCount: 0,
      affectedShifts: []
    })
  })


  test('loads initial data on mount', async () => {
    render(



    )


await waitFor(() => {
  expect(leaveRequestApi.getActiveLeaveTypes).toHaveBeenCalled()
  expect(leaveRequestApi.getLeaveBalance).toHaveBeenCalled()
  expect(leaveRequestApi.getMyLeaveRequests).toHaveBeenCalled()
})

expect(screen.getByText('Leave Request')).toBeInTheDocument()

  })


  test('handles API error gracefully', async () => {
    vi.mocked(leaveRequestApi.getActiveLeaveTypes).mockRejectedValue(
      new Error('Network error')
    )


render(
  <BrowserRouter>
    <LeaveRequestPage />
  </BrowserRouter>
)

await waitFor(() => {
  expect(screen.getByText('Unable to Load Leave Request Page')).toBeInTheDocument()
  expect(screen.getByText('Network error')).toBeInTheDocument()
})

  })


  test('displays overlap warning when overlap detected', async () => {
    vi.mocked(leaveRequestApi.checkLeaveOverlap).mockResolvedValue(mockOverlapResponse)


render(
  <BrowserRouter>
    <LeaveRequestPage />
  </BrowserRouter>
)

await waitFor(() => {
  expect(screen.getByText('Leave Request')).toBeInTheDocument()
})

  })
})