import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import 'crop_plan_screen.dart';
import 'dashboard_screen.dart';
import 'inspection_screen.dart';
import 'resources_screen.dart';
import 'status_screen.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;

  final _screens = const [
    DashboardScreen(),
    CropPlanScreen(),
    InspectionScreen(),
    ResourcesScreen(),
    StatusScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return Scaffold(
      appBar: AppBar(
        title: Text(state.user?.fullName ?? 'AgriAssist'),
        actions: [
          IconButton(tooltip: 'Refresh', onPressed: state.isBusy ? null : state.refresh, icon: const Icon(Icons.refresh)),
          IconButton(tooltip: 'Sign out', onPressed: state.logout, icon: const Icon(Icons.logout)),
        ],
      ),
      body: Stack(
        children: [
          _screens[_index],
          if (state.isBusy) const LinearProgressIndicator(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (value) => setState(() => _index = value),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: 'Dashboard'),
          NavigationDestination(icon: Icon(Icons.spa_outlined), selectedIcon: Icon(Icons.spa), label: 'Plans'),
          NavigationDestination(icon: Icon(Icons.camera_alt_outlined), selectedIcon: Icon(Icons.camera_alt), label: 'Inspect'),
          NavigationDestination(icon: Icon(Icons.inventory_2_outlined), selectedIcon: Icon(Icons.inventory_2), label: 'Resources'),
          NavigationDestination(icon: Icon(Icons.assignment_outlined), selectedIcon: Icon(Icons.assignment), label: 'My status'),
        ],
      ),
      bottomSheet: state.error == null
          ? null
          : Material(
              color: Theme.of(context).colorScheme.errorContainer,
              child: SafeArea(
                top: false,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Text(state.error!),
                ),
              ),
            ),
    );
  }
}
