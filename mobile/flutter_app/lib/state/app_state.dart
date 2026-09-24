import 'package:flutter/foundation.dart';

import '../models/api_models.dart';
import '../services/api_client.dart';

class AppState extends ChangeNotifier {
  AppState({ApiClient? apiClient}) : _apiClient = apiClient ?? ApiClient();

  final ApiClient _apiClient;
  static const staffPortalMessage =
      'This account is for staff. Please use the React Staff Portal.';
  static const farmerRole = 1;
  static const resourceOfficerRole = 3;

  /// Mobile serves Farmers, plus Resource Officers for inventory and reservations.
  /// Every other staff role must use the React Staff Portal.
  static bool isMobileRole(int role) =>
      role == farmerRole || role == resourceOfficerRole;

  UserProfile? user;
  AuthenticationSession? passwordChangeSession;
  FarmerOnboardingStatus? farmerOnboarding;
  DashboardSummary summary = DashboardSummary.empty();
  List<FarmOption> farms = [];
  List<FieldOption> fields = [];
  List<CropTypeOption> cropTypes = [];
  List<CropVarietyOption> cropVarieties = [];
  List<CropPlanRecord> cropPlans = [];
  Map<String, CropPlanningWorkflowStatus> planWorkflows = {};
  List<FarmTaskRecord> tasks = [];
  List<IrrigationScheduleRecord> irrigationSchedules = [];
  List<ApprovalHistoryRecord> approvalHistory = [];
  String? lastCropPlanRequestId;
  CropPlanningWorkflowStart? lastWorkflowStart;
  CropPlanningWorkflowStatus? lastWorkflowStatus;
  CropPlanningResult? lastPlanningResult;
  String? error;
  bool isBusy = false;

  ApiClient get apiClient => _apiClient;
  bool get isAuthenticated => user != null;
  bool get isResourceOfficer => user?.role == resourceOfficerRole;
  bool get requiresTemporaryPasswordChange =>
      passwordChangeSession?.requiresPasswordChange ?? false;
  Future<void> restoreSession() async {
    isBusy = true;
    notifyListeners();
    try {
      final token = await _apiClient.loadToken();
      if (token != null) {
        user = await _apiClient.profile();
        if (!isMobileRole(user!.role)) {
          await _rejectStaffSession();
          return;
        }
        await _loadAuthenticatedLanding();
      }
    } catch (_) {
      await _apiClient.clearToken();
      user = null;
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }

  Future<void> login(String email, String password) async {
    await _guard(() async {
      final session = await _apiClient.login(email, password);
      if (!isMobileRole(session.user.role)) {
        await _rejectStaffSession();
        return;
      }
      if (session.requiresPasswordChange) {
        passwordChangeSession = session;
        user = null;
        farmerOnboarding = null;
        return;
      }

      passwordChangeSession = null;
      user = session.user;
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> registerFarmer({
    required String fullName,
    required String email,
    required String password,
  }) async {
    await _guard(() async {
      final session = await _apiClient.registerFarmer(
        fullName: fullName,
        email: email,
        password: password,
      );
      passwordChangeSession = null;
      user = session.user;
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> changeTemporaryPassword(String newPassword) async {
    final session = passwordChangeSession;
    if (session?.passwordChangeToken == null) {
      error = 'Your password-change session has ended. Sign in again.';
      passwordChangeSession = null;
      notifyListeners();
      return;
    }

    await _guard(() async {
      final authenticatedSession = await _apiClient.changeTemporaryPassword(
        passwordChangeToken: session!.passwordChangeToken!,
        newPassword: newPassword,
      );
      if (!isMobileRole(authenticatedSession.user.role)) {
        await _rejectStaffSession();
        return;
      }
      passwordChangeSession = null;
      user = authenticatedSession.user;
      await _loadAuthenticatedLanding();
    });
  }

  void cancelTemporaryPasswordChange() {
    passwordChangeSession = null;
    error = null;
    notifyListeners();
  }

  Future<void> logout() async {
    await _apiClient.clearToken();
    user = null;
    passwordChangeSession = null;
    farmerOnboarding = null;
    cropPlans = [];
    planWorkflows = {};
    notifyListeners();
  }

  Future<void> createOnboardingFarm({
    required String name,
    required String location,
    required num totalArea,
  }) async {
    await _guard(() async {
      await _apiClient.createFarm(
        name: name,
        location: location,
        totalArea: totalArea,
      );
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> createOnboardingField({
    required String farmId,
    required String name,
    required num area,
    required String soilType,
  }) async {
    await _guard(() async {
      await _apiClient.createField(
        farmId: farmId,
        name: name,
        area: area,
        soilType: soilType,
      );
      await _loadAuthenticatedLanding();
    });
  }

  Future<void> retryAuthenticatedLanding() async {
    await _guard(_loadAuthenticatedLanding);
  }

  Future<void> refresh() async {
    summary = await _apiClient.dashboard();
    farms = await _apiClient.farms();
    fields = await _apiClient.fields();
    cropTypes = await _apiClient.cropTypes();
    cropVarieties = await _apiClient.cropVarieties();
    cropPlans = await _apiClient.cropPlans();
    final workflowEntries = await Future.wait(
      cropPlans.map((plan) async {
        try {
          final status = await _apiClient.cropPlanningWorkflowStatus(plan.id);
          return MapEntry(plan.id, status);
        } on ApiException catch (error) {
          if (error.statusCode == 404) return null;
          rethrow;
        }
      }),
    );
    planWorkflows = {
      for (final entry in workflowEntries)
        if (entry != null) entry.key: entry.value,
    };
    tasks = await _apiClient.tasks();
    irrigationSchedules = await _apiClient.irrigationSchedules();
    approvalHistory = await _apiClient.approvalHistory();
    notifyListeners();
  }

  Future<ApprovedWorkflowDetail> approvedWorkflowDetail(String workflowId) =>
      _apiClient.approvedWorkflowDetail(workflowId);

  Future<void> _loadAuthenticatedLanding() async {
    if (user == null || !isMobileRole(user!.role)) {
      await _rejectStaffSession();
      return;
    }

    // Resource Officers have no farm to onboard; their screens load their own data.
    if (isResourceOfficer) return;

    farmerOnboarding = await _apiClient.farmerOnboardingStatus();
    if (farmerOnboarding?.stage == 'field') {
      farms = await _apiClient.farms();
      return;
    }

    if (farmerOnboarding?.stage == 'complete') {
      await refresh();
    }
  }

  Future<void> createAndStartAiCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String startDate,
    required String endDate,
    required num budget,
    required String objective,
    String? cropVarietyId,
    int cultivationSeason = 0,
    String? previousCropTypeId,
    List<String> previousKnownProblems = const [],
  }) async {
    await _guard(() async {
      final requestId = await _apiClient.createPreliminaryCropPlan(
        farmId: farmId,
        fieldId: fieldId,
        cropTypeId: cropTypeId,
        cropVarietyId: cropVarietyId,
        cultivationSeason: cultivationSeason,
        previousCropTypeId: previousCropTypeId,
        previousKnownProblems: previousKnownProblems,
        preferredStartDate: startDate,
        preferredEndDate: endDate,
        budget: budget,
        objective: objective,
      );
      lastCropPlanRequestId = requestId;
      lastWorkflowStart = await _apiClient.startCropPlanningWorkflow(requestId);
      await refreshLastCropPlanningWorkflow();
      await refresh();
    });
  }

  Future<void> createPreliminaryCropPlan({
    required String farmId,
    required String? fieldId,
    required String cropTypeId,
    required String startDate,
    required String endDate,
    required num budget,
    required String objective,
    String? cropVarietyId,
    int cultivationSeason = 0,
    String? previousCropTypeId,
    List<String> previousKnownProblems = const [],
  }) async {
    await _guard(() async {
      lastCropPlanRequestId = await _apiClient.createPreliminaryCropPlan(
        farmId: farmId,
        fieldId: fieldId,
        cropTypeId: cropTypeId,
        cropVarietyId: cropVarietyId,
        cultivationSeason: cultivationSeason,
        previousCropTypeId: previousCropTypeId,
        previousKnownProblems: previousKnownProblems,
        preferredStartDate: startDate,
        preferredEndDate: endDate,
        budget: budget,
        objective: objective,
      );
      await refresh();
    });
  }

  Future<void> refreshLastCropPlanningWorkflow() async {
    final requestId = lastCropPlanRequestId;
    if (requestId == null) return;
    lastWorkflowStatus = await _apiClient.cropPlanningWorkflowStatus(requestId);
    lastPlanningResult = await _apiClient.cropPlanningResult(requestId);
    notifyListeners();
  }

  Future<void> _rejectStaffSession() async {
    await _apiClient.clearToken();
    user = null;
    passwordChangeSession = null;
    farmerOnboarding = null;
    error = staffPortalMessage;
  }

  Future<void> _guard(Future<void> Function() action) async {
    isBusy = true;
    error = null;
    notifyListeners();
    try {
      await action();
    } catch (err) {
      error = err.toString();
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }
}
