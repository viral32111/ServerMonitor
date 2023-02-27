# Start from Microsoft's .NET Core Runtime
FROM mcr.microsoft.com/dotnet/runtime:7.0-windowsservercore-ltsc2022
SHELL [ "powershell" ]

# Configure directories & files
ARG SERVERMONITOR_INSTALL_DIRECTORY=C:/Server-Monitor-Install \
	SERVERMONITOR_CONFIG_DIRECTORY=C:/Server-Monitor-Config

# Add artifacts from build
COPY ./ ${SERVERMONITOR_INSTALL_DIRECTORY}

# Move the configuration file to the system-wide configuration directory
RUN New-Item -ItemType Directory -Path ${SERVERMONITOR_CONFIG_DIRECTORY}
RUN Move-Item -Path ${SERVERMONITOR_INSTALL_DIRECTORY}/config.json -Destination ${SERVERMONITOR_CONFIG_DIRECTORY}/config.json

# Switch to the install directory
WORKDIR ${SERVERMONITOR_INSTALL_DIRECTORY}

# Start service when launched
ENTRYPOINT [ "dotnet", "C:/Server-Monitor-Install/ServerMonitor.dll" ]
