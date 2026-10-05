import 'package:flutter/material.dart';

import '../../core/api/adeni_api_client.dart';
import '../../core/config/app_config.dart';
import '../employee/employee_home_screen.dart';
import '../front_desk/front_desk_screen.dart';
import '../owner/owner_home_screen.dart';
import '../shared/api_status_banner.dart';

/// Dev entry: pick operational mode. Production will route from Auth0 roles.
class ModeLauncherScreen extends StatelessWidget {
  ModeLauncherScreen({super.key});

  final AdeniApiClient _api = AdeniApiClient();

  @override
  Widget build(BuildContext context) {
    final config = AppConfig.fromEnvironment();
    return Scaffold(
      appBar: AppBar(title: const Text('Adeni Business')),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ApiStatusBanner(api: _api),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Text(
              'API: ${config.apiBaseUrl}\n'
              'Choose a mode (skeleton). Auth0 routing comes in phase 2.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ),
          _ModeTile(
            icon: Icons.storefront_outlined,
            title: 'Owner',
            subtitle: 'Plan, payouts, business overview',
            onTap: () => _open(context, OwnerHomeScreen(api: _api)),
          ),
          _ModeTile(
            icon: Icons.calendar_today_outlined,
            title: 'Employee',
            subtitle: 'Your appointments today',
            onTap: () => _open(context, EmployeeHomeScreen(api: _api)),
          ),
          _ModeTile(
            icon: Icons.table_bar_outlined,
            title: 'Front desk',
            subtitle: 'Check in guests (e.g. pedicure)',
            onTap: () => _open(context, FrontDeskScreen(api: _api)),
          ),
        ],
      ),
    );
  }

  void _open(BuildContext context, Widget screen) {
    Navigator.of(context).push(
      MaterialPageRoute<void>(builder: (_) => screen),
    );
  }
}

class _ModeTile extends StatelessWidget {
  const _ModeTile({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      leading: Icon(icon, size: 32),
      title: Text(title),
      subtitle: Text(subtitle),
      trailing: const Icon(Icons.chevron_right),
      onTap: onTap,
    );
  }
}
