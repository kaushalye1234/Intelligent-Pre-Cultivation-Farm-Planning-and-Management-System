import 'package:flutter/material.dart';

class AgriColors {
  static const canvas = Color(0xFFF7F6F1);
  static const surface = Color(0xFFFFFFFF);
  static const forest = Color(0xFF173F32);
  static const forestLight = Color(0xFF2D644D);
  static const ink = Color(0xFF202D27);
  static const muted = Color(0xFF65736C);
  static const sage = Color(0xFFE8F0E9);
  static const sageStrong = Color(0xFFB9D2BE);
  static const amber = Color(0xFFAD741D);
  static const amberSoft = Color(0xFFFFF2D9);
  static const border = Color(0xFFE3E7DF);
  static const danger = Color(0xFFAA4B3B);
  static const dangerSoft = Color(0xFFFFEFEB);
}

class AgriTheme {
  static ThemeData light() {
    const scheme = ColorScheme.light(
      primary: AgriColors.forest,
      onPrimary: Colors.white,
      secondary: AgriColors.forestLight,
      onSecondary: Colors.white,
      surface: AgriColors.surface,
      onSurface: AgriColors.ink,
      error: AgriColors.danger,
      onError: Colors.white,
      primaryContainer: AgriColors.sage,
      onPrimaryContainer: AgriColors.forest,
      secondaryContainer: AgriColors.amberSoft,
      onSecondaryContainer: AgriColors.ink,
    );
    final base = ThemeData(useMaterial3: true, colorScheme: scheme);
    return base.copyWith(
      scaffoldBackgroundColor: AgriColors.canvas,
      appBarTheme: const AppBarTheme(
        backgroundColor: AgriColors.canvas,
        foregroundColor: AgriColors.ink,
        elevation: 0,
        centerTitle: false,
        titleTextStyle: TextStyle(
          color: AgriColors.ink,
          fontSize: 19,
          fontWeight: FontWeight.w700,
        ),
      ),
      textTheme: base.textTheme.copyWith(
        headlineLarge: const TextStyle(
          color: AgriColors.ink,
          fontSize: 34,
          height: 1.12,
          fontWeight: FontWeight.w800,
          letterSpacing: -1.1,
        ),
        headlineMedium: const TextStyle(
          color: AgriColors.ink,
          fontSize: 29,
          height: 1.17,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.8,
        ),
        headlineSmall: const TextStyle(
          color: AgriColors.ink,
          fontSize: 24,
          height: 1.2,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.5,
        ),
        titleLarge: const TextStyle(
          color: AgriColors.ink,
          fontSize: 20,
          height: 1.3,
          fontWeight: FontWeight.w700,
        ),
        titleMedium: const TextStyle(
          color: AgriColors.ink,
          fontSize: 16,
          height: 1.35,
          fontWeight: FontWeight.w700,
        ),
        bodyLarge: const TextStyle(
          color: AgriColors.ink,
          fontSize: 16,
          height: 1.48,
        ),
        bodyMedium: const TextStyle(
          color: AgriColors.muted,
          fontSize: 14,
          height: 1.48,
        ),
        labelLarge: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
      ),
      cardTheme: CardThemeData(
        color: AgriColors.surface,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          side: const BorderSide(color: AgriColors.border),
          borderRadius: BorderRadius.circular(22),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AgriColors.surface,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 18,
          vertical: 18,
        ),
        labelStyle: const TextStyle(color: AgriColors.muted),
        hintStyle: const TextStyle(color: AgriColors.muted),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: AgriColors.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: AgriColors.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(16),
          borderSide: const BorderSide(color: AgriColors.forest, width: 1.5),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(54),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
          textStyle: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size.fromHeight(52),
          side: const BorderSide(color: AgriColors.border),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
          ),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: AgriColors.surface,
        indicatorColor: AgriColors.sage,
        height: 72,
        labelTextStyle: WidgetStateProperty.all(
          const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
        ),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: AgriColors.surface,
        selectedColor: AgriColors.sage,
        side: const BorderSide(color: AgriColors.border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        labelStyle: const TextStyle(
          color: AgriColors.ink,
          fontWeight: FontWeight.w600,
        ),
      ),
      dividerTheme: const DividerThemeData(
        color: AgriColors.border,
        thickness: 1,
      ),
    );
  }
}
