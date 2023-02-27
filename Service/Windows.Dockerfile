# Start from Microsoft's .NET Core Runtime
FROM mcr.microsoft.com/dotnet/runtime:7.0-windowsservercore-ltsc2022
SHELL [ "powershell" ]

# Configure directories & files
ARG SERVERMONITOR_DIRECTORY=C:\\ServerMonitor\\Install \
	SERVERMONITOR_CONFIG_DIRECTORY=C:\\ServerMonitor\\Config

# Add artifacts from build
COPY ./ ${SERVERMONITOR_DIRECTORY}

# Move the configuration file to the system-wide configuration directory
RUN New-Item -ItemType Directory -Path ${SERVERMONITOR_CONFIG_DIRECTORY}; `
	Move-Item -Path ${SERVERMONITOR_DIRECTORY}/config.json -Destination ${SERVERMONITOR_CONFIG_DIRECTORY}/config.json;

# Switch to the install directory
WORKDIR ${SERVERMONITOR_DIRECTORY}

# Start service when launched
ENTRYPOINT [ "dotnet", "C:\\ServerMonitor\\Install\\ServerMonitor.dll" ]
