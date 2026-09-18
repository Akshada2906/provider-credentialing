package com.vis;


import com.vis.app.controller.LeaveRequestController;
import com.vis.app.dto.*;
import com.vis.app.entity.LeaveRequest;
import com.vis.app.entity.LeaveType;
import com.vis.app.exception.ConflictException;
import com.vis.app.exception.ResourceNotFoundException;
import com.vis.app.repository.LeaveRequestRepository;
import com.vis.app.repository.LeaveTypeRepository;
import com.vis.app.service.LeaveRequestService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.InjectMocks;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContext;
import org.springframework.security.core.context.SecurityContextHolder;


import java.math.BigDecimal;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Optional;


import static org.junit.jupiter.api.Assertions.;
import static org.mockito.ArgumentMatchers.;
import static org.mockito.Mockito.*;


@ExtendWith(MockitoExtension.class)
class AppTest {


@Mock
private LeaveTypeRepository leaveTypeRepository;

@Mock
private LeaveRequestRepository leaveRequestRepository;

@Mock
private JdbcTemplate jdbcTemplate;

@InjectMocks
private LeaveRequestService leaveRequestService;

@Mock
private LeaveRequestService mockLeaveRequestService;

@InjectMocks
private LeaveRequestController leaveRequestController;

private LeaveType testLeaveType;
private LeaveRequest testLeaveRequest;

@BeforeEach
void setUp() {
    testLeaveType = new LeaveType();
    testLeaveType.setId(1L);
    testLeaveType.setFacilityId(100L);
    testLeaveType.setCode("ANNUAL");
    testLeaveType.setName("Annual Leave");
    testLeaveType.setReasonRequired(false);
    testLeaveType.setAnnualEntitlementDays(new BigDecimal("20.00"));
    testLeaveType.setIsActive(true);

    testLeaveRequest = new LeaveRequest();
    testLeaveRequest.setId(1L);
    testLeaveRequest.setFacilityId(100L);
    testLeaveRequest.setStaffId(200L);
    testLeaveRequest.setLeaveTypeId(1L);
    testLeaveRequest.setStartDate(LocalDate.of(2024, 6, 1));
    testLeaveRequest.setEndDate(LocalDate.of(2024, 6, 5));
    testLeaveRequest.setRequestedDays(new BigDecimal("5.00"));
    testLeaveRequest.setStatus("PENDING");
    testLeaveRequest.setSubmittedAt(LocalDateTime.now());
}

@Test
void getActiveLeaveTypes_validFacility_returns200() {
    when(leaveTypeRepository.findByFacilityIdAndIsActiveTrue(100L))
        .thenReturn(Collections.singletonList(testLeaveType));

    LeaveTypeResponseDto response = leaveRequestService.getActiveLeaveTypes(100L);

    assertNotNull(response);
    assertEquals(1, response.getLeaveTypes().size());
    assertEquals("Annual Leave", response.getLeaveTypes().get(0).getName());
    verify(leaveTypeRepository, times(1)).findByFacilityIdAndIsActiveTrue(100L);
}

@Test
void getActiveLeaveTypes_noActiveTypes_returnsEmptyList() {
    when(leaveTypeRepository.findByFacilityIdAndIsActiveTrue(100L))
        .thenReturn(Collections.emptyList());

    LeaveTypeResponseDto response = leaveRequestService.getActiveLeaveTypes(100L);

    assertNotNull(response);
    assertEquals(0, response.getLeaveTypes().size());
    verify(leaveTypeRepository, times(1)).findByFacilityIdAndIsActiveTrue(100L);
}

@Test
void calculateLeaveBalance_validStaff_returns200() {
    when(leaveTypeRepository.findByFacilityIdAndIsActiveTrue(100L))
        .thenReturn(Collections.singletonList(testLeaveType));
    when(leaveRequestRepository.findLeaveRequestsForBalance(
        eq(200L), eq(1L), anyList(), any(LocalDateTime.class), any(LocalDateTime.class)))
        .thenReturn(Collections.emptyList());

    LeaveBalanceDto response = leaveRequestService.calculateLeaveBalance(200L, 100L);

    assertNotNull(response);
    assertEquals(1, response.getBalances().size());
    assertEquals(new BigDecimal("20.00"), response.getBalances().get(0).getAnnualEntitlementDays());
    assertEquals(BigDecimal.ZERO, response.getBalances().get(0).getUsedDays());
    verify(leaveTypeRepository, times(1)).findByFacilityIdAndIsActiveTrue(100L);
}

@Test
void calculateLeaveBalance_withUsedDays_calculatesCorrectly() {
    LeaveRequest approvedRequest = new LeaveRequest();
    approvedRequest.setRequestedDays(new BigDecimal("5.00"));

    when(leaveTypeRepository.findByFacilityIdAndIsActiveTrue(100L))
        .thenReturn(Collections.singletonList(testLeaveType));
    when(leaveRequestRepository.findLeaveRequestsForBalance(
        eq(200L), eq(1L), anyList(), any(LocalDateTime.class), any(LocalDateTime.class)))
        .thenReturn(Collections.singletonList(approvedRequest));

    LeaveBalanceDto response = leaveRequestService.calculateLeaveBalance(200L, 100L);

    assertNotNull(response);
    assertEquals(new BigDecimal("5.00"), response.getBalances().get(0).getUsedDays());
    assertEquals(new BigDecimal("15.00"), response.getBalances().get(0).getRemainingDays());
}

@Test
void checkOverlap_noOverlap_returnsNoOverlap() {
    when(jdbcTemplate.query(anyString(), any(Object[].class), any(org.springframework.jdbc.core.RowMapper.class)))
        .thenReturn(Collections.emptyList());

    LeaveOverlapDto response = leaveRequestService.checkOverlap(
        200L, LocalDate.of(2024, 6, 1), LocalDate.of(2024, 6, 5));

    assertNotNull(response);
    assertFalse(response.getHasOverlap());
    assertEquals(0, response.getOverlapCount());
    verify(jdbcTemplate, times(1)).query(anyString(), any(Object[].class), any(org.springframework.jdbc.core.RowMapper.class));
}

@Test
void checkOverlap_withOverlap_returnsOverlapDetails() {
    AffectedShiftDto shift = new AffectedShiftDto(LocalDate.of(2024, 6, 2), "09:00-17:00", "Nurse");
    when(jdbcTemplate.query(anyString(), any(Object[].class), any(org.springframework.jdbc.core.RowMapper.class)))
        .thenReturn(Collections.singletonList(shift));

    LeaveOverlapDto response = leaveRequestService.checkOverlap(
        200L, LocalDate.of(2024, 6, 1), LocalDate.of(2024, 6, 5));

    assertNotNull(response);
    assertTrue(response.getHasOverlap());
    assertEquals(1, response.getOverlapCount());
    assertEquals(1, response.getAffectedShifts().size());
}

@Test
void createLeaveRequest_validData_returns201() {
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(1L)).thenReturn(Optional.of(testLeaveType));
    when(leaveRequestRepository.findOverlappingLeaveRequests(
        eq(200L), anyList(), any(LocalDate.class), any(LocalDate.class)))
        .thenReturn(Collections.emptyList());
    when(leaveRequestRepository.findLeaveRequestsForBalance(
        eq(200L), eq(1L), anyList(), any(LocalDateTime.class), any(LocalDateTime.class)))
        .thenReturn(Collections.emptyList());
    when(leaveRequestRepository.save(any(LeaveRequest.class))).thenReturn(testLeaveRequest);

    LeaveRequestResponseDto response = leaveRequestService.createLeaveRequest(dto, 200L, 100L);

    assertNotNull(response);
    assertEquals(1L, response.getId());
    assertEquals("PENDING", response.getStatus());
    verify(leaveRequestRepository, times(1)).save(any(LeaveRequest.class));
}

@Test
void createLeaveRequest_endDateBeforeStartDate_throwsIllegalArgumentException() {
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 5));
    dto.setEndDate(LocalDate.of(2024, 6, 1));

    assertThrows(IllegalArgumentException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void createLeaveRequest_leaveTypeNotFound_throwsResourceNotFoundException() {
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(999L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(999L)).thenReturn(Optional.empty());

    assertThrows(ResourceNotFoundException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void createLeaveRequest_leaveTypeInactive_throwsResourceNotFoundException() {
    testLeaveType.setIsActive(false);
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(1L)).thenReturn(Optional.of(testLeaveType));

    assertThrows(ResourceNotFoundException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void createLeaveRequest_wrongFacility_throwsIllegalArgumentException() {
    testLeaveType.setFacilityId(999L);
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(1L)).thenReturn(Optional.of(testLeaveType));

    assertThrows(IllegalArgumentException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void createLeaveRequest_reasonRequiredButMissing_throwsIllegalArgumentException() {
    testLeaveType.setReasonRequired(true);
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(1L)).thenReturn(Optional.of(testLeaveType));

    assertThrows(IllegalArgumentException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void createLeaveRequest_overlappingRequest_throwsConflictException() {
    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(1L)).thenReturn(Optional.of(testLeaveType));
    when(leaveRequestRepository.findOverlappingLeaveRequests(
        eq(200L), anyList(), any(LocalDate.class), any(LocalDate.class)))
        .thenReturn(Collections.singletonList(testLeaveRequest));

    assertThrows(ConflictException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void createLeaveRequest_insufficientBalance_throwsConflictException() {
    LeaveRequest existingRequest = new LeaveRequest();
    existingRequest.setRequestedDays(new BigDecimal("18.00"));

    CreateLeaveRequestDto dto = new CreateLeaveRequestDto();
    dto.setLeaveTypeId(1L);
    dto.setStartDate(LocalDate.of(2024, 6, 1));
    dto.setEndDate(LocalDate.of(2024, 6, 5));

    when(leaveTypeRepository.findById(1L)).thenReturn(Optional.of(testLeaveType));
    when(leaveRequestRepository.findOverlappingLeaveRequests(
        eq(200L), anyList(), any(LocalDate.class), any(LocalDate.class)))
        .thenReturn(Collections.emptyList());
    when(leaveRequestRepository.findLeaveRequestsForBalance(
        eq(200L), eq(1L), anyList(), any(LocalDateTime.class), any(LocalDateTime.class)))
        .thenReturn(Collections.singletonList(existingRequest));

    assertThrows(ConflictException.class, () -> {
        leaveRequestService.createLeaveRequest(dto, 200L, 100L);
    });
}

@Test
void getMyLeaveRequests_validStaff_returns200() {
    when(leaveRequestRepository.findByStaffIdOrderBySubmittedAtDesc(200L))
        .thenReturn(Collections.singletonList(testLeaveRequest));

    LeaveRequestHistoryResponseDto response = leaveRequestService.getMyLeaveRequests(200L);

    assertNotNull(response);
    assertEquals(1, response.getRequests().size());
    verify(leaveRequestRepository, times(1)).findByStaffIdOrderBySubmittedAtDesc(200L);
}

@Test
void getMyLeaveRequests_noRequests_returnsEmptyList() {
    when(leaveRequestRepository.findByStaffIdOrderBySubmittedAtDesc(200L))
        .thenReturn(Collections.emptyList());

    LeaveRequestHistoryResponseDto response = leaveRequestService.getMyLeaveRequests(200L);

    assertNotNull(response);
    assertEquals(0, response.getRequests().size());
    verify(leaveRequestRepository, times(1)).findByStaffIdOrderBySubmittedAtDesc(200L);
}

}