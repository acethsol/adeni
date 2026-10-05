import 'package:flutter/material.dart';

import '../../core/api/adeni_api_client.dart';
import '../shared/api_status_banner.dart';

class OwnerHomeScreen extends StatelessWidget {
  const OwnerHomeScreen({super.key, required this.api});

  final AdeniApiClient api;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Owner')),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ApiStatusBanner(api: api),
          const Expanded(
            child: Center(
              child: Padding(
                padding: EdgeInsets.all(24),
                child: Text(
                  'Owner dashboard stub.\n\n'
                  'Parity targets: subscription usage, booking volume, '
                  'payouts — heavy config stays on Angular portal.',
                  textAlign: TextAlign.center,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
