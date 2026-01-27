# Storage Configuration

## Overview

This app uses **Preferences-based storage with runtime encoding/decoding** for API keys. This approach avoids the need for keychain entitlements while providing basic obfuscation.

## How It Works

1. **Encoding**: Values are XOR obfuscated with an app-specific key, then Base64 encoded
2. **Storage**: Encoded values are stored in platform Preferences (MAUI) or localStorage (Web)
3. **Decoding**: On retrieval, values are automatically decoded
4. **Prefix**: Encoded values start with `ENC:` for identification

## Platform Implementation

| Platform | Storage Location |
|----------|------------------|
| macOS (Catalyst) | NSUserDefaults via Preferences |
| iOS | NSUserDefaults via Preferences |
| Android | SharedPreferences |
| Windows | Local Settings |
| Web | localStorage |

## Mac Catalyst Entitlements

**File:** `Platforms/MacCatalyst/Entitlements.plist`

Current entitlements (no keychain required):

| Entitlement | Purpose |
|-------------|---------|
| `com.apple.security.app-sandbox` | Required for Mac App Store |
| `com.apple.security.network.client` | Outbound network connections |
| `com.apple.security.network.server` | Incoming network connections |
| `com.apple.security.files.user-selected.read-write` | User-selected files |
| `com.apple.security.files.downloads.read-write` | Downloads folder |
| `com.apple.security.files.pictures.read-write` | Pictures folder |
| `com.apple.security.files.documents.read-write` | Documents folder |
| `com.apple.security.personal-information.photos-library` | Photos library |
| `com.apple.security.assets.pictures.read-write` | Picture assets |

## Security Notes

1. **Obfuscation is not encryption** - provides protection against casual inspection only
2. Values are not visible in plain text in storage
3. For production with sensitive data, consider Azure Key Vault
4. **Never commit API keys** to source control
