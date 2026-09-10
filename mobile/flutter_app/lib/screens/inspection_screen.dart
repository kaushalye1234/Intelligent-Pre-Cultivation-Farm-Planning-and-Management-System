import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/app_state.dart';

class InspectionScreen extends StatelessWidget {
  const InspectionScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppState>();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text('Inspection tools', style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 12),
        Card(
          child: ListTile(
            leading: const Icon(Icons.camera_alt_outlined),
            title: const Text('Capture inspection photo'),
            subtitle: Text(state.lastPhotoName ?? 'No photo selected'),
            trailing: IconButton(
              tooltip: 'Open camera',
              icon: const Icon(Icons.add_a_photo_outlined),
              onPressed: state.captureInspectionPhoto,
            ),
          ),
        ),
        Card(
          child: ListTile(
            leading: const Icon(Icons.location_on_outlined),
            title: const Text('Capture GPS position'),
            subtitle: Text(state.lastPosition == null ? 'No position captured' : '${state.lastPosition!.latitude}, ${state.lastPosition!.longitude}'),
            trailing: IconButton(
              tooltip: 'Get location',
              icon: const Icon(Icons.my_location),
              onPressed: state.captureLocation,
            ),
          ),
        ),
      ],
    );
  }
}