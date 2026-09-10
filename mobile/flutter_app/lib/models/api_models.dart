class UserProfile {
  const UserProfile({
    required this.id,
    required this.fullName,
    required this.email,
    required this.role,
    required this.isActive,
  });

  final String id;
  final String fullName;
  final String email;
  final int role;
  final bool isActive;

  factory UserProfile.fromJson(Map<String, dynamic> json) {
    return UserProfile(
      id: json['id'] as String,
      fullName: json['fullName'] as String,
      email: json['email'] as String,
      role: json['role'] as int,
      isActive: json['isActive'] as bool,
    );
  }
}

class DashboardSummary {
  const DashboardSummary({
    required this.activeFarms,
    required this.activeCropPlans,
    required this.openCropIssues,
    required this.lowStockResources,
    required this.pendingTasks,
    required this.pendingApprovals,
  });

  final int activeFarms;
  final int activeCropPlans;
  final int openCropIssues;
  final int lowStockResources;
  final int pendingTasks;
  final int pendingApprovals;

  factory DashboardSummary.empty() {
    return const DashboardSummary(
      activeFarms: 0,
      activeCropPlans: 0,
      openCropIssues: 0,
      lowStockResources: 0,
      pendingTasks: 0,
      pendingApprovals: 0,
    );
  }

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    return DashboardSummary(
      activeFarms: json['activeFarms'] as int? ?? 0,
      activeCropPlans: json['activeCropPlans'] as int? ?? 0,
      openCropIssues: json['openCropIssues'] as int? ?? 0,
      lowStockResources: json['lowStockResources'] as int? ?? 0,
      pendingTasks: json['pendingTasks'] as int? ?? 0,
      pendingApprovals: json['pendingApprovals'] as int? ?? 0,
    );
  }
}

class FarmOption {
  const FarmOption({required this.id, required this.name});

  final String id;
  final String name;

  factory FarmOption.fromJson(Map<String, dynamic> json) {
    return FarmOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class FieldOption {
  const FieldOption({required this.id, required this.name});

  final String id;
  final String name;

  factory FieldOption.fromJson(Map<String, dynamic> json) {
    return FieldOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class CropTypeOption {
  const CropTypeOption({required this.id, required this.name});

  final String id;
  final String name;

  factory CropTypeOption.fromJson(Map<String, dynamic> json) {
    return CropTypeOption(id: json['id'] as String, name: json['name'] as String);
  }
}

class InventoryStock {
  const InventoryStock({
    required this.id,
    required this.resourceId,
    required this.availableQuantity,
    required this.lowStockThreshold,
  });

  final String id;
  final String resourceId;
  final num availableQuantity;
  final num lowStockThreshold;

  factory InventoryStock.fromJson(Map<String, dynamic> json) {
    return InventoryStock(
      id: json['id'] as String,
      resourceId: json['resourceId'] as String,
      availableQuantity: json['availableQuantity'] as num? ?? 0,
      lowStockThreshold: json['lowStockThreshold'] as num? ?? 0,
    );
  }
}