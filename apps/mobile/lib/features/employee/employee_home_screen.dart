import 'package:flutter/material.dart';

import '../../core/api/adeni_api_client.dart';
import '../shared/api_status_banner.dart';

class EmployeeHomeScreen extends StatelessWidget {
  const EmployeeHomeScreen({super.key, required this.api});

  final AdeniApiClient api;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Employee')),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ApiStatusBanner(api: api),
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: const [
                Text(
                  'Today’s schedule (stub)',
                  style: TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
                ),
                SizedBox(height: 8),
                Text(
                  'Will call GET /api/v1/business/bookings with Auth0 business JWT.',
                ),
                SizedBox(height: 16),
                _StubAppointment(
                  time: '10:00',
                  service: 'Classic pedicure',
                  customer: 'Ada O.',
                  status: 'Confirmed',
                ),
                _StubAppointment(
                  time: '11:30',
                  service: 'Gel manicure',
                  customer: 'Tunde K.',
                  status: 'Pending',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _StubAppointment extends StatelessWidget {
  const _StubAppointment({
    required this.time,
    required this.service,
    required this.customer,
    required this.status,
  });

  final String time;
  final String service;
  final String customer;
  final String status;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        title: Text('$time · $service'),
        subtitle: Text(customer),
        trailing: Text(status),
      ),
    );
  }
}
