# Server Monitor

[![App](https://github.com/viral32111/ServerMonitor/actions/workflows/app.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/app.yml) [![Service](https://github.com/viral32111/ServerMonitor/actions/workflows/service.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/service.yml) [![CodeQL](https://github.com/viral32111/ServerMonitor/actions/workflows/codeql.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/codeql.yml)

This is a modern [Material 3](https://m3.material.io/) [Android app](/App) written in Kotlin for monitoring servers in real-time.

The [backend API](/Service) is a [.NET Core 7.0](https://dotnet.microsoft.com/) console application written in C#.

**NOTE: This project is currently under development, so functionality is not guaranteed!**

## Usage

A [Prometheus time-series database](https://prometheus.io/) is required for storing the metrics data. Additionally, a [Cloudflare Tunnel](https://www.cloudflare.com/en-gb/products/tunnel/) (or other secure HTTP tunnel) is highly recommended to protect the service and allow secure access to it from the mobile app.

1. Download the [latest release](https://github.com/viral32111/ServerMonitor/releases/latest) of the mobile app and server service.
2. Install the service on all the servers you wish to manage.
   * One server must be designated as the *"connection point"* for the mobile app to connect to & fetch metrics data from Prometheus. Start the service with the `connection` argument to run in this mode.
   * All other servers must run in the *"collection"* mode for gathering metrics data for Prometheus to scrape. Start the service with the `collector` argument to run in this mode.
3. Configure the service appropriately using the JSON configuration file or environment variables.
4. Install the mobile app on your Android device & connect to the *"connection point"* service URL using any configured credentials.

Alternatively, use the [premade Docker image](https://github.com/users/viral32111/packages/container/package/server-monitor) by running `docker pull ghcr.io/viral32111/server-monitor:latest`. The configuration file is located at `/etc/server-monitor/config.json`.

## Configuration

The JSON configuration file is searched for at the following paths:
 * Windows
   * System: `C:\ProgramData\ServerMonitor\config.json`
   * User: `C:\Users\USERNAME\AppData\Local\ServerMonitor\config.json`
 * Linux
   * System: `/etc/server-monitor/config.json`
   * User: `/home/USERNAME/.config/server-monitor/config.json`

Alternatively, a path to a configuration file in a non-standard location can be specified with the `--config <path>` option (use `--help` for more information).

Configuration properties can also be set via environment variables prefixed with `SERVER_MONITOR_`.

The configuration priority is as follows (higher entries will override previously configured properties):
1. Environment variables.
2. Non-standard configuration file (`--config <path>` option).
3. User-specific configuration file.
4. System-wide configuration file.

## Building

Build the mobile app with Gradle using `./gradlew assembleRelease` and optionally run tests using `./gradlew test` in the [`App`](/App) directory. The resulting APK file will be created at `App/app/build/outputs/apk/release/app-release.apk`.

Build the server service with .NET using `dotnet build --configuration Release` and optionally run tests using `dotnet test --configuration Release` in the [`Service`](/Service) directory. The resulting DLL file will be created at `Service/ServerMonitor/bin/Release/net7.0/ServerMonitor.dll`.

## Progress

There is a [Kanban board](https://github.com/users/viral32111/projects/7/views/1) that uses [the issues](https://github.com/viral32111/ServerMonitor/issues) for tracking progress on individual steps of the project.

There are also [dated milestones](https://github.com/viral32111/ServerMonitor/milestones) which correspond to the stage completion dates from initial Gantt chart.

See the [releases page](https://github.com/viral32111/ServerMonitor/releases) for minimum-viable products from most stages/tasks.

## License

This project is licensed under the Creative Commons Attribution-ShareAlike 4.0 International license, see the [license file](/LICENSE.md) for more information.
