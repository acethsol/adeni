import 'package:flutter/material.dart';

import '../../core/api/adeni_api_client.dart';
import '../shared/api_status_banner.dart';

/// Reception / kiosk-oriented layout (large targets, search-first).
class FrontDeskScreen extends StatefulWidget {
  const FrontDeskScreen({super.key, required this.api});

  final AdeniApiClient api;

  @override
  State<FrontDeskScreen> createState() => _FrontDeskScreenState();
}

class _FrontDeskScreenState extends State<FrontDeskScreen> {
  final _searchController = TextEditingController();

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Front desk')),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ApiStatusBanner(api: widget.api),
          Padding(
            padding: const EdgeInsets.all(16),
            child: TextField(
              controller: _searchController,
              decoration: const InputDecoration(
                labelText: 'Find booking',
                hintText: 'Name or confirmation code',
                prefixIcon: Icon(Icons.search),
                border: OutlineInputBorder(),
              ),
              textInputAction: TextInputAction.search,
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: FilledButton.icon(
              onPressed: () {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text(
                      'Check-in API not wired yet — see flutter-business-mobile-skeleton spec.',
                    ),
                  ),
                );
              },
              icon: const Icon(Icons.login, size: 28),
              label: const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Text('Check in for service', style: TextStyle(fontSize: 18)),
              ),
            ),
          ),
          const SizedBox(height: 8),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: OutlinedButton.icon(
              onPressed: () {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Check-out stub')),
                );
              },
              icon: const Icon(Icons.logout),
              label: const Text('Check out / complete'),
            ),
          ),
          const Expanded(
            child: Center(
              child: Text(
                'Arrivals today will list here.\n'
                'Optimized for tablet at the nail desk.',
                textAlign: TextAlign.center,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
