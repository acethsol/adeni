import 'package:flutter/material.dart';

import 'features/home/mode_launcher_screen.dart';

void main() {
  runApp(const AdeniBusinessApp());
}

class AdeniBusinessApp extends StatelessWidget {
  const AdeniBusinessApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Adeni Business',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF0F766E)),
        useMaterial3: true,
      ),
      home: ModeLauncherScreen(),
    );
  }
}
