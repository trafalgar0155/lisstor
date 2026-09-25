# Android CI signing setup

The `Build signed Android APK` workflow runs on every push and can also be
started manually. It publishes a Release APK, verifies its signature, and
stores it as a GitHub Actions artifact for 30 days.

## One-time setup

Create and safely back up a release keystore. Do not commit it to Git:

```powershell
keytool -genkeypair -v -keystore lisstor.keystore -alias lisstor -keyalg RSA -keysize 2048 -validity 10000
```

Convert the keystore to a single-line Base64 value in PowerShell:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes((Resolve-Path .\lisstor.keystore))) | Set-Clipboard
```

In the GitHub repository, open **Settings > Secrets and variables > Actions**
and create these repository secrets:

| Secret | Value |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | The Base64 text copied above |
| `ANDROID_KEYSTORE_PASSWORD` | The keystore password |
| `ANDROID_KEY_ALIAS` | The key alias, for example `lisstor` |
| `ANDROID_KEY_PASSWORD` | The key password |

Keep an offline backup of the keystore and passwords. Android updates must be
signed with the same key as the installed version.

After adding the secrets, either push a commit or manually run the workflow
from the repository's **Actions** page. Download the APK from the completed
workflow run's **Artifacts** section.
