import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';
import '../state/resource_state.dart';
import '../ui/agri_theme.dart';
import 'resource_inventory_screen.dart';
import 'resource_reservations_screen.dart';

/// Home for Resource Officer accounts: inventory lookup and reservations.
class ResourceOfficerShell extends StatelessWidget {
  const ResourceOfficerShell({super.key});

  @override
  Widget build(BuildContext context) {
    final appState = context.read<AppState>();
    return ChangeNotifierProvider<ResourceState>(
      create: (_) => ResourceState(
        apiClient: appState.apiClient,
        // Resource Officers may release or cancel reservations.
        canManage: appState.isResourceOfficer,
      )..loadStocks()..loadReservations(),
      child: const _ResourceOfficerScaffold(),
    );
  }
}

class _ResourceOfficerScaffold extends StatefulWidget {
  const _ResourceOfficerScaffold();

  @override
  State<_ResourceOfficerScaffold> createState() =>
      _ResourceOfficerScaffoldState();
}

class _ResourceOfficerScaffoldState extends State<_ResourceOfficerScaffold> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    final appState = context.watch<AppState>();
    final resources = context.read<ResourceState>();
    return Scaffold(
      appBar: AppBar(
        title: const Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.spa_rounded, size: 24, color: AgriColors.forest),
            SizedBox(width: 9),
            Text('AgriAssist Resources'),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Refresh',
            onPressed: () {
              resources.loadStocks();
              resources.loadReservations();
            },
            icon: const Icon(Icons.refresh_rounded),
          ),
          PopupMenuButton<String>(
            tooltip: 'Account',
            icon: const Icon(Icons.account_circle_outlined),
            onSelected: (value) {
              if (value == 'signout') appState.logout();
            },
            itemBuilder: (_) => [
              PopupMenuItem(
                enabled: false,
                child: Text(appState.user?.fullName ?? 'Resource Officer'),
              ),
              const PopupMenuDivider(),
              const PopupMenuItem(value: 'signout', child: Text('Sign out')),
            ],
          ),
        ],
      ),
      body: IndexedStack(
        index: _index,
        children: const [
          ResourceInventoryScreen(),
          ResourceReservationsScreen(),
        ],
      ),
      bottomNavigationBar: DecoratedBox(
        decoration: const BoxDecoration(
          border: Border(top: BorderSide(color: AgriColors.border)),
        ),
        child: NavigationBar(
          selectedIndex: _index,
          onDestinationSelected: (value) => setState(() => _index = value),
          destinations: const [
            NavigationDestination(
              icon: Icon(Icons.inventory_2_outlined),
              selectedIcon: Icon(Icons.inventory_2_rounded),
              label: 'Inventory',
            ),
            NavigationDestination(
              icon: Icon(Icons.bookmark_border_rounded),
              selectedIcon: Icon(Icons.bookmark_rounded),
              label: 'Reservations',
            ),
          ],
        ),
      ),
    );
  }
}
