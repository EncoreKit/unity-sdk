# Game Harness

A daily rewards screen with the Encore gift button on it, for demos, tests and screenshots. `HarnessBuild` builds it headless from a host project that links this folder into its `Assets`.

Each result line also goes to `harness.log` in `Application.persistentDataPath`, marked `[main]` when it arrived on Unity's main thread.

## Build against local SDK builds

On Android, the published SDK can use the local backend in a debuggable build; see `docs~/runbooks/game-harness.md`. On iOS, point the harness at a local build of the native SDK to run it against a local backend. Both switches act only on the harness build; `Editor/EncoreDependencies.xml` stays as it is.

iOS, a Debug `Encore.xcframework` from a `swift` checkout:

```bash
CONFIG=Debug bash scripts/release/build-xcframework.sh
```

Build it with the same Xcode that builds the harness. Then build the harness with `ENCORE_IOS_SDK_PATH` set to that checkout. The build writes the release binary podspec there and points the `EncoreKit` pod at it. Build the Xcode workspace in the Debug configuration.

Android, a local `android` build published as `2.5.0-local`, with a Gradle init script that sets the publication version:

```bash
./gradlew :encore:publishToMavenLocal -I local-version.init.gradle
```

Then build the harness with `ENCORE_ANDROID_SDK_VERSION=2.5.0-local`. The host project's Gradle settings template must list `mavenLocal()`.

## What a publisher needs

Nothing from this section. A publisher uses the pinned SDKs from `EncoreDependencies.xml` and gets production.
