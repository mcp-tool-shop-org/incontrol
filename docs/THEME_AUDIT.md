# Theme Consistency Audit

> **Audit Date**: 2026-02-03
> **InControl Version**: 0.9.0-rc.1

## Summary

This document tracks the theme consistency audit for light and dark mode support across all UI components.

## Audit Scope

### Files Audited
- `App.xaml` - Application resources
- `MainWindow.xaml` - Main window layout
- `WelcomePanel.xaml` - Welcome/onboarding UI
- `ConnectivityPage.xaml` - Network settings page
- `PolicyPage.xaml` - Policy configuration page
- `HelpPage.xaml` - Help center
- `ModelManagerPage.xaml` - Model management
- `SettingsPage.xaml` - App settings
- `InControlTheme.xaml` - Design system tokens

## Issues Found & Fixed

### 1. App.xaml - Message Card Colors
**Before:**
```xml
<SolidColorBrush x:Key="UserIntentBackgroundBrush" Color="#1A0078D4" />
<SolidColorBrush x:Key="ModelOutputBackgroundBrush" Color="#F3F3F3" />
```
**Issue:** `#F3F3F3` is light gray - invisible in dark mode.

**After:**
```xml
<SolidColorBrush x:Key="UserIntentBackgroundBrush" Color="{ThemeResource SystemAccentColor}" Opacity="0.1" />
<SolidColorBrush x:Key="ModelOutputBackgroundBrush" Color="{ThemeResource CardBackgroundFillColorDefault}" />
```

### 2. WelcomePanel.xaml - Step Indicator Colors
**Before:**
```xml
<SolidColorBrush x:Name="Step1Background" Color="#0078D4" />
<SolidColorBrush x:Name="Step2Background" Color="#888888" />
<SolidColorBrush x:Name="Step3Background" Color="#888888" />
```
**Issue:** Hardcoded colors don't adapt to theme.

**After:**
```xml
<Border Background="{ThemeResource AccentFillColorDefaultBrush}">
<Border Background="{ThemeResource ControlFillColorDisabledBrush}">
```

### 3. WelcomePanel.xaml - Text on Colored Backgrounds
**Before:**
```xml
<TextBlock Foreground="White" />
```
**Issue:** "White" is hardcoded - may have contrast issues.

**After:**
```xml
<TextBlock Foreground="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
<TextBlock Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
```

### 4. ConnectivityPage.xaml - Icon Background
**Before:**
```xml
<SolidColorBrush x:Name="OfflineIconBackground" Color="#1A0078D4" />
```
**Issue:** Hardcoded accent tint.

**After:**
```xml
<Border Background="{ThemeResource ControlFillColorSecondaryBrush}">
```

### 5. MainWindow.xaml - Overlay Background
**Before:**
```xml
<Grid Background="#80000000">
```
**Issue:** Hardcoded semi-transparent black overlay.

**After:**
```xml
<Grid Background="{ThemeResource SmokeFillColorDefaultBrush}">
```

## Theme Resource Usage Guidelines

### Recommended Resources for Common Scenarios

| Scenario | Light Theme Resource | Dark Theme Adaptation |
|----------|---------------------|----------------------|
| Card backgrounds | `CardBackgroundFillColorDefaultBrush` | Auto |
| Page backgrounds | `SolidBackgroundFillColorBaseBrush` | Auto |
| Overlays/modals | `SmokeFillColorDefaultBrush` | Auto |
| Primary text | `TextFillColorPrimaryBrush` | Auto |
| Secondary text | `TextFillColorSecondaryBrush` | Auto |
| Tertiary text | `TextFillColorTertiaryBrush` | Auto |
| Success state | `SystemFillColorSuccessBrush` | Auto |
| Warning state | `SystemFillColorCautionBrush` | Auto |
| Error state | `SystemFillColorCriticalBrush` | Auto |
| Accent color | `AccentFillColorDefaultBrush` | Auto |
| Text on accent | `TextOnAccentFillColorPrimaryBrush` | Auto |
| Disabled state | `ControlFillColorDisabledBrush` | Auto |
| Dividers | `DividerStrokeColorDefaultBrush` | Auto |

### Colors to AVOID

❌ Never use:
- Hardcoded hex colors (`#FFFFFF`, `#000000`, `#F3F3F3`)
- Named colors (`White`, `Black`, `Gray`)
- RGB values in XAML

✅ Always use:
- `{ThemeResource ResourceName}` for theme-aware colors
- `{StaticResource ResourceName}` for non-theme resources (spacing, etc.)

## Contrast Requirements

All text must meet WCAG 2.1 AA contrast requirements:
- Normal text: 4.5:1 minimum
- Large text (18pt+): 3:1 minimum
- UI components: 3:1 minimum

WinUI 3 ThemeResources are designed to meet these requirements automatically.

## Verification Checklist

- [x] All hardcoded colors replaced with ThemeResources
- [x] Code-behind uses Application.Current.Resources for dynamic colors
- [x] Build succeeds with no XAML warnings
- [x] InControlTheme.xaml provides design tokens
- [ ] Manual visual testing in Light mode
- [ ] Manual visual testing in Dark mode
- [ ] Manual visual testing in High Contrast mode

## Post-Audit Status

✅ **PASS** - All identified hardcoded colors have been replaced with theme-aware resources.

## Screenshots (To Be Added)

Screenshots should be captured for:
1. Main shell - Light mode
2. Main shell - Dark mode
3. Policy page - Light mode
4. Policy page - Dark mode
5. Connectivity page - Light mode
6. Connectivity page - Dark mode
7. Help page - Light mode
8. Help page - Dark mode
9. Focus ring visibility check
