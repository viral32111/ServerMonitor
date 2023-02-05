# Server Monitor

[![App](https://github.com/viral32111/ServerMonitor/actions/workflows/app.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/app.yml) [![Service](https://github.com/viral32111/ServerMonitor/actions/workflows/service.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/service.yml) [![CodeQL](https://github.com/viral32111/ServerMonitor/actions/workflows/codeql.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/codeql.yml)

This is a modern [Material 3](https://m3.material.io/) [Android app](/App) written in Kotlin for monitoring servers in real-time. The [backend API](/Service) is a [.NET Core 7.0](https://dotnet.microsoft.com/) console application written in C#.

**NOTE: This project is currently under development, so functionality is not guaranteed!**

## Usage

A [Prometheus time-series database](https://prometheus.io/) is required for storing the metrics data. Additionally, a [Cloudflare Tunnel](https://www.cloudflare.com/en-gb/products/tunnel/) (or other secure HTTP tunnel) is highly recommended to protect the service and allow secure access to it from the mobile app.

1. Download the [latest release](https://github.com/viral32111/ServerMonitor/releases/latest) of the mobile app and server service.
2. Install the service on all the servers you wish to manage.
  * One server must be designated as the *"connection point"* for the mobile app to connect to & fetch metrics data from Prometheus. Start the service with the `connection` argument to run in this mode.
  * All other servers must run in the *"collection"* mode for gathering metrics data for Prometheus to scrape. Start the service with the `collector` argument to run in this mode.
3. Configure the service appropriately using the JSON configuration file or environment variables.
4. Install the mobile app on your Android device & connect to the *"connection point"* service URL using any configured credentials.

## Building

### App

Build the mobile app with Gradle using `./gradlew assembleRelease` in the [`App`](/App) directory.

The APK file will be created at `App/app/build/outputs/apk/release/app-release.apk`.

Optionally run tests using `./gradlew test`.

### Service

Build the server service with .NET using `dotnet build --configuration Release` in the [`Service`](/Service) directory.

The DLL file will be created at `Service/ServerMonitor/bin/Release/net7.0/ServerMonitor.dll`.

Optionally run tests using `dotnet test --configuration Release`.

## Progress

There is a [Kanban board](https://github.com/users/viral32111/projects/7/views/1) that uses [the issues](https://github.com/viral32111/ServerMonitor/issues) for tracking progress on individual steps of the project.

There is also [dated milestones](https://github.com/viral32111/ServerMonitor/milestones) which correspond to the stage completion dates from initial Gantt chart.

See the [releases page](https://github.com/viral32111/ServerMonitor/releases) for minimum-viable products from most stages/tasks.

## License

This project is licensed under the Creative Commons Attribution-ShareAlike 4.0 International license, see the [license file](/LICENSE.md) for more information.
