# Server Monitor

[![App](https://github.com/viral32111/ServerMonitor/actions/workflows/app.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/app.yml) [![Service](https://github.com/viral32111/ServerMonitor/actions/workflows/service.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/service.yml) [![CodeQL](https://github.com/viral32111/ServerMonitor/actions/workflows/codeql.yml/badge.svg)](https://github.com/viral32111/ServerMonitor/actions/workflows/codeql.yml) [![MVP](https://img.shields.io/github/v/release/viral32111/ServerMonitor?include_prereleases&label=Latest%20MVP)](https://github.com/viral32111/ServerMonitor/releases/latest) [![Issues](https://img.shields.io/github/issues-raw/viral32111/ServerMonitor?label=Issues)](https://github.com/viral32111/ServerMonitor/issues) [![Size](https://img.shields.io/github/repo-size/viral32111/ServerMonitor?label=Size)](https://github.com/viral32111/ServerMonitor) [![Commits](https://img.shields.io/github/commit-activity/w/viral32111/ServerMonitor?label=Commits)](https://github.com/viral32111/ServerMonitor/commits/main)

A mobile app for monitoring server metrics & status information in real-time.

The [front-end client](/App/) is a modern [Material 3](https://m3.material.io/) Android application written in Kotlin for displaying, and the [back-end server service](/Service/) is a [.NET Core 7.0](https://dotnet.microsoft.com/) console application written in C# that acts as either a RESTful API or a Prometheus metrics exporter.

**NOTE: This project is currently under development, so functionality is not guaranteed!**

## Usage

A [Prometheus time-series database](https://prometheus.io/) is required for storing the metrics data. Additionally, a [Cloudflare Tunnel](https://www.cloudflare.com/en-gb/products/tunnel/) (or other secure HTTP tunnel) is highly recommended to expose the RESTful API service to the Internet, allowing secure access to it from the Android application.

1. Download the [latest release](https://github.com/viral32111/ServerMonitor/releases/latest) of the Android application (`Server-Monitor-App.apk`) & server service (`Server-Monitor-Service.zip`).
2. Install the server service on all the servers you wish to manage (the [.NET Core 7.0 Runtime](https://dotnet.microsoft.com/download/dotnet/7.0) is required).
   * One server must be designated as the *"connector"* (RESTful API) for the Android application to fetch metrics data from. See [Connector](#connector).
   * All other servers must run in the *"collection"* mode for gathering & exporting metrics data for Prometheus to scrape. See [Collector](#collector).
3. Configure the server service appropriately using the JSON configuration file or environment variables ([see configuration](#configuration)).
4. Install the Android application on your device & connect to the publicly-accessible URL of the *"connector"* service using any of the configured credentials.

### Docker

Alternatively, a [premade Docker image](https://github.com/users/viral32111/packages/container/package/server-monitor) of the back-end server service is available to simplify the process of deployment.

Download & start the *"collector*" by running the following command:
```
docker container run \
  --name server-monitor-collector \
  --mount type=bind,source=/path/to/your/config.json,target=/etc/server-monitor/config.json \
  --mount type=bind,source=/etc/systemd,target=/etc/systemd \
  --mount type=bind,source=/var/lib/systemd,target=/var/lib/systemd \
  --mount type=bind,source=/usr/lib/systemd,target=/usr/lib/systemd \
  --mount type=bind,source=/run/systemd,target=/run/systemd \
  --mount type=bind,source=/run/dbus/system_bus_socket,target=/run/dbus/system_bus_socket \
  --network host \
  --privileged \
  --user 0:0 \
  --restart on-failure \
  --pull always \
  ghcr.io/viral32111/server-monitor:main-ubuntu
  collector
```

* Use the `:main-windows` image tag for Windows-based Docker installations.
* Remove all the systemd & dbus mounts when running on Windows-based Docker installations.
* The JSON configuration file should be mounted at `/etc/server-monitor/config.json`.
* By default the Prometheus metrics exporter uses port `5000`, and the action server uses port `6997`.

Download & start the *"connector*" by running the following command:
```
docker container run \
  --name server-monitor-connector \
  --mount type=bind,source=/path/to/your/config.json,target=/etc/server-monitor/config.json \
  --publish published=127.0.0.1:6996,target=6996,protocol=tcp \
  --restart on-failure \
  --pull always \
  ghcr.io/viral32111/server-monitor:main-ubuntu
  connector
```

* Use the `:main-windows` image tag for Windows-based Docker installations.
* The JSON configuration file should be mounted at `/etc/server-monitor/config.json`.
* By default the RESTful API uses port `6996`.

**NOTE: Some features of the Docker image in *"collector"* mode are tedious on some systems due to availability of system API functions, especially on Windows-based Docker installations. It is recommended to only use the Docker image for the *"connector"* mode, then a regular installation for the *"collector"* mode.**

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

See [config.json](/Service/ServerMonitor/config.json) for an example configuration file.

## Modes

### Collector

This mode exports metrics from configured sources for Prometheus to scrape.

Either start the service with the `collector` command-line argument, or register a new system service using a command below.

On Windows, run this PowerShell command to create a new service:

```powershell
New-Service -Name "Server-Monitor-Collector" -DisplayName "Server Monitor (Collector)" -Description "Export metrics to Prometheus from configured sources." -BinaryPathName "C:\Path\To\Dotnet\Runtime\dotnet C:\Path\To\ServerMonitor\ServerMonitor.dll collector" -StartupType "Automatic" -DependsOn "Server"
```

On Linux (for systemd), firstly create a new service file at `/etc/systemd/service/server-monitor-collector.service` with the contents:

```
[Unit]
Description=Server Monitor (Collector)
Documentation=https://github.com/viral32111/ServerMonitor
After=network.target

[Service]
User=root
Group=root
WorkingDirectory=/path/to/servermonitor
ExecStart=/path/to/dotnet /path/to/servermonitor/ServerMonitor.dll collector
Type=simple
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

Then run `systemctl daemon-reload` to reload service files, followed by `systemctl enable server-monitor-collector` so the service launches on system startup.

### Connector

This mode serve metrics from Prometheus to the Android app via a RESTful API.

Either start the service with the `connector` command-line argument, or register a new system service using a command below.

On Windows, run this PowerShell command to create a new service:

```powershell
New-Service -Name "Server-Monitor-Connector" -DisplayName "Server Monitor (Connector)" -Description "Serve metrics from Prometheus to the Android app." -BinaryPathName "C:\Path\To\Dotnet\Runtime\dotnet C:\Path\To\ServerMonitor\ServerMonitor.dll connector" -StartupType "Automatic" -DependsOn "Server"
```

On Linux (for systemd), firstly create a new service file at `/etc/systemd/service/server-monitor-connector.service` with the contents:

```
[Unit]
Description=Server Monitor (Connector)
Documentation=https://github.com/viral32111/ServerMonitor
After=network.target

[Service]
User=root
Group=root
WorkingDirectory=/path/to/servermonitor
ExecStart=/path/to/dotnet /path/to/servermonitor/ServerMonitor.dll connector
Type=simple
Restart=on-failure

[Install]
WantedBy=multi-user.target
```

Then run `systemctl daemon-reload` to reload service files, followed by `systemctl enable server-monitor-connector` so the service launches on system startup.

## Building

### App

1. Create a key store & signing key, then [configure signing](https://github.com/viral32111/ServerMonitor/blob/main/App/app/build.gradle#L22-L25) using environment variables or the global Gradle configuration file (`~/.gradle/gradle.properties`).
  * Alternatively remove the signing configuration from the Gradle build file to disable APK signing.
2. Build the Android application with Gradle by running `./gradlew assembleRelease` in the [`App`](/App/) directory.
3. Optionally run instrumented tests by running `./gradlew test` in the [`App`](/App/) directory.

The APK file will be created at `App/app/build/outputs/apk/release/app-release.apk`.

### Service

1. Add `https://nuget.pkg.github.com/viral32111/index.json` as a NuGet packages source, as one of the dependencies is my [JSON Extensions](https://github.com/viral32111/JsonExtensions) package.
2. Build the server service with .NET by running `dotnet build --configuration Release` in the [`Service`](/Service/) directory.
3. Optionally run unit & integration tests by running `dotnet test --configuration Release` in the [`Service`](/Service) directory.

The resulting DLL file & dependencies will be created at `Service/ServerMonitor/bin/Release/net7.0/ServerMonitor.dll`.

## Progress

There is a [Kanban board](https://github.com/users/viral32111/projects/7/views/1) that uses [the issues](https://github.com/viral32111/ServerMonitor/issues) for tracking progress on individual steps of the project.

There are also [dated milestones](https://github.com/viral32111/ServerMonitor/milestones) which correspond to the stage completion dates from initial Gantt chart.

See the [releases page](https://github.com/viral32111/ServerMonitor/releases) for minimum-viable products from most stages/tasks.

## License

This project is licensed under the Creative Commons Attribution-ShareAlike 4.0 International license, see the [license file](/LICENSE.md) for more information.
